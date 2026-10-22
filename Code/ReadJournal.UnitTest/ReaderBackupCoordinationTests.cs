using System.Text.Json;
using ReadJournal.Library.Models;
using ReadJournal.Library.Services;
using Xunit;
using ReadJournal.Library.Services.Backup;
namespace ReadJournal.UnitTest;

public sealed class ReaderBackupCoordinationTests : IDisposable
{
    private readonly string _directory=Path.Combine(Path.GetTempPath(),"ReadJournalP2",Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task RestoreBlocksOrdinaryWritesThenRejectsTheirCapturedOldEpoch()
    {
        var paths=new DataPaths(_directory);using var db=new DatabaseService(paths);await db.InitializeAsync();
        var files=new BlockedFiles();var backup=BackupRuntime.CreateService(db,new ReadingService(),paths,files);
        var old=db.Coordinator.CurrentEpoch;var input=Path.Combine(_directory,"synthetic.json");await File.WriteAllTextAsync(input,JsonSerializer.Serialize(DemoData.Create()));var restore=backup.RestoreAsync(input,default);
        await files.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var write=db.WriteAsync(c=>c.Insert(new Book {Title="过期写入",TotalPages=10})); Assert.False(write.IsCompleted);
        files.Release.SetResult();await restore;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>write);
        Assert.NotEqual(old,db.Coordinator.CurrentEpoch);
        Assert.Equal(3,await db.ReadAsync(c=>c.Table<Book>().Count()));
        Assert.DoesNotContain(await db.ReadAsync(c=>c.Table<Book>().ToList()),b=>b.Title=="过期写入");
    }
    [Fact]
    public async Task FailedAutomaticBackupLeavesDatasetAndEpochIntactAndUnlocksMaintenance()
    {
        var paths=new DataPaths(_directory);using var db=new DatabaseService(paths);
        await new BookStorage(db,new ReadingService()).SaveAsync(new(){Title="原数据",TotalPages=20});
        var old=db.Coordinator.CurrentEpoch;var files=new BlockedFiles{Fail=true};
        var input=Path.Combine(_directory,"synthetic.json");await File.WriteAllTextAsync(input,JsonSerializer.Serialize(DemoData.Create()));
        var restore=BackupRuntime.CreateService(db,new ReadingService(),paths,files).RestoreAsync(input,default);
        await files.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));files.Release.SetResult();
        await Assert.ThrowsAsync<IOException>(()=>restore);
        Assert.Equal(old,db.Coordinator.CurrentEpoch);Assert.Equal("原数据",(await db.ReadAsync(c=>c.Table<Book>().Single())).Title);
        await db.WriteAsync(c=>c.Insert(new Book {Title="恢复失败后仍可保存",TotalPages=1}));
        Assert.Equal(2,await db.ReadAsync(c=>c.Table<Book>().Count()));
    }
    private sealed class BlockedFiles : ManagedBookFiles
    {
        internal TaskCompletionSource Entered {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Fail {get;init;}
        public override void Checkpoint(string point)
        { if(point!="backup.before-install")return;Entered.TrySetResult();Release.Task.GetAwaiter().GetResult();if(Fail)throw new IOException("injected automatic backup failure"); }

    }
    public void Dispose(){if(Directory.Exists(_directory))Directory.Delete(_directory,true);}
}
