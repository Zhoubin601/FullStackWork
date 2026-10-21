using ReadJournal.Library.Models;
namespace ReadJournal.Library.Services;
public class ReadingPlanStorage : IReadingPlanStorage
{
    private readonly IDatabaseService _database; private readonly IReadingService _reading;
    public ReadingPlanStorage(IDatabaseService database, IReadingService reading) { _database = database; _reading = reading; }
    public Task<ReadingPlan?> GetAsync(int id) => _database.ReadAsync(c => (ReadingPlan?)c.Find<ReadingPlan>(id));
    public Task<IReadOnlyList<PlanItem>> GetAllAsync() => _database.ReadAsync(c =>
    {
        var books = c.Table<Book>().ToDictionary(x => x.Id); var records = c.Table<ReadingRecord>().ToList();
        return (IReadOnlyList<PlanItem>)c.Table<ReadingPlan>().OrderBy(x => x.TargetDate).ToList().Select(p => new PlanItem { Plan = p, Progress = _reading.Calculate(books[p.BookId], records) }).ToArray();
    });
    public Task<int> SaveAsync(ReadingPlan plan) => _database.WriteAsync(c =>
    {
        var book = c.Find<Book>(plan.BookId) ?? throw new ArgumentException("请选择有效的书籍。");
        var old = plan.Id == 0 ? null : c.Find<ReadingPlan>(plan.Id);
        if (plan.Id != 0 && old is null) throw new InvalidOperationException("计划已被删除。");
        plan.StartDate = old?.StartDate ?? DateTime.Today; plan.TargetDate = plan.TargetDate.Date;
        if (plan.TargetDate < DateTime.Today || plan.TargetDate < plan.StartDate) throw new ArgumentException("目标日期不得早于今天或计划开始日期。");
        var progress = _reading.Calculate(book, c.Table<ReadingRecord>().Where(x => x.BookId == book.Id));
        if (old is null && progress.State == "已完成") throw new ArgumentException("已读完的书籍不能新建普通阅读计划。");
        if (c.Table<ReadingPlan>().Any(x => x.BookId == book.Id && x.Id != plan.Id)) throw new ArgumentException("该书籍已有阅读计划，请编辑已有计划或先删除多余计划，不能关联第二个计划。");
        if (old is null) c.Insert(plan); else c.Update(plan); return plan.Id;
    });
    public Task DeleteAsync(int id) => _database.WriteAsync(c => c.Delete<ReadingPlan>(id));
}
