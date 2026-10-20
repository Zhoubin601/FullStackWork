namespace ReadJournal.Library.Services;
public interface IPreferenceStorage
{
    event EventHandler<string>? ThemeChanged;
    Task<string> GetThemeAsync();
    Task SetThemeAsync(string theme);
}
