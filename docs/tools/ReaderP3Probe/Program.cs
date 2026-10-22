using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Backup;
using ReadJournal.UnitTest;

if(args.Length==5&&args[0]=="--pause-restore")
{
    var paths=new DataPaths(args[1]);using var db=new DatabaseService(paths);await db.InitializeAsync();
    await BackupRuntime.CreateService(db,new ReadingService(),paths,new Pause(args[3],args[4])).RestoreAsync(args[2],default);
    return 0;
}
if(args.Length==2&&args[0]=="--prepare-ui")
{
    var root=Path.GetFullPath(args[1]);if(Directory.Exists(root))throw new IOException("UI root must be new.");Directory.CreateDirectory(root);
    var a=new DataPaths(Path.Combine(root,"source"));var b=new DataPaths(Path.Combine(root,"target"));
    using var source=new DatabaseService(a);using var target=new DatabaseService(b);
    await ReaderBackupTestData.SeedAsync(source,a,"A");await ReaderBackupTestData.SeedAsync(target,b,"B");
    var zip=Path.Combine(root,"target.zip");await BackupRuntime.CreateService(source,new ReadingService(),a).ExportAsync(zip,default);
    var expected=new{source=await State(source),target=await State(target),input=zip,data=b.DirectoryPath};
    await File.WriteAllTextAsync(Path.Combine(root,"expected.json"),JsonSerializer.Serialize(expected));
    Console.WriteLine(JsonSerializer.Serialize(expected));return 0;
}
if(args.Length==2&&args[0]=="--verify-ui")
{
    var root=Path.GetFullPath(args[1]);using var expected=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root,"expected.json")));
    var paths=new DataPaths(Path.Combine(root,"target"));using var db=new DatabaseService(paths);await db.InitializeAsync();
    var hash=await State(db);if(hash!=expected.RootElement.GetProperty("source").GetString())throw new InvalidOperationException("UI restored dataset differs from source backup.");
    var data=await db.ReadAsync(BackupDataStore.Snapshot);var inventory=ManagedBookInventory.Inspect(paths.DirectoryPath,data,new());
    var automatic=Directory.GetFiles(Path.Combine(paths.DirectoryPath,"backups"),"auto-backup-*.zip").Single();
    using var zip=System.IO.Compression.ZipFile.OpenRead(automatic);using var stream=zip.GetEntry("data.json")!.Open();
    var old=BackupDataStore.Normalize(JsonSerializer.Deserialize<ReadJournal.Library.Models.Reader.BackupDataV2>(stream)!);
    if(BackupDataStore.Hash(old)!=expected.RootElement.GetProperty("target").GetString())throw new InvalidOperationException("UI automatic backup lost original data.");
    Console.WriteLine(JsonSerializer.Serialize(new{passed=true,dataSha256=hash,fileSet=inventory,automaticBackupOldDataMatches=true,bookCount=data.Books.Count,sessionCount=data.Sessions.Count,batchReceipt=data.Batches.Single().InsertedQuoteIdsJson,integrity=await db.ReadAsync(c=>c.ExecuteScalar<string>("PRAGMA integrity_check"))}));return 0;
}
if(args.Length!=1)throw new ArgumentException("Usage: ReaderP3Probe <new-evidence-directory>");
var evidence=Path.GetFullPath(args[0]);if(Directory.Exists(evidence))throw new IOException("Evidence directory must be new.");Directory.CreateDirectory(evidence);
var sourcePaths=new DataPaths(Path.Combine(evidence,"isolated-data","source"));string sourceHash;var backup=Path.Combine(evidence,"synthetic-complete.zip");
using(var source=new DatabaseService(sourcePaths))
{
    await ReaderBackupTestData.SeedAsync(source,sourcePaths,"A");sourceHash=await State(source);
    await BackupRuntime.CreateService(source,new ReadingService(),sourcePaths).ExportAsync(backup,default);
}
var results=new List<object>();
foreach(var point in new[]{"restore.after-isolate","restore.after-install","restore.in-transaction","restore.after-commit"})
{
    var label=point.Replace('.','-');var paths=new DataPaths(Path.Combine(evidence,"isolated-data",label));string old;
    using(var db=new DatabaseService(paths)){await ReaderBackupTestData.SeedAsync(db,paths,"B");old=await State(db);}
    var signal=Path.Combine(evidence,label+".ready");using var child=Start(paths.DirectoryPath,backup,point,signal);
    await WaitReady(child,signal);child.Kill(entireProcessTree:true);await child.WaitForExitAsync();
    await File.WriteAllTextAsync(Path.Combine(evidence,label+"-child.log"),await child.StandardError.ReadToEndAsync());
    using var reopened=new DatabaseService(paths);await reopened.InitializeAsync();var current=await State(reopened);
    var expected=point=="restore.after-commit"?sourceHash:old;if(current!=expected)throw new InvalidOperationException("Crash recovery dataset mismatch at "+point);
    var files=await reopened.ReadAsync(c=>c.Table<ReadJournal.Library.Models.Reader.EbookFile>().ToList());
    var fileSet=ManagedBookInventory.Inspect(paths.DirectoryPath,await reopened.ReadAsync(BackupDataStore.Snapshot),new());
    if(fileSet.Count!=5)throw new InvalidOperationException("Managed file set incomplete.");
    results.Add(new{point,termination="parent Process.Kill after checkpoint signal (actual process interruption)",childExit=child.ExitCode,recovered=point=="restore.after-commit"?"complete_new":"complete_old",dataSha256=current,fileSet,integrity=await reopened.ReadAsync(c=>c.ExecuteScalar<string>("PRAGMA integrity_check")),lastRestoreId=await reopened.ReadAsync(BackupDataStore.LastRestoreId)});
}
// A real interrupted restore with two retained datasets, then damaged journal: startup must stop without deleting either.
var brokenPaths=new DataPaths(Path.Combine(evidence,"isolated-data","unrecoverable"));
using(var db=new DatabaseService(brokenPaths))await ReaderBackupTestData.SeedAsync(db,brokenPaths,"B");
var brokenSignal=Path.Combine(evidence,"unrecoverable.ready");
using(var child=Start(brokenPaths.DirectoryPath,backup,"restore.after-install",brokenSignal))
{await WaitReady(child,brokenSignal);child.Kill(entireProcessTree:true);await child.WaitForExitAsync();}
var operation=Directory.GetFiles(Path.Combine(brokenPaths.DirectoryPath,"operations"),"*.json").Single();await File.WriteAllTextAsync(operation,"{truncated");
string oldDatabase;
using(var raw=new SQLite.SQLiteConnection(brokenPaths.DatabasePath))oldDatabase=BackupDataStore.Hash(BackupDataStore.Snapshot(raw));
var before=Tree(brokenPaths.DirectoryPath);bool blocked;string diagnosis;
using(var db=new DatabaseService(brokenPaths))
{
    try{await db.InitializeAsync();blocked=false;diagnosis="unexpected success";}
    catch(InvalidOperationException ex){blocked=!db.IsInitialized;diagnosis=ex.Message;}
}
string afterDatabase;
using(var raw=new SQLite.SQLiteConnection(brokenPaths.DatabasePath))afterDatabase=BackupDataStore.Hash(BackupDataStore.Snapshot(raw));
if(oldDatabase!=afterDatabase)throw new InvalidOperationException("Damaged journal changed database rows.");
var after=Tree(brokenPaths.DirectoryPath);if(!blocked||!before.SequenceEqual(after))throw new InvalidOperationException("Damaged journal did not preserve both datasets.");
var output=new{platform=Environment.OSVersion.ToString(),runtime=Environment.Version.ToString(),sourceDataSha256=sourceHash,crashCases=results,insufficientJournal=new{blocked,diagnosis,preservedManagedFiles=Directory.GetFiles(Path.Combine(brokenPaths.DirectoryPath,"books","managed"),"*",SearchOption.AllDirectories).Length,preservedTrashFiles=Directory.GetFiles(Path.Combine(brokenPaths.DirectoryPath,"trash"),"*",SearchOption.AllDirectories).Length,allFilesUnchanged=before.SequenceEqual(after),databaseRowsUnchanged=oldDatabase==afterDatabase},passed=true};
await File.WriteAllTextAsync(Path.Combine(evidence,"probe.json"),JsonSerializer.Serialize(output,new JsonSerializerOptions{WriteIndented=true})+"\n");Console.WriteLine("Four actual process interruptions and damaged-journal preservation passed.");return 0;

static Task<string> State(DatabaseService db)=>db.ReadAsync(c=>BackupDataStore.Hash(BackupDataStore.Snapshot(c)));
static string[] Tree(string root)=>Directory.GetFiles(root,"*",SearchOption.AllDirectories).Where(p=>!p.EndsWith("library.lock")&&!p.Contains("readjournal.sqlite3")).Select(p=>Path.GetRelativePath(root,p)+":"+ManagedBookFiles.HashFile(p)).Order(StringComparer.Ordinal).ToArray();
static Process Start(string data,string zip,string point,string signal)
{
    var executable=Environment.ProcessPath!;var start=new ProcessStartInfo(executable){RedirectStandardError=true,RedirectStandardOutput=true};
    if(Path.GetFileNameWithoutExtension(executable).Equals("dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    foreach(var argument in new[]{"--pause-restore",data,zip,point,signal})start.ArgumentList.Add(argument);return Process.Start(start)!;
}
static async Task WaitReady(Process child,string path)
{
    var watch=Stopwatch.StartNew();while(!File.Exists(path))
    {if(child.HasExited)throw new InvalidOperationException("Child exited before checkpoint: "+await child.StandardError.ReadToEndAsync());if(watch.Elapsed>TimeSpan.FromSeconds(30)){child.Kill(true);throw new TimeoutException("Checkpoint not reached.");}await Task.Delay(50);}
}
sealed class Pause(string point,string signal):ManagedBookFiles
{public override void Checkpoint(string current){if(current!=point)return;File.WriteAllText(signal,current);using var wait=new ManualResetEventSlim();wait.Wait();}}
