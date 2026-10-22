using System.Text.Json;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Reader;
using Xunit;
using Document = ReadJournal.Library.Models.Reader.ReaderDocument;
namespace ReadJournal.UnitTest;

public sealed class ReaderDocumentContractTests : IDisposable
{
    private readonly string _directory=Path.Combine(Path.GetTempPath(),"ReadJournalP2",Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task ActualDocumentOpenChecksBookUidDocumentAndEpochThenReadsGraphemeSafeSlices()
    {
        using var db=new DatabaseService(new DataPaths(_directory)); await db.InitializeAsync();
        var key=ReaderCoordinatorTests.Key();var book=new Book {Title="合成",Kind=BookKind.Ebook};await new BookStorage(db,new ReadingService()).SaveAsync(book);
        await db.WriteAsync(c=>c.Insert(new EbookFile { BookId=book.Id,FileId=key.FileId,Format=EbookFormat.Txt,SourceSha256=key.SourceSha256,CanonicalSha256=key.CanonicalSha256,ParserVersion=key.ParserVersion }));
        var service=new ReaderDocumentService(db,db.Coordinator);
        var document=new Document(key,"标题",[new("a","前言",0,5),new("b","正文",1,4)],[],9);
        var input=new Dictionary<string,string>{{"a","e\u0301📖甲"},{"b","正文尾。"}}; service.PublishText(document,input);input["a"]="被修改！";
        var identity=new ReaderOpenIdentity(book.Id,book.BookUid,key,db.Coordinator.CurrentEpoch,Guid.NewGuid(),1);
        Assert.Equal(document.Key,(await service.OpenAsync(identity,default)).Key);
        var slice=await service.ReadSliceAsync(key,"a",1,1,default);Assert.Equal(0,slice.StartOffset);Assert.Equal("e\u0301",slice.Text);
        Assert.Equal(0,await service.GetPercentAsync(new(key,"a",0),default));Assert.Equal(100,await service.GetPercentAsync(new(key,"b",4),default));
        var snapshot=await service.ExtractAsync(new(new(key,"a",2),new(key,"b",2)),default);
        Assert.Equal("📖甲正文",string.Concat(snapshot.Spans.Select(s=>s.Text)));Assert.Equal(2,snapshot.Spans.Count);
        Assert.False((await service.ResolveAsync(new(key,"a",1),null,default)).IsExact);
        Assert.False((await service.ResolveAsync(new(key,"a",0),"错误原句",default)).IsExact);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.OpenAsync(identity with {BookUid=Guid.NewGuid()},default));
        await using(var maintenance=await db.Coordinator.EnterMaintenanceAsync(MaintenanceKind.Restore,default))maintenance.CompleteReplacement();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.OpenAsync(identity,default));
        await service.ReleaseAsync(key);await Assert.ThrowsAsync<InvalidOperationException>(()=>service.DescribeAsync(key,default));
    }
    [Fact]
    public void ProgressValidationRejectsVersionMismatchAndDocumentDisagreement()
    {
        var key=ReaderCoordinatorTests.Key();
        var row=new EbookProgress{DocumentKeyJson=ReaderContractValidator.WriteDocument(key),LastPositionJson=ReaderContractValidator.WritePosition(new(key,"c",0))};
        BackupEntityValidator.ValidateProgress(row);
        row.LastPositionJson=ReaderContractValidator.WritePosition(new(ReaderCoordinatorTests.Key(),"c",0));
        Assert.Throws<ArgumentException>(()=>BackupEntityValidator.ValidateProgress(row));
        row.LastPositionJson="{}";Assert.Throws<JsonException>(()=>BackupEntityValidator.ValidateProgress(row));
    }
    public void Dispose(){if(Directory.Exists(_directory))Directory.Delete(_directory,true);}
}
