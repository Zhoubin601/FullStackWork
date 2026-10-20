using ReadJournal.Library.Services;
using ReadJournal.Library.ViewModels;
namespace ReadJournal.Services;
public class ContentNavigationService : IContentNavigationService
{
    public void NavigateTo(string view, object? parameter = null)
    {
        ViewModelBase target = view switch
        {
            ContentNavigationConstant.BookDetailView => ServiceLocator.Current.BookDetailViewModel,
            ContentNavigationConstant.BookEditView => ServiceLocator.Current.BookEditViewModel,
            ContentNavigationConstant.QuoteEditView => ServiceLocator.Current.QuoteEditViewModel,
            ContentNavigationConstant.ReadingPlanEditView => ServiceLocator.Current.ReadingPlanEditViewModel,
            _ => throw new ArgumentException("未知或尚未实现的内容目标。", nameof(view))
        };
        // 阅记新增保护：必须在 SetParameter 之前拦截重复单例，避免覆盖原参数。
        if (ServiceLocator.Current.MainViewModel.ContentStack.Contains(target)) return;
        target.SetParameter(parameter!);
        ServiceLocator.Current.MainViewModel.PushContent(target);
    }
}
