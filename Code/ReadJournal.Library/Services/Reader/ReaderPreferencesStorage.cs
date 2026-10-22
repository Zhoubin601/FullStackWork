using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Backup;
namespace ReadJournal.Library.Services.Reader;

public sealed class ReaderPreferencesStorage(DataPaths paths, ManagedBookFiles files) : IReaderPreferencesStorage
{
    public static ReaderAppearance Default => new("Noto Sans CJK SC", 22, 34, 0, 0, 24, 24, "paper");
    public static void Validate(ReaderAppearance a)
    {
        if (string.IsNullOrWhiteSpace(a.FontFamily) || a.FontFamily.Length > 200 || !double.IsFinite(a.FontSize + a.LineHeight + a.MarginHorizontal + a.MarginVertical) ||
            a.FontSize is < 10 or > 72 || a.LineHeight < a.FontSize || a.LineHeight > 144 || a.FirstLineIndent != 0 || a.ParagraphSpacing != 0 ||
            a.MarginHorizontal is < 0 or > 100 || a.MarginVertical is < 0 or > 100 || a.Palette is not ("paper" or "light" or "dark")) throw new ArgumentException("阅读字体/行距/边距或配色无效。");
    }
    public async Task<ReaderAppearance> GetAsync(CancellationToken ct)
    {
        var path = ManagedBookFiles.SafePath(paths.DirectoryPath, "reader-preferences.json"); if (!File.Exists(path)) return Default;
        try { var a = BackupJson.Read<ReaderAppearance>(await File.ReadAllBytesAsync(path, ct)); Validate(a); return a; }
        catch (Exception ex) when (ex is ArgumentException or System.Text.Json.JsonException) { throw new InvalidOperationException("阅读偏好无效，请检查reader-preferences.json；应用主题不受影响。", ex); }
    }
    public Task SaveAsync(ReaderAppearance appearance, CancellationToken ct)
    { Validate(appearance); return Task.Run(() => { ct.ThrowIfCancellationRequested(); files.WriteDurable(ManagedBookFiles.SafePath(paths.DirectoryPath, "reader-preferences.json"), BackupJson.Write(appearance)); }, ct); }
}
