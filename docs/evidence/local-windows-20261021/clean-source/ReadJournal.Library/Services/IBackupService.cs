namespace ReadJournal.Library.Services;
public interface IBackupService
{
    Task ExportAsync(string path);
    Task<string> RestoreAsync(string path);
    Task LoadDemoAsync();
}
