using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ReadJournal.Library.Services;
namespace ReadJournal.Services;
public class FileService : IFileService
{
    private static Window Owner => ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;
    private static FilePickerFileType JsonType => new("JSON 备份") { Patterns = ["*.json"] };
    public async Task<string?> ChooseSavePathAsync() { var file = await Owner.StorageProvider.SaveFilePickerAsync(new() { Title = "保存阅记备份", SuggestedFileName = "阅记备份-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json", DefaultExtension = "json", FileTypeChoices = [JsonType] }); return file?.TryGetLocalPath(); }
    public async Task<string?> ChooseOpenPathAsync() { var files = await Owner.StorageProvider.OpenFilePickerAsync(new() { Title = "选择阅记备份", AllowMultiple = false, FileTypeFilter = [JsonType] }); return files.FirstOrDefault()?.TryGetLocalPath(); }
    public Task<string> ReadTextAsync(string path) => File.ReadAllTextAsync(path);
    public async Task WriteTextAsync(string path, string contents)
    {
        var full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); var temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await File.WriteAllTextAsync(temp, contents); File.Move(temp, full, true); } finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
