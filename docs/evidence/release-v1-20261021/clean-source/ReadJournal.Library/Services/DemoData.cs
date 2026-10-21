using ReadJournal.Library.Models;
namespace ReadJournal.Library.Services;
public static class DemoData
{
    public static BackupDocument Create()
    {
        var today = DateTime.Today; var created = today.AddDays(-20);
        return new BackupDocument
        {
            Books = [new() { Id = 1, Title = "风穿过书页（虚构演示）", Author = "林序（虚构）", Category = "文学", TotalPages = 240, Description = "虚构演示资料：一段关于城市与阅读的故事。", CreatedAt = created, UpdatedAt = today }, new() { Id = 2, Title = "慢读的练习（虚构演示）", Author = "陈简（虚构）", Category = "随笔", TotalPages = 120, Description = "虚构演示资料，用于展示阅读完成与重读。", CreatedAt = created, UpdatedAt = today }, new() { Id = 3, Title = "星光笔记（虚构演示）", Author = "余墨（虚构）", Category = "科普", TotalPages = 180, Description = "虚构演示资料，用于展示逾期计划。", CreatedAt = created, UpdatedAt = today }],
            ReadingRecords = [new() { Id = 1, BookId = 1, ReadingDate = today.AddDays(-2), StartPage = 1, EndPage = 30, Minutes = 40 }, new() { Id = 2, BookId = 1, ReadingDate = today, StartPage = 31, EndPage = 60, Minutes = 35 }, new() { Id = 3, BookId = 2, ReadingDate = today.AddDays(-1), StartPage = 1, EndPage = 120, Minutes = 120 }, new() { Id = 4, BookId = 2, ReadingDate = today, StartPage = 10, EndPage = 20, Minutes = 15 }],
            Quotes = [new() { Id = 1, BookId = 1, Text = "虚构书摘：每次翻页，都是一次新的相遇。", Thoughts = "演示感想：为阅读留一点空白。", Page = 30, IsFavorite = true, CreatedAt = created, UpdatedAt = today }, new() { Id = 2, BookId = 2, Text = "虚构书摘：慢下来，文字就有了回声。", Thoughts = "演示数据，可自由编辑。", Page = 100, CreatedAt = created, UpdatedAt = today }],
            ReadingPlans = [new() { Id = 1, BookId = 1, StartDate = today.AddDays(-2), TargetDate = today.AddDays(6) }, new() { Id = 2, BookId = 3, StartDate = today.AddDays(-10), TargetDate = today.AddDays(-1) }, new() { Id = 3, BookId = 2, StartDate = today.AddDays(-5), TargetDate = today.AddDays(-1) }]
        };
    }
}
