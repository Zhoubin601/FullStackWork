namespace ReadJournal.Library.Services;
public interface IContentNavigationService { void NavigateTo(string view, object? parameter = null); }
public static class ContentNavigationConstant
{
    public const string BookDetailView = nameof(BookDetailView);
    public const string BookEditView = nameof(BookEditView);
    public const string QuoteEditView = nameof(QuoteEditView);
    public const string ReadingPlanEditView = nameof(ReadingPlanEditView);
}
