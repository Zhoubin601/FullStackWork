using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Backup;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.Services;

public class BackupService : IBackupService
{
    private readonly IDatabaseService _database;
    private readonly IReadingService _reading;
    private readonly ICompleteBackupService? _complete;
    public string? LastWarning { get; private set; }
    public BackupService(IDatabaseService database, IReadingService reading, IFileService files, DataPaths paths)
    { _database = database; _reading = reading; if (database is DatabaseService actual) _complete = BackupRuntime.CreateService(actual, reading, paths); }
    public BackupService(IDatabaseService database, IReadingService reading, IFileService files, DataPaths paths, IApplicationDataCoordinator coordinator)
        : this(database, reading, files, paths) { }
    public BackupService(IDatabaseService database, IReadingService reading, IFileService files, DataPaths paths, IApplicationDataCoordinator coordinator, ICompleteBackupService complete)
        : this(database, reading, files, paths) { _complete = complete; }
    private ICompleteBackupService Complete => _complete ?? throw new InvalidOperationException("完整备份服务未配置。");
    public async Task ExportAsync(string path) { LastWarning = null; await Complete.ExportAsync(path, default); }
    public async Task<string> RestoreAsync(string path)
    {
        LastWarning = null; var restored = await Complete.RestoreAsync(path, default);
        LastWarning = restored.Warnings?.Count > 0 ? string.Join("\n", restored.Warnings) : null;
        return restored.AutomaticBackupPath;
    }
    public Task LoadDemoAsync() => _database.WriteAsync(c =>
    {
        var data = BackupDataStore.Snapshot(c);
        if (data.Books.Count + data.ReadingRecords.Count + data.Quotes.Count + data.ReadingPlans.Count + data.Files.Count + data.Progress.Count + data.Sessions.Count + data.Origins.Count + data.Batches.Count + data.BatchItems.Count != 0)
            throw new InvalidOperationException("演示数据仅可载入空业务库，已有数据不会混合导入。");
        var doc = DemoData.Create(); Validate(doc);
        foreach (var row in doc.Books) c.InsertOrReplace(row);
        foreach (var row in doc.ReadingRecords) c.InsertOrReplace(row);
        foreach (var row in doc.Quotes) c.InsertOrReplace(row);
        foreach (var row in doc.ReadingPlans) c.InsertOrReplace(row);
        return 0;
    });
    public void Validate(BackupDocument doc) => new BackupEntityValidator(_reading).ValidateV1(doc);
}
