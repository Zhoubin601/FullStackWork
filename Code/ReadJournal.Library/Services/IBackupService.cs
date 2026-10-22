namespace ReadJournal.Library.Services;
public interface IBackupService
{
    string? LastWarning => null;
    Task ExportAsync(string path);
    Task<string> RestoreAsync(string path);
    Task LoadDemoAsync();
}
