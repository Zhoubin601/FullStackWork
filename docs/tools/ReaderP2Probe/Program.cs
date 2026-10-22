using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using SQLite;

if (args.Length == 2 && args[0] is "--lock-check" or "--lock-hold")
{
    try
    {
        if (args[0] == "--lock-hold")
        {
            using var own = new ApplicationDataCoordinator(new DataPaths(args[1])); own.EnsureInstanceLock();
            Console.WriteLine("lock-acquired"); await Task.Delay(Timeout.Infinite);
        }
        using var childDatabase = new DatabaseService(new DataPaths(args[1]));
        await childDatabase.InitializeAsync();
        await childDatabase.WriteAsync(c => c.Execute("INSERT OR REPLACE INTO AppMetadata(Key,Value) VALUES('P2ChildMarker','written-after-lock-acquired')"));
        Console.WriteLine("lock-acquired-and-database-write-committed");
        return 0;
    }
    catch (InvalidOperationException ex) { Console.WriteLine(ex.Message); return 23; }
}
if (args.Length != 3) throw new ArgumentException("Usage: ReaderP2Probe <schema1-sql.json> <schema1.json> <new-evidence-directory>");
var evidence = Path.GetFullPath(args[2]);
if (Directory.Exists(evidence)) throw new IOException("Evidence directory must be new.");
Directory.CreateDirectory(evidence);
var data = Path.Combine(evidence,"isolated-data"); Directory.CreateDirectory(data);
var paths = new DataPaths(data);
using (var old = new SQLiteConnection(paths.DatabasePath))
    foreach (var sql in JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(args[0]))!) old.Execute(sql);
var manifest = new Dictionary<string,object?> { ["platform"] = Environment.OSVersion.ToString(), ["runtime"] = Environment.Version.ToString(), ["schemaBefore"] = 1 };
using (var db = new DatabaseService(paths))
{
    await db.InitializeAsync();
    var rows = await db.ReadAsync(c => new BackupDocument { Books=c.Table<Book>().OrderBy(x=>x.Id).ToList(),ReadingRecords=c.Table<ReadingRecord>().OrderBy(x=>x.Id).ToList(),Quotes=c.Table<Quote>().OrderBy(x=>x.Id).ToList(),ReadingPlans=c.Table<ReadingPlan>().OrderBy(x=>x.Id).ToList() });
    var expected=JsonSerializer.Deserialize<BackupDocument>(await File.ReadAllTextAsync(args[1]))!;
    var exact=JsonSerializer.Serialize(rows)==JsonSerializer.Serialize(expected);
    if(!exact)throw new InvalidOperationException("Legacy fields differ after migration.");
    manifest["legacyAllFieldsMatch"]=exact;
    manifest["schemaAfter"]=await db.ReadAsync(c=>c.ExecuteScalar<int>("PRAGMA user_version"));
    manifest["books"]=rows.Books.Select(b=>new{b.Id,b.BookUid,b.Kind,b.Title}).ToArray();
    manifest["quoteRevisions"]=rows.Quotes.Select(q=>new{q.Id,q.Revision}).ToArray();
    manifest["schemaObjects"]=await db.ReadAsync(c=>c.Query<SchemaObject>("SELECT type,name,sql FROM sqlite_master WHERE name NOT LIKE 'sqlite_%' ORDER BY type,name"));
    manifest["integrity"]=await db.ReadAsync(c=>c.ExecuteScalar<string>("PRAGMA integrity_check"));
    manifest["foreignKeyViolations"]=await db.ReadAsync(c=>c.Query<FkError>("PRAGMA foreign_key_check").Count);
    var child=await CheckChild("--lock-check",data);
    if(child.ExitCode!=23)throw new InvalidOperationException("Second process did not reject occupied directory.");
    manifest["secondProcessWhileHeld"]=child;
    var blockedWrites=await db.ReadAsync(c=>c.ExecuteScalar<int>("SELECT count(*) FROM AppMetadata WHERE Key='P2ChildMarker'"));
    if(blockedWrites!=0)throw new InvalidOperationException("Blocked process wrote SQL.");
    manifest["blockedProcessWriteCount"]=blockedWrites;
}
var available=await CheckChild("--lock-check",data);
if(available.ExitCode!=0)throw new InvalidOperationException("Lock did not release after disposal.");
manifest["secondProcessAfterRelease"]=available;
using(var crashing=StartChild("--lock-hold",data))
{
    var ready=await crashing.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
    if(ready!="lock-acquired")throw new InvalidOperationException("Crash child failed to acquire lock.");
    crashing.Kill(entireProcessTree:true);await crashing.WaitForExitAsync();
}
var afterCrash=await CheckChild("--lock-check",data);
if(afterCrash.ExitCode!=0)throw new InvalidOperationException("OS lock was not released on crash.");
manifest["secondProcessAfterCrashWithStaleLockFile"]=afterCrash;
manifest["passed"]=true;
await File.WriteAllTextAsync(Path.Combine(evidence,"probe.json"),JsonSerializer.Serialize(manifest,new JsonSerializerOptions{WriteIndented=true})+"\n");
Console.WriteLine(JsonSerializer.Serialize(manifest));return 0;

static Process StartChild(string mode,string data)
{
    var executable=Environment.ProcessPath!;
    var start=new ProcessStartInfo(executable){RedirectStandardOutput=true,RedirectStandardError=true};
    if(Path.GetFileNameWithoutExtension(executable).Equals("dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    start.ArgumentList.Add(mode);start.ArgumentList.Add(data);return Process.Start(start)!;
}
static async Task<ChildResult> CheckChild(string mode,string data)
{
    using var child=StartChild(mode,data);var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();
    await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));return new(child.ExitCode,await stdout,await stderr);
}
sealed record ChildResult(int ExitCode,string Output,string Error);
sealed class FkError { public string table {get;set;}=""; }

sealed class SchemaObject { public string type {get;set;}=""; public string name {get;set;}=""; public string? sql {get;set;} }
