using System.Text.Json;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Backup;
using ReadJournal.Library.Services.Reader;
using ReadJournal.UnitTest;

if(args.Length != 2) throw new ArgumentException("Usage: --compatibility <new-evidence-root> | --prepare-ui <new-root> | --verify-ui <existing-root>");
if(args[0] == "--prepare-ui")
{
    var root=NewRoot(args[1]);var source=EpubTestBooks.Write(Path.Combine(root,"data","books"),"large-image");
    await File.WriteAllTextAsync(Path.Combine(root,"source.json"),JsonSerializer.Serialize(new {source,sha256=ManagedBookFiles.HashFile(source)},JsonOptions()));Console.WriteLine("Prepared one actual large-image EPUB in isolated inbox.");return;
}
if(args[0] == "--verify-ui")
{
    var root=Path.GetFullPath(args[1]);using var db=new DatabaseService(new DataPaths(Path.Combine(root,"data")));await db.InitializeAsync();var data=await db.ReadAsync(BackupDataStore.Snapshot);
    if(data.Books.Count!=1||data.Files.Single().Format!=EbookFormat.Epub||data.Sessions.Count<2||data.Origins.Count!=1||!data.Progress.Single().HasStarted)throw new InvalidOperationException("Actual desktop state incomplete.");
    var expected=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root,"source.json"))).RootElement;var source=expected.GetProperty("source").GetString()!;
    if(ManagedBookFiles.HashFile(source)!=expected.GetProperty("sha256").GetString())throw new InvalidOperationException("Desktop changed original source.");
    var f=data.Files.Single();var manifest=BackupJson.Read<CanonicalManifest>(File.ReadAllBytes(ManagedBookFiles.SafePath(db.Paths.DirectoryPath,f.CanonicalRelativePath)));
    var documents=new ReaderDocumentService(db,db.Coordinator);var sessions=new ReaderSessionService(db,db.Coordinator,documents,new EbookProgressMaintenance(db,db.Coordinator),new ReaderClock(),new ReaderSessionCheckpoints());var quotes=new ReaderQuoteService(db,db.Coordinator,documents,sessions,new ReadingService(),new ReaderQuoteCheckpoints());
    if(!(await quotes.ResolveQuoteAsync(data.Origins.Single().QuoteId,default)).IsExact)throw new InvalidOperationException("Desktop quote no longer maps to original text.");
    var inventory=ManagedBookInventory.Inspect(db.Paths.DirectoryPath,data,new());
    var result=new {passed=true,books=data.Books.Count,sessions=data.Sessions.Count,origin=data.Origins.Single(),progress=data.Progress.Single(),resources=manifest.Resources,inventory,sourceSha256=f.SourceSha256,allSourceBytesPreserved=true,quoteExactlyResolves=true,integrity=await db.ReadAsync(c=>c.ExecuteScalar<string>("pragma integrity_check"))};
    Console.WriteLine(JsonSerializer.Serialize(result,JsonOptions()));return;
}
if(args[0] != "--compatibility")throw new ArgumentException("Unknown mode.");
var evidence=NewRoot(args[1]);var rows=new List<object>();
foreach(var variant in EpubTestBooks.Supported.Concat(EpubTestBooks.Rejected))
{
    var source=EpubTestBooks.Write(Path.Combine(evidence,"sources"),variant);var repeated=EpubTestBooks.Write(Path.Combine(evidence,"repeated-sources"),variant);var sha=ManagedBookFiles.HashFile(source);if(sha!=ManagedBookFiles.HashFile(repeated))throw new InvalidOperationException("Fixture generator is not deterministic.");
    using var db=new DatabaseService(new DataPaths(Path.Combine(evidence,"data",variant)));var library=EbookRuntime.CreateLibrary(db);var imported=await library.ImportAsync(new(source),null,default);var expected=EpubTestBooks.Supported.Contains(variant);
    if((imported.State==ImportState.Imported)!=expected)throw new InvalidOperationException(variant+": "+imported.Message);
    if(ManagedBookFiles.HashFile(source)!=sha)throw new InvalidOperationException("Source changed.");
    if(!expected){rows.Add(new {variant,synthetic=true,sourceSha256=sha,result="rejected",reason=imported.Message,sourceUnchanged=true});continue;}
    var initial=await db.ReadAsync(BackupDataStore.Snapshot);var b=initial.Books.Single();var f=initial.Files.Single();var key=ReaderContractValidator.ReadDocument(initial.Progress.Single().DocumentKeyJson);var start=ReaderContractValidator.ReadPosition(initial.Progress.Single().LastPositionJson);
    var documents=new ReaderDocumentService(db,db.Coordinator);var document=await documents.OpenAsync(new(b.Book.Id,b.BookUid,key,db.Coordinator.CurrentEpoch,Guid.NewGuid(),1),default);
    var sessions=new ReaderSessionService(db,db.Coordinator,documents,new EbookProgressMaintenance(db,db.Coordinator),new ReaderClock(),new ReaderSessionCheckpoints());var now=DateTimeOffset.Now;var context=await sessions.BeginAsync(new(b.Book.Id,b.BookUid,key,db.Coordinator.CurrentEpoch,Guid.NewGuid(),1),start,now,default);
    var quotes=new ReaderQuoteService(db,db.Coordinator,documents,sessions,new ReadingService(),new ReaderQuoteCheckpoints());var quoteId=await quotes.SaveSelectionAsync(context,new(start,start with{Utf16Offset=5}),"P7 deterministic compatibility",default);
    await sessions.CloseAsync(new(context,start,start with{Utf16Offset=5},now,now.AddMilliseconds(1275),false,ReaderExitReason.Back),default);await documents.ReleaseAllAsync();
    var before=await db.ReadAsync(BackupDataStore.Snapshot);var files=ManagedBookInventory.Inspect(db.Paths.DirectoryPath,before,new());var backup=BackupRuntime.CreateService(db,new ReadingService(),db.Paths);await backup.ExportAsync(Path.Combine(evidence,variant+".zip"),default);
    await library.DeleteBookAsync(b.Book.Id,b.BookUid,db.Coordinator.CurrentEpoch,default);await backup.RestoreAsync(Path.Combine(evidence,variant+".zip"),default);
    if(BackupDataStore.Hash(before)!=BackupDataStore.Hash(await db.ReadAsync(BackupDataStore.Snapshot))||!ManagedBookInventory.Same(files,ManagedBookInventory.Inspect(db.Paths.DirectoryPath,await db.ReadAsync(BackupDataStore.Snapshot),new()))||!(await quotes.ResolveQuoteAsync(quoteId,default)).IsExact)throw new InvalidOperationException("Roundtrip mismatch: "+variant);
    var manifest=BackupJson.Read<CanonicalManifest>(File.ReadAllBytes(ManagedBookFiles.SafePath(db.Paths.DirectoryPath,f.CanonicalRelativePath)));
    rows.Add(new {variant,synthetic=true,sourceSha256=sha,result=imported.Message.Contains("注意")?"supported_with_declared_degradation":"supported",reason=imported.Message,canonicalSha256=f.CanonicalSha256,chapters=manifest.Chapters.Select(c=>new{c.Key,c.Title,c.Order,c.Utf16Length}),toc=manifest.TableOfContents,images=manifest.Resources,sourceUnchanged=true,sessionMilliseconds=1275,quoteExactAfterCompleteBackupDeleteRestore=true,dataSha256=BackupDataStore.Hash(before),inventory=files,integrity=await db.ReadAsync(c=>c.ExecuteScalar<string>("pragma integrity_check"))});
}
await File.WriteAllTextAsync(Path.Combine(evidence,"compatibility.json"),JsonSerializer.Serialize(new {passed=true,syntheticSupported=EpubTestBooks.Supported.Length,syntheticRejected=EpubTestBooks.Rejected.Length,realBooksProvided=0,marketCompatibilityRate="not measured",allFixturesDeterministic=true,rows},JsonOptions())+"\n");Console.WriteLine("All deterministic fixtures, actual EPUB transactions, and complete roundtrips passed.");
static JsonSerializerOptions JsonOptions()=>new(){WriteIndented=true};
static string NewRoot(string path){path=Path.GetFullPath(path);if(Directory.Exists(path))throw new IOException("Choose a new directory.");Directory.CreateDirectory(path);return path;}
