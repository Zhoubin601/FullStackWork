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
        var main = ServiceLocator.Current.MainViewModel;
        // Direct Add/Edit commands must obey the same busy-page boundary as menu/back/close.
        // Check before SetParameter so a rejected navigation cannot erase a cached editor's draft.
        if (main.Content?.IsBusy == true || main.ContentStack.Contains(target)) return;
        target.SetParameter(parameter!);
        main.PushContent(target);
    }
}
