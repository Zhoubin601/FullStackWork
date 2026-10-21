using System.Text.Json;
namespace ReadJournal.Library.Services;
public class FilePreferenceStorage : IPreferenceStorage
{
    private readonly DataPaths _paths;
    public event EventHandler<string>? ThemeChanged;
    public FilePreferenceStorage(DataPaths paths) { _paths = paths; }
    public async Task<string> GetThemeAsync()
    {
        var path = Path.Combine(_paths.DirectoryPath, "preferences.json"); if (!File.Exists(path)) return "浅色";
        var prefs = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(path));
        return prefs is not null && prefs.TryGetValue("Theme", out var theme) && theme == "深色" ? "深色" : "浅色";
    }
    public async Task SetThemeAsync(string theme)
    {
        if (theme is not ("浅色" or "深色")) throw new ArgumentException("未知主题。");
        Directory.CreateDirectory(_paths.DirectoryPath); var path = Path.Combine(_paths.DirectoryPath, "preferences.json");
        var temp = path + ".tmp"; await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(new Dictionary<string, string> { { "Theme", theme } })); File.Move(temp, path, true); ThemeChanged?.Invoke(this, theme);
    }
}
