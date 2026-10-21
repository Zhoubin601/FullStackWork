using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
using Xunit;
namespace ReadJournal.UnitTest;
public class ReadingBusinessTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ReadJournalTests", Guid.NewGuid().ToString());
    private readonly DatabaseService _database;
    private readonly BookStorage _books; private readonly ReadingRecordStorage _records; private readonly QuoteStorage _quotes;
    private readonly ReadingService _rules = new();
    public ReadingBusinessTests() { _database = new(new DataPaths(_directory)); _books = new(_database, _rules); _records = new(_database, _rules); _quotes = new(_database, _rules); }
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(100, 100, 1)]
    [InlineData(1, 100, 100)]
    [InlineData(10, 20, 11)]
    public async Task ClosedIntervalsAndRepeatedReading(int start, int end, int pages)
    {
        var book = new Book { Title = "测试书", TotalPages = 100 }; await _books.SaveAsync(book);
        for (var i = 0; i < 2; i++) await _records.SaveAsync(new ReadingRecord { BookId = book.Id, StartPage = start, EndPage = end, Minutes = 10 });
        var progress = (await _books.GetProgressAsync(book.Id))!;
        Assert.Equal(pages * 2, progress.TotalReadPages); Assert.Equal(end, progress.CurrentPage); Assert.Equal(20, progress.TotalMinutes);
    }
    [Theory]
    [InlineData(0, 10, 1, 0)]
    [InlineData(20, 10, 1, 0)]
    [InlineData(1, 101, 1, 0)]
    [InlineData(1, 10, 0, 0)]
    [InlineData(1, 10, -1, 0)]
    [InlineData(1, 10, 1, 1)]
    public async Task InvalidRecordRejectedWithoutPersistence(int start, int end, int minutes, int futureDays)
    {
        var book = new Book { Title = "边界", TotalPages = 100 }; await _books.SaveAsync(book);
        await Assert.ThrowsAsync<ArgumentException>(() => _records.SaveAsync(new ReadingRecord { BookId = book.Id, StartPage = start, EndPage = end, Minutes = minutes, ReadingDate = DateTime.Today.AddDays(futureDays) }));
        Assert.Empty(await _records.GetForBookAsync(book.Id));
    }
    [Fact]
    public async Task EditDeleteAndTotalPagesRecomputeCompletionAndProgress()
    {
        var book = new Book { Title = "重算", TotalPages = 100 }; await _books.SaveAsync(book);
        var early = new ReadingRecord { BookId = book.Id, StartPage = 10, EndPage = 20, Minutes = 11, ReadingDate = DateTime.Today.AddDays(-3) }; await _records.SaveAsync(early);
        var completion = new ReadingRecord { BookId = book.Id, StartPage = 80, EndPage = 100, Minutes = 20, ReadingDate = DateTime.Today.AddDays(-2) }; await _records.SaveAsync(completion);
        Assert.Equal(DateTime.Today.AddDays(-2), (await _books.GetProgressAsync(book.Id))!.CompletedOn);
        var duplicate = new ReadingRecord { BookId = book.Id, StartPage = 100, EndPage = 100, Minutes = 1 }; await _records.SaveAsync(duplicate);
        await _records.DeleteAsync(completion.Id); Assert.Equal(DateTime.Today, (await _books.GetProgressAsync(book.Id))!.CompletedOn);
        await _records.DeleteAsync(duplicate.Id); Assert.Equal("在读", (await _books.GetProgressAsync(book.Id))!.State);
        early.EndPage = 30; await _records.SaveAsync(early); Assert.Equal(30, (await _books.GetProgressAsync(book.Id))!.CurrentPage);
        book.TotalPages = 20; await Assert.ThrowsAsync<ArgumentException>(() => _books.SaveAsync(book));
        book.TotalPages = 30; await _books.SaveAsync(book); Assert.Equal("已完成", (await _books.GetProgressAsync(book.Id))!.State);
        await _records.DeleteAsync(early.Id); Assert.Equal("未开始", (await _books.GetProgressAsync(book.Id))!.State);
    }
    [Fact]
    public async Task SearchPaginationQuotesFavoritesAndCascadeDelete()
    {
        var book = new Book { Title = "SQLite阅读", Author = "测试作者", TotalPages = 100 }; await _books.SaveAsync(book);
        for (var i = 0; i < 13; i++) await _books.SaveAsync(new Book { Title = "其他" + i, TotalPages = 10 });
        Assert.Equal(10, (await _books.SearchAsync("", "全部", 1, 10)).Items.Count);
        Assert.Equal(4, (await _books.SearchAsync("", "全部", 2, 10)).Items.Count);
        Assert.Single((await _books.SearchAsync("sqlite", "全部", 1, 10)).Items);
        Assert.Single((await _books.SearchAsync("作者", "全部", 1, 10)).Items);
        await _records.SaveAsync(new ReadingRecord { BookId = book.Id, StartPage = 1, EndPage = 10, Minutes = 1 });
        var quote = new Quote { BookId = book.Id, Text = "书摘正文", Page = 70, IsFavorite = true }; await _quotes.SaveAsync(quote);
        Assert.Single(await _quotes.SearchAsync("阅读", true)); quote.IsFavorite = false; await _quotes.SaveAsync(quote); Assert.Empty(await _quotes.SearchAsync("", true));
        book.TotalPages = 50; await Assert.ThrowsAsync<ArgumentException>(() => _books.SaveAsync(book));
        quote.Page = 0; await Assert.ThrowsAsync<ArgumentException>(() => _quotes.SaveAsync(quote));
        await _books.DeleteAsync(book.Id); Assert.Empty(await _records.GetForBookAsync(book.Id)); Assert.Empty(await _quotes.SearchAsync("", false));
    }
    public void Dispose() { _database.Dispose(); if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
