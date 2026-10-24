using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using ReadJournal.Library.Services;
using ReadJournal.Library.ViewModels;
namespace ReadJournal.Services;
public class AlertService : IAlertService, ReadJournal.Library.Services.Reader.IReaderAlertService
{
    private static Window Owner => ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;
    private static async Task<string> ChooseAsync(string title, string message, params string[] choices)
    {
        var panel = new StackPanel { Spacing = 18 };
        panel.Children.Add(new TextBlock { Text = title, Classes = { "h2" } });
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        panel.Children.Add(buttons);
        var card = new Border { Classes = { "card" }, Margin = new Thickness(18), Child = panel };
        var dialog = new Window { Title = title, Width = 540, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = card };
        for (var i = 0; i < choices.Length; i++)
        {
            var choice = choices[i];
            var button = new Button { Content = choice };
            if (choice.Contains("放弃") || choice.Contains("删除")) button.Classes.Add("danger");
            else if (i == choices.Length - 1) button.Classes.Add("primary");
            else button.Classes.Add("secondary");
            button.Click += (_, _) => dialog.Close(choice);
            buttons.Children.Add(button);
        }
        return await dialog.ShowDialog<string>(Owner) ?? "取消";
    }
    public async Task<ReadJournal.Library.Models.Reader.ReaderCloseDecision> ChooseCloseFailureAsync(string message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return await ChooseAsync("阅读会话保存失败", message, "留在阅读器", "继续阅读", "重试保存") switch
        { "重试保存" => ReadJournal.Library.Models.Reader.ReaderCloseDecision.Retry, "继续阅读" => ReadJournal.Library.Models.Reader.ReaderCloseDecision.ContinueReading, _ => ReadJournal.Library.Models.Reader.ReaderCloseDecision.Stay };
    }
    public async Task<bool> ConfirmAsync(string title, string message) => await ChooseAsync(title, message, "取消", "确定") == "确定";
    public async Task<LeaveDecision> ConfirmLeaveAsync() => await ChooseAsync("尚未保存", "当前修改尚未保存，请选择如何离开。", "取消离开", "放弃修改", "保存并离开") switch { "保存并离开" => LeaveDecision.Save, "放弃修改" => LeaveDecision.Discard, _ => LeaveDecision.Cancel };
    public async Task<bool> EditRecordAsync(ReadingRecordEditViewModel editor)
    {
        var dialog = new Window { Title = "阅读记录", Width = 580, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = editor };
        var saved = false; var closing = false;
        void Saved(object? sender, EventArgs e) { saved = true; closing = true; dialog.Close(true); }
        editor.Saved += Saved;
        dialog.Closing += async (_, e) => { if (closing) return; e.Cancel = true; if (await editor.CanLeaveAsync()) { closing = true; dialog.Close(false); } };
        try { return await dialog.ShowDialog<bool>(Owner) || saved; }
        finally { editor.Saved -= Saved; dialog.Content = null; }
    }
}
