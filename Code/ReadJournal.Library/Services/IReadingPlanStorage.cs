using ReadJournal.Library.Models;
namespace ReadJournal.Library.Services;
public interface IReadingPlanStorage
{
    Task<ReadingPlan?> GetAsync(int id);
    Task<IReadOnlyList<PlanItem>> GetAllAsync();
    Task<int> SaveAsync(ReadingPlan plan);
    Task DeleteAsync(int id);
}
