using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Backup;
using ReadJournal.Library.Services.Reader;
using ReadJournal.UnitTest;

if(args.Length==6&&args[0]=="--pause-import")
{
    using var db=new DatabaseService(new DataPaths(args[1]));
    var result=await EbookRuntime.CreateLibrary(db,new Pause(args[3],args[4])).ImportAsync(new(args[2]),null,default);
    Console.WriteLine(JsonSerializer.Serialize(result));return 0;
}
if(args.Length==7&&args[0]=="--pause-delete")
{
    using var db=new DatabaseService(new DataPaths(args[1]));await db.InitializeAsync();
    await EbookRuntime.CreateLibrary(db,new Pause(args[4],args[5])).DeleteBookAsync(int.Parse(args[2]),Guid.Parse(args[3]),db.Coordinator.CurrentEpoch,default);return 0;
}
if(args.Length==2&&args[0]=="--prepare-ui")
{
    var root=NewRoot(args[1]);Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    var sources=new Dictionary<string,byte[]>{{"data/books/inbox.txt",new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(ReaderP4TestData.Text)).ToArray()},
        {"source/selected.txt",Encoding.UTF8.GetBytes(ReaderP4TestData.Text+"\n外部文件选择器样本。")},{"source/duplicate.txt",Encoding.UTF8.GetBytes(ReaderP4TestData.Text+"\n外部文件选择器样本。")},
        {"source/gb18030.txt",Encoding.GetEncoding("gb18030").GetBytes("第一章 编码\r\n这是明确指定GB18030导入的合成正文。")},{"source/invalid.txt",[0xff,0xff,0x61]}};
    foreach(var item in sources){var path=Path.Combine(root,item.Key);Directory.CreateDirectory(Path.GetDirectoryName(path)!);await File.WriteAllBytesAsync(path,item.Value);}
    var hashes=sources.Keys.ToDictionary(k=>k,k=>ManagedBookFiles.HashFile(Path.Combine(root,k)));
    await File.WriteAllTextAsync(Path.Combine(root,"source-hashes.json"),JsonSerializer.Serialize(hashes,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Prepared isolated inbox/UTF8/GB18030/duplicate/invalid UI sources.");return 0;
}
if(args.Length==2&&args[0]=="--verify-ui")
{
    var root=Path.GetFullPath(args[1]);var hashes=JsonSerializer.Deserialize<Dictionary<string,string>>(await File.ReadAllTextAsync(Path.Combine(root,"source-hashes.json")))!;
    if(hashes.Any(p=>ManagedBookFiles.HashFile(Path.Combine(root,p.Key))!=p.Value))throw new InvalidOperationException("UI changed source bytes.");
    var paths=new DataPaths(Path.Combine(root,"data"));using var db=new DatabaseService(paths);await db.InitializeAsync();var data=await db.ReadAsync(BackupDataStore.Snapshot);
    if(data.Books.Count!=3||data.Files.Any(f=>f.Status!=0)||data.Progress.Any(p=>p.HasStarted||p.CurrentPercent!=0))throw new InvalidOperationException("UI final initial state mismatch.");
    var inventory=ManagedBookInventory.Inspect(paths.DirectoryPath,data,new());
    var archive=Archive();using var exported=archive.Prepare(Path.Combine(root,"desktop.zip"),default);
    if(BackupDataStore.Hash(exported.Data)!=BackupDataStore.Hash(data)||!ManagedBookInventory.Same(exported.Files,inventory))throw new InvalidOperationException("UI restored data differs from desktop ZIP.");
    var auto=Directory.GetFiles(Path.Combine(paths.DirectoryPath,"backups"),"auto-backup-*.zip").Single();using var old=archive.Prepare(auto,default);
    if(old.Data.Books.Count!=2)throw new InvalidOperationException("UI automatic backup is not the two-book deleted state.");
    Console.WriteLine(JsonSerializer.Serialize(new{passed=true,books=data.Books.Select(b=>new{b.Book.Id,b.Book.Title,b.BookUid,b.Kind}),initialProgressIntact=true,dataSha256=BackupDataStore.Hash(data),inventory,sourceHashes=hashes,automaticBackupBookCount=old.Data.Books.Count,integrity=await db.ReadAsync(c=>c.ExecuteScalar<string>("PRAGMA integrity_check"))}));return 0;
}
if(args.Length!=1)throw new ArgumentException("Usage: ReaderP4Probe <new-evidence-directory>");
var evidence=NewRoot(args[0]);var source=Path.Combine(evidence,"isolated-data","outside","real.txt");Directory.CreateDirectory(Path.GetDirectoryName(source)!);await File.WriteAllTextAsync(source,ReaderP4TestData.Text,new UTF8Encoding(true));var sourceSha=ManagedBookFiles.HashFile(source);
var cases=new List<object>();
foreach(var point in new[]{"import.copying","import.parsing","import.after-install","import.before-transaction","import.in-transaction","import.after-commit"})
{
    var label=point.Replace('.','-');var paths=new DataPaths(Path.Combine(evidence,"isolated-data",label));string oldHash;
    using(var initial=new DatabaseService(paths)){await initial.InitializeAsync();oldHash=await Hash(initial);}
    var signal=Path.Combine(evidence,label+".ready");using var child=Start("--pause-import",paths.DirectoryPath,source,point,signal,"reserved");await KillAtSignal(child,signal);
    await File.WriteAllTextAsync(Path.Combine(evidence,label+"-child.log"),await child.StandardError.ReadToEndAsync());
    using var reopened=new DatabaseService(paths);await reopened.InitializeAsync();var data=await reopened.ReadAsync(BackupDataStore.Snapshot);var committed=point=="import.after-commit";
    if(data.Books.Count!=(committed?1:0)||!committed&&await Hash(reopened)!=oldHash||committed&&(data.Files.Single().SourceSha256!=sourceSha||data.Progress.Single().HasStarted))throw new InvalidOperationException("Import crash result mismatch at "+point);
    var inventory=ManagedBookInventory.Inspect(paths.DirectoryPath,data,new());if(ManagedBookFiles.HashFile(source)!=sourceSha)throw new InvalidOperationException("Source changed.");
    cases.Add(new{point,termination="actual parent Process.Kill after child checkpoint signal",childExit=child.ExitCode,recovered=committed?"complete_import":"complete_old",dataSha256=await Hash(reopened),inventory,sourceUnchanged=true,integrity=await reopened.ReadAsync(c=>c.ExecuteScalar<string>("PRAGMA integrity_check"))});
}
foreach(var point in new[]{"delete.before-transaction","delete.in-transaction","delete.after-commit","delete.after-trash"})
{
    var label=point.Replace('.','-');var paths=new DataPaths(Path.Combine(evidence,"isolated-data",label));ImportResult imported;string oldHash;
    using(var initial=new DatabaseService(paths)){imported=await EbookRuntime.CreateLibrary(initial).ImportAsync(new(source),null,default);CheckImported(imported);await ReaderP4TestData.AddRelations(initial,imported.BookId!.Value);oldHash=await Hash(initial);}
    var signal=Path.Combine(evidence,label+".ready");using var child=Start("--pause-delete",paths.DirectoryPath,imported.BookId!.Value.ToString(),imported.BookUid!.Value.ToString("D"),point,signal,"reserved");await KillAtSignal(child,signal);
    await File.WriteAllTextAsync(Path.Combine(evidence,label+"-child.log"),await child.StandardError.ReadToEndAsync());
    using var reopened=new DatabaseService(paths);await reopened.InitializeAsync();var data=await reopened.ReadAsync(BackupDataStore.Snapshot);var committed=point is "delete.after-commit" or "delete.after-trash";
    if(data.Books.Count!=(committed?0:1)||!committed&&await Hash(reopened)!=oldHash||committed&&(data.Sessions.Count!=0||data.Origins.Count!=0||data.Batches.Count!=0||data.BatchItems.Count!=0))throw new InvalidOperationException("Delete crash result mismatch at "+point);
    var inventory=ManagedBookInventory.Inspect(paths.DirectoryPath,data,new());if(ManagedBookFiles.HashFile(source)!=sourceSha)throw new InvalidOperationException("Source changed.");
    cases.Add(new{point,termination="actual parent Process.Kill after child checkpoint signal",childExit=child.ExitCode,recovered=committed?"complete_deleted":"complete_old",dataSha256=await Hash(reopened),inventory,sourceUnchanged=true,integrity=await reopened.ReadAsync(c=>c.ExecuteScalar<string>("PRAGMA integrity_check"))});
}
var roundPaths=new DataPaths(Path.Combine(evidence,"isolated-data","roundtrip"));object roundtrip;
using(var db=new DatabaseService(roundPaths))
{
    var library=EbookRuntime.CreateLibrary(db);var imported=await library.ImportAsync(new(source),null,default);CheckImported(imported);var initialHash=await Hash(db);var initial=Path.Combine(evidence,"actual-import-initial.zip");
    var backup=BackupRuntime.CreateService(db,new ReadingService(),roundPaths);await backup.ExportAsync(initial,default);await ReaderP4TestData.AddRelations(db,imported.BookId!.Value);
    var allHash=await Hash(db);var allData=await db.ReadAsync(BackupDataStore.Snapshot);var files=ManagedBookInventory.Inspect(roundPaths.DirectoryPath,allData,new());var impact=await library.GetDeleteImpactAsync(imported.BookId.Value,default);var full=Path.Combine(evidence,"actual-import-relations.zip");await backup.ExportAsync(full,default);
    await library.DeleteBookAsync(imported.BookId.Value,imported.BookUid!.Value,db.Coordinator.CurrentEpoch,default);if((await db.ReadAsync(BackupDataStore.Snapshot)).Books.Count!=0)throw new InvalidOperationException("Delete left rows.");
    await backup.RestoreAsync(full,default);if(await Hash(db)!=allHash||!ManagedBookInventory.Same(files,ManagedBookInventory.Inspect(roundPaths.DirectoryPath,await db.ReadAsync(BackupDataStore.Snapshot),new())))throw new InvalidOperationException("Full roundtrip mismatch.");
    await library.DeleteBookAsync(imported.BookId.Value,imported.BookUid.Value,db.Coordinator.CurrentEpoch,default);await backup.RestoreAsync(initial,default);if(await Hash(db)!=initialHash)throw new InvalidOperationException("Initial progress roundtrip mismatch.");
    roundtrip=new{passed=true,initialDataSha256=initialHash,allRelatedDataSha256=allHash,impact,inventory=files,initialProgressPreserved=true,sourceUnchanged=ManagedBookFiles.HashFile(source)==sourceSha};
}
// A damaged import journal must preserve an installed but uncommitted file set.
var broken=new DataPaths(Path.Combine(evidence,"isolated-data","unrecoverable"));var brokenSignal=Path.Combine(evidence,"unrecoverable.ready");
using(var child=Start("--pause-import",broken.DirectoryPath,source,"import.after-install",brokenSignal,"reserved"))await KillAtSignal(child,brokenSignal);
var journal=Directory.GetFiles(Path.Combine(broken.DirectoryPath,"operations"),"*.json").Single();await File.WriteAllTextAsync(journal,"{truncated");
var before=Tree(broken.DirectoryPath);bool blocked;string diagnosis;
using(var db=new DatabaseService(broken))
{try{await db.InitializeAsync();blocked=false;diagnosis="unexpected success";}catch(InvalidOperationException ex){blocked=!db.IsInitialized;diagnosis=ex.Message;}}
var after=Tree(broken.DirectoryPath);if(!blocked||!before.SequenceEqual(after))throw new InvalidOperationException("Damaged import journal did not preserve files.");
var output=new{passed=true,platform=Environment.OSVersion.ToString(),runtime=Environment.Version.ToString(),sourceSha256=sourceSha,sourceUnchanged=ManagedBookFiles.HashFile(source)==sourceSha,actualInterruptions=cases,roundtrip,insufficientJournal=new{blocked,diagnosis,allFilesUnchanged=before.SequenceEqual(after),preservedManagedFiles=Directory.GetFiles(Path.Combine(broken.DirectoryPath,"books","managed"),"*",SearchOption.AllDirectories).Length}};
await File.WriteAllTextAsync(Path.Combine(evidence,"probe.json"),JsonSerializer.Serialize(output,new JsonSerializerOptions{WriteIndented=true})+"\n");Console.WriteLine("Ten actual process interruptions, actual TXT backup/delete/restore and damaged import journal preservation passed.");return 0;

static void CheckImported(ImportResult result){if(result.State!=ImportState.Imported)throw new InvalidOperationException(result.Message);}
static string NewRoot(string path){path=Path.GetFullPath(path);if(Directory.Exists(path))throw new IOException("Choose a new evidence root.");Directory.CreateDirectory(path);return path;}
static Task<string> Hash(DatabaseService db)=>db.ReadAsync(c=>BackupDataStore.Hash(BackupDataStore.Snapshot(c)));
static CompleteBackupArchive Archive(){var reading=new ReadingService();return new(new(reading,new ReadingStateCalculator()),new(new BackupEntityValidator(reading)),new(),new());}
static string[] Tree(string root)=>Directory.GetFiles(root,"*",SearchOption.AllDirectories).Where(p=>!p.EndsWith("library.lock")).Select(p=>Path.GetRelativePath(root,p)+":"+ManagedBookFiles.HashFile(p)).Order(StringComparer.Ordinal).ToArray();
static Process Start(params string[] arguments)
{
    var executable=Environment.ProcessPath!;var start=new ProcessStartInfo(executable){RedirectStandardError=true,RedirectStandardOutput=true};
    if(Path.GetFileNameWithoutExtension(executable).Equals("dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    foreach(var a in arguments)start.ArgumentList.Add(a);return Process.Start(start)!;
}
static async Task KillAtSignal(Process child,string path)
{
    var watch=Stopwatch.StartNew();while(!File.Exists(path))
    {if(child.HasExited)throw new InvalidOperationException("Child exited before checkpoint: "+await child.StandardError.ReadToEndAsync()+await child.StandardOutput.ReadToEndAsync());if(watch.Elapsed>TimeSpan.FromSeconds(30)){child.Kill(true);throw new TimeoutException("Checkpoint not reached.");}await Task.Delay(50);}
    child.Kill(true);await child.WaitForExitAsync();
}
sealed class Pause(string point,string signal):ManagedBookFiles
{public override void Checkpoint(string current){if(current!=point)return;File.WriteAllText(signal,current);using var wait=new ManualResetEventSlim();wait.Wait();}}
