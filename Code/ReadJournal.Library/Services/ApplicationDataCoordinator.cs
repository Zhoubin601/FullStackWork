using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.Services;

// Lock order: reader action -> data lease -> database gate. Never wait for readers during maintenance.
public sealed class ApplicationDataCoordinator : IApplicationDataCoordinator, IDisposable
{
    private readonly object _sync = new();
    private readonly DataPaths _paths;
    private readonly SemaphoreSlim _maintenanceQueue = new(1, 1);
    private TaskCompletionSource _changed = Signal();
    private DataEpoch _epoch = new(Guid.NewGuid());
    private readonly HashSet<Guid> _readers = new();
    private int _operations;
    private bool _maintenance, _disposed;
    private FileStream? _instanceLock;
    public ApplicationDataCoordinator(DataPaths paths) { _paths = paths; }
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private void Pulse() { var old = _changed; _changed = Signal(); old.TrySetResult(); }
    public DataEpoch CurrentEpoch { get { lock (_sync) return _epoch; } }
    public void EnsureInstanceLock()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_instanceLock is not null) return;
            Directory.CreateDirectory(_paths.DirectoryPath);
            try
            {
                // FileShare.None is an OS-backed exclusive lock, not a lock-file-exists test.
                _instanceLock = new FileStream(Path.Combine(_paths.DirectoryPath, "library.lock"),
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex) { throw new InvalidOperationException("该书库已被另一实例打开，或锁文件无法访问。请关闭另一实例后重试。", ex); }
        }
    }
    public async Task<IDataOperationLease> EnterOperationAsync(DataEpoch expectedEpoch, CancellationToken ct)
    {
        EnsureInstanceLock();
        while (true)
        {
            Task wait;
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this); ct.ThrowIfCancellationRequested();
                if (_epoch != expectedEpoch) throw new InvalidOperationException("StaleContext：数据集已改变，请重新打开。");
                if (!_maintenance) { _operations++; return new OperationLease(this, _epoch); }
                wait = _changed.Task;
            }
            await wait.WaitAsync(ct);
        }
    }
    public async Task<IDataMaintenanceLease> EnterMaintenanceAsync(MaintenanceKind kind, CancellationToken ct)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentException("维护类型无效。");
        EnsureInstanceLock();
        await _maintenanceQueue.WaitAsync(ct);
        var started = false;
        try
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this); ct.ThrowIfCancellationRequested();
                if (_readers.Count != 0) throw new InvalidOperationException("请先退出阅读器，再执行维护。");
                _maintenance = started = true; Pulse();
            }
            while (true)
            {
                Task wait;
                lock (_sync)
                {
                    ct.ThrowIfCancellationRequested(); ObjectDisposedException.ThrowIf(_disposed, this);
                    if (_operations == 0) return new MaintenanceLease(this, _epoch, kind);
                    wait = _changed.Task;
                }
                await wait.WaitAsync(ct);
            }
        }
        catch
        {
            lock (_sync) { if (started) { _maintenance = false; Pulse(); } }
            _maintenanceQueue.Release(); throw;
        }
    }
    public Task<IReaderRegistration> RegisterReaderAsync(ReaderOpenIdentity identity, CancellationToken ct)
    {
        EnsureInstanceLock();
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this); ct.ThrowIfCancellationRequested();
            if (_maintenance) throw new InvalidOperationException("维护正在进行，不能打开阅读器。");
            if (identity.Epoch != _epoch) throw new InvalidOperationException("StaleContext：数据集已改变。");
            if (identity.BookId <= 0 || identity.BookUid == Guid.Empty || identity.ReaderInstanceId == Guid.Empty || identity.ParameterVersion < 0)
                throw new ArgumentException("阅读器身份无效。");
            ReaderContractValidator.Validate(identity.Document);
            if (!_readers.Add(identity.ReaderInstanceId)) throw new InvalidOperationException("阅读器已注册。");
            return Task.FromResult<IReaderRegistration>(new Registration(this, identity.ReaderInstanceId));
        }
    }
    internal void ValidateLease(IDataOperationLease lease)
    {
        lock (_sync)
        {
            if (lease is not OperationLease own || own.Owner != this || own.Released || own.Epoch != _epoch)
                throw new InvalidOperationException("正常操作租约已失效或不属于该数据集。");
        }
    }
    internal void ValidateLease(IDataMaintenanceLease lease)
    {
        lock (_sync)
        {
            if (lease is not MaintenanceLease own || own.Owner != this || own.Released || !_maintenance || own.Epoch != _epoch)
                throw new InvalidOperationException("维护租约已失效或不属于该数据集。");
        }
    }
    public void Dispose()
    {
        lock (_sync)
        {
            if (_operations != 0 || _maintenance || _readers.Count != 0) throw new InvalidOperationException("数据操作尚未结束，不能关闭书库。");
            _disposed = true; _instanceLock?.Dispose(); _instanceLock = null; Pulse();
        }
    }
    private sealed class OperationLease(ApplicationDataCoordinator owner, DataEpoch epoch) : IDataOperationLease
    {
        public ApplicationDataCoordinator Owner => owner;
        public DataEpoch Epoch { get; } = epoch;
        public bool Released { get; private set; }
        public ValueTask DisposeAsync() { lock (owner._sync) { if (!Released) { Released = true; owner._operations--; owner.Pulse(); } } return ValueTask.CompletedTask; }
    }
    private sealed class MaintenanceLease(ApplicationDataCoordinator owner, DataEpoch epoch, MaintenanceKind kind) : IDataMaintenanceLease
    {
        public ApplicationDataCoordinator Owner => owner;
        public DataEpoch Epoch { get; private set; } = epoch;
        public bool Released { get; private set; }
        private bool _replaced;
        public DataEpoch CompleteReplacement()
        {
            lock (owner._sync)
            {
                owner.ValidateLease(this);
                if (kind == MaintenanceKind.Backup) throw new InvalidOperationException("备份不改变数据代际。");
                if (!_replaced) { owner._epoch = Epoch = new(Guid.NewGuid()); _replaced = true; owner.Pulse(); }
                return Epoch;
            }
        }
        public ValueTask DisposeAsync()
        {
            lock (owner._sync) { if (Released) return ValueTask.CompletedTask; Released = true; owner._maintenance = false; owner.Pulse(); }
            owner._maintenanceQueue.Release(); return ValueTask.CompletedTask;
        }
    }
    private sealed class Registration(ApplicationDataCoordinator owner, Guid instance) : IReaderRegistration
    {
        public ValueTask DisposeAsync() { lock (owner._sync) { owner._readers.Remove(instance); owner.Pulse(); } return ValueTask.CompletedTask; }
    }
}
