using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using ReadJournal.Library.ViewModels;
using ReadJournal.Reader;
namespace ReadJournal.Views;
public partial class ReaderView : UserControl
{
    public ReaderView()
    {
        InitializeComponent();
        var surface = this.FindControl<ReaderSurface>("Surface")!;
        surface.LayoutService = ServiceLocator.Current.ReaderLayoutService;
        surface.Presented = id => DataContext is ReaderViewModel vm ? vm.PagePresentedAsync(id) : Task.CompletedTask;
        surface.SizeChanged += async (_, _) => { if (DataContext is ReaderViewModel vm) await vm.SetViewportAsync(surface.Bounds.Width, surface.Bounds.Height, TopLevel.GetTopLevel(this)?.RenderScaling ?? 1); };
        Loaded += async (_, _) => { if (DataContext is ReaderViewModel vm) { await vm.SetViewportAsync(surface.Bounds.Width, surface.Bounds.Height, TopLevel.GetTopLevel(this)?.RenderScaling ?? 1); if (!vm.OnLoadedCommand.IsRunning) await vm.OnLoadedCommand.ExecuteAsync(null); surface.Focus(); } };
        KeyDown += async (_, e) =>
        {
            if (DataContext is not ReaderViewModel vm || e.Source is Control source && (source is TextBox or ComboBox || source.GetVisualAncestors().Any(v => v is TextBox or ComboBox))) return;
            var command = e.Key switch { Key.Right or Key.PageDown => vm.NextCommand, Key.Left or Key.PageUp => vm.PreviousCommand, Key.F11 => vm.FullScreenCommand, _ => null };
            if (command is null || command.IsRunning) return; e.Handled = true; await command.ExecuteAsync(null);
        };
    }
}
