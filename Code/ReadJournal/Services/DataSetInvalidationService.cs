using Avalonia.Threading;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Services;

public sealed class DataSetInvalidationService(IReaderDocumentService documents, IReaderLayoutService layouts) : IDataSetInvalidationService
{
    public async Task InvalidateAsync(CancellationToken ct)
    {
        await documents.ReleaseAllAsync();
        await layouts.ReleaseAllAsync();
        if (!Dispatcher.UIThread.CheckAccess()) { await Dispatcher.UIThread.InvokeAsync(ResetViews); return; }
        ResetViews();
    }
    private static void ResetViews()
    {
        var locator = ServiceLocator.Current;
        foreach (var property in typeof(ServiceLocator).GetProperties().Where(p => p.PropertyType.IsSubclassOf(typeof(Library.ViewModels.ViewModelBase))))
            if (property.GetValue(locator) is Library.ViewModels.ViewModelBase vm && vm is not (Library.ViewModels.MainViewModel or Library.ViewModels.MainWindowViewModel or Library.ViewModels.InitializationViewModel or Library.ViewModels.SettingsViewModel)) vm.InvalidateDataSet();
        locator.MainViewModel.SetMenuAndContent(Library.Services.MenuNavigationConstant.SettingsView, locator.SettingsViewModel);
    }
}
