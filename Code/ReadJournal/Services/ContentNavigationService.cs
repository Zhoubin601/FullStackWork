using ReadJournal.Library.Services;
using ReadJournal.Library.ViewModels;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
namespace ReadJournal.Services;
public class ContentNavigationService : IContentNavigationService
{
    private static ViewModelBase Resolve(string view, object? parameter)
    {
        var valid = view switch
        {
            ContentNavigationConstant.BookDetailView => parameter is int id && id > 0,
            ContentNavigationConstant.BookEditView => parameter is null || parameter is int id && id > 0,
            ContentNavigationConstant.QuoteEditView => parameter is null or Quote,
            ContentNavigationConstant.ReadingPlanEditView => parameter is null or ReadingPlan,
            ContentNavigationConstant.ReaderView => parameter is ReaderOpenRequest { BookId: > 0 },
            _ => false
        };
        if (!valid) throw new ArgumentException("内容导航目标或参数无效。");
        if (parameter is ReaderOpenRequest { Target: not null } request) ReadJournal.Library.Services.Reader.ReaderContractValidator.Validate(request.Target);
        ViewModelBase target = view switch
        {
            ContentNavigationConstant.BookDetailView => ServiceLocator.Current.BookDetailViewModel,
            ContentNavigationConstant.BookEditView => ServiceLocator.Current.BookEditViewModel,
            ContentNavigationConstant.QuoteEditView => ServiceLocator.Current.QuoteEditViewModel,
            ContentNavigationConstant.ReadingPlanEditView => ServiceLocator.Current.ReadingPlanEditViewModel,
            ContentNavigationConstant.ReaderView => ServiceLocator.Current.ReaderViewModel,
            _ => throw new ArgumentException("未知或尚未实现的内容目标。", nameof(view))
        };
        return target;
    }
    public void NavigateTo(string view, object? parameter = null)
    {
        var target = Resolve(view, parameter);
        var main = ServiceLocator.Current.MainViewModel;
        if (main.Content is not null && !main.IsNavigationCommit) throw new InvalidOperationException("运行期内容导航必须等待NavigateToAsync。");
        target.SetParameter(parameter!);
        main.PushContent(target);
    }
    public async Task<bool> NavigateToAsync(string view, object? parameter = null, CancellationToken ct = default)
    {
        var target = Resolve(view, parameter); var main = ServiceLocator.Current.MainViewModel;
        if (target is ReaderViewModel reader && ReferenceEquals(main.Content, reader) && reader.IsSameBook(parameter!))
            return await main.RequestInPlaceAsync(() => reader.NavigateWithinBookAsync((ReaderOpenRequest)parameter!, ct), ct);
        if (ReferenceEquals(main.Content, target) && target is EditViewModelBase) return true;
        var accepted = await main.RequestNavigationAsync(() => NavigateTo(view, parameter), ct);
        if (accepted && ReferenceEquals(main.Content, target) && !target.OnLoadedCommand.IsRunning) await target.OnLoadedCommand.ExecuteAsync(null);
        return accepted;
    }
}
