using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Moq;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Ai;
using ReadJournal.Library.Services.Reader;
using ReadJournal.Library.ViewModels;
using ReadJournal.Reader;
using Xunit;
namespace ReadJournal.UnitTest;

public sealed class AiViewModelTests
{
    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() => HeadlessUnitTestSession.StartNew(typeof(AiViewModelTests)));
    public static AppBuilder BuildAvaloniaApp()
    {
        Environment.SetEnvironmentVariable("READJOURNAL_DATA_DIR", Path.Combine(Path.GetTempPath(), "ReadJournalP8UiApp", Guid.NewGuid().ToString("N")));
        return AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseSkia();
    }
    private static Task Ui(Func<Task> action) => Session.Value.Dispatch(async () => { await action(); return true; }, default);
    private sealed class ReaderFixture : IAsyncDisposable
    {
        public AiTestFixture Data { get; private set; } = null!;
        public AvaloniaReaderLayoutService Layouts { get; private set; } = null!;
        public ReaderViewModel Vm { get; private set; } = null!;
        public MainViewModel Main { get; private set; } = null!;
        public Mock<IAlertService> Alerts { get; } = new();
        public static async Task<ReaderFixture> Create(IReaderLayoutService? scopeLayouts = null)
        {
            var x = new ReaderFixture(); x.Data = await AiTestFixture.Create(AiTestFixture.Text + "\n" + new string('字', 7000)); await x.Data.Close();
            x.Layouts = new(x.Data.Documents); x.Main = new(Mock.Of<IMenuNavigationService>());
            x.Alerts.Setup(a => a.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            x.Vm = new(x.Data.Db, x.Data.Db.Coordinator, x.Data.Documents, x.Layouts, x.Data.Sessions, new ReaderPreferencesStorage(x.Data.Db.Paths, new()), Mock.Of<IReaderPlatformService>(), Mock.Of<IReaderAlertService>(), new(), x.Main, null, x.Alerts.Object,
                new AiScopeFactory(x.Data.Documents, scopeLayouts ?? x.Layouts), x.Data.Ai, x.Data.Settings);
            x.Main.SetMenuAndContent(MenuNavigationConstant.BookListView, x.Vm); await x.Open(); return x;
        }
        public async Task Open()
        { Vm.SetParameter(new ReaderOpenRequest(Data.Context.Identity.BookId)); await Vm.OnLoadedCommand.ExecuteAsync(null); Assert.Equal("", Vm.Error); await Vm.PagePresentedAsync(Vm.Page!.Ref.Layout); Assert.Equal(ReaderPhase.Active, Vm.Phase); }
        public async ValueTask DisposeAsync() { await Vm.CanLeaveAsync(); Layouts.Dispose(); await Data.DisposeAsync(); }
    }
    [Fact]
    public Task AnalysisGenerationRetryUndoAndSourceViewsHaveActualBoundControls() => Ui(async () =>
    {
        await using var x = await ReaderFixture.Create(); var vm = x.Vm;
        await vm.AnalyzeCommand.ExecuteAsync(null); Assert.Contains("模拟分析", vm.AiResult); Assert.Equal(0, await x.Data.QuoteCount());
        x.Alerts.Verify(a => a.ConfirmAsync("确认解读范围", It.Is<string>(s => s.Contains("deepseek-flash") && s.Contains("24000"))), Times.Once);
        x.Data.Faults.Point = "ai.before-commit"; await vm.GenerateQuotesCommand.ExecuteAsync(null); Assert.True(vm.CanRetryAiSave); Assert.False(vm.IsAiBusy); Assert.False(vm.IsBusy);
        var requests = x.Data.Client.Count; await vm.RetryAiSaveCommand.ExecuteAsync(null); Assert.Equal(requests, x.Data.Client.Count); Assert.True(vm.CanUndoAi); Assert.Contains("保存1条", vm.AiStatus);
        await vm.UndoAiCommand.ExecuteAsync(null); Assert.Equal(0, await x.Data.QuoteCount()); Assert.False(vm.CanUndoAi);
        // Parse real XAML and inspect bindings; no substitute controls are used.
        var view = new ReadJournal.Views.ReaderView { DataContext = vm }; view.ApplyTemplate(); Dispatcher.UIThread.RunJobs();
        var generate = view.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "生成并自动保存书摘")); Assert.Same(vm.GenerateQuotesCommand, generate.Command);
        Assert.NotNull(new ReadJournal.Views.QuoteListView()); Assert.NotNull(new ReadJournal.Views.QuoteEditView());
    });
    [Fact]
    public Task ReflowKeepsSubmittedSnapshotWhileLocalReadingRemainsAvailable() => Ui(async () =>
    {
        await using var x = await ReaderFixture.Create(); var vm = x.Vm;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var reply = new TaskCompletionSource<AiProviderResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.Data.Client.Reply = _ => { started.SetResult(); return reply.Task; };
        var pending = vm.GenerateQuotesCommand.ExecuteAsync(null); await started.Task; var request = x.Data.Client.Request!.UserText;
        Assert.True(vm.IsAiBusy); Assert.False(vm.IsBusy); var page = vm.Page;
        await vm.NextCommand.ExecuteAsync(null); Assert.NotEqual(page, vm.Page); vm.AiFirstPage = "1"; vm.AiLastPage = "2";
        vm.FontSizeText = "28"; await vm.ApplyAppearanceCommand.ExecuteAsync(null); Assert.Equal("", vm.AiFirstPage); Assert.True(vm.IsAiBusy); Assert.Equal(request, x.Data.Client.Request.UserText);
        reply.SetResult(new(null, [new("春天的河流缓缓流过。", "感想", null, null)], null, null, null)); await pending;
        Assert.Contains("保存1条", vm.AiStatus); Assert.Equal(1, await x.Data.QuoteCount()); Assert.Equal("", vm.Error);
    });
    [Fact]
    public Task LeavingAndReusingSingletonDoesNotWaitForNetworkOrLetOldResultChangeNewPanel() => Ui(async () =>
    {
        await using var x = await ReaderFixture.Create(); var vm = x.Vm;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var reply = new TaskCompletionSource<AiProviderResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.Data.Client.Reply = _ => { started.SetResult(); return reply.Task; }; var pending = vm.GenerateQuotesCommand.ExecuteAsync(null); await started.Task;
        var close = vm.CanLeaveAsync(); Assert.True(await close.WaitAsync(TimeSpan.FromSeconds(5))); Assert.False(pending.IsCompleted); await x.Open();
        var status = vm.AiStatus; var context = vm.Context;
        reply.SetResult(new(null, [new("春天的河流缓缓流过。", "旧结果", null, null)], null, null, null)); await pending;
        Assert.Equal(context, vm.Context); Assert.Equal(status, vm.AiStatus); Assert.Equal("", vm.AiResult); Assert.False(vm.IsAiBusy); Assert.Equal(0, await x.Data.QuoteCount());
    });
    [Fact]
    public Task PendingPageIndexCanBeCancelledByReflowAndSendsNothing() => Ui(async () =>
    {
        var layouts = new Mock<IReaderLayoutService>(); var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        layouts.Setup(l => l.ResolveGlobalPagesAsync(It.IsAny<LayoutHandle>(), It.IsAny<PageSpan>(), null, It.IsAny<CancellationToken>())).Returns(async (LayoutHandle _, PageSpan _, IProgress<LayoutIndexStatus>? _, CancellationToken ct) =>
        { started.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, ct); throw new Exception("unreachable"); });
        await using var x = await ReaderFixture.Create(layouts.Object); var vm = x.Vm; vm.AiScope = "全书页范围"; vm.AiFirstPage = "1"; vm.AiLastPage = "2";
        var pending = vm.GenerateQuotesCommand.ExecuteAsync(null); await started.Task; Assert.True(vm.IsAiBusy); Assert.Equal(0, x.Data.Client.Count);
        vm.FontSizeText = "28"; await vm.ApplyAppearanceCommand.ExecuteAsync(null); await pending;
        Assert.False(vm.IsAiBusy); Assert.Contains("排版", vm.AiStatus); Assert.Equal("", vm.AiFirstPage); Assert.Equal(0, x.Data.Client.Count); Assert.Equal(0, await x.Data.QuoteCount());
    });
    [Fact]
    public Task ScopeControlsUseOneInterpretationActionAndNeverFallBackFromMissingSelection() => Ui(async () =>
    {
        await using var x = await ReaderFixture.Create(); var vm = x.Vm;
        var view = new ReadJournal.Views.ReaderView { DataContext = vm }; view.ApplyTemplate(); Dispatcher.UIThread.RunJobs();
        var buttons = view.GetLogicalDescendants().OfType<Button>().ToArray();
        Assert.Single(buttons.Where(b => Equals(b.Content, "AI 解读")));
        Assert.DoesNotContain(buttons, b => Equals(b.Content, "解释选区") || Equals(b.Content, "分析所选范围"));
        var inputs = view.FindControl<Grid>("AiPageRangeInputs")!;
        Assert.False(inputs.IsVisible);
        vm.AiScope = "全书页范围"; Dispatcher.UIThread.RunJobs(); Assert.True(inputs.IsVisible);
        vm.AiFirstPage = "invalid"; vm.AiLastPage = "2";
        await vm.AnalyzeCommand.ExecuteAsync(null); Assert.Equal(0, x.Data.Client.Count); Assert.Contains("起止", vm.AiStatus);
        vm.AiScope = "当前选区"; Dispatcher.UIThread.RunJobs();
        Assert.False(inputs.IsVisible); Assert.Equal("", vm.AiFirstPage); Assert.Equal("", vm.AiLastPage);
        Assert.False(vm.CanRunAi); Assert.False(vm.GenerateQuotesCommand.CanExecute(null)); Assert.Contains("请先选择正文", vm.AiScopeDescription);
        await vm.GenerateQuotesCommand.ExecuteAsync(null); Assert.Equal(0, x.Data.Client.Count);
        var start = vm.Page!.ContentRanges[0].Start;
        await vm.SetSelectionAsync(new(start, start with { Utf16Offset = 20 })); Assert.True(vm.CanRunAi);
        await vm.AnalyzeCommand.ExecuteAsync(null); Assert.Equal(AiOperation.ExplainSelection, x.Data.Client.Request!.Operation); Assert.Equal(0, await x.Data.QuoteCount());
        await vm.GenerateQuotesCommand.ExecuteAsync(null); Assert.Equal(1, await x.Data.QuoteCount());
        Assert.Equal(AiTestFixture.Text[..20], Assert.Single(await x.Data.Quotes.SearchAsync("", false)).Quote.Text);
        vm.AiScope = "当前页"; vm.AiFirstPage = "ignored"; vm.AiLastPage = "ignored";
        await vm.AnalyzeCommand.ExecuteAsync(null); Assert.Equal(AiOperation.Analyze, x.Data.Client.Request!.Operation);
        Assert.Equal(AiScopeKind.CurrentPage, (await new AiScopeFactory(x.Data.Documents, x.Layouts).CurrentPageAsync(vm.Context!, vm.Page!, default)).Kind);
    });

    [Fact]
    public Task PanelCombinationsReleaseSpaceAndPreservePositionSelectionAndTaskControls() => Ui(async () =>
    {
        await using var x = await ReaderFixture.Create(); var vm = x.Vm;
        var view = new ReadJournal.Views.ReaderView { DataContext = vm }; view.ApplyTemplate();
        var surface = view.FindControl<ReaderSurface>("Surface")!;
        surface.LayoutService = x.Layouts;
        var window = new Window { Content = view, Width = 1140, Height = 800 };
        window.Show();
        try
        {
        for (var i = 0; i < 100 && (surface.Bounds.Width == 0 || vm.IsLayoutBusy); i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        var anchor = vm.Position; var start = vm.Page!.ContentRanges[0].Start;
        var selection = new DocumentRange(start, start with { Utf16Offset = 20 }); await vm.SetSelectionAsync(selection);
        var context = vm.Context; double expandedWidth = 0, expandedHeight = 0;
        for (var mask = 7; mask >= 0; mask--)
        {
            view.FindControl<ToggleButton>("TocToggle")!.IsChecked = (mask & 1) != 0;
            view.FindControl<ToggleButton>("AppearanceToggle")!.IsChecked = (mask & 2) != 0;
            view.FindControl<ToggleButton>("AiToggle")!.IsChecked = (mask & 4) != 0;
            Dispatcher.UIThread.RunJobs(); view.Measure(new Size(1140, 800)); view.Arrange(new Rect(0, 0, 1140, 800));
            for (var i = 0; i < 100 && vm.IsLayoutBusy; i++) await Task.Delay(10);
            Assert.Equal((mask & 1) != 0, view.FindControl<Border>("TocPanel")!.IsVisible);
            Assert.Equal((mask & 2) != 0, view.FindControl<Border>("AppearancePanel")!.IsVisible);
            Assert.Equal((mask & 4) != 0, view.FindControl<Border>("AiPanel")!.IsVisible);
            Assert.Equal(selection, vm.Selection); Assert.Equal(anchor, vm.Position); Assert.Equal(context, vm.Context);
            if (mask == 7) { expandedWidth = surface.Bounds.Width; expandedHeight = surface.Bounds.Height; }
            if (mask == 0) { Assert.True(surface.Bounds.Width > expandedWidth + 400, $"width {expandedWidth} -> {surface.Bounds.Width}"); Assert.True(surface.Bounds.Height > expandedHeight, $"height {expandedHeight} -> {surface.Bounds.Height}"); }
        }
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<AiProviderResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.Data.Client.Reply = _ => { started.SetResult(); return reply.Task; };
        var pending = vm.GenerateQuotesCommand.ExecuteAsync(null); await started.Task;
        var cancel = view.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "取消AI任务"));
        Dispatcher.UIThread.RunJobs(); Assert.True(cancel.IsVisible); Assert.False(vm.IsAiExpanded);
        vm.IsAiExpanded = true; vm.IsAiExpanded = false;
        vm.CancelAiCommand.Execute(null); reply.SetResult(new(null, [], null, null, null)); await pending;
        Assert.Equal(1, x.Data.Client.Count); Assert.Equal(0, await x.Data.QuoteCount());
        }
        finally { window.Content = null; window.Close(); }
    });

    [Fact]
    public Task RejectedAndMixedCandidatesReportOutcomeWithoutClaimingUnsavedOriginalsWereSaved() => Ui(async () =>
    {
        await using var x = await ReaderFixture.Create(); var vm = x.Vm;
        x.Data.Client.Reply = _ => Task.FromResult(new AiProviderResponse(null, [new("不存在", "", null, null)], null, 5, 7));
        await vm.GenerateQuotesCommand.ExecuteAsync(null); Assert.Contains("未通过原文核验", vm.AiStatus); Assert.Contains("没有新增", vm.AiResult);
        x.Data.Client.Reply = _ => Task.FromResult(new AiProviderResponse(null, [new("春天的河流缓缓流过。", "整体感想", null, null), new("不存在", "", null, null)], null, 5, 7));
        await vm.GenerateQuotesCommand.ExecuteAsync(null); Assert.Contains("部分成功", vm.AiStatus); Assert.Equal(1, await x.Data.QuoteCount());
        await vm.GenerateQuotesCommand.ExecuteAsync(null); Assert.Contains("无新增", vm.AiStatus); Assert.Contains("没有新增", vm.AiResult);
    });
    [Fact]
    public Task SettingsMaskAndClearInputDeleteCredentialsAndPersistOnlyNonSecrets() => Ui(async () =>
    {
        await using var x = await AiTestFixture.Create(); var vm = new SettingsViewModel(Mock.Of<IPreferenceStorage>(), Mock.Of<IBackupService>(), Mock.Of<IFileService>(), Mock.Of<IAlertService>(), x.Settings, x.Secrets);
        await vm.OnLoadedCommand.ExecuteAsync(null); Assert.True(vm.UseMock); var key = "fictional-" + Guid.NewGuid().ToString("N");
        var view = new ReadJournal.Views.SettingsView { DataContext = vm }; view.ApplyTemplate(); Dispatcher.UIThread.RunJobs();
        var box = view.FindControl<TextBox>("ApiKeyBox")!; Assert.Equal('●', box.PasswordChar);
        vm.ApiKeyInput = key; await vm.SaveApiKeyCommand.ExecuteAsync(null); Assert.Equal("", vm.ApiKeyInput); Assert.True(await x.Secrets.HasApiKeyAsync(default));
        await vm.SaveAiSettingsCommand.ExecuteAsync(null); Assert.False((await File.ReadAllTextAsync(Path.Combine(x.Db.Paths.DirectoryPath, "ai-settings.json"))).Contains(key, StringComparison.Ordinal));
        vm.ApiKeyInput = "unsaved"; Assert.True(await vm.CanLeaveAsync()); Assert.Equal("", vm.ApiKeyInput);
        await vm.DeleteApiKeyCommand.ExecuteAsync(null); Assert.False(await x.Secrets.HasApiKeyAsync(default)); Assert.Contains("删除", vm.CredentialStatus);
        vm.ApiKeyInput = "invalid\ncredential"; await vm.SaveApiKeyCommand.ExecuteAsync(null); Assert.Equal("", vm.ApiKeyInput); Assert.DoesNotContain("invalid", vm.Error);
    });
}
