using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Backup;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Services;

public sealed class ReaderPlatformService(DataPaths paths) : IReaderPlatformService
{
    private static Window Owner => ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;
    public async Task CopyTextAsync(string text, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); await (Owner.Clipboard ?? throw new InvalidOperationException("系统剪贴板不可用。")).SetTextAsync(text); }
    public Task SetFullScreenAsync(bool enabled, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); Owner.WindowState = enabled ? WindowState.FullScreen : WindowState.Normal; return Task.CompletedTask; }
    public Task OpenLibraryDirectoryAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var inbox = ManagedBookFiles.SafePath(paths.DirectoryPath, "books"); Directory.CreateDirectory(inbox);
        Process.Start(new ProcessStartInfo(inbox) { UseShellExecute = true }); return Task.CompletedTask;
    }
}
