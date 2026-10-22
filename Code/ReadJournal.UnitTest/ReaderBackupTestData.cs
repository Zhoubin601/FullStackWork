using System.Text;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Backup;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.UnitTest;

// Deterministic small synthetic business/file fixture; no user documents or real credentials.
public static class ReaderBackupTestData
{
    public static async Task SeedAsync(DatabaseService db, DataPaths paths, string label)
    {
        await db.InitializeAsync();
        var fileId = Guid.Parse(label == "A" ? "00000000-0000-0000-0000-00000000000a" : "00000000-0000-0000-0000-00000000000b");
        var uid = Guid.Parse(label == "A" ? "10000000-0000-0000-0000-00000000000a" : "10000000-0000-0000-0000-00000000000b");
        var prefix = ManagedBookFiles.BookPrefix(fileId); var now = DateTime.Today.AddDays(-2); var utc = new DateTimeOffset(now, TimeSpan.Zero);
        var first = "第一章 " + label + "\n规范化正文 e\u0301 与 📖。\n"; var second = "图片前。\ufffc图片后。\n";
        var a = new UnicodeEncoding(false, false, true).GetBytes(first); var b = new UnicodeEncoding(false, false, true).GetBytes(second);
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a0R8AAAAASUVORK5CYII=");
        var resource = new CanonicalResource("pixel", "resources/pixel.png", "image/png", 1, 1, ManagedBookFiles.Sha256(png));
        var manifest = new CanonicalManifest(1, "合成电子书" + label,
            [new("one","第一章",0,"chapters/one.utf16",first.Length,ManagedBookFiles.Sha256(a),[new("p1",ReaderBlockKind.Paragraph,0,first.Length,null,[])]),
             new("two","第二章",1,"chapters/two.utf16",second.Length,ManagedBookFiles.Sha256(b),
                [new("before",ReaderBlockKind.Paragraph,0,4,null,[]),new("image",ReaderBlockKind.Image,4,1,new("pixel",1,1,"image/png",resource.Sha256),[]),new("after",ReaderBlockKind.Paragraph,5,second.Length-5,null,[])])],
            [resource],first.Length+second.Length-1,[new("toc","合成目录","one",0,null,[])]);
        var bytes = BackupJson.Write(manifest); var source = Encoding.UTF8.GetBytes("合成源文件 " + label + "\n" + first + second);
        var io = new ManagedBookFiles();
        io.WriteDurable(ManagedBookFiles.SafePath(paths.DirectoryPath,prefix+"source.txt"),source);
        io.WriteDurable(ManagedBookFiles.SafePath(paths.DirectoryPath,prefix+"document-v1.json"),bytes);
        io.WriteDurable(ManagedBookFiles.SafePath(paths.DirectoryPath,prefix+"chapters/one.utf16"),a);
        io.WriteDurable(ManagedBookFiles.SafePath(paths.DirectoryPath,prefix+"chapters/two.utf16"),b);
        io.WriteDurable(ManagedBookFiles.SafePath(paths.DirectoryPath,prefix+resource.Path),png);
        var key = new DocumentKey(fileId,ManagedBookFiles.Sha256(source),ManagedBookFiles.Sha256(bytes),1);
        var start = new ReaderPosition(key,"one",0); var end = new ReaderPosition(key,"one",4); var batchId = Guid.NewGuid();
        await db.WriteAsync(c =>
        {
            c.InsertOrReplace(new Book {Id=10,Title="合成手动书"+label,TotalPages=100,CreatedAt=now,UpdatedAt=now});
            c.InsertOrReplace(new Book {Id=20,Title="合成电子书"+label,Kind=BookKind.Ebook,BookUid=uid,TotalPages=0,CreatedAt=now,UpdatedAt=now});
            c.InsertOrReplace(new ReadingRecord {Id=15,BookId=10,ReadingDate=now,StartPage=10,EndPage=20,Minutes=5});
            c.InsertOrReplace(new Quote {Id=25,BookId=10,Text="合成手动书摘"+label,Page=12,Thoughts="合成感想",CreatedAt=now,UpdatedAt=now});
            c.InsertOrReplace(new Quote {Id=26,BookId=20,Text="用户已编辑的合成书摘"+label,Thoughts="保留修改",Revision=2,IsFavorite=true,CreatedAt=now,UpdatedAt=now});
            c.InsertOrReplace(new ReadingPlan {Id=30,BookId=10,StartDate=now,TargetDate=DateTime.Today.AddDays(7)});
            c.InsertOrReplace(new ReadingPlan {Id=31,BookId=20,StartDate=now,TargetDate=DateTime.Today.AddDays(7)});
            c.Insert(new EbookFile {BookId=20,FileId=fileId,Format=EbookFormat.Txt,SourceRelativePath=prefix+"source.txt",CanonicalRelativePath=prefix+"document-v1.json",SourceSha256=key.SourceSha256,CanonicalSha256=key.CanonicalSha256,SourceBytes=source.Length,ParserVersion=1,ImportedUtcTicks=utc.UtcTicks});
            c.Insert(new EbookProgress {BookId=20,DocumentKeyJson=ReaderContractValidator.WriteDocument(key),LastPositionJson=ReaderContractValidator.WritePosition(start),HasStarted=true,CurrentPercent=0,LastOpenedUtcTicks=utc.UtcTicks,LastSavedUtcTicks=utc.UtcTicks});
            c.Insert(new EbookReadingSession {SessionId=Guid.NewGuid(),BookId=20,BookUid=uid,DocumentKeyJson=ReaderContractValidator.WriteDocument(key),StartPositionJson=ReaderContractValidator.WritePosition(start),EndPositionJson=ReaderContractValidator.WritePosition(end),StartPercent=0,EndPercent=400.0/manifest.TotalTextUnits,OpenedUtcTicks=utc.UtcTicks,ClosedUtcTicks=utc.AddMilliseconds(90123).UtcTicks,DurationMilliseconds=90123,CloseLocalDate=utc.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture)});
            c.Insert(new AiGenerationBatch {BatchId=batchId,BookId=20,BookUid=uid,DocumentKeyJson=ReaderContractValidator.WriteDocument(key),ScopeSha256=new string('a',64),CreatedUtcTicks=utc.UtcTicks,InsertedCount=2,DuplicateCount=1,RejectedCount=1,InsertedQuoteIdsJson="[26,999]"});
            c.Insert(new QuoteOrigin {QuoteId=26,Source=QuoteSource.AiGenerated,OriginalText=first[..4],DocumentKeyJson=ReaderContractValidator.WriteDocument(key),StartPositionJson=ReaderContractValidator.WritePosition(start),EndPositionJson=ReaderContractValidator.WritePosition(end),PrefixHash=new string('b',64),SuffixHash=new string('c',64),BatchId=batchId,DedupeKey=new string('d',64)});
            c.Insert(new AiGenerationBatchItem {BatchId=batchId,QuoteId=26,CreatedRevision=0,CreatedContentHash=new string('e',64)});return 0;
        });
    }
    public static BackupDocument Legacy(string zip)
    {
        using var archive=System.IO.Compression.ZipFile.OpenRead(zip);using var stream=archive.GetEntry("data.json")!.Open();
        var data=BackupDataStore.Normalize(System.Text.Json.JsonSerializer.Deserialize<BackupDataV2>(stream)!);
        return new(){Books=data.Books.Select(x=>x.Book).ToList(),ReadingRecords=data.ReadingRecords.ToList(),Quotes=data.Quotes.Select(x=>x.Quote).ToList(),ReadingPlans=data.ReadingPlans.ToList()};
    }
}
