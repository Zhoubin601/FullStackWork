using System.Text.Json;
using ReadJournal.Library.Models;
using SQLite;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.Services;
public class BackupService : IBackupService
{
    private readonly IDatabaseService _database; private readonly IReadingService _reading; private readonly IFileService _files; private readonly DataPaths _paths;
    private readonly IApplicationDataCoordinator? _coordinator;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public BackupService(IDatabaseService database, IReadingService reading, IFileService files, DataPaths paths) { _database = database; _reading = reading; _files = files; _paths = paths; _coordinator = (database as DatabaseService)?.Coordinator; }
    public BackupService(IDatabaseService database, IReadingService reading, IFileService files, DataPaths paths, IApplicationDataCoordinator coordinator) : this(database, reading, files, paths) { _coordinator = coordinator; }
    private static BackupDocument Snapshot(SQLiteConnection c)
    {
        EnsureManualDataset(c);
        return new() { Books = c.Table<Book>().ToList(), ReadingRecords = c.Table<ReadingRecord>().ToList(), Quotes = c.Table<Quote>().ToList(), ReadingPlans = c.Table<ReadingPlan>().ToList() };
    }
    private static void EnsureManualDataset(SQLiteConnection c)
    {
        if (c.Table<Book>().Any(x => x.Kind != BookKind.Manual) || c.Table<EbookFile>().Count() + c.Table<EbookProgress>().Count() + c.Table<EbookReadingSession>().Count() + c.Table<QuoteOrigin>().Count() + c.Table<AiGenerationBatch>().Count() != 0)
            throw new InvalidOperationException("当前书库含电子书或定位数据，旧 JSON 备份不能完整保护它；需要完整 ZIP 备份，当前版本暂不支持。");
    }
    public async Task ExportAsync(string path)
    {
        await _database.InitializeAsync();
        var coordinator = _coordinator ?? throw new InvalidOperationException("备份需要数据协调器。");
        await using var maintenance = await coordinator.EnterMaintenanceAsync(MaintenanceKind.Backup, CancellationToken.None);
        var doc = await _database.ReadAsync(maintenance, Snapshot);
        await _files.WriteTextAsync(path, JsonSerializer.Serialize(doc, Options));
    }
    public async Task<string> RestoreAsync(string path)
    {
        var doc = JsonSerializer.Deserialize<BackupDocument>(await _files.ReadTextAsync(path), Options) ?? throw new ArgumentException("备份内容为空。");
        var upgraded = new BackupV1Adapter(new BackupEntityValidator(_reading)).Upgrade(doc);
        doc.Books = upgraded.Books.Select(x => x.Book).ToList();
        doc.Quotes = upgraded.Quotes.Select(x => x.Quote).ToList();
        var auto = Path.Combine(_paths.DirectoryPath, "auto-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".json");
        await _database.InitializeAsync();
        var coordinator = _coordinator ?? throw new InvalidOperationException("恢复需要数据协调器。");
        await using var maintenance = await coordinator.EnterMaintenanceAsync(MaintenanceKind.Restore, CancellationToken.None);
        var original = await _database.ReadAsync(maintenance, Snapshot);
        // The maintenance lease protects the snapshot while file IO runs outside the SQL gate/transaction.
        await _files.WriteTextAsync(auto, JsonSerializer.Serialize(original, Options));
        Func<SQLiteConnection, bool> replace = c =>
        {
            EnsureManualDataset(c);
            c.DeleteAll<ReadingPlan>(); c.DeleteAll<Quote>(); c.DeleteAll<ReadingRecord>(); c.DeleteAll<Book>();
            Insert(c, doc); return true;
        };
        await _database.WriteAsync(maintenance, replace); maintenance.CompleteReplacement();
        return auto;
    }
    public Task LoadDemoAsync() => _database.WriteAsync(c =>
    {
        EnsureManualDataset(c);
        if (c.Table<Book>().Count() + c.Table<ReadingRecord>().Count() + c.Table<Quote>().Count() + c.Table<ReadingPlan>().Count() != 0) throw new InvalidOperationException("演示数据仅可载入空业务库，已有数据不会混合导入。");
        var doc = DemoData.Create(); Validate(doc); Insert(c, doc); return true;
    });
    private static void Insert(SQLiteConnection c, BackupDocument doc)
    {
        // InsertOrReplace 显式包含自增主键，恢复原 ID，不由 Insert 重新分配关联键。
        foreach (var b in doc.Books) c.InsertOrReplace(b);
        foreach (var r in doc.ReadingRecords) c.InsertOrReplace(r);
        foreach (var q in doc.Quotes) c.InsertOrReplace(q);
        foreach (var p in doc.ReadingPlans) c.InsertOrReplace(p);
    }
    public void Validate(BackupDocument doc) => new BackupEntityValidator(_reading).ValidateV1(doc);
}
