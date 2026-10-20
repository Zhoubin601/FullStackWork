using Moq;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
using ReadJournal.Library.ViewModels;
using Xunit;

namespace ReadJournal.UnitTest;

// 独立检查用例；不属于仓库源码。预期行为断言用于复现当前缺陷。
public class AuditRegressionTests
{
    [Fact]
    public async Task LoadingExistingBookMustNotOverwriteDraftTypedDuringAwait()
    {
        var pending = new TaskCompletionSource<Book?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var books = new Mock<IBookStorage>();
        books.Setup(x => x.GetAsync(1)).Returns(pending.Task);
        var main = new MainViewModel(Mock.Of<IMenuNavigationService>());
        var vm = new BookEditViewModel(books.Object, Mock.Of<IAlertService>(), main);
        vm.SetParameter(1);
        var loading = vm.OnLoadedCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy);
        vm.Title = "加载时已输入的新书名"; // XAML 的 TextBox 未绑定 IsEnabled，加载中用户可输入。
        pending.SetResult(new Book { Id = 1, Title = "数据库旧书名", TotalPages = 100 });
        await loading;
        Assert.Equal("加载时已输入的新书名", vm.Title);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task LoadingQuoteBookChoicesMustNotEraseTextAlreadyTyped()
    {
        var pending = new TaskCompletionSource<IReadOnlyList<Book>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var books = new Mock<IBookStorage>();
        books.Setup(x => x.GetAllAsync()).Returns(pending.Task);
        var main = new MainViewModel(Mock.Of<IMenuNavigationService>());
        var vm = new QuoteEditViewModel(books.Object, Mock.Of<IQuoteStorage>(), Mock.Of<IAlertService>(), main);
        vm.SetParameter(null!);
        var loading = vm.OnLoadedCommand.ExecuteAsync(null);
        vm.Text = "加载中输入的摘录";
        pending.SetResult(new[] { new Book { Id = 1, Title = "测试书", TotalPages = 100 } });
        await loading;
        Assert.Equal("加载中输入的摘录", vm.Text);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task ChangesTypedDuringSaveMustNotBeMarkedSavedAndDiscarded()
    {
        var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Book? persisted = null;
        var books = new Mock<IBookStorage>();
        books.Setup(x => x.SaveAsync(It.IsAny<Book>())).Callback<Book>(x => persisted = x).Returns(pending.Task);
        books.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new PageResult<BookProgress> { Items = Array.Empty<BookProgress>(), TotalCount = 0 });
        var main = new MainViewModel(Mock.Of<IMenuNavigationService>());
        var vm = new BookEditViewModel(books.Object, Mock.Of<IAlertService>(), main);
        vm.SetParameter(null!); await vm.OnLoadedCommand.ExecuteAsync(null);
        main.PushContent(new BookListViewModel(books.Object, Mock.Of<IContentNavigationService>())); main.PushContent(vm);
        vm.Title = "提交时的书名"; vm.TotalPages = "100";
        var saving = vm.SaveCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy);
        vm.Title = "保存中又改了书名"; // XAML 未禁用输入。
        pending.SetResult(1); await saving;
        Assert.Equal("提交时的书名", persisted!.Title);
        Assert.True(vm.IsDirty); // 当前实现将保存中新增输入也标记为 clean。
        Assert.Same(vm, main.Content);
    }

    [Fact]
    public async Task RemovingCompletionRecordMustNotProduceTwoUnfinishedPlans()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ReadJournalAudit", Guid.NewGuid().ToString());
        using var database = new DatabaseService(new DataPaths(dir));
        try
        {
            var reading = new ReadingService(); var books = new BookStorage(database, reading);
            var records = new ReadingRecordStorage(database, reading); var plans = new ReadingPlanStorage(database, reading);
            var a = new Book { Title = "A", TotalPages = 100 }; var b = new Book { Title = "B", TotalPages = 100 };
            await books.SaveAsync(a); await books.SaveAsync(b);
            var pa = new ReadingPlan { BookId = a.Id, TargetDate = DateTime.Today };
            var pb = new ReadingPlan { BookId = b.Id, TargetDate = DateTime.Today };
            await plans.SaveAsync(pa); await plans.SaveAsync(pb);
            var completion = new ReadingRecord { BookId = a.Id, StartPage = 1, EndPage = 100, Minutes = 1 };
            await records.SaveAsync(completion);
            pb.BookId = a.Id; await plans.SaveAsync(pb); // UI 可修改计划关联书籍；已完成时允许重复。
            await records.DeleteAsync(completion.Id);
            var unfinished = (await plans.GetAllAsync()).Count(p => p.Plan.BookId == a.Id && p.State != "已完成");
            Assert.True(unfinished <= 1, $"A 出现 {unfinished} 个未完成计划");
        }
        finally { database.Dispose(); if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task BackupExportedAfterLegalUiOperationsMustPassOwnValidation()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ReadJournalAudit", Guid.NewGuid().ToString());
        using var database = new DatabaseService(new DataPaths(dir));
        try
        {
            var reading = new ReadingService(); var books = new BookStorage(database, reading);
            var records = new ReadingRecordStorage(database, reading); var plans = new ReadingPlanStorage(database, reading);
            var a = new Book { Title = "A", TotalPages = 100 }; var b = new Book { Title = "B", TotalPages = 100 };
            await books.SaveAsync(a); await books.SaveAsync(b);
            var pa = new ReadingPlan { BookId = a.Id, TargetDate = DateTime.Today };
            var pb = new ReadingPlan { BookId = b.Id, TargetDate = DateTime.Today };
            await plans.SaveAsync(pa); await plans.SaveAsync(pb);
            var completion = new ReadingRecord { BookId = a.Id, StartPage = 1, EndPage = 100, Minutes = 1 };
            await records.SaveAsync(completion); pb.BookId = a.Id; await plans.SaveAsync(pb);
            await records.DeleteAsync(completion.Id);
            string? exported = null; var files = new Mock<IFileService>();
            files.Setup(x => x.WriteTextAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((path, json) => exported = json).Returns(Task.CompletedTask);
            var backup = new BackupService(database, reading, files.Object, new DataPaths(dir));
            await backup.ExportAsync("audit.json");
            var snapshot = System.Text.Json.JsonSerializer.Deserialize<BackupDocument>(exported!)!;
            backup.Validate(snapshot); // 当前实现在此抛出“同一本书有多个未完成计划”。
        }
        finally { database.Dispose(); if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

}
