using System.Text.Json;
using ReadJournal.Library.Models;
using SQLite;
namespace ReadJournal.Library.Services;
public class BackupService : IBackupService
{
    private readonly IDatabaseService _database; private readonly IReadingService _reading; private readonly IFileService _files; private readonly DataPaths _paths;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public BackupService(IDatabaseService database, IReadingService reading, IFileService files, DataPaths paths) { _database = database; _reading = reading; _files = files; _paths = paths; }
    private static BackupDocument Snapshot(SQLiteConnection c) => new() { Books = c.Table<Book>().ToList(), ReadingRecords = c.Table<ReadingRecord>().ToList(), Quotes = c.Table<Quote>().ToList(), ReadingPlans = c.Table<ReadingPlan>().ToList() };
    public async Task ExportAsync(string path) { var doc = await _database.ReadAsync(Snapshot); await _files.WriteTextAsync(path, JsonSerializer.Serialize(doc, Options)); }
    public async Task<string> RestoreAsync(string path)
    {
        var doc = JsonSerializer.Deserialize<BackupDocument>(await _files.ReadTextAsync(path), Options) ?? throw new ArgumentException("备份内容为空。");
        Validate(doc);
        var auto = Path.Combine(_paths.DirectoryPath, "auto-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".json");
        await _database.WriteAsync(c =>
        {
            // 在同一串行边界捕获当前快照并写出自动备份，再进行事务替换，防止并发写入丢失。
            // 此委托由数据库服务在工作线程执行；文件服务这里仅做文件 IO，不调用 UI。
            _files.WriteTextAsync(auto, JsonSerializer.Serialize(Snapshot(c), Options)).GetAwaiter().GetResult();
            c.DeleteAll<ReadingPlan>(); c.DeleteAll<Quote>(); c.DeleteAll<ReadingRecord>(); c.DeleteAll<Book>();
            Insert(c, doc); return true;
        }); return auto;
    }
    public Task LoadDemoAsync() => _database.WriteAsync(c =>
    {
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
    public void Validate(BackupDocument doc)
    {
        if (doc.FormatVersion != 1) throw new ArgumentException("不支持此备份格式版本。");
        if (doc.Books is null || doc.ReadingRecords is null || doc.Quotes is null || doc.ReadingPlans is null) throw new ArgumentException("备份缺少实体集合。");
        void Ids<T>(IEnumerable<T> items, Func<T, int> id) { var all = items.ToArray(); if (all.Any(x => x is null) || all.Select(id).Any(x => x <= 0) || all.Select(id).Distinct().Count() != all.Length) throw new ArgumentException("备份包含空实体、非法或重复 ID。"); }
        Ids(doc.Books, b => b.Id); Ids(doc.ReadingRecords, r => r.Id); Ids(doc.Quotes, q => q.Id); Ids(doc.ReadingPlans, p => p.Id);
        var books = doc.Books.ToDictionary(x => x.Id);
        Book Parent(int id) => books.TryGetValue(id, out var book) ? book : throw new ArgumentException("备份包含孤立关联。");
        foreach (var b in doc.Books) { _reading.ValidateBook(b, 0); if (b.Author is null || b.Category is null || b.Description is null || b.CreatedAt == default || b.UpdatedAt < b.CreatedAt) throw new ArgumentException("书籍字段或时间无效。"); }
        foreach (var r in doc.ReadingRecords) _reading.ValidateRecord(r, Parent(r.BookId));
        foreach (var q in doc.Quotes) { _reading.ValidateQuote(q, Parent(q.BookId)); if (q.Thoughts is null || q.CreatedAt == default || q.UpdatedAt < q.CreatedAt) throw new ArgumentException("书摘字段或时间无效。"); }
        foreach (var p in doc.ReadingPlans) { Parent(p.BookId); if (p.StartDate == default || p.StartDate.Date > DateTime.Today || p.TargetDate.Date < p.StartDate.Date) throw new ArgumentException("计划日期无效。"); }
        foreach (var group in doc.ReadingPlans.GroupBy(x => x.BookId)) if (group.Count() > 1 && _reading.Calculate(Parent(group.Key), doc.ReadingRecords).State != "已完成") throw new ArgumentException("同一本书有多个未完成计划。");
    }
}
