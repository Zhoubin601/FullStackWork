using System.Text.Json;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
using ReadJournal.Library.ViewModels;
using SQLite;

PumpContext.Run(async () => {
var folder = Path.Combine("/workspace/scratch/recheck-business", "db-" + Guid.NewGuid().ToString("N"));
var paths = new DataPaths(folder);
using var realDatabase = new DatabaseService(paths);
var database = new PausingDatabase(realDatabase);
var reading = new ReadingService();
var books = new BookStorage(database, reading);
var quotes = new QuoteStorage(database, reading);
var book = new Book {Title = "Old book", TotalPages = 100}; await books.SaveAsync(book);
var old = new Quote {BookId=book.Id, Text="OLD quote text", Thoughts="OLD thoughts", IsFavorite=false, Page=1}; await quotes.SaveAsync(old);
var files = new Files();
var doc = new BackupDocument {
 Books=[new Book{Id=book.Id,Title="RESTORED book",TotalPages=100, CreatedAt=DateTime.Now,UpdatedAt=DateTime.Now}],
 Quotes=[new Quote{Id=old.Id,BookId=book.Id,Text="RESTORED quote text",Thoughts="RESTORED thoughts",Page=2,IsFavorite=false,CreatedAt=DateTime.Now,UpdatedAt=DateTime.Now}]
};
files.Values["input.json"] = JsonSerializer.Serialize(doc);
var backup = new BackupService(database,reading,files,paths);
var alerts = new Alerts(); var menu = new Menu(); var main = new MainViewModel(menu);
var list = new QuoteListViewModel(quotes, new Content(), alerts);
var settings = new SettingsViewModel(new Preferences(),backup,files,alerts);
menu.Navigate = (view, parameter) => { settings.SetParameter(parameter!); main.SetMenuAndContent(view,settings); };
main.SetMenuAndContent(MenuNavigationConstant.QuoteListView,list);
await list.OnLoadedCommand.ExecuteAsync(null);
var item = list.QuotesList.Single();
database.PauseNextRead = true;
var blocking = database.ReadAsync(c=>0);
await database.ReadCaptured.Task.WaitAsync(TimeSpan.FromSeconds(5));
var toggling = list.ToggleFavoriteCommand.ExecuteAsync(item);
var canLeaveBusyList = await list.CanLeaveAsync();
await main.OnMenuTappedCommand.ExecuteAsync(MenuItem.MenuItems.Single(x=>x.View==MenuNavigationConstant.SettingsView));
var navigatedWhileOldOperationBusy = ReferenceEquals(settings,main.Content) && list.IsBusy;
database.ObserveNextWrite = true;
var restoring = settings.RestoreCommand.ExecuteAsync(null);
await database.WriteQueued.Task.WaitAsync(TimeSpan.FromSeconds(5));
database.ReleaseRead.Set();
await Task.WhenAll(blocking,toggling,restoring).WaitAsync(TimeSpan.FromSeconds(10));
var actual = await quotes.GetAsync(old.Id);
var restoredBook = await books.GetAsync(book.Id);
var result = new { NavigatedWhileOldOperationBusy=navigatedWhileOldOperationBusy, CanLeaveWhileBusy=canLeaveBusyList, RestoreStatus=settings.Status, RestoreError=settings.Error, ToggleError=list.Error, ExpectedQuoteText="RESTORED quote text", ActualQuoteText=actual!.Text, ActualThoughts=actual.Thoughts, ActualPage=actual.Page, ActualFavorite=actual.IsFavorite, ActualBookTitle=restoredBook!.Title, AutoBackupCount=files.Values.Keys.Count(x=>x.Contains("auto-backup-")), DataFolder=folder, DefectConfirmed=actual.Text!="RESTORED quote text" };
var json=JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true});
Console.WriteLine(json); await File.WriteAllTextAsync("/workspace/scratch/recheck-business/result.json",json);

});

class PumpContext:SynchronizationContext {
 private readonly System.Collections.Concurrent.BlockingCollection<(SendOrPostCallback,object?)> queue=new();
 public override void Post(SendOrPostCallback d,object? state)=>queue.Add((d,state));
 public static void Run(Func<Task> action) {var previous=Current;var context=new PumpContext();SetSynchronizationContext(context);try {var task=action();while(!task.IsCompleted){if(context.queue.TryTake(out var work,100))work.Item1(work.Item2);}task.GetAwaiter().GetResult();}finally{SetSynchronizationContext(previous);}}
}
class PausingDatabase(IDatabaseService inner):IDatabaseService {
 public bool IsInitialized=>inner.IsInitialized; public Task InitializeAsync()=>inner.InitializeAsync();
 public bool PauseNextRead,ObserveNextWrite;
 public TaskCompletionSource ReadCaptured=new(TaskCreationOptions.RunContinuationsAsynchronously),WriteQueued=new(TaskCreationOptions.RunContinuationsAsynchronously);
 public ManualResetEventSlim ReleaseRead=new(false);
 public Task<T> ReadAsync<T>(Func<SQLiteConnection,T> read) { if(!PauseNextRead)return inner.ReadAsync(read); PauseNextRead=false; return inner.ReadAsync(c=>{ var captured=read(c); ReadCaptured.SetResult(); if(!ReleaseRead.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException(); return captured; }); }
 public Task<T> WriteAsync<T>(Func<SQLiteConnection,T> write) { var task=inner.WriteAsync(write); if(ObserveNextWrite){ObserveNextWrite=false;WriteQueued.SetResult();} return task; }
}
class Files:IFileService {
 public Dictionary<string,string> Values=new();
 public Task<string?> ChooseSavePathAsync()=>Task.FromResult<string?>("output.json");
 public Task<string?> ChooseOpenPathAsync()=>Task.FromResult<string?>("input.json");
 public Task<string> ReadTextAsync(string path)=>Task.FromResult(Values[path]);
 public Task WriteTextAsync(string path,string content){Values[path]=content;return Task.CompletedTask;}
}
class Alerts:IAlertService {
 public Task<bool> ConfirmAsync(string title,string message)=>Task.FromResult(true);
 public Task<LeaveDecision> ConfirmLeaveAsync()=>Task.FromResult(LeaveDecision.Cancel);
 public Task<bool> EditRecordAsync(ReadingRecordEditViewModel e)=>Task.FromResult(false);
}
class Menu:IMenuNavigationService {public Action<string,object?>? Navigate; public void NavigateTo(string view,object? parameter=null)=>Navigate!(view,parameter);}
class Content:IContentNavigationService {public void NavigateTo(string view,object? parameter=null){}}
class Preferences:IPreferenceStorage {public event EventHandler<string>? ThemeChanged;public Task<string> GetThemeAsync()=>Task.FromResult("浅色");public Task SetThemeAsync(string theme){ThemeChanged?.Invoke(this,theme);return Task.CompletedTask;}}
