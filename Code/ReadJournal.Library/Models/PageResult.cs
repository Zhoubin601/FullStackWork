namespace ReadJournal.Library.Models;
public class PageResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
}
