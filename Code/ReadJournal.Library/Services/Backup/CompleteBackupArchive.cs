using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
namespace ReadJournal.Library.Services.Backup;

public sealed class PreparedBackup : IDisposable
{
    public string Root { get; }
    public BackupDataV2 Data { get; }
    public IReadOnlyList<BackupFileEntry> Files { get; }
    public int InputVersion { get; }
    internal PreparedBackup(string root, BackupDataV2 data, IReadOnlyList<BackupFileEntry> files, int inputVersion)
    { Root = root; Data = data; Files = files; InputVersion = inputVersion; }
    public void Dispose()
    {
        try { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { System.Diagnostics.Trace.WriteLine("备份校验临时目录清理未完成。"); }
    }
}
public sealed class CompleteBackupArchive
{
    private readonly BackupV2Validator _validator;
    private readonly BackupV1Adapter _v1;
    private readonly BackupLimits _limits;
    private readonly ManagedBookFiles _files;
    public CompleteBackupArchive(BackupV2Validator validator, BackupV1Adapter v1, BackupLimits limits, ManagedBookFiles files)
    { _validator = validator; _v1 = v1; _limits = limits; _files = files; }
    public PreparedBackup Prepare(string path, CancellationToken ct)
    {
        var root = Path.Combine(Path.GetTempPath(), "ReadJournalBackupValidation", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            ct.ThrowIfCancellationRequested();
            using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (source.Length > _limits.MaxArchiveBytes || source.Length < 2) throw new ArgumentException("LimitExceeded：备份包大小无效。");
            var signature = new byte[4]; var read = source.Read(signature); source.Position = 0;
            if (read < 4 || signature[0] != 'P' || signature[1] != 'K')
            {
                if (source.Length > _limits.MaxJsonBytes) throw new ArgumentException("LimitExceeded：JSON备份过大。");
                var bytes = ReadBounded(source, _limits.MaxJsonBytes, ct);
                if (bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) bytes = bytes[3..];
                // Legacy properties retain their original optional/required rules; duplicate names still reject.
                using var parsed = JsonDocument.Parse(bytes);
                if (!parsed.RootElement.TryGetProperty("FormatVersion", out var version) || version.GetInt32() != 1) throw new ArgumentException("InvalidFormat：不支持的备份格式。");
                RejectDuplicates(parsed.RootElement);
                var legacy = JsonSerializer.Deserialize<BackupDocument>(bytes) ?? throw new ArgumentException("InvalidFormat：旧备份为空。");
                var data = _validator.Validate(_v1.Upgrade(legacy));
                return new PreparedBackup(root, data, [], 1);
            }
            using var zip = new ZipArchive(source, ZipArchiveMode.Read, true);
            if (zip.Entries.Count > _limits.MaxEntries || zip.Entries.Count < 2) throw new ArgumentException("LimitExceeded：ZIP条目数无效。");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0;
            foreach (var entry in zip.Entries)
            {
                ct.ThrowIfCancellationRequested(); var name = entry.FullName; ManagedBookFiles.ValidateRelative(name);
                var mode = (entry.ExternalAttributes >> 16) & 0xf000;
                if ((mode != 0 && mode != 0x8000) || (entry.ExternalAttributes & 0x10) != 0 || !names.Add(name) ||
                    name != "manifest.json" && name != "data.json" && !name.StartsWith("books/managed/", StringComparison.Ordinal)) throw new ArgumentException("InvalidFormat：ZIP路径、文件类型或重复项无效。");
                total = checked(total + entry.Length);
                if (entry.Length > _limits.MaxEntryBytes || total > _limits.MaxExpandedBytes || ((name is "manifest.json" or "data.json") && entry.Length > _limits.MaxJsonBytes)) throw new ArgumentException("LimitExceeded：ZIP展开量超限。");
            }
            var manifestEntry = zip.GetEntry("manifest.json") ?? throw new ArgumentException("InvalidFormat：缺少manifest.json。");
            var dataEntry = zip.GetEntry("data.json") ?? throw new ArgumentException("InvalidFormat：缺少data.json。");
            var manifest = BackupJson.Read<CompleteBackupManifest>(ReadBounded(manifestEntry.Open(), _limits.MaxJsonBytes, ct, dispose: true));
            var dataBytes = ReadBounded(dataEntry.Open(), _limits.MaxJsonBytes, ct, dispose: true);
            if (manifest.FormatVersion != 2 || manifest.SchemaVersion != 2 || manifest.CreatedAt == default || manifest.Files is null ||
                manifest.Files.Any(f => f is null) || !Services.Reader.ReaderContractValidator.IsSha256(manifest.DataSha256) || ManagedBookFiles.Sha256(dataBytes) != manifest.DataSha256) throw new ArgumentException("DocumentMismatch：备份清单或data.json摘要无效。");
            var dataV2 = _validator.Validate(BackupJson.Read<BackupDataV2>(dataBytes));
            var declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in manifest.Files)
            {
                ct.ThrowIfCancellationRequested(); ManagedBookFiles.ValidateRelative(file.Path);
                if (!file.Path.StartsWith("books/managed/", StringComparison.Ordinal) || !declared.Add(file.Path) || file.Bytes < 0 || file.Bytes > _limits.MaxEntryBytes || !Services.Reader.ReaderContractValidator.IsSha256(file.Sha256)) throw new ArgumentException("InvalidFormat：文件清单无效。");
                var entry = zip.GetEntry(file.Path) ?? throw new ArgumentException("FileMissing：备份缺少文件。");
                if (entry.Length != file.Bytes) throw new ArgumentException("DocumentMismatch：文件长度不一致。");
                var target = ManagedBookFiles.SafePath(root, file.Path); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var input = entry.Open(); using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[65536]; long copied = 0; int count;
                while ((count = input.Read(buffer)) > 0)
                { ct.ThrowIfCancellationRequested(); copied = checked(copied + count); if (copied > file.Bytes) throw new ArgumentException("LimitExceeded：实际展开长度超限。"); hash.AppendData(buffer, 0, count); output.Write(buffer, 0, count); }
                if (copied != file.Bytes || Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant() != file.Sha256) throw new ArgumentException("DocumentMismatch：展开后文件摘要不一致。");
            }
            if (!names.SetEquals(declared.Append("manifest.json").Append("data.json"))) throw new ArgumentException("InvalidFormat：ZIP包含清单外文件。");
            var actual = ManagedBookInventory.Inspect(root, dataV2, _limits);
            if (!ManagedBookInventory.Same(actual, manifest.Files)) throw new ArgumentException("DocumentMismatch：清单与规范化资源不一致。");
            return new PreparedBackup(root, dataV2, actual, 2);
        }
        catch { Directory.Delete(root, true); throw; }
    }
    public void Write(string sourceRoot, BackupDataV2 data, IReadOnlyList<BackupFileEntry> inventory, string path, CancellationToken ct)
    {
        data = _validator.Validate(data);
        if (!string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("请使用 .zip 扩展名保存完整备份。");
        var full = Path.GetFullPath(path); ManagedBookFiles.RejectLink(full); _files.CreateDirectory(Path.GetDirectoryName(full)!);
        var temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var dataBytes = BackupJson.Write(data);
            var manifest = new CompleteBackupManifest(2, 2, DateTimeOffset.UtcNow, ManagedBookFiles.Sha256(dataBytes), inventory);
            var manifestBytes = BackupJson.Write(manifest);
            if (dataBytes.Length > _limits.MaxJsonBytes || manifestBytes.Length > _limits.MaxJsonBytes) throw new ArgumentException("LimitExceeded：业务快照或清单超限。");
            using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
                {
                    using (var stream = zip.CreateEntry("manifest.json", CompressionLevel.Optimal).Open()) stream.Write(manifestBytes);
                    using (var stream = zip.CreateEntry("data.json", CompressionLevel.Optimal).Open()) stream.Write(dataBytes);
                    foreach (var file in inventory)
                    {
                        ct.ThrowIfCancellationRequested();
                        using var source = File.OpenRead(ManagedBookFiles.SafePath(sourceRoot, file.Path));
                        using var destination = zip.CreateEntry(file.Path, CompressionLevel.Optimal).Open();
                        var buffer = new byte[65536]; int count;
                        while ((count = source.Read(buffer)) > 0) { ct.ThrowIfCancellationRequested(); destination.Write(buffer, 0, count); }
                    }
                }
                output.Flush(true);
            }
            using (var verified = Prepare(temp, ct))
                if (BackupDataStore.Hash(verified.Data) != BackupDataStore.Hash(data) || !ManagedBookInventory.Same(verified.Files, inventory)) throw new IOException("备份复核失败。");
            ct.ThrowIfCancellationRequested(); _files.Checkpoint("backup.before-install"); _files.InstallFile(temp, full);
        }
        finally { _files.DeleteFile(temp); }
    }
    private static byte[] ReadBounded(Stream stream, long limit, CancellationToken ct, bool dispose = false)
    {
        try
        {
            using var memory = new MemoryStream(); var buffer = new byte[65536]; int count;
            while ((count = stream.Read(buffer)) > 0) { ct.ThrowIfCancellationRequested(); if (memory.Length + count > limit) throw new ArgumentException("LimitExceeded：JSON实际长度超限。"); memory.Write(buffer, 0, count); }
            return memory.ToArray();
        }
        finally { if (dispose) stream.Dispose(); }
    }
    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject()) { if (!names.Add(property.Name)) throw new ArgumentException("InvalidFormat：JSON重复属性。"); RejectDuplicates(property.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) RejectDuplicates(item);
    }
}
