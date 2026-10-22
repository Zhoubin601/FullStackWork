using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Services;
public class FileService : IFileService, IAppFileDialogService
{
    private static Window Owner => ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;
    private static FilePickerFileType ZipType => new("完整 ZIP 备份") { Patterns = ["*.zip"] };
    private static FilePickerFileType JsonType => new("JSON 备份") { Patterns = ["*.json"] };
    public Task<string?> ChooseBackupSavePathAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); return ChooseSavePathAsync(); }
    public Task<string?> ChooseBackupOpenPathAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); return ChooseOpenPathAsync(); }
    public async Task<IReadOnlyList<string>> ChooseBooksAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var selected = await Owner.StorageProvider.OpenFilePickerAsync(new() { Title = "选择要导入的书籍（当前支持TXT）", AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("TXT / EPUB书籍") { Patterns = ["*.txt", "*.epub"] }] });
        ct.ThrowIfCancellationRequested(); return selected.Select(f => f.TryGetLocalPath()).Where(p => p is not null).Cast<string>().ToArray();
    }
    public async Task<string?> ChooseSavePathAsync() { var file = await Owner.StorageProvider.SaveFilePickerAsync(new() { Title = "保存阅记备份", SuggestedFileName = "阅记备份-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip", DefaultExtension = "zip", FileTypeChoices = [ZipType] }); return file?.TryGetLocalPath(); }
    public async Task<string?> ChooseOpenPathAsync() { var files = await Owner.StorageProvider.OpenFilePickerAsync(new() { Title = "选择阅记备份", AllowMultiple = false, FileTypeFilter = [ZipType, JsonType] }); return files.FirstOrDefault()?.TryGetLocalPath(); }
    public Task<string> ReadTextAsync(string path) => File.ReadAllTextAsync(path);
    public async Task WriteTextAsync(string path, string contents)
    {
        var full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); var temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await File.WriteAllTextAsync(temp, contents); File.Move(temp, full, true); } finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
