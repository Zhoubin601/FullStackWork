using Avalonia;
using Avalonia.Headless;
using Moq;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Reader;
using ReadJournal.Library.ViewModels;
using ReadJournal.Reader;
using Xunit;
namespace ReadJournal.UnitTest;
public sealed class ReaderLifecycleTests
{
    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() => HeadlessUnitTestSession.StartNew(typeof(ReaderLifecycleTests)));
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<ReaderLayoutContractTests.LayoutTestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseSkia();
    private static Task Ui(Func<Task> action) => Session.Value.Dispatch(async () => { await action(); return true; }, default);
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "ReadJournalP5Lifecycle", Guid.NewGuid().ToString("N"));
        public DatabaseService Db { get; }
        public ReaderDocumentService Documents { get; }
        public AvaloniaReaderLayoutService Layouts { get; }
        public ReaderSessionService Sessions { get; }
        public ReaderViewModel Vm { get; }
        public MainViewModel Main { get; }
        public Fixture(ReaderSessionCheckpoints? faults = null, IReaderAlertService? alerts = null, IReaderFontCatalog? fonts = null,
            Func<AvaloniaReaderLayoutService, IReaderLayoutService>? layoutFactory = null)
        {
            Db = new(new DataPaths(Path.Combine(Root, "data"))); Documents = new(Db, Db.Coordinator); Layouts = new(Documents);
            Sessions = new(Db, Db.Coordinator, Documents, new EbookProgressMaintenance(Db, Db.Coordinator), new ReaderClock(), faults ?? new ReaderSessionCheckpoints());
            Main = new MainViewModel(Mock.Of<IMenuNavigationService>());
            var confirmation = new Mock<IAlertService>(); confirmation.Setup(a => a.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            var quotes = new ReaderQuoteService(Db, Db.Coordinator, Documents, Sessions, new ReadingService(), new ReaderQuoteCheckpoints());
            Vm = new(Db, Db.Coordinator, Documents, layoutFactory?.Invoke(Layouts) ?? Layouts, Sessions, new ReaderPreferencesStorage(Db.Paths, new()), Mock.Of<IReaderPlatformService>(), alerts ?? Mock.Of<IReaderAlertService>(), new ReaderClock(), Main, quotes, confirmation.Object, fonts: fonts);
        }
        public async Task<int> Add(int number, int repeats = 2000)
        {
            Directory.CreateDirectory(Root); var source = Path.Combine(Root, $"book{number}.txt"); await File.WriteAllTextAsync(source, $"第一章 合成{number}\n" + string.Concat(Enumerable.Repeat("正文 e\u0301 与中文标点。\n", repeats)));
            var result = await EbookRuntime.CreateLibrary(Db).ImportAsync(new(source), null, default); Assert.Equal(ImportState.Imported, result.State); return result.BookId!.Value;
        }
        public async Task Open(int id, bool present = true)
        {
            Vm.SetParameter(new ReaderOpenRequest(id)); await Vm.OnLoadedCommand.ExecuteAsync(null);
            Assert.Equal("", Vm.Error); Assert.NotNull(Vm.Page); Assert.Equal(ReaderPhase.Presenting, Vm.Phase);
            if (present) { await Vm.PagePresentedAsync(Vm.Page!.Ref.Layout); Assert.Equal(ReaderPhase.Active, Vm.Phase); }
        }
        public void Dispose() { Layouts.Dispose(); Db.Dispose(); if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
    [Fact]
    public Task ViewportChangeDuringFirstLayoutStillPresentsAndStartsExactlyOneSession() => Ui(async () =>
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var creates = 0;
        using var x = new Fixture(layoutFactory: inner =>
        {
            var layouts = new Mock<IReaderLayoutService>();
            layouts.Setup(l => l.CreateAsync(It.IsAny<DocumentKey>(), It.IsAny<LayoutSettings>(), It.IsAny<ReaderPosition>(), It.IsAny<CancellationToken>()))
                .Returns(async (DocumentKey document, LayoutSettings settings, ReaderPosition anchor, CancellationToken ct) =>
                {
                    if (++creates == 1) { started.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
                    return await inner.CreateAsync(document, settings, anchor, ct);
                });
            layouts.Setup(l => l.LocateAsync(It.IsAny<LayoutHandle>(), It.IsAny<ReaderPosition>(), It.IsAny<CancellationToken>()))
                .Returns((LayoutHandle handle, ReaderPosition position, CancellationToken ct) => inner.LocateAsync(handle, position, ct));
            layouts.Setup(l => l.ReleaseAsync(It.IsAny<LayoutHandle>())).Returns((LayoutHandle handle) => inner.ReleaseAsync(handle));
            layouts.Setup(l => l.ReleaseAllAsync()).Returns(() => inner.ReleaseAllAsync());
            layouts.Setup(l => l.BuildIndexAsync(It.IsAny<LayoutHandle>(), It.IsAny<IProgress<LayoutIndexStatus>>(), It.IsAny<CancellationToken>()))
                .Returns((LayoutHandle handle, IProgress<LayoutIndexStatus>? progress, CancellationToken ct) => inner.BuildIndexAsync(handle, progress, ct));
            layouts.Setup(l => l.GetIndexStatusAsync(It.IsAny<LayoutHandle>(), It.IsAny<CancellationToken>()))
                .Returns((LayoutHandle handle, CancellationToken ct) => inner.GetIndexStatusAsync(handle, ct));
            return layouts.Object;
        });
        var id = await x.Add(1, 100);
        x.Vm.SetParameter(new ReaderOpenRequest(id));
        var opening = x.Vm.OnLoadedCommand.ExecuteAsync(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await x.Vm.SetViewportAsync(640, 320, 1.5);
            await opening;
            Assert.NotNull(x.Vm.Page);
            Assert.Equal(ReaderPhase.Presenting, x.Vm.Phase);
            await x.Vm.PagePresentedAsync(x.Vm.Page!.Ref.Layout);
            Assert.Equal(ReaderPhase.Active, x.Vm.Phase);
            await x.Vm.PagePresentedAsync(x.Vm.Page.Ref.Layout);
            Assert.True(await x.Vm.CanLeaveAsync());
            Assert.Single(await x.Sessions.GetForBookAsync(id, default));
        }
        finally { await x.Vm.CanLeaveAsync(); }
    });
    [Fact]
    public Task SystemFontChangeKeepsAnchorAndPersistsAcrossReaderSessions() => Ui(async () =>
    {
        var catalog = new SystemReaderFontCatalog(); Assert.NotEmpty(catalog.FontFamilies);
        using var x = new Fixture(fonts: catalog); var id = await x.Add(1, 100);
        await x.Open(id); await x.Vm.NextCommand.ExecuteAsync(null); var anchor = x.Vm.Position;
        var layout = x.Vm.Page!.Ref.Layout;
        x.Vm.FontFamily = catalog.FontFamilies.Last(); x.Vm.FontSizeText = "28";
        await x.Vm.ApplyAppearanceCommand.ExecuteAsync(null);
        Assert.Equal("", x.Vm.Error); Assert.Equal(anchor, x.Vm.Position); Assert.NotEqual(layout, x.Vm.Page!.Ref.Layout);
        var saved = await new ReaderPreferencesStorage(x.Db.Paths, new()).GetAsync(default);
        Assert.Equal(x.Vm.FontFamily, saved.FontFamily); Assert.Equal(28, saved.FontSize);
        Assert.True(await x.Vm.CanLeaveAsync()); await x.Open(id);
        Assert.Equal(saved.FontFamily, x.Vm.FontFamily); Assert.Equal("28", x.Vm.FontSizeText); Assert.Equal(anchor, x.Vm.Position);
        Assert.True(await x.Vm.CanLeaveAsync()); Assert.Equal(2, (await x.Sessions.GetForBookAsync(id, default)).Count);
    });
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MissingDefaultOrSavedFontCanStillChangeTypographyWithoutOverwritingPreferencesOnOpen(bool hasSavedPreference) => Ui(async () =>
    {
        var fonts = new Mock<IReaderFontCatalog>();
        fonts.SetupGet(c => c.FontFamilies).Returns(new[] { "Arial" });
        using var x = new Fixture(fonts: fonts.Object);
        var preferences = new ReaderPreferencesStorage(x.Db.Paths, new());
        if (hasSavedPreference)
            await preferences.SaveAsync(ReaderPreferencesStorage.Default with { FontFamily = "MissingFontFromAnotherComputer", Palette = "dark" }, default);
        await x.Open(await x.Add(1, 100));
        try
        {
            var anchor = x.Vm.Position;
            Assert.Equal("Arial", x.Vm.FontFamily);
            if (hasSavedPreference)
            {
                Assert.Equal("MissingFontFromAnotherComputer", (await preferences.GetAsync(default)).FontFamily);
                Assert.Equal("dark", x.Vm.Palette);
            }
            else Assert.False(File.Exists(Path.Combine(x.Db.Paths.DirectoryPath, "reader-preferences.json")));
            x.Vm.FontSizeText = "28";
            await x.Vm.ApplyAppearanceCommand.ExecuteAsync(null);
            Assert.Equal("", x.Vm.Error);
            Assert.Equal("", x.Vm.Notice);
            Assert.Equal(anchor, x.Vm.Position);
            Assert.Equal("Arial", (await preferences.GetAsync(default)).FontFamily);
            Assert.Equal(28, (await preferences.GetAsync(default)).FontSize);
        }
        finally { Assert.True(await x.Vm.CanLeaveAsync()); }
    });
    [Fact]
    public Task UnknownFontDoesNotChangeSavedLayoutAndResetRecovers() => Ui(async () =>
    {
        var catalog = new SystemReaderFontCatalog(); using var x = new Fixture(fonts: catalog);
        await x.Open(await x.Add(1, 100)); var layout = x.Vm.Page!.Ref.Layout;
        x.Vm.FontFamily = "NotInstalled-" + Guid.NewGuid();
        await x.Vm.ApplyAppearanceCommand.ExecuteAsync(null);
        Assert.Contains("未安装", x.Vm.Error); Assert.Equal(layout, x.Vm.Page!.Ref.Layout);
        Assert.False(File.Exists(Path.Combine(x.Db.Paths.DirectoryPath, "reader-preferences.json")));
        await x.Vm.SetViewportAsync(800, 460, 1); Assert.NotNull(x.Vm.Page);
        await x.Vm.ResetTypographyCommand.ExecuteAsync(null);
        Assert.Equal("", x.Vm.Error); Assert.Equal(catalog.FontFamilies.First(), x.Vm.FontFamily); Assert.Equal("22", x.Vm.FontSizeText);
        Assert.True(await x.Vm.CanLeaveAsync());
    });
    [Fact]
    public Task FontPreviewBindingStaysRenderableWhileSearchingAndUsesSelectedFont() => Ui(async () =>
    {
        using var x = new Fixture(fonts: new SystemReaderFontCatalog());
        var preview = new Avalonia.Controls.TextBlock { Text = "中文 Reading" };
        preview.Bind(Avalonia.Controls.TextBlock.FontFamilyProperty, new Avalonia.Data.Binding(nameof(ReaderViewModel.FontFamily))
        { Source = x.Vm, Converter = new ReaderFontFamilyConverter() });
        x.Vm.FontFamily = "#partial search"; Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.NotNull(preview.FontFamily);
        x.Vm.FontFamily = x.Vm.FontFamilies.First(); Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.StartsWith(x.Vm.FontFamily, preview.FontFamily.Name);
        preview.Measure(new Avalonia.Size(800, 100)); Assert.True(preview.DesiredSize.Width > 0);
        await Task.CompletedTask;
    });
    [Fact]
    public Task ActualRoutesKeepSingleReaderNodeAndCloseBeforeSwitchThenRestoreDetail() => Ui(async () =>
    {
        var root = Path.Combine(Path.GetTempPath(), "ReadJournalP5Routes", Guid.NewGuid().ToString("N"));
        var previous = Environment.GetEnvironmentVariable("READJOURNAL_DATA_DIR"); Environment.SetEnvironmentVariable("READJOURNAL_DATA_DIR", Path.Combine(root, "data"));
        var locator = new ServiceLocator();
        typeof(ServiceLocator).GetField("_current", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.SetValue(null, locator);
        var provider = (IServiceProvider)typeof(ServiceLocator).GetField("_serviceProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(locator)!;
        try
        {
            Directory.CreateDirectory(root); var library = (IEbookLibraryService)provider.GetService(typeof(IEbookLibraryService))!;
            var ids = new List<int>();
            for (var i = 0; i < 2; i++) { var path = Path.Combine(root, $"route{i}.txt"); await File.WriteAllTextAsync(path, $"第一章 Route{i}\n" + new string('字', 3000)); var imported = await library.ImportAsync(new(path), null, default); Assert.True(imported.State == ImportState.Imported, imported.Message); ids.Add(imported.BookId!.Value); }
            var main = locator.MainViewModel; main.SetMenuAndContent(MenuNavigationConstant.BookListView, locator.BookListViewModel);
            var nav = new ReadJournal.Services.ContentNavigationService();
            Assert.True(await nav.NavigateToAsync(ContentNavigationConstant.BookDetailView, ids[0]));
            Assert.True(await nav.NavigateToAsync(ContentNavigationConstant.ReaderView, new ReaderOpenRequest(ids[0])));
            var vm = locator.ReaderViewModel; await vm.PagePresentedAsync(vm.Page!.Ref.Layout); var context = vm.Context;
            var position = vm.Position! with { Utf16Offset = 100 }; Assert.True(await nav.NavigateToAsync(ContentNavigationConstant.ReaderView, new ReaderOpenRequest(ids[0], new(position, position with { Utf16Offset = 104 }))));
            Assert.Equal(context, vm.Context); Assert.Equal(3, main.ContentStack.Count);
            Assert.True(await nav.NavigateToAsync(ContentNavigationConstant.ReaderView, new ReaderOpenRequest(ids[1]))); await vm.PagePresentedAsync(vm.Page!.Ref.Layout);
            Assert.NotEqual(context!.SessionId, vm.Context!.SessionId); Assert.Equal(ids[1], vm.Context.Identity.BookId); Assert.Equal(3, main.ContentStack.Count);
            Assert.True(await main.RequestBackAsync()); Assert.Same(locator.BookDetailViewModel, main.Content); Assert.Equal(ids[0], locator.BookDetailViewModel.Book!.Id);
            var sessions = (IReaderSessionService)provider.GetService(typeof(IReaderSessionService))!; Assert.Single(await sessions.GetForBookAsync(ids[0], default)); Assert.Single(await sessions.GetForBookAsync(ids[1], default));
        }
        finally
        {
            if (locator.ReaderViewModel.Context is not null) await locator.ReaderViewModel.CanLeaveAsync();
            ((IDisposable)provider).Dispose(); typeof(ServiceLocator).GetField("_current", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.SetValue(null, null);
            Environment.SetEnvironmentVariable("READJOURNAL_DATA_DIR", previous); if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    });
    [Fact]
    public Task SelectionCompletionAndResetCommandsKeepHistoryAndReopenFreshEpoch() => Ui(async () =>
    {
        using var x = new Fixture(); var id = await x.Add(1); x.Main.SetMenuAndContent(MenuNavigationConstant.BookListView, x.Vm); await x.Open(id);
        var oldEpoch = x.Db.Coordinator.CurrentEpoch; var position = x.Vm.Position!;
        await x.Vm.SetSelectionAsync(new(position, position with { Utf16Offset = 5 })); await x.Vm.SaveSelectionCommand.ExecuteAsync(null);
        Assert.Equal(1, await x.Db.ReadAsync(c => c.Table<ReadJournal.Library.Models.Quote>().Count()));
        await x.Vm.MarkCompletedCommand.ExecuteAsync(null); Assert.NotNull((await x.Db.ReadAsync(c => c.Find<EbookProgress>(id))).CompletedUtcTicks);
        await x.Vm.ResetProgressCommand.ExecuteAsync(null); Assert.Equal("", x.Vm.Error); Assert.Equal(ReaderPhase.Presenting, x.Vm.Phase); Assert.NotEqual(oldEpoch, x.Db.Coordinator.CurrentEpoch);
        Assert.Single(await x.Sessions.GetForBookAsync(id, default)); Assert.Equal(1, await x.Db.ReadAsync(c => c.Table<ReadJournal.Library.Models.Reader.QuoteOrigin>().Count()));
        Assert.False((await x.Db.ReadAsync(c => c.Find<EbookProgress>(id)))?.HasStarted ?? false);
        await x.Vm.PagePresentedAsync(x.Vm.Page!.Ref.Layout); Assert.Null((await x.Db.ReadAsync(c => c.Find<EbookProgress>(id))).CompletedUtcTicks); Assert.True(await x.Vm.CanLeaveAsync());
    });
    private sealed class Failure : ReaderSessionCheckpoints
    { public string? Point; public override void Checkpoint(string point) { if (Point == point) { Point = null; throw new IOException(point); } } }
    [Theory][InlineData(false)][InlineData(true)]
    public Task CloseFailureStaysFrozenThenContinuesUsingCommittedFact(bool committed) => Ui(async () =>
    {
        var fault = new Failure(); var alerts = new Mock<IReaderAlertService>(); alerts.Setup(a => a.ChooseCloseFailureAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ReaderCloseDecision.Stay);
        using var x = new Fixture(fault, alerts.Object); var id = await x.Add(1); await x.Open(id); var original = x.Vm.Context;
        fault.Point = committed ? "session.after-commit" : "session.in-transaction";
        Assert.False(await x.Vm.CanLeaveAsync()); Assert.Equal(ReaderPhase.CloseFailed, x.Vm.Phase); Assert.NotNull(x.Vm.Page);
        await x.Vm.ContinueReadingCommand.ExecuteAsync(null); Assert.Equal(ReaderPhase.Active, x.Vm.Phase); Assert.Equal(committed, original!.SessionId != x.Vm.Context!.SessionId);
        Assert.True(await x.Vm.CanLeaveAsync()); Assert.Equal(committed ? 2 : 1, (await x.Sessions.GetForBookAsync(id, default)).Count);
    });
    [Fact]
    public Task NoSessionOrStartedFlagUntilFirstPageActuallyPresentedAndFailedOpenStaysEmpty() => Ui(async () =>
    {
        using var x = new Fixture(); var id = await x.Add(1); await x.Open(id, false);
        Assert.Null(x.Vm.Context); Assert.False(await x.Db.ReadAsync(c => c.Find<EbookProgress>(id).HasStarted));
        Assert.True(await x.Vm.CanLeaveAsync()); Assert.Empty(await x.Sessions.GetForBookAsync(id, default));
        x.Vm.SetParameter(new ReaderOpenRequest(999)); await x.Vm.OnLoadedCommand.ExecuteAsync(null); Assert.Equal(ReaderPhase.OpenFailed, x.Vm.Phase); Assert.Null(x.Vm.Context); Assert.True(await x.Vm.CanLeaveAsync());
    });
    [Fact]
    public Task BackwardPositionSurvivesFontViewportReflowAndFreshOpenWithoutNewSameBookSession() => Ui(async () =>
    {
        using var x = new Fixture(); var id = await x.Add(1); await x.Open(id); var context = x.Vm.Context;
        await x.Vm.NextCommand.ExecuteAsync(null); await x.Vm.NextCommand.ExecuteAsync(null); await x.Vm.PreviousCommand.ExecuteAsync(null);
        var anchor = x.Vm.Position; Assert.True(anchor!.Utf16Offset > 0);
        x.Vm.FontSizeText = "28"; x.Vm.Palette = "dark"; await x.Vm.ApplyAppearanceCommand.ExecuteAsync(null); await x.Vm.SetViewportAsync(480, 320, 1.25);
        Assert.Equal(anchor, x.Vm.Position); Assert.Equal(context, x.Vm.Context);
        await x.Vm.NavigateWithinBookAsync(new(id, new(anchor, anchor)), default); Assert.Equal(context, x.Vm.Context);
        Assert.True(await x.Vm.CanLeaveAsync()); Assert.Single(await x.Sessions.GetForBookAsync(id, default)); Assert.Equal(0, x.Layouts.ActiveLayoutCount);
        var preferences = await new ReaderPreferencesStorage(x.Db.Paths, new()).GetAsync(default); Assert.Equal(28, preferences.FontSize); Assert.Equal("dark", preferences.Palette); Assert.False(File.Exists(Path.Combine(x.Db.Paths.DirectoryPath, "preferences.json")));
        await x.Open(id); Assert.Equal(anchor, x.Vm.Position); Assert.True(await x.Vm.CanLeaveAsync());
    });
    [Fact]
    public Task TwentySwitchesCancelBackgroundIndexReleaseLayoutsAndIgnoreOldPresentation() => Ui(async () =>
    {
        using var x = new Fixture(); var a = await x.Add(1, 6000); var b = await x.Add(2, 6000); ReadJournal.Library.Models.Reader.LayoutId? old = null;
        for (var i = 0; i < 20; i++)
        {
            await x.Open(i % 2 == 0 ? a : b); var context = x.Vm.Context;
            if (old is not null) { await x.Vm.PagePresentedAsync(old.Value); Assert.Equal(context, x.Vm.Context); }
            old = x.Vm.Page!.Ref.Layout;
            Assert.InRange(x.Layouts.ActiveLayoutCount, 1, 1); Assert.True(await x.Vm.CanLeaveAsync().WaitAsync(TimeSpan.FromSeconds(10))); Assert.Equal(0, x.Layouts.ActiveLayoutCount); Assert.Null(x.Vm.Page);
        }
        Assert.Equal(10, (await x.Sessions.GetForBookAsync(a, default)).Count); Assert.Equal(10, (await x.Sessions.GetForBookAsync(b, default)).Count);
    });
}
