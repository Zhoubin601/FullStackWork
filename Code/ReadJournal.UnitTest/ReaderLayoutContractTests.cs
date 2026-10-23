using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Microsoft.Extensions.DependencyInjection;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Reader;
using ReadJournal.Reader;
using Xunit;
using Document = ReadJournal.Library.Models.Reader.ReaderDocument;
namespace ReadJournal.UnitTest;

public sealed class ReaderLayoutContractTests
{
    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() => HeadlessUnitTestSession.StartNew(typeof(ReaderLayoutContractTests)));
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<LayoutTestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseSkia();
    private static Task Ui(Func<Task> action) => Session.Value.Dispatch(async () => { await action(); return true; }, default);
    private static LayoutSettings Settings(double width = 500, string font = "Noto Sans CJK SC") => new(width, 400, 1,
        new ReaderAppearance(font,22,34,0,0,28,28,"paper"),1,"p2-test-fontset");
    [Fact]
    public Task FormalLayoutPreservesCanonicalChapterKeysAndNearEndAnchorWithoutFullIndex() => Ui(async () =>
    {
        using var fixture = new Fixture();
        var texts = new Dictionary<string,string> { ["spine/front"] = "前言\n并非正则章节名称。\n", ["spine/main"] = string.Concat(Enumerable.Repeat("正文📖组合 e\u0301。\n",15000)) };
        var document = fixture.Publish(texts);
        using var service = new AvaloniaReaderLayoutService(fixture.Documents);
        var anchor = new ReaderPosition(document.Key,"spine/main",texts["spine/main"].Length - 12);
        // Snap the anchor using the document service rather than split a grapheme.
        anchor = anchor with { Utf16Offset = (await fixture.Documents.ReadSliceAsync(document.Key,anchor.ChapterKey,anchor.Utf16Offset,1,default)).StartOffset };
        var handle = await service.CreateAsync(document.Key,Settings(),anchor,default);
        var page = await service.LocateAsync(handle,anchor,default);
        Assert.Equal("spine/main",page.Ref.ChapterKey); Assert.Null(page.GlobalPageNumber); Assert.Null(page.Ref.PageInChapter);
        Assert.True(page.ContentRanges[0].Start.Utf16Offset <= anchor.Utf16Offset && anchor.Utf16Offset <= page.ContentRanges[0].End.Utf16Offset);
        Assert.Null((await service.GetIndexStatusAsync(handle,default)).ExactGlobalPageCount);
        var older = page;
        await service.ReleaseAsync(handle);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAdjacentAsync(older.Ref,PageDirection.Previous,default));
    });
    [Fact]
    public Task FormalRangesCopyExactTextAcrossChaptersAndFrozenSnapshotSurvivesReflow() => Ui(async () =>
    {
        using var fixture = new Fixture(); var texts = new Dictionary<string,string> { ["a"] = "第一部分\n"+new string('甲',1800),["b"]="第二部分\n"+new string('乙',1700) };
        var document = fixture.Publish(texts); using var service = new AvaloniaReaderLayoutService(fixture.Documents);
        var handle = await service.CreateAsync(document.Key,Settings(),new(document.Key,"a",0),default);
        var page = await service.LocateAsync(handle,new(document.Key,"a",0),default); var copied = new System.Text.StringBuilder(); var count = 0;
        while (true)
        {
            var text = await fixture.Documents.ExtractAsync(page.ContentRanges[0],default); copied.Append(string.Concat(text.Spans.Select(s=>s.Text))); count++;
            var next = await service.GetAdjacentAsync(page.Ref,PageDirection.Next,default); if (next is null) break; page = next;
            Assert.InRange(count,1,100);
        }
        Assert.Equal(string.Concat(texts.Values),copied.ToString()); Assert.True(page.IsDocumentEnd);
        Assert.Equal(count,(await service.GetIndexStatusAsync(handle,default)).ExactGlobalPageCount);
        var snapshot = await service.ResolveGlobalPagesAsync(handle,new(1,2),null,default);
        var frozen = string.Concat(snapshot.Text.Spans.Select(s=>s.Text)); Assert.NotEmpty(frozen);
        var newHandle = await service.CreateAsync(document.Key,Settings(420,"MissingFont-P1"),new(document.Key,"a",2),default);
        await service.ReleaseAsync(handle);
        Assert.Equal(frozen,string.Concat(snapshot.Text.Spans.Select(s=>s.Text)));
        using var cancelled = new CancellationTokenSource();cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.ResolveGlobalPagesAsync(newHandle,new(1,2),null,cancelled.Token));
        await service.ReleaseAsync(newHandle);
    });
    [Fact]
    public Task UnifiedRenderHitAndSelectionUseOneFormalLayoutInstance() => Ui(async () =>
    {
        using var fixture = new Fixture(); var document=fixture.Publish(new Dictionary<string,string>{{"body","中文标点📖 e\u0301。\n"+new string('字',1000)}});
        using var service = new AvaloniaReaderLayoutService(fixture.Documents); var handle=await service.CreateAsync(document.Key,Settings(),new(document.Key,"body",0),default);
        var page=await service.LocateAsync(handle,new(document.Key,"body",0),default);
        var hit=await service.HitTestAsync(page.Ref,new PointD(40,15),default); Assert.NotNull(hit);
        Assert.True((await fixture.Documents.ResolveAsync(hit!,null,default)).IsExact);
        var range=new DocumentRange(new(document.Key,"body",0),new(document.Key,"body",4));
        Assert.NotEmpty(await service.GetSelectionRectsAsync(page.Ref,range,default));
        var surface=new ReaderSurface { LayoutService=service,Page=page,Selection=range,Width=500,Height=400 };
        var window=new Window { Content=surface,Width=500,Height=400 };
        try { window.Show(); surface.Measure(new Size(500,400));surface.Arrange(new Rect(0,0,500,400));using var bitmap=new RenderTargetBitmap(new PixelSize(500,400));bitmap.Render(surface); Assert.Equal(500,bitmap.PixelSize.Width); }
        finally { window.Close(); }
        await service.ReleaseAsync(handle);
    });
    [Fact]
    public Task ContainerResolvesP2AliasesAndLibraryHasNoAvaloniaDependency() => Ui(async () =>
    {
        var previous=Environment.GetEnvironmentVariable("READJOURNAL_DATA_DIR"); var directory=Path.Combine(Path.GetTempPath(),"ReadJournalP2",Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("READJOURNAL_DATA_DIR",directory);
        var locator=new ServiceLocator();
        try
        {
            var provider=(ServiceProvider)typeof(ServiceLocator).GetField("_serviceProvider",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(locator)!;
            try
            {
                foreach(var type in new[]{typeof(IApplicationDataCoordinator),typeof(IReadingStateCalculator),typeof(IEbookProgressMaintenance),typeof(IReaderDocumentService),typeof(IReaderLayoutService),typeof(BackupV1Adapter),typeof(BackupEntityValidator)})
                { var value=provider.GetRequiredService(type); Assert.Same(value,provider.GetRequiredService(type)); }
                Assert.Same(provider.GetRequiredService<AvaloniaReaderLayoutService>(),provider.GetRequiredService<IReaderLayoutService>());
                Assert.Same(provider.GetRequiredService<ApplicationDataCoordinator>(),provider.GetRequiredService<IApplicationDataCoordinator>());
                Assert.Same(provider.GetRequiredService<ReaderDocumentService>(),provider.GetRequiredService<IReaderDocumentService>());
                await provider.GetRequiredService<IDatabaseService>().InitializeAsync();
                Assert.DoesNotContain(typeof(IReaderLayoutService).Assembly.GetReferencedAssemblies(),a=>a.Name!.StartsWith("Avalonia"));
            }
            finally { provider.Dispose(); }
        }
        finally { Environment.SetEnvironmentVariable("READJOURNAL_DATA_DIR",previous);if(Directory.Exists(directory))Directory.Delete(directory,true); }
    });
    private sealed class Fixture : IDisposable
    {
        private readonly string _directory=Path.Combine(Path.GetTempPath(),"ReadJournalP2",Guid.NewGuid().ToString("N"));
        private readonly DatabaseService _db;
        internal ReaderDocumentService Documents { get; }
        internal Fixture() { _db=new(new DataPaths(_directory));Documents=new(_db,_db.Coordinator); }
        internal Document Publish(Dictionary<string,string> texts)
        {
            var document=new Document(ReaderCoordinatorTests.Key(),"合成",texts.Select((p,i)=>new ChapterDescriptor(p.Key,p.Key,i,p.Value.Length)).ToArray(),[],texts.Values.Sum(s=>(long)s.Length));
            Documents.PublishText(document,texts);return document;
        }
        public void Dispose(){_db.Dispose();if(Directory.Exists(_directory))Directory.Delete(_directory,true);}
    }
    public sealed class LayoutTestApp : Application { public override void Initialize()=>Styles.Add(new FluentTheme()); }
}
