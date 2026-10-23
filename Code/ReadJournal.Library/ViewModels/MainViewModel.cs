using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using ReadJournal.Library.Services;
namespace ReadJournal.Library.ViewModels;
public class MainViewModel : ViewModelBase
{
    private readonly IMenuNavigationService _menu;
    private bool _navigating;
    private bool _navigationCommit;
    public bool IsNavigationCommit => _navigationCommit;
    private ViewModelBase? _content; private MenuItem? _selected; private string _title = "阅记";
    public ViewModelBase? Content { get => _content; private set => SetProperty(ref _content, value); }
    public MenuItem? SelectedMenuItem { get => _selected; private set => SetProperty(ref _selected, value); }
    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public ObservableCollection<ViewModelBase> ContentStack { get; } = [];
    public bool CanGoBack => ContentStack.Count > 1;
    public IAsyncRelayCommand GoBackCommand { get; }
    public IAsyncRelayCommand<MenuItem> OnMenuTappedCommand { get; }
    public MainViewModel(IMenuNavigationService menu)
    {
        _menu = menu;
        GoBackCommand = new AsyncRelayCommand(() => RequestBackAsync(), () => CanGoBack);
        OnMenuTappedCommand = new AsyncRelayCommand<MenuItem>(item => item is null ? Task.CompletedTask : RequestNavigationAsync(() => _menu.NavigateTo(item.View)));
        ContentStack.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CanGoBack));
            GoBackCommand.NotifyCanExecuteChanged();
        };
    }
    public async Task<bool> CanCloseAsync()
    {
        return await RequestNavigationAsync(() => { });
    }
    public async Task<bool> RequestNavigationAsync(Action navigationCommit, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); if (_navigating) return false; _navigating = true;
        try
        {
            if (Content is not null && !await Content.CanLeaveAsync()) return false;
            _navigationCommit = true; try { navigationCommit(); } finally { _navigationCommit = false; }
            return true;
        }
        finally { _navigating = false; }
    }
    public async Task<bool> RequestInPlaceAsync(Func<Task> action, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); if (_navigating) return false; _navigating = true;
        try { await action(); return true; } finally { _navigating = false; }
    }
    public async Task<bool> RequestBackAsync(ViewModelBase? expectedPage = null)
    {
        if (!CanGoBack || expectedPage is not null && !ReferenceEquals(Content, expectedPage)) return false;
        var accepted = await RequestNavigationAsync(GoBack);
        if (accepted && Content is not null && !Content.OnLoadedCommand.IsRunning) await Content.OnLoadedCommand.ExecuteAsync(null);
        return accepted;
    }
    public void PushContent(ViewModelBase content)
    {
        if (ContentStack.Contains(content))
        { while (!ReferenceEquals(ContentStack[0], content)) ContentStack.RemoveAt(0); Content = content; return; }
        ContentStack.Insert(0, Content = content);
    }
    public void SetMenuAndContent(string view, ViewModelBase content)
    {
        ContentStack.Clear(); PushContent(content);
        SelectedMenuItem = MenuItem.MenuItems.First(x => x.View == view); Title = SelectedMenuItem.Name;
    }
    public void GoBack() { if (!_navigationCommit) throw new InvalidOperationException("返回必须先通过异步离开门禁。"); if (ContentStack.Count <= 1) return; ContentStack.RemoveAt(0); Content = ContentStack[0]; }
}
