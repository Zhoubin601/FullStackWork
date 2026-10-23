using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReadJournal.Library.Models.Reader;
namespace ReadJournal.Library.Services.Backup;

// Shared IO seam for backup/restore and later import/delete. Fault probes override checkpoints or individual operations.
public class ManagedBookFiles
{
    public virtual void Checkpoint(string point) { }
    public virtual void CreateDirectory(string path) { Directory.CreateDirectory(path); }
    public virtual void MoveDirectory(string source, string destination) { Directory.Move(source, destination); SyncDirectory(Path.GetDirectoryName(source)!); SyncDirectory(Path.GetDirectoryName(destination)!); }
    public virtual void DeleteDirectory(string path) { if (Directory.Exists(path)) Directory.Delete(path, true); }
    public virtual void DeleteFile(string path) { if (File.Exists(path)) File.Delete(path); }
    public virtual void InstallFile(string source, string destination) { File.Move(source, destination, true); SyncDirectory(Path.GetDirectoryName(destination)!); }
    public virtual void WriteDurable(string path, byte[] bytes)
    {
        CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(true); }
            InstallFile(temporary, path);
        }
        finally { DeleteFile(temporary); }
    }
    public virtual void WriteJournal(string path, RestoreJournal journal)
    {
        Checkpoint("journal.before-write");
        WriteDurable(path, BackupJson.Write(new RestoreJournalEnvelope(1, journal, Sha256(BackupJson.Write(journal)))));
    }
    public virtual void WriteBookJournal(string path, BookFileJournal journal)
    {
        Checkpoint("book-journal.before-write");
        WriteDurable(path, BackupJson.Write(new BookFileJournalEnvelope(1, journal, Sha256(BackupJson.Write(journal)))));
    }
    // Flush rename metadata on the validated Linux filesystem as well as file contents.
    // Windows durability is retained for separate target-platform validation.
    private static void SyncDirectory(string path)
    {
        if (!OperatingSystem.IsLinux()) return;
        var fd = OpenDirectory(path, 0x10000);
        if (fd < 0) throw new IOException("目录同步无法打开目标目录。", new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastPInvokeError()));
        try { if (Fsync(fd) != 0) throw new IOException("目录同步失败。", new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastPInvokeError())); }
        finally { Close(fd); }
    }
    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "open", SetLastError = true)] private static extern int OpenDirectory(string path, int flags);
    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "fsync", SetLastError = true)] private static extern int Fsync(int fd);
    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "close")] private static extern int Close(int fd);
    public static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public static string HashFile(string path)
    { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    public static void ValidateRelative(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 400 || path.StartsWith('/') || path.Contains('\\') || path.Contains(':') ||
            path.Split('/').Any(p => p is "" or "." or ".." || p.Any(c => c < 32 || c > 126 || "<>\"|?*".Contains(c)) || p.EndsWith('.') || p.EndsWith(' ')))
            throw new ArgumentException("InvalidFormat：文件路径无效或越界。");
        foreach (var part in path.Split('/'))
            if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(part.Split('.')[0], StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException("InvalidFormat：文件名是系统保留名称。");
    }
    public static string SafePath(string root, string relative)
    {
        ValidateRelative(relative); root = Path.GetFullPath(root);
        RejectLink(root);
        var full = root;
        foreach (var part in relative.Split('/')) { full = Path.Combine(full, part); RejectLink(full); }
        if (!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("InvalidFormat：路径超出受管目录。");
        return full;
    }
    public static void RejectLink(string path)
    {
        // Inspect every existing ancestor too, including links to absent targets.
        for (var item = new FileInfo(Path.GetFullPath(path)); item is not null; item = item.Directory is { } parent ? new FileInfo(parent.FullName) : null)
            if ((File.Exists(item.FullName) || Directory.Exists(item.FullName) || item.LinkTarget is not null) &&
                ((File.GetAttributes(item.FullName) & FileAttributes.ReparsePoint) != 0 || item.LinkTarget is not null))
                throw new ArgumentException("InvalidFormat：不允许符号链接或重解析点。");
    }
    public static string BookPrefix(Guid fileId) => "books/managed/" + fileId.ToString("D") + "/";
}
public static class BackupJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Kind == System.Text.Json.Serialization.Metadata.JsonTypeInfoKind.Object)
                foreach (var property in info.Properties) if (property.Get is not null && property.Set is not null) property.IsRequired = true;
        });
        return new() { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow, TypeInfoResolver = resolver };
    }
    public static T Read<T>(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        void Unique(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                { if (!names.Add(property.Name)) throw new ArgumentException("InvalidFormat：JSON包含重复属性。"); Unique(property.Value); }
            }
            else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) Unique(item);
        }
        Unique(document.RootElement);
        return JsonSerializer.Deserialize<T>(bytes, Options) ?? throw new ArgumentException("InvalidFormat：JSON内容为空。");
    }
    public static byte[] Write<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);
}
