using System.Reflection;
using Avalonia;
using Avalonia.Controls;
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
        x.Alerts.Verify(a => a.ConfirmAsync("确认分析范围", It.Is<string>(s => s.Contains("deepseek-flash") && s.Contains("24000"))), Times.Once);
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
