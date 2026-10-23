using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Backup;
using Xunit;
namespace ReadJournal.UnitTest;

public sealed class CompleteBackupTests : IDisposable
{
    private readonly string _root=Path.Combine(Path.GetTempPath(),"ReadJournalP3Tests",Guid.NewGuid().ToString("N"));
    private async Task<(DatabaseService Db, DataPaths Paths)> Library(string name)
    { var paths=new DataPaths(Path.Combine(_root,name));var db=new DatabaseService(paths);await ReaderBackupTestData.SeedAsync(db,paths,name);return(db,paths); }
    private static Task<string> State(DatabaseService db)=>db.ReadAsync(c=>BackupDataStore.Hash(BackupDataStore.Snapshot(c)));
    private static Dictionary<string,string> Files(DataPaths paths)=>Directory.GetFiles(Path.Combine(paths.DirectoryPath,"books","managed"),"*",SearchOption.AllDirectories).ToDictionary(p=>Path.GetRelativePath(paths.DirectoryPath,p),ManagedBookFiles.HashFile);
    [Fact]
    public async Task CompleteZipRoundTripPreservesAllBusinessRowsFilesAndLocalPreferences()
    {
        var a=await Library("A");var b=await Library("B");using var source=a.Db;using var target=b.Db;
        var original=await State(source);var hashes=Files(a.Paths);var old=await State(target);
        var zip=Path.Combine(_root,"all.zip");await BackupRuntime.CreateService(source,new ReadingService(),a.Paths).ExportAsync(zip,default);
        foreach(var local in new[]{"secure/fake-key.txt","logs/trace.txt","layout-cache/cache.bin","books/inbox.txt"})
        {var p=Path.Combine(b.Paths.DirectoryPath,local);Directory.CreateDirectory(Path.GetDirectoryName(p)!);await File.WriteAllTextAsync(p,"synthetic-local-only");}
        await new FilePreferenceStorage(b.Paths).SetThemeAsync("深色");
        var epoch=target.Coordinator.CurrentEpoch;
        var result=await BackupRuntime.CreateService(target,new ReadingService(),b.Paths).RestoreAsync(zip,default);
        Assert.NotEqual(epoch,result.NewEpoch);Assert.Equal(original,await State(target));Assert.Equal(hashes.OrderBy(x=>x.Key),Files(b.Paths).OrderBy(x=>x.Key));
        Assert.Equal(old,BackupDataStore.Hash(ReadData(result.AutomaticBackupPath)));
        Assert.Equal("深色",await new FilePreferenceStorage(b.Paths).GetThemeAsync());
        foreach(var local in new[]{"secure/fake-key.txt","logs/trace.txt","layout-cache/cache.bin","books/inbox.txt"})Assert.Equal("synthetic-local-only",await File.ReadAllTextAsync(Path.Combine(b.Paths.DirectoryPath,local)));
        using var package=ZipFile.OpenRead(zip);Assert.Equal(7,package.Entries.Count);Assert.All(package.Entries,e=>Assert.True(e.FullName is "manifest.json" or "data.json" || e.FullName.StartsWith("books/managed/")));
        Assert.DoesNotContain(package.Entries,e=>e.FullName.Contains("secure")||e.FullName.Contains("operations")||e.FullName.Contains("staging")||e.FullName.Contains("layout-cache"));
        var data=ReadData(zip);Assert.Single(data.Files);Assert.Single(data.Progress);Assert.Single(data.Sessions);Assert.Single(data.Origins);Assert.Single(data.Batches);Assert.Single(data.BatchItems);
        Assert.Equal("[26,999]",data.Batches.Single().InsertedQuoteIdsJson);Assert.Equal(2,data.Quotes.Single(q=>q.Quote.Id==26).Revision);
        Assert.Empty(Directory.GetFiles(Path.Combine(b.Paths.DirectoryPath,"operations")));Assert.Empty(result.Warnings!);
    }
    [Fact]
    public async Task LegacyJsonReplacesEbooksOnlyAfterReadableCompleteAutomaticBackup()
    {
        var b=await Library("B");using var db=b.Db;var before=await State(db);
        var legacy=Path.Combine(_root,"old.json");var input=DemoData.Create();await File.WriteAllTextAsync(legacy,JsonSerializer.Serialize(input));
        var result=await BackupRuntime.CreateService(db,new ReadingService(),b.Paths).RestoreAsync(legacy,default);
        var data=await db.ReadAsync(BackupDataStore.Snapshot);
        Assert.All(data.Books,x=>Assert.Equal(BookKind.Manual,x.Kind));Assert.Empty(data.Files);Assert.Empty(data.Sessions);Assert.Empty(data.Origins);Assert.Empty(data.Batches);
        Assert.Equal(3,data.Books.Count);Assert.Equal(before,BackupDataStore.Hash(ReadData(result.AutomaticBackupPath)));
        Assert.Single(ReadData(result.AutomaticBackupPath).Files);
    }
    [Theory]
    [InlineData("missing")][InlineData("hash")][InlineData("traversal")][InlineData("absolute")][InlineData("windows")]
    [InlineData("case-duplicate")][InlineData("symlink")][InlineData("extra-secret")][InlineData("data-hash")]
    [InlineData("duplicate-id")][InlineData("duplicate-uid")][InlineData("orphan")][InlineData("position")]
    [InlineData("receipt")][InlineData("missing-collection")][InlineData("canonical")][InlineData("duplicate-json")]
    [InlineData("block-gap")][InlineData("resource-pixels")][InlineData("toc-target")][InlineData("grapheme")][InlineData("canonical-cr")][InlineData("resource-dimensions")][InlineData("percent")]
    public async Task UntrustedPackageIsRejectedBeforeAutomaticBackupOrDataChanges(string kind)
    {
        var a=await Library("A");var b=await Library("B");using var source=a.Db;using var target=b.Db;
        var zip=Path.Combine(_root,"good.zip");await BackupRuntime.CreateService(source,new ReadingService(),a.Paths).ExportAsync(zip,default);
        var bad=Path.Combine(_root,"bad.zip");Mutate(zip,bad,kind);
        var before=await State(target);var files=Files(b.Paths);var epoch=target.Coordinator.CurrentEpoch;
        var service=BackupRuntime.CreateService(target,new ReadingService(),b.Paths);
        Assert.False((await service.ValidateAsync(bad,default)).IsValid);
        await Assert.ThrowsAnyAsync<Exception>(()=>service.RestoreAsync(bad,default));
        Assert.Equal(before,await State(target));Assert.Equal(epoch,target.Coordinator.CurrentEpoch);Assert.Equal(files.OrderBy(x=>x.Key),Files(b.Paths).OrderBy(x=>x.Key));
        Assert.False(Directory.Exists(Path.Combine(b.Paths.DirectoryPath,"backups")));
    }
    [Theory]
    [InlineData("entries")][InlineData("archive")][InlineData("expanded")][InlineData("json")]
    public async Task ArchiveBudgetsRejectBeforeExpansionOrMutation(string budget)
    {
        var a=await Library("A");using var db=a.Db;var zip=Path.Combine(_root,"good.zip");await BackupRuntime.CreateService(db,new ReadingService(),a.Paths).ExportAsync(zip,default);
        var limits=budget switch{"entries"=>new BackupLimits(MaxEntries:3),"archive"=>new BackupLimits(MaxArchiveBytes:20),"expanded"=>new BackupLimits(MaxExpandedBytes:100),_=>new BackupLimits(MaxJsonBytes:20)};
        var service=BackupRuntime.CreateService(db,new ReadingService(),a.Paths,limits:limits);
        Assert.False((await service.ValidateAsync(zip,default)).IsValid);
    }
    [Theory]
    [InlineData("automatic")][InlineData("install")][InlineData("journal")][InlineData("sql")]
    public async Task PreCommitFailuresRestoreWholeOldDataset(string point)
    {
        var a=await Library("A");var b=await Library("B");using var source=a.Db;using var target=b.Db;
        var zip=Path.Combine(_root,"good.zip");await BackupRuntime.CreateService(source,new ReadingService(),a.Paths).ExportAsync(zip,default);
        var old=await State(target);var files=Files(b.Paths);var epoch=target.Coordinator.CurrentEpoch;
        if(point=="sql")await target.WriteAsync(c=>c.Execute("CREATE TRIGGER injected_quote_failure BEFORE INSERT ON Quote BEGIN SELECT RAISE(ABORT,'injected SQL failure'); END"));
        var fault=new Failure(point);var service=BackupRuntime.CreateService(target,new ReadingService(),b.Paths,fault);
        await Assert.ThrowsAnyAsync<Exception>(()=>service.RestoreAsync(zip,default));
        Assert.Equal(old,await State(target));Assert.Equal(epoch,target.Coordinator.CurrentEpoch);Assert.Equal(files.OrderBy(x=>x.Key),Files(b.Paths).OrderBy(x=>x.Key));
    }
    [Fact]
    public async Task CleanupFailureReportsCommittedSuccessAndNextStartupCanCleanAfterMoreEdits()
    {
        var a=await Library("A");var b=await Library("B");using var source=a.Db;var target=b.Db;
        var zip=Path.Combine(_root,"good.zip");await BackupRuntime.CreateService(source,new ReadingService(),a.Paths).ExportAsync(zip,default);
        var result=await BackupRuntime.CreateService(target,new ReadingService(),b.Paths,new Failure("cleanup")).RestoreAsync(zip,default);
        Assert.NotEmpty(result.Warnings!);Assert.Equal(await State(source),await State(target));
        await new BookStorage(target,new ReadingService()).SaveAsync(new(){Title="清理失败后新增",TotalPages=10});target.Dispose();
        using var reopened=new DatabaseService(b.Paths);await reopened.InitializeAsync();Assert.Contains(await new BookStorage(reopened,new ReadingService()).GetAllAsync(),b=>b.Title=="清理失败后新增");
        Assert.Empty(Directory.GetFiles(Path.Combine(b.Paths.DirectoryPath,"operations")));
    }
    [Fact]
    public async Task InvalidJournalStopsStartupWithoutDeletingDataOrRecoveryCopies()
    {
        var b=await Library("B");var dataBefore=await State(b.Db);b.Db.Dispose();
        var paths=b.Paths;Directory.CreateDirectory(Path.Combine(paths.DirectoryPath,"operations"));Directory.CreateDirectory(Path.Combine(paths.DirectoryPath,"trash","preserved"));
        var marker=Path.Combine(paths.DirectoryPath,"trash","preserved","synthetic.txt");await File.WriteAllTextAsync(marker,"preserve");
        await File.WriteAllTextAsync(Path.Combine(paths.DirectoryPath,"operations","restore-"+Guid.NewGuid().ToString("D")+".json"),"{broken");
        using var reopen=new DatabaseService(paths);var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>reopen.InitializeAsync());
        Assert.Contains("RecoveryRequired",error.Message);Assert.False(reopen.IsInitialized);Assert.Equal("preserve",await File.ReadAllTextAsync(marker));
        using var raw=new SQLite.SQLiteConnection(paths.DatabasePath);Assert.Equal(dataBefore,BackupDataStore.Hash(BackupDataStore.Snapshot(raw)));
    }
    [SymlinkFact]
    public async Task SymlinkInManagedContentOrDestinationIsRejected()
    {
        var a=await Library("A");using var db=a.Db;var file=(await db.ReadAsync(c=>c.Table<EbookFile>().Single())).SourceRelativePath;
        var full=ManagedBookFiles.SafePath(a.Paths.DirectoryPath,file);var outside=Path.Combine(_root,"outside.txt");File.Move(full,outside);File.CreateSymbolicLink(full,outside);
        await Assert.ThrowsAnyAsync<Exception>(()=>BackupRuntime.CreateService(db,new ReadingService(),a.Paths).ExportAsync(Path.Combine(_root,"no.zip"),default));
        Assert.True(File.Exists(outside));
    }
    [Fact]
    public async Task AtomicExportFailurePreservesExistingReadableTargetAndNoTemporaryFiles()
    {
        var a=await Library("A");using var db=a.Db;var path=Path.Combine(_root,"existing.zip");
        var good=BackupRuntime.CreateService(db,new ReadingService(),a.Paths);await good.ExportAsync(path,default);var hash=ManagedBookFiles.HashFile(path);
        await Assert.ThrowsAsync<IOException>(()=>BackupRuntime.CreateService(db,new ReadingService(),a.Paths,new CheckpointFailure("backup.before-install")).ExportAsync(path,default));
        Assert.Equal(hash,ManagedBookFiles.HashFile(path));Assert.True((await good.ValidateAsync(path,default)).IsValid);Assert.Empty(Directory.GetFiles(_root,"*.tmp"));
    }
    [Theory]
    [InlineData("restore.after-isolate")][InlineData("restore.after-install")]
    public async Task CancellationBeforeSqlCommitRollsBackFilesAndKeepsEpoch(string point)
    {
        var a=await Library("A");var b=await Library("B");using var source=a.Db;using var target=b.Db;
        var path=Path.Combine(_root,"input.zip");await BackupRuntime.CreateService(source,new ReadingService(),a.Paths).ExportAsync(path,default);
        var old=await State(target);var files=Files(b.Paths);var epoch=target.Coordinator.CurrentEpoch;using var cancelled=new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>BackupRuntime.CreateService(target,new ReadingService(),b.Paths,new CancelAt(point,cancelled)).RestoreAsync(path,cancelled.Token));
        Assert.Equal(old,await State(target));Assert.Equal(files.OrderBy(x=>x.Key),Files(b.Paths).OrderBy(x=>x.Key));Assert.Equal(epoch,target.Coordinator.CurrentEpoch);
    }
    [Fact]
    public async Task ActiveReaderPreventsRestoreBeforeAutomaticBackup()
    {
        var a=await Library("A");var b=await Library("B");using var source=a.Db;using var target=b.Db;
        var path=Path.Combine(_root,"input.zip");await BackupRuntime.CreateService(source,new ReadingService(),a.Paths).ExportAsync(path,default);
        var row=await target.ReadAsync(c=>c.Table<EbookFile>().Single());var book=await target.ReadAsync(c=>c.Find<Book>(row.BookId));
        var key=new DocumentKey(row.FileId,row.SourceSha256,row.CanonicalSha256,row.ParserVersion);
        await using var registration=await target.Coordinator.RegisterReaderAsync(new(book.Id,book.BookUid,key,target.Coordinator.CurrentEpoch,Guid.NewGuid(),1),default);
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>BackupRuntime.CreateService(target,new ReadingService(),b.Paths).RestoreAsync(path,default));
        Assert.Contains("退出阅读器",error.Message);Assert.False(Directory.Exists(Path.Combine(b.Paths.DirectoryPath,"backups")));
    }
    [Fact]
    public async Task PostCommitFailureReturnsSuccessWithRestartWarningAndRecoveryUsesCommittedFact()
    {
        var a=await Library("A");var b=await Library("B");using var source=a.Db;var target=b.Db;
        var path=Path.Combine(_root,"input.zip");await BackupRuntime.CreateService(source,new ReadingService(),a.Paths).ExportAsync(path,default);
        var result=await BackupRuntime.CreateService(target,new ReadingService(),b.Paths,new CheckpointFailure("restore.after-commit")).RestoreAsync(path,default);
        Assert.NotEmpty(result.Warnings!);await Assert.ThrowsAsync<InvalidOperationException>(()=>target.ReadAsync(c=>c.Table<Book>().Count()));target.Dispose();
        using var reopened=new DatabaseService(b.Paths);await reopened.InitializeAsync();Assert.Equal(await State(source),await State(reopened));
    }
    private sealed class CheckpointFailure(string point):ManagedBookFiles
    {public override void Checkpoint(string current){if(point==current)throw new IOException("injected checkpoint failure");}}
    private sealed class CancelAt(string point,CancellationTokenSource cancel):ManagedBookFiles
    {public override void Checkpoint(string current){if(point==current)cancel.Cancel();}}
    private sealed class Failure(string point):ManagedBookFiles
    {
        private bool _fired;
        public override void InstallFile(string source,string destination)
        {if(point=="automatic"&&Path.GetFileName(destination).StartsWith("auto-backup-"))throw new IOException("injected automatic backup failure");base.InstallFile(source,destination);}
        public override void MoveDirectory(string source,string destination)
        {if(!_fired&&point=="install"&&source.Contains(Path.DirectorySeparatorChar+"staging"+Path.DirectorySeparatorChar)){_fired=true;throw new IOException("injected install failure");}base.MoveDirectory(source,destination);}
        public override void Checkpoint(string current)
        {if(!_fired&&((point=="journal"&&current=="journal.before-write")||(point=="cleanup"&&current=="restore.cleanup"))){_fired=true;throw new IOException("injected "+point+" failure");}}
    }
    public static BackupDataV2 ReadData(string path)
    {using var zip=ZipFile.OpenRead(path);using var stream=zip.GetEntry("data.json")!.Open();return BackupDataStore.Normalize(JsonSerializer.Deserialize<BackupDataV2>(stream)!);}
    private static void Mutate(string source,string target,string kind)
    {
        using var input=ZipFile.OpenRead(source);var entries=input.Entries.ToDictionary(e=>e.FullName,e=>{using var s=e.Open();using var m=new MemoryStream();s.CopyTo(m);return m.ToArray();});
        var manifest=JsonNode.Parse(entries["manifest.json"])!.AsObject();var data=JsonNode.Parse(entries["data.json"])!.AsObject();
        string managed=entries.Keys.First(k=>k.EndsWith("pixel.png"));var special=new Dictionary<string,int>();
        switch(kind)
        {
            case "missing":entries.Remove(managed);break;
            case "hash":entries[managed]=[1,2,3];break;
            case "traversal":entries["../escape"]=[1];break;
            case "absolute":entries["/tmp/escape"]=[1];break;
            case "windows":entries["C:\\escape"]=[1];break;
            case "case-duplicate":entries["DATA.JSON"]=entries["data.json"];break;
            case "symlink":special[managed]=0xa000<<16;break;
            case "extra-secret":entries["secure/key.txt"]=[1];break;
            case "duplicate-id":data["Books"]![1]!["Book"]!["Id"]=data["Books"]![0]!["Book"]!["Id"]!.GetValue<int>();break;
            case "duplicate-uid":data["Books"]![1]!["BookUid"]=data["Books"]![0]!["BookUid"]!.GetValue<string>();break;
            case "orphan":data["Quotes"]![0]!["Quote"]!["BookId"]=999;break;
            case "position":var position=JsonNode.Parse(data["Progress"]![0]!["LastPositionJson"]!.GetValue<string>())!;position["Value"]!["Utf16Offset"]=99999;data["Progress"]![0]!["LastPositionJson"]=position.ToJsonString();break;
            case "percent":data["Progress"]![0]!["CurrentPercent"]=99;break;
            case "receipt":data["Batches"]![0]!["InsertedQuoteIdsJson"]="[26]";break;
            case "missing-collection":data.Remove("Sessions");break;
            case "grapheme":
                var grapheme=JsonNode.Parse(data["Progress"]![0]!["LastPositionJson"]!.GetValue<string>())!;
                var chapterName=entries.Keys.First(k=>k.EndsWith("one.utf16"));var chapterText=new System.Text.UnicodeEncoding(false,false,true).GetString(entries[chapterName]);
                grapheme["Value"]!["Utf16Offset"]=chapterText.IndexOf('e')+1;data["Progress"]![0]!["LastPositionJson"]=grapheme.ToJsonString();break;
            case "block-gap":case "resource-pixels":case "resource-dimensions":case "toc-target":case "canonical-cr":
                var canonicalName=entries.Keys.First(k=>k.EndsWith("document-v1.json"));var canonical=JsonNode.Parse(entries[canonicalName])!;
                if(kind=="block-gap")canonical["Chapters"]![0]!["Blocks"]![0]!["StartOffset"]=1;
                if(kind=="resource-dimensions"){canonical["Resources"]![0]!["PixelWidth"]=2;canonical["Chapters"]![1]!["Blocks"]![1]!["Image"]!["PixelWidth"]=2;}
                if(kind=="resource-pixels"){canonical["Resources"]![0]!["PixelWidth"]=50000001;canonical["Chapters"]![1]!["Blocks"]![1]!["Image"]!["PixelWidth"]=50000001;}
                if(kind=="toc-target")canonical["TableOfContents"]![0]!["ChapterKey"]="missing";
                if(kind=="canonical-cr")
                {
                    var textName=entries.Keys.First(k=>k.EndsWith("one.utf16"));var codec=new System.Text.UnicodeEncoding(false,false,true);
                    entries[textName]=codec.GetBytes(codec.GetString(entries[textName]).Replace('\n','\r'));
                    canonical["Chapters"]![0]!["Sha256"]=ManagedBookFiles.Sha256(entries[textName]);
                    var listedText=manifest["Files"]!.AsArray().Single(n=>n!["Path"]!.GetValue<string>()==textName)!;listedText["Sha256"]=ManagedBookFiles.Sha256(entries[textName]);
                }
                var canonicalObject=JsonSerializer.Deserialize<CanonicalManifest>(canonical.ToJsonString())!;entries[canonicalName]=BackupJson.Write(canonicalObject);
                var oldCanonical=data["Files"]![0]!["CanonicalSha256"]!.GetValue<string>();var newCanonical=ManagedBookFiles.Sha256(entries[canonicalName]);
                data=JsonNode.Parse(data.ToJsonString().Replace(oldCanonical,newCanonical))!.AsObject();
                var listed=manifest["Files"]!.AsArray().Single(n=>n!["Path"]!.GetValue<string>()==canonicalName)!;listed["Bytes"]=entries[canonicalName].Length;listed["Sha256"]=newCanonical;
                break;
            case "canonical":var name=entries.Keys.First(k=>k.EndsWith("document-v1.json"));entries[name]="{}"u8.ToArray();break;
        }
        entries["data.json"]=System.Text.Encoding.UTF8.GetBytes(data.ToJsonString());
        if(kind=="duplicate-json")entries["data.json"]=System.Text.Encoding.UTF8.GetBytes("{\"FormatVersion\":2,"+System.Text.Encoding.UTF8.GetString(entries["data.json"])[1..]);
        manifest["DataSha256"]=kind=="data-hash"?new string('0',64):ManagedBookFiles.Sha256(entries["data.json"]);
        entries["manifest.json"]=System.Text.Encoding.UTF8.GetBytes(manifest.ToJsonString());
        using var output=ZipFile.Open(target,ZipArchiveMode.Create);
        foreach(var (name,bytes)in entries){var e=output.CreateEntry(name);if(special.TryGetValue(name,out var attributes))e.ExternalAttributes=attributes;using var stream=e.Open();stream.Write(bytes);}
    }
    public void Dispose(){if(Directory.Exists(_root))Directory.Delete(_root,true);}
}
