using CommunityToolkit.Mvvm.Input;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.ViewModels;

public sealed class ReaderViewModel : ViewModelBase
{
    private readonly IDatabaseService _database; private readonly IApplicationDataCoordinator _data;
    private readonly IReaderDocumentService _documents; private readonly IReaderLayoutService _layouts;
    private readonly IReaderSessionService _sessions; private readonly IReaderPreferencesStorage _preferences;
    private readonly IReaderPlatformService _platform; private readonly IReaderAlertService _alerts; private readonly ReaderClock _clock;
    private readonly IReaderQuoteService? _quotes; private readonly IAlertService? _generalAlerts;
    private DocumentRange? _selection; private string _notice = "";
    public DocumentRange? Selection { get => _selection; private set => SetProperty(ref _selection, value); }
    public string Notice { get => _notice; private set => SetProperty(ref _notice, value); }
    public IAsyncRelayCommand CopySelectionCommand { get; }
    public IAsyncRelayCommand SaveSelectionCommand { get; }
    public IAsyncRelayCommand MarkCompletedCommand { get; }
    public IAsyncRelayCommand ResetProgressCommand { get; }
    private ReaderOpenRequest? _request; private ReaderOpenIdentity? _identity; private ReaderContext? _context;
    private ReaderPosition? _position; private ReaderPosition? _start; private DateTimeOffset _opened;
    private SessionCloseCommand? _frozen; private LayoutHandle? _layout;
    private CancellationTokenSource _lifetime = new(); private CancellationTokenSource? _reflow; private CancellationTokenSource? _index;
    private Task _opening = Task.CompletedTask, _beginning = Task.CompletedTask, _reflowing = Task.CompletedTask, _indexing = Task.CompletedTask;
    private readonly SemaphoreSlim _layoutGate = new(1, 1);
    private int _revision; private double _width = 760, _height = 440, _dpi = 1;
    private ReaderPage? _page; private ReaderDocument? _document; private ReaderPhase _phase;
    private ReaderAppearance _appearance = ReaderPreferencesStorage.Default;
    private string _title = "阅读器", _indexText = "总页数计算中", _fontSize = "22", _palette = "paper";
    private bool _layoutBusy, _fullScreen;
    protected override bool UseGlobalBusyForLoad => false;
    public ReaderPage? Page { get => _page; private set => SetProperty(ref _page, value); }
    public ReaderDocument? Document { get => _document; private set => SetProperty(ref _document, value); }
    public ReaderContext? Context => _context;
    public ReaderPosition? Position => _position;
    public ReaderPhase Phase { get => _phase; private set { SetProperty(ref _phase, value); OnPropertyChanged(nameof(IsCloseFailed)); } }
    public bool IsCloseFailed => Phase == ReaderPhase.CloseFailed;
    public bool IsLayoutBusy { get => _layoutBusy; private set => SetProperty(ref _layoutBusy, value); }
    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string IndexText { get => _indexText; private set => SetProperty(ref _indexText, value); }
    public string FontSizeText { get => _fontSize; set => SetProperty(ref _fontSize, value); }
    public string Palette { get => _palette; set => SetProperty(ref _palette, value); }
    public string FontFamily { get => _appearance.FontFamily; set { _appearance = _appearance with { FontFamily = value }; OnPropertyChanged(); } }
    public IAsyncRelayCommand NextCommand { get; }
    public IAsyncRelayCommand PreviousCommand { get; }
    public IAsyncRelayCommand ApplyAppearanceCommand { get; }
    public IAsyncRelayCommand FullScreenCommand { get; }
    public IAsyncRelayCommand RetryCloseCommand { get; }
    public IAsyncRelayCommand ContinueReadingCommand { get; }
    public IAsyncRelayCommand<TocEntry> GoToChapterCommand { get; }
    public ReaderViewModel(IDatabaseService database, IApplicationDataCoordinator data, IReaderDocumentService documents,
        IReaderLayoutService layouts, IReaderSessionService sessions, IReaderPreferencesStorage preferences,
        IReaderPlatformService platform, IReaderAlertService alerts, ReaderClock clock, MainViewModel main, IReaderQuoteService? quotes = null, IAlertService? generalAlerts = null)
    {
        _database = database; _data = data; _documents = documents; _layouts = layouts; _sessions = sessions;
        _preferences = preferences; _platform = platform; _alerts = alerts; _clock = clock; _quotes = quotes; _generalAlerts = generalAlerts;
        CopySelectionCommand = new AsyncRelayCommand(() => Observe(CopySelectionAsync));
        SaveSelectionCommand = new AsyncRelayCommand(() => Observe(SaveSelectionAsync));
        MarkCompletedCommand = new AsyncRelayCommand(() => Observe(async () => { if (_context is null || Phase != ReaderPhase.Active) return; await _sessions.MarkCompletedAsync(_context, _lifetime.Token); Notice = "已标记读完；继续回看不会取消完成状态。"; }));
        ResetProgressCommand = new AsyncRelayCommand(() => Observe(async () => { if (ReferenceEquals(main.Content, this)) await main.RequestInPlaceAsync(ResetAsync); }));
        NextCommand = new AsyncRelayCommand(() => Observe(() => FlipAsync(PageDirection.Next)));
        PreviousCommand = new AsyncRelayCommand(() => Observe(() => FlipAsync(PageDirection.Previous)));
        ApplyAppearanceCommand = new AsyncRelayCommand(() => Observe(ApplyAppearanceAsync));
        FullScreenCommand = new AsyncRelayCommand(() => Observe(async () => { if (Phase is ReaderPhase.Closing or ReaderPhase.CloseFailed) return; _fullScreen = !_fullScreen; await _platform.SetFullScreenAsync(_fullScreen, _lifetime.Token); }));
        RetryCloseCommand = new AsyncRelayCommand(() => NavigateAsync(() => main.RequestBackAsync(this)));
        ContinueReadingCommand = new AsyncRelayCommand(() => Observe(ContinueAsync));
        GoToChapterCommand = new AsyncRelayCommand<TocEntry>(e => e is null ? Task.CompletedTask : Observe(() => LocateAsync(e.Target)));
    }
    private async Task Observe(Func<Task> action)
    { var version = ParameterVersion; try { await action(); } catch (OperationCanceledException) { } catch (Exception ex) { if (version == ParameterVersion) Error = ex.Message; } }
    public override void SetParameter(object parameter)
    {
        if (parameter is not ReaderOpenRequest { BookId: > 0 } request) throw new ArgumentException("阅读器参数无效。");
        if (_context is not null) throw new InvalidOperationException("请先关闭当前阅读会话。");
        base.SetParameter(parameter); _request = request; _identity = null; _position = null; _start = null; _frozen = null;
        _lifetime.Dispose(); _lifetime = new(); Phase = ReaderPhase.Idle; Page = null; Document = null; Selection = null; Notice = "";
    }
    public override void InvalidateDataSet()
    { if (_context is not null) throw new InvalidOperationException("活动阅读器不能替换数据集。"); _lifetime.Cancel(); base.SetParameter(null!); _request = null; Page = null; Document = null; _identity = null; }
    protected override Task LoadAsync()
    { if (_request is null || Phase != ReaderPhase.Idle) return Task.CompletedTask; return _opening = Observe(OpenAsync); }
    private async Task OpenAsync()
    {
        Phase = ReaderPhase.Opening; var request = _request!; var version = ParameterVersion; var ct = _lifetime.Token;
        try
        {
            await _database.InitializeAsync(); var epoch = _data.CurrentEpoch;
            await using var lease = await _data.EnterOperationAsync(epoch, ct);
            var info = await _database.ReadAsync(lease, c => (c.Find<Book>(request.BookId), c.Find<EbookFile>(request.BookId), c.Find<EbookProgress>(request.BookId)));
            if (info.Item1 is not { Kind: BookKind.Ebook } book || info.Item2 is not { Status: 0 } file) throw new InvalidOperationException("原文缺失或不可读，请返回书库修复。");
            var key = new DocumentKey(file.FileId, file.SourceSha256, file.CanonicalSha256, file.ParserVersion);
            var identity = new ReaderOpenIdentity(book.Id, book.BookUid, key, epoch, Guid.NewGuid(), version);
            var document = await _documents.OpenAsync(identity, ct); var appearance = await _preferences.GetAsync(ct);
            var position = request.Target?.Start ?? (info.Item3 is { } saved ? ReaderContractValidator.ReadPosition(saved.LastPositionJson) : new(key, document.Chapters[0].Key, 0));
            var resolution = await _documents.ResolveAsync(position, null, ct);
            if (!resolution.IsExact) throw new InvalidOperationException(resolution.Reason ?? "正文位置已失效。");
            ct.ThrowIfCancellationRequested(); if (version != ParameterVersion) return;
            _identity = identity; Document = document; Title = document.Title; _appearance = appearance;
            FontSizeText = appearance.FontSize.ToString(System.Globalization.CultureInfo.InvariantCulture); Palette = appearance.Palette; OnPropertyChanged(nameof(FontFamily));
            _position = position; Selection = request.Target; await ReflowAsync(); ct.ThrowIfCancellationRequested(); Phase = ReaderPhase.Presenting;
        }
        catch { if (!ct.IsCancellationRequested && version == ParameterVersion) Phase = ReaderPhase.OpenFailed; throw; }
    }
    public Task PagePresentedAsync(LayoutId layout)
    {
        if (Phase != ReaderPhase.Presenting || _layout?.Id != layout || _identity is null || _position is null) return Task.CompletedTask;
        if (!_beginning.IsCompleted) return _beginning;
        return _beginning = Observe(async () =>
        {
            var identity = _identity; var position = _position; var now = _clock.Now;
            _context = await _sessions.BeginAsync(identity, position, now, _lifetime.Token);
            _start = position; _opened = now; Phase = ReaderPhase.Active; StartIndex();
        });
    }
    public bool IsSameBook(object parameter) => parameter is ReaderOpenRequest request && request.BookId == _identity?.BookId && _identity.Epoch == _data.CurrentEpoch && Phase == ReaderPhase.Active;
    public Task NavigateWithinBookAsync(ReaderOpenRequest request, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); Selection = request.Target; return request.Target is null ? Task.CompletedTask : LocateAsync(request.Target.Start); }
    public Task SetSelectionAsync(DocumentRange? selection) => Observe(async () =>
    {
        if (Phase != ReaderPhase.Active) return;
        if (selection is not null) { ReaderContractValidator.Validate(selection); if (selection.Start.Document != _identity?.Document) return; await _documents.ExtractAsync(selection, _lifetime.Token); }
        Selection = selection;
    });
    private async Task CopySelectionAsync()
    {
        if (_context is null || Selection is null || Phase != ReaderPhase.Active) return;
        await using var action = await _sessions.EnterActiveActionAsync(_context, _lifetime.Token);
        var text = await _documents.ExtractAsync(Selection, _lifetime.Token);
        await _platform.CopyTextAsync(string.Concat(text.Spans.Select(s => s.Text)), _lifetime.Token); Notice = "已复制选区正文。";
    }
    private async Task SaveSelectionAsync()
    {
        if (_context is null || Selection is null || Phase != ReaderPhase.Active || _quotes is null) return;
        var id = await _quotes.SaveSelectionAsync(_context, Selection, "", _lifetime.Token); Notice = $"书摘已保存（#{id}），可在书摘收藏中编辑感想或回跳。";
    }
    private async Task ResetAsync()
    {
        if (_context is null || _identity is null || Phase != ReaderPhase.Active || _generalAlerts is null) return;
        var identity = _identity;
        await using (var lease = await _data.EnterOperationAsync(identity.Epoch, _lifetime.Token))
            await _database.ReadAsync(lease, c => { var book = c.Find<Book>(identity.BookId)!; if (book.BookUid != identity.BookUid) throw new InvalidOperationException("StaleContext"); ReadingStateCalculator.ValidateReset(book, c.Table<ReadingPlan>().Count(p => p.BookId == book.Id)); return 0; });
        if (!await _generalAlerts.ConfirmAsync("重置正文进度", "将先保存本次阅读，再重置位置与完成标记。历史阅读时长和书摘保留，是否继续？")) return;
        if (!await CanLeaveAsync()) return;
        string? failure = null;
        try { await _sessions.ResetProgressAsync(identity.BookId, identity.BookUid, identity.Epoch, CancellationToken.None); }
        catch (Exception ex) { failure = ex.Message; }
        SetParameter(new ReaderOpenRequest(identity.BookId)); await OnLoadedCommand.ExecuteAsync(null);
        if (failure is not null) Error = "重置未完成：" + failure;
    }
    private async Task LocateAsync(ReaderPosition position)
    {
        if (_layout is null || Phase is ReaderPhase.Closing or ReaderPhase.CloseFailed) return;
        var layout = _layout; var revision = _revision; var page = await _layouts.LocateAsync(layout, position, _lifetime.Token);
        if (revision != _revision || _layout != layout) return; _position = position; Page = page;
    }
    private async Task FlipAsync(PageDirection direction)
    {
        if (Phase != ReaderPhase.Active || Page is null || IsLayoutBusy) return;
        var layout = _layout; var revision = _revision; var page = await _layouts.GetAdjacentAsync(Page.Ref, direction, _lifetime.Token);
        if (page is null || revision != _revision || layout != _layout) return; _position = page.ContentRanges[0].Start; Page = page;
    }
    public Task SetViewportAsync(double width, double height, double dpi)
    {
        if (width < 180 || height < 120 || !double.IsFinite(width + height + dpi) || dpi <= 0 ||
            (Math.Abs(width - _width) < 1 && Math.Abs(height - _height) < 1 && dpi == _dpi)) return Task.CompletedTask;
        _width = width; _height = height; _dpi = dpi;
        return _identity is null || Phase is ReaderPhase.Idle or ReaderPhase.OpenFailed or ReaderPhase.Closing or ReaderPhase.CloseFailed ? Task.CompletedTask : Observe(ReflowAsync);
    }
    private async Task ReflowAsync()
    {
        _reflow?.Cancel(); var source = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token); _reflow = source;
        var task = ReflowCoreAsync(source); _reflowing = task; await task;
    }
    private async Task ReflowCoreAsync(CancellationTokenSource source)
    {
        var ct = source.Token; var acquired = false;
        try
        {
            await _layoutGate.WaitAsync(ct); acquired = true;
            ct.ThrowIfCancellationRequested(); IsLayoutBusy = true; ++_revision;
            _index?.Cancel(); await _indexing; Page = null;
            if (_layout is not null) { await _layouts.ReleaseAsync(_layout); _layout = null; }
            var anchor = _position!;
            var layout = await _layouts.CreateAsync(_identity!.Document, new(_width, _height, _dpi, _appearance, 1, "native-fallback-v1"), anchor, ct);
            try { ct.ThrowIfCancellationRequested(); var page = await _layouts.LocateAsync(layout, anchor, ct); ct.ThrowIfCancellationRequested(); _layout = layout; Page = page; }
            catch { await _layouts.ReleaseAsync(layout); throw; }
            if (Phase == ReaderPhase.Active) StartIndex();
        }
        finally { if (acquired) { IsLayoutBusy = false; _layoutGate.Release(); } source.Dispose(); if (ReferenceEquals(_reflow, source)) _reflow = null; }
    }
    private void StartIndex()
    {
        if (_layout is null) return; _index?.Dispose(); _index = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var layout = _layout; var ct = _index.Token; var revision = _revision;
        IndexText = "总页数计算中";
        _indexing = Observe(async () =>
        {
            await _layouts.BuildIndexAsync(layout, null, ct); var status = await _layouts.GetIndexStatusAsync(layout, ct);
            if (ct.IsCancellationRequested || revision != _revision || layout != _layout) return;
            IndexText = status.ExactGlobalPageCount is int pages ? $"全书 {pages} 页（随排版变化）" : "总页数计算中";
        });
    }
    private async Task ApplyAppearanceAsync()
    {
        if (Phase is ReaderPhase.Closing or ReaderPhase.CloseFailed) return;
        if (!double.TryParse(FontSizeText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var size)) throw new ArgumentException("字号请输入数字。");
        var appearance = _appearance with { FontSize = size, LineHeight = size * 1.55, Palette = Palette };
        await _preferences.SaveAsync(appearance, _lifetime.Token); _appearance = appearance; if (_identity is not null) await ReflowAsync();
    }
    public override async Task<bool> CanLeaveAsync()
    {
        if (_context is null)
        {
            _lifetime.Cancel(); _reflow?.Cancel(); _index?.Cancel(); await _opening; await _beginning;
            // Begin may have committed while cancellation was arriving.
            if (_context is null) { await ReleaseAsync(); return true; }
        }
        Phase = ReaderPhase.Closing;
        _reflow?.Cancel(); _index?.Cancel(); await Observe(async () => await _reflowing); await _indexing;
        while (true)
        {
            _frozen ??= new(_context!, _start!, _position!, _opened, _clock.Now, false, ReaderExitReason.Back);
            try
            {
                await _sessions.CloseAsync(_frozen, CancellationToken.None); _context = null; Phase = ReaderPhase.Idle;
                await ReleaseAsync(); return true;
            }
            catch (Exception ex)
            {
                Error = "保存阅读会话失败：" + ex.Message; Phase = ReaderPhase.CloseFailed;
                var decision = await _alerts.ChooseCloseFailureAsync(Error, CancellationToken.None);
                if (decision == ReaderCloseDecision.Retry) continue;
                if (decision == ReaderCloseDecision.ContinueReading) await Observe(ContinueAsync);
                return false;
            }
        }
    }
    private async Task ContinueAsync()
    {
        if (_context is null || Phase != ReaderPhase.CloseFailed) return;
        var now = _clock.Now; var previous = _context;
        _context = await _sessions.ContinueAfterCloseFailureAsync(previous, _position!, now, CancellationToken.None);
        if (_context != previous) { _identity = _context.Identity; _start = _position; _opened = now; }
        if (_lifetime.IsCancellationRequested) { _lifetime.Dispose(); _lifetime = new(); }
        _frozen = null; Error = ""; Phase = ReaderPhase.Active; if (_layout is null) await ReflowAsync(); else StartIndex();
    }
    private async Task ReleaseAsync()
    {
        _lifetime.Cancel(); _index?.Cancel(); await _indexing; Page = null;
        await _layouts.ReleaseAllAsync(); await _documents.ReleaseAllAsync(); _layout = null;
        if (_fullScreen) { await _platform.SetFullScreenAsync(false, CancellationToken.None); _fullScreen = false; }
        _identity = null; Document = null;
    }
}
