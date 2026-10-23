using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Reader;
using ReadJournal.Library.ViewModels;
using Xunit;
using Document=ReadJournal.Library.Models.Reader.ReaderDocument;
namespace ReadJournal.UnitTest;

public sealed class BackupUiIntegrationTests
{
    [Fact]
    public Task P4RealContainerScansBeforeNavigationAndSharesFileAndParserServices()=>Session.Value.Dispatch(async()=>
    {
        var directory=Path.Combine(Path.GetTempPath(),"ReadJournalP4Ui",Guid.NewGuid().ToString("N"));var previous=Environment.GetEnvironmentVariable("READJOURNAL_DATA_DIR");
        Directory.CreateDirectory(Path.Combine(directory,"books"));await File.WriteAllTextAsync(Path.Combine(directory,"books","合成TXT.txt"),ReaderP4TestData.Text);
        await File.WriteAllBytesAsync(Path.Combine(directory,"books","损坏.txt"),[0xff]);
        Environment.SetEnvironmentVariable("READJOURNAL_DATA_DIR",directory);var locator=new ServiceLocator();Application.Current!.Resources[nameof(ServiceLocator)]=locator;
        typeof(ServiceLocator).GetField("_current",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,null);
        var provider=(ServiceProvider)typeof(ServiceLocator).GetField("_serviceProvider",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(locator)!;
        try
        {
            Assert.Same(provider.GetRequiredService<IFileService>(),provider.GetRequiredService<IAppFileDialogService>());
            Assert.Same(provider.GetRequiredService<TxtBookParser>(),provider.GetRequiredService<IBookParser>());
            Assert.Same(provider.GetRequiredService<IEbookLibraryService>(),provider.GetRequiredService<IEbookLibraryService>());
            await locator.InitializationViewModel.OnLoadedCommand.ExecuteAsync(null);Assert.Empty(locator.InitializationViewModel.Error);
            var books=await provider.GetRequiredService<IBookStorage>().GetAllAsync();Assert.Single(books);Assert.Equal("合成TXT",books.Single().Title);
            Assert.Same(locator.MainViewModel,locator.MainWindowViewModel.Content);
            locator.MainViewModel.SetMenuAndContent(MenuNavigationConstant.BookListView,locator.BookListViewModel);await locator.BookListViewModel.OnLoadedCommand.ExecuteAsync(null);
            Assert.Single(locator.BookListViewModel.BooksPage);await locator.BookListViewModel.ScanInboxCommand.ExecuteAsync(null);Assert.Empty(locator.BookListViewModel.Error);
            Assert.Contains("新增0本",locator.BookListViewModel.OperationMessage);Assert.Contains("EncodingRequired",locator.BookListViewModel.OperationMessage);
            locator.BookDetailViewModel.SetParameter(books.Single().Id);await locator.BookDetailViewModel.OnLoadedCommand.ExecuteAsync(null);Assert.False(locator.BookDetailViewModel.CanAddManualRecord);
        }
        finally{provider.Dispose();Environment.SetEnvironmentVariable("READJOURNAL_DATA_DIR",previous);Directory.Delete(directory,true);typeof(ServiceLocator).GetField("_current",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,null);}
        return true;
    },default);
    private static readonly Lazy<HeadlessUnitTestSession> Session=new(()=>HeadlessUnitTestSession.StartNew(typeof(BackupUiIntegrationTests)));
    public static AppBuilder BuildAvaloniaApp()=>AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions{UseHeadlessDrawing=false}).UseSkia();
    [Fact]
    public Task ActualSingletonRestoreClearsCachedViewsDocumentsLayoutsAndOldBookIdentity()=>Session.Value.Dispatch(async()=>
    {
        var directory=Path.Combine(Path.GetTempPath(),"ReadJournalP3Ui",Guid.NewGuid().ToString("N"));var previous=Environment.GetEnvironmentVariable("READJOURNAL_DATA_DIR");
        Environment.SetEnvironmentVariable("READJOURNAL_DATA_DIR",directory);var locator=new ServiceLocator();Application.Current!.Resources[nameof(ServiceLocator)]=locator;
        typeof(ServiceLocator).GetField("_current",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,null);
        var provider=(ServiceProvider)typeof(ServiceLocator).GetField("_serviceProvider",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(locator)!;
        try
        {
            var backup=provider.GetRequiredService<IBackupService>();await backup.LoadDemoAsync();var old=(await provider.GetRequiredService<IBookStorage>().GetAsync(1))!;
            locator.BookDetailViewModel.SetParameter(1);await locator.BookDetailViewModel.OnLoadedCommand.ExecuteAsync(null);
            locator.BookEditViewModel.SetParameter(1);await locator.BookEditViewModel.OnLoadedCommand.ExecuteAsync(null);locator.BookEditViewModel.Title="旧数据草稿";
            await locator.BookListViewModel.OnLoadedCommand.ExecuteAsync(null);await locator.OverviewViewModel.OnLoadedCommand.ExecuteAsync(null);await locator.QuoteListViewModel.OnLoadedCommand.ExecuteAsync(null);
            var documents=provider.GetRequiredService<ReaderDocumentService>();var key=ReaderCoordinatorTests.Key();
            documents.PublishText(new Document(key,"合成",[new("body","正文",0,3)],[],3),new Dictionary<string,string>{{"body","正文。"}});
            var layouts=provider.GetRequiredService<IReaderLayoutService>();var layout=await layouts.CreateAsync(key,new(400,300,1,new("Noto Sans CJK SC",22,34,0,0,20,20,"paper"),1,"fixture"),new(key,"body",0),default);
            var page=await layouts.LocateAsync(layout,new(key,"body",0),default);
            locator.MainViewModel.SetMenuAndContent(MenuNavigationConstant.SettingsView,locator.SettingsViewModel);
            var input=Path.Combine(directory,"legacy.json");await File.WriteAllTextAsync(input,JsonSerializer.Serialize(DemoData.Create()));
            var epoch=provider.GetRequiredService<IApplicationDataCoordinator>().CurrentEpoch;
            await backup.RestoreAsync(input);
            Assert.Null(backup.LastWarning);
            Assert.NotEqual(epoch,provider.GetRequiredService<IApplicationDataCoordinator>().CurrentEpoch);
            Assert.NotEqual(old.BookUid,(await provider.GetRequiredService<IBookStorage>().GetAsync(1))!.BookUid);
            Assert.Null(locator.BookDetailViewModel.Book);Assert.Empty(locator.BookDetailViewModel.RecordsList);Assert.Empty(locator.BookListViewModel.BooksPage);
            Assert.Empty(locator.BookEditViewModel.Title);Assert.False(locator.BookEditViewModel.IsDirty);Assert.Empty(locator.QuoteListViewModel.QuotesList);Assert.Empty(locator.OverviewViewModel.ReadingBooks);
            Assert.Single(locator.MainViewModel.ContentStack);Assert.Same(locator.SettingsViewModel,locator.MainViewModel.Content);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>documents.DescribeAsync(key,default));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>layouts.GetAdjacentAsync(page.Ref,PageDirection.Next,default));
            Assert.Same(provider.GetRequiredService<DatabaseService>(),provider.GetRequiredService<IOperationRecoveryService>());
        }
        finally{provider.Dispose();Environment.SetEnvironmentVariable("READJOURNAL_DATA_DIR",previous);if(Directory.Exists(directory))Directory.Delete(directory,true);typeof(ServiceLocator).GetField("_current",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,null);}
        return true;
    },default);
    [Fact]
    public async Task LateOldListResponseCannotRefillClearedSingletonAfterReplacement()
    {
        var pending=new TaskCompletionSource<PageResult<BookProgress>>(TaskCreationOptions.RunContinuationsAsynchronously);var books=new Mock<IBookStorage>();
        books.Setup(b=>b.SearchAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<int>(),It.IsAny<int>())).Returns(pending.Task);
        var vm=new BookListViewModel(books.Object,Mock.Of<IContentNavigationService>());var search=vm.SearchCommand.ExecuteAsync(null);
        vm.InvalidateDataSet();pending.SetResult(new(){Items=[new(){Book=new(){Id=10,Title="过期书籍",TotalPages=20}}],TotalCount=1});await search;
        Assert.Empty(vm.BooksPage);Assert.True(vm.IsEmpty);
    }
    [Fact]
    public async Task RecoveryFailureKeepsInitializationPageAndPreservesEvidence()
    {
        var directory=Path.Combine(Path.GetTempPath(),"ReadJournalP3Ui",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(directory,"operations"));
        var log=Path.Combine(directory,"operations","restore-"+Guid.NewGuid().ToString("D")+".json");await File.WriteAllTextAsync(log,"{truncated");
        try
        {
            using var db=new DatabaseService(new DataPaths(directory));var root=new Mock<IRootNavigationService>();var menu=new Mock<IMenuNavigationService>();
            var vm=new InitializationViewModel(db,root.Object,menu.Object,db);await vm.OnLoadedCommand.ExecuteAsync(null);
            Assert.Contains("RecoveryRequired",vm.Error);Assert.False(db.IsInitialized);root.Verify(r=>r.NavigateTo(It.IsAny<string>()),Times.Never);menu.Verify(m=>m.NavigateTo(It.IsAny<string>(),It.IsAny<object>()),Times.Never);
            Assert.Equal("{truncated",await File.ReadAllTextAsync(log));
        }
        finally{Directory.Delete(directory,true);}
    }
}
