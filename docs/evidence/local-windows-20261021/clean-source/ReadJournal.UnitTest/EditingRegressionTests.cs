using Moq;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
using ReadJournal.Library.ViewModels;
using Xunit;
namespace ReadJournal.UnitTest;

public class EditingRegressionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BookLoadMergesUntouchedFieldsWithoutOverwritingTypedDraft(bool typeBeforeLoaded)
    {
        var pending = new TaskCompletionSource<Book?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var books = new Mock<IBookStorage>(); books.Setup(x => x.GetAsync(1)).Returns(pending.Task);
        var vm = new BookEditViewModel(books.Object, Mock.Of<IAlertService>(), NewMain()); vm.SetParameter(1);
        if (typeBeforeLoaded) vm.Title = "加载期间的新书名";
        var loading = vm.OnLoadedCommand.ExecuteAsync(null);
        if (!typeBeforeLoaded) vm.Title = "加载期间的新书名";
        pending.SetResult(new Book { Id = 1, Title = "原书名", Author = "原作者", TotalPages = 100 }); await loading;
        Assert.Equal("加载期间的新书名", vm.Title); Assert.Equal("原作者", vm.Author); Assert.Equal("100", vm.TotalPages); Assert.True(vm.IsDirty);
        await vm.OnLoadedCommand.ExecuteAsync(null); Assert.Equal("加载期间的新书名", vm.Title);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task QuoteLoadPreservesTypedTextAndSelectionWhileStillLoadingChoices(bool chooseAnotherBook, bool typeBeforeLoaded)
    {
        var pending = new TaskCompletionSource<IReadOnlyList<Book>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var books = new Mock<IBookStorage>(); books.Setup(x => x.GetAllAsync()).Returns(pending.Task);
        var quotes = new Mock<IQuoteStorage>(); quotes.Setup(x => x.SaveAsync(It.IsAny<Quote>())).ReturnsAsync(19);
        var vm = new QuoteEditViewModel(books.Object, quotes.Object, Mock.Of<IAlertService>(), NewMain());
        vm.SetParameter(new Quote { BookId = 1 });
        void TypeDraft() { vm.Text = "加载期间的新摘录"; if (chooseAnotherBook) vm.SelectedBook = new Book { Id = 2, Title = "第二本", TotalPages = 100 }; }
        if (typeBeforeLoaded) TypeDraft();
        var loading = vm.OnLoadedCommand.ExecuteAsync(null);
        if (!typeBeforeLoaded) TypeDraft();
        var choices = new[] { new Book { Id = 1, Title = "第一本", TotalPages = 100 }, new Book { Id = 2, Title = "第二本", TotalPages = 100 } };
        pending.SetResult(choices); await loading;
        Assert.Equal("加载期间的新摘录", vm.Text); Assert.Equal(2, vm.BooksList.Count); Assert.Same(choices[chooseAnotherBook ? 1 : 0], vm.SelectedBook); Assert.True(vm.IsDirty);
        await vm.SaveCommand.ExecuteAsync(null);
        quotes.Verify(x => x.SaveAsync(It.Is<Quote>(q => q.Text == "加载期间的新摘录" && q.BookId == (chooseAnotherBook ? 2 : 1))), Times.Once);
    }

    [Fact]
    public async Task RetryingQuoteChoiceLoadPreservesAnAlreadyDirtyDraft()
    {
        var books = new Mock<IBookStorage>(); var choices = new[] { new Book { Id = 1, Title = "测试", TotalPages = 100 } };
        books.SetupSequence(x => x.GetAllAsync()).ThrowsAsync(new IOException("暂时无法读取")).ReturnsAsync(choices);
        var vm = new QuoteEditViewModel(books.Object, Mock.Of<IQuoteStorage>(), Mock.Of<IAlertService>(), NewMain()); vm.SetParameter(null!);
        await vm.OnLoadedCommand.ExecuteAsync(null); Assert.Contains("暂时无法读取", vm.Error);
        vm.Text = "失败后输入的草稿"; vm.SelectedBook = new Book { Id = 1 };
        await vm.OnLoadedCommand.ExecuteAsync(null);
        Assert.Equal("失败后输入的草稿", vm.Text); Assert.Same(choices[0], vm.SelectedBook); Assert.Single(vm.BooksList); Assert.True(vm.IsDirty); Assert.Empty(vm.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlanLoadPreservesChangedDateAndLoadsBookProgressForSuggestions(bool typeBeforeLoaded)
    {
        var pending = new TaskCompletionSource<IReadOnlyList<Book>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var book = new Book { Id = 1, Title = "测试", TotalPages = 100 }; var books = new Mock<IBookStorage>();
        books.Setup(x => x.GetAllAsync()).Returns(pending.Task); books.Setup(x => x.GetProgressAsync(1)).ReturnsAsync(new BookProgress { Book = book, CurrentPage = 20 });
        var vm = new ReadingPlanEditViewModel(books.Object, Mock.Of<IReadingPlanStorage>(), Mock.Of<IAlertService>(), NewMain());
        vm.SetParameter(new ReadingPlan { BookId = 1, TargetDate = DateTime.Today.AddDays(2) });
        var changedDate = new DateTimeOffset(DateTime.Today.AddDays(8));
        if (typeBeforeLoaded) vm.TargetDate = changedDate;
        var loading = vm.OnLoadedCommand.ExecuteAsync(null);
        if (!typeBeforeLoaded) vm.TargetDate = changedDate;
        pending.SetResult(new[] { book }); await loading;
        Assert.Equal(changedDate, vm.TargetDate); Assert.Same(book, vm.SelectedBook); Assert.True(vm.IsDirty); Assert.Contains("建议每日 9 页", vm.Suggestion);
    }

    [Theory]
    [InlineData("book")]
    [InlineData("quote")]
    [InlineData("plan")]
    [InlineData("record")]
    public async Task SaveKeepsNewerDraftOnPageAndNextSaveUpdatesTheCreatedEntity(string kind)
    {
        var scenario = await NewScenarioAsync(kind); var vm = scenario.Editor;
        scenario.Change(1); var submittedValue = scenario.CurrentValue();
        var saving = vm.SaveCommand.ExecuteAsync(null); Assert.True(vm.IsBusy);
        scenario.Change(2); scenario.Pending.SetResult(23); await saving;
        Assert.True(vm.IsDirty); Assert.Same(vm, scenario.Main.Content); Assert.Contains("新修改尚未保存", vm.Error);
        Assert.Equal((0, submittedValue), scenario.Writes[0]); Assert.Equal(0, scenario.SavedEvents());
        var newerValue = scenario.CurrentValue(); await vm.SaveCommand.ExecuteAsync(null);
        Assert.False(vm.IsDirty); Assert.Equal((23, newerValue), scenario.Writes[1]);
        if (kind == "record") Assert.Equal(1, scenario.SavedEvents()); else Assert.NotSame(vm, scenario.Main.Content);
    }

    [Theory]
    [InlineData("book")]
    [InlineData("quote")]
    [InlineData("plan")]
    [InlineData("record")]
    public async Task SaveAndLeaveRefusesToDiscardInputChangedDuringStorageAwait(string kind)
    {
        var scenario = await NewScenarioAsync(kind); var vm = scenario.Editor;
        scenario.Change(1); var leaving = vm.CanLeaveAsync(); Assert.True(vm.IsBusy);
        scenario.Change(2); scenario.Pending.SetResult(23);
        Assert.False(await leaving); Assert.True(vm.IsDirty); Assert.Same(vm, scenario.Main.Content); Assert.Equal(0, scenario.SavedEvents());
        Assert.True(await vm.CanLeaveAsync()); Assert.False(vm.IsDirty); Assert.Equal(23, scenario.Writes[1].Id);
    }

    [Theory]
    [InlineData("book")]
    [InlineData("quote")]
    [InlineData("plan")]
    public async Task FailedInitialReadBlocksSaveAndRetryHydratesUntouchedFields(string kind)
    {
        var books = new Mock<IBookStorage>(); var firstBook = new Book { Id = 1, Title = "原书名", Author = "原作者", TotalPages = 100 };
        var secondBook = new Book { Id = 2, Title = "第二本", TotalPages = 100 };
        books.Setup(x => x.GetAllAsync()).ReturnsAsync(new[] { firstBook, secondBook });
        books.Setup(x => x.GetProgressAsync(It.IsAny<int>())).ReturnsAsync(new BookProgress { Book = firstBook });
        var saves = 0; EditViewModelBase editor; Action typeDraft; Action verifyHydrated;
        switch (kind)
        {
            case "book":
                books.SetupSequence(x => x.GetAsync(1)).ThrowsAsync(new IOException("读取失败")).ReturnsAsync(firstBook);
                books.Setup(x => x.SaveAsync(It.IsAny<Book>())).Callback(() => saves++).ReturnsAsync(1);
                var bookEditor = new BookEditViewModel(books.Object, Mock.Of<IAlertService>(), NewMain()); bookEditor.SetParameter(1);
                editor = bookEditor; typeDraft = () => bookEditor.Title = "新的书名";
                verifyHydrated = () => { Assert.Equal("新的书名", bookEditor.Title); Assert.Equal("原作者", bookEditor.Author); Assert.Equal("100", bookEditor.TotalPages); }; break;
            case "quote":
                var quote = new Quote { Id = 10, BookId = 1, Text = "原摘录", Thoughts = "原感想", Page = 8, IsFavorite = true };
                var quotes = new Mock<IQuoteStorage>(); quotes.SetupSequence(x => x.GetAsync(10)).ThrowsAsync(new IOException("读取失败")).ReturnsAsync(quote);
                quotes.Setup(x => x.SaveAsync(It.IsAny<Quote>())).Callback(() => saves++).ReturnsAsync(10);
                var quoteEditor = new QuoteEditViewModel(books.Object, quotes.Object, Mock.Of<IAlertService>(), NewMain()); quoteEditor.SetParameter(quote);
                editor = quoteEditor; typeDraft = () => quoteEditor.Text = "新的摘录";
                verifyHydrated = () => { Assert.Equal("新的摘录", quoteEditor.Text); Assert.Equal("原感想", quoteEditor.Thoughts); Assert.Equal("8", quoteEditor.Page); Assert.True(quoteEditor.IsFavorite); Assert.Same(firstBook, quoteEditor.SelectedBook); }; break;
            case "plan":
                var plan = new ReadingPlan { Id = 10, BookId = 1, TargetDate = DateTime.Today.AddDays(7) };
                var plans = new Mock<IReadingPlanStorage>(); plans.SetupSequence(x => x.GetAsync(10)).ThrowsAsync(new IOException("读取失败")).ReturnsAsync(plan);
                plans.Setup(x => x.SaveAsync(It.IsAny<ReadingPlan>())).Callback(() => saves++).ReturnsAsync(10);
                var planEditor = new ReadingPlanEditViewModel(books.Object, plans.Object, Mock.Of<IAlertService>(), NewMain()); planEditor.SetParameter(plan);
                editor = planEditor; typeDraft = () => planEditor.SelectedBook = secondBook;
                verifyHydrated = () => { Assert.Same(secondBook, planEditor.SelectedBook); Assert.Equal(new DateTimeOffset(plan.TargetDate), planEditor.TargetDate); }; break;
            default: throw new ArgumentException(kind);
        }
        await editor.OnLoadedCommand.ExecuteAsync(null); Assert.Contains("读取失败", editor.Error);
        typeDraft(); await editor.SaveCommand.ExecuteAsync(null);
        Assert.Equal(0, saves); Assert.Contains("重新加载", editor.Error); Assert.True(editor.IsDirty);
        await editor.OnLoadedCommand.ExecuteAsync(null); verifyHydrated(); Assert.True(editor.IsDirty); Assert.Empty(editor.Error);
        await editor.SaveCommand.ExecuteAsync(null); Assert.Equal(1, saves); Assert.False(editor.IsDirty);
    }

    private static MainViewModel NewMain() => new(Mock.Of<IMenuNavigationService>());
    private sealed class EditScenario
    {
        public required EditViewModelBase Editor { get; init; }
        public required MainViewModel Main { get; init; }
        public required Action<int> Change { get; init; }
        public required Func<string> CurrentValue { get; init; }
        public required List<(int Id, string Value)> Writes { get; init; }
        public required TaskCompletionSource<int> Pending { get; init; }
        public required Func<int> SavedEvents { get; init; }
    }
    private static async Task<EditScenario> NewScenarioAsync(string kind)
    {
        var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writes = new List<(int Id, string Value)>(); var savedEvents = 0;
        Task<int> Persist(int id, string value) { writes.Add((id, value)); return writes.Count == 1 ? pending.Task : Task.FromResult(23); }
        var main = NewMain(); var alerts = new Mock<IAlertService>(); alerts.Setup(x => x.ConfirmLeaveAsync()).ReturnsAsync(LeaveDecision.Save);
        var book = new Book { Id = 1, Title = "测试", TotalPages = 100 }; var books = new Mock<IBookStorage>();
        books.Setup(x => x.GetAllAsync()).ReturnsAsync(new[] { book }); books.Setup(x => x.GetProgressAsync(1)).ReturnsAsync(new BookProgress { Book = book });
        EditViewModelBase editor; Action<int> change; Func<string> currentValue;
        switch (kind)
        {
            case "book":
                books.Setup(x => x.SaveAsync(It.IsAny<Book>())).Returns<Book>(b => Persist(b.Id, b.Title));
                var bookEditor = new BookEditViewModel(books.Object, alerts.Object, main); bookEditor.SetParameter(null!);
                editor = bookEditor; change = i => { bookEditor.Title = "草稿" + i; bookEditor.TotalPages = "100"; }; currentValue = () => bookEditor.Title; break;
            case "quote":
                var quotes = new Mock<IQuoteStorage>(); quotes.Setup(x => x.SaveAsync(It.IsAny<Quote>())).Returns<Quote>(q => Persist(q.Id, q.Text));
                var quoteEditor = new QuoteEditViewModel(books.Object, quotes.Object, alerts.Object, main); quoteEditor.SetParameter(new Quote { BookId = 1 });
                editor = quoteEditor; change = i => quoteEditor.Text = "摘录" + i; currentValue = () => quoteEditor.Text; break;
            case "plan":
                var plans = new Mock<IReadingPlanStorage>(); plans.Setup(x => x.SaveAsync(It.IsAny<ReadingPlan>())).Returns<ReadingPlan>(p => Persist(p.Id, p.TargetDate.ToString("O")));
                var planEditor = new ReadingPlanEditViewModel(books.Object, plans.Object, alerts.Object, main); planEditor.SetParameter(new ReadingPlan { BookId = 1 });
                editor = planEditor; change = i => planEditor.TargetDate = DateTime.Today.AddDays(i); currentValue = () => planEditor.TargetDate!.Value.Date.ToString("O"); break;
            case "record":
                var records = new Mock<IReadingRecordStorage>(); records.Setup(x => x.SaveAsync(It.IsAny<ReadingRecord>())).Returns<ReadingRecord>(r => Persist(r.Id, r.Minutes.ToString()));
                var recordEditor = new ReadingRecordEditViewModel(records.Object, alerts.Object, main); recordEditor.SetParameter(new ReadingRecord { BookId = 1 }); recordEditor.Saved += (_, _) => savedEvents++;
                editor = recordEditor; change = i => recordEditor.Minutes = (i + 1).ToString(); currentValue = () => recordEditor.Minutes; break;
            default: throw new ArgumentException(kind);
        }
        await editor.OnLoadedCommand.ExecuteAsync(null);
        main.PushContent(new MainWindowViewModel(Mock.Of<IDatabaseService>(), Mock.Of<IRootNavigationService>(), Mock.Of<IMenuNavigationService>())); main.PushContent(editor);
        return new EditScenario { Editor = editor, Main = main, Change = change, CurrentValue = currentValue, Writes = writes, Pending = pending, SavedEvents = () => savedEvents };
    }
}
