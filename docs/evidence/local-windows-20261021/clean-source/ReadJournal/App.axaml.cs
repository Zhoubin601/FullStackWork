using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReadJournal.Views;
namespace ReadJournal;
public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted() { if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.MainWindow = new MainWindow(); base.OnFrameworkInitializationCompleted(); InitializeTheme(); }
    private async void InitializeTheme()
    {
        var preferences = ServiceLocator.Current.PreferenceStorage;
        void Apply(string theme) => RequestedThemeVariant = theme == "深色" ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
        preferences.ThemeChanged += (_, theme) => Apply(theme);
        try { Apply(await preferences.GetThemeAsync()); } catch (Exception ex) { System.Diagnostics.Trace.WriteLine("主题读取失败：" + ex.Message); }
    }
}
