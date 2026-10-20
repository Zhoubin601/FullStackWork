using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using ReadJournal.Library.Services;
using ReadJournal.Library.ViewModels;
namespace ReadJournal.Services;
public class AlertService : IAlertService
{
    private static Window Owner => ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;
    private static async Task<string> ChooseAsync(string title, string message, params string[] choices)
    {
        var panel = new StackPanel { Spacing = 20, Margin = new Thickness(28) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right }; panel.Children.Add(buttons);
        var dialog = new Window { Title = title, Width = 560, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = panel };
        foreach (var choice in choices) { var button = new Button { Content = choice }; button.Click += (_, _) => dialog.Close(choice); buttons.Children.Add(button); }
        return await dialog.ShowDialog<string>(Owner) ?? "取消";
    }
    public async Task<bool> ConfirmAsync(string title, string message) => await ChooseAsync(title, message, "取消", "确定") == "确定";
    public async Task<LeaveDecision> ConfirmLeaveAsync() => await ChooseAsync("尚未保存", "当前修改尚未保存，请选择如何离开。", "取消离开", "放弃修改", "保存并离开") switch { "保存并离开" => LeaveDecision.Save, "放弃修改" => LeaveDecision.Discard, _ => LeaveDecision.Cancel };
    public async Task<bool> EditRecordAsync(ReadingRecordEditViewModel editor)
    {
        var dialog = new Window { Title = "阅读记录", Width = 650, Height = 660, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = editor };
        var saved = false; var closing = false;
        void Saved(object? sender, EventArgs e) { saved = true; closing = true; dialog.Close(true); }
        editor.Saved += Saved;
        dialog.Closing += async (_, e) => { if (closing) return; e.Cancel = true; if (await editor.CanLeaveAsync()) { closing = true; dialog.Close(false); } };
        try { return await dialog.ShowDialog<bool>(Owner) || saved; }
        finally { editor.Saved -= Saved; dialog.Content = null; }
    }
}
