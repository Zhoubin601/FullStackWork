using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;

if (args.Length != 2) throw new ArgumentException("Usage: ReaderBaseline <schema1.json> <new-output-directory>");
var source = Path.GetFullPath(args[0]);
var destination = Path.GetFullPath(args[1]);
if (Directory.Exists(destination) || File.Exists(destination))
    throw new IOException("Output must be a new directory; existing data is never overwritten.");
Directory.CreateDirectory(destination);
var paths = new DataPaths(destination);
var input = JsonSerializer.Deserialize<BackupDocument>(await File.ReadAllTextAsync(source))!;
var options = new JsonSerializerOptions { WriteIndented = true };
string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
string JsonHash(object data) => Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data)));

using (var database = new DatabaseService(paths))
{
    var reading = new ReadingService();
    var backup = new BackupService(database, reading, new LocalFiles(), paths);
    backup.Validate(input);
    await database.InitializeAsync();
    var version = await database.ReadAsync(c => c.ExecuteScalar<int>("PRAGMA user_version"));
    if (version != 1) throw new InvalidOperationException("This generator requires the original schema 1 implementation. Use the frozen SQL fixture after migration.");
    await backup.RestoreAsync(source);
    await backup.ExportAsync(Path.Combine(destination, "exported.json"));
    var actual = await database.ReadAsync(c => new BackupDocument
    {
        Books = c.Table<Book>().OrderBy(x => x.Id).ToList(),
        ReadingRecords = c.Table<ReadingRecord>().OrderBy(x => x.Id).ToList(),
        Quotes = c.Table<Quote>().OrderBy(x => x.Id).ToList(),
        ReadingPlans = c.Table<ReadingPlan>().OrderBy(x => x.Id).ToList()
    });
    if (JsonHash(actual) != JsonHash(input)) throw new InvalidOperationException("Fixture round trip changed fields.");
    var books = new BookStorage(database, reading);
    var states = new List<object>();
    foreach (var b in actual.Books)
    {
        var state = (await books.GetProgressAsync(b.Id))!;
        states.Add(new { b.Id, state.State, state.CurrentPage, state.TotalReadPages, state.TotalMinutes, state.CompletedOn });
    }
    var manifest = new
    {
        SchemaVersion = version,
        FormatVersion = actual.FormatVersion,
        Integrity = await database.ReadAsync(c => c.ExecuteScalar<string>("PRAGMA integrity_check")),
        ForeignKeyViolations = await database.ReadAsync(c => c.Query<ForeignKeyViolation>("PRAGMA foreign_key_check").Count),
        SourceSha256 = Hash(await File.ReadAllBytesAsync(source)),
        FieldSha256 = JsonHash(actual),
        Counts = new { Books = actual.Books.Count, ReadingRecords = actual.ReadingRecords.Count, Quotes = actual.Quotes.Count, ReadingPlans = actual.ReadingPlans.Count },
        Fields = actual,
        States = states,
        PlanStates = (await new ReadingPlanStorage(database, reading).GetAllAsync()).Select(p => new { p.Plan.Id, p.State }).ToArray()
    };
    await File.WriteAllTextAsync(Path.Combine(destination, "manifest.json"), JsonSerializer.Serialize(manifest, options) + "\n");
}
Console.WriteLine($"schema 1 fixture verified at {destination}");

sealed class ForeignKeyViolation { public string table { get; set; } = ""; }
sealed class LocalFiles : IFileService
{
    public Task<string?> ChooseSavePathAsync() => Task.FromResult<string?>(null);
    public Task<string?> ChooseOpenPathAsync() => Task.FromResult<string?>(null);
    public Task<string> ReadTextAsync(string path) => File.ReadAllTextAsync(path);
    public Task WriteTextAsync(string path, string contents) => File.WriteAllTextAsync(path, contents);
}
