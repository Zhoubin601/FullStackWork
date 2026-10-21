using Moq;
using ReadJournal.Library.Services;
using ReadJournal.Library.Models;
using ReadJournal.Library.ViewModels;
using Xunit;
namespace ReadJournal.UnitTest;
public class EditingTests
{
    [Fact]
    public async Task CancelDiscardAndFailedSavePreserveDraftAndNavigation()
    {
        var books = new Mock<IBookStorage>(); var alerts = new Mock<IAlertService>(); var menu = new Mock<IMenuNavigationService>(); var main = new MainViewModel(menu.Object);
        var vm = new BookEditViewModel(books.Object, alerts.Object, main); vm.SetParameter(null!); await vm.OnLoadedCommand.ExecuteAsync(null);
        main.PushContent(vm); vm.Title = "未保存"; vm.TotalPages = "100"; Assert.True(vm.IsDirty);
        alerts.Setup(x => x.ConfirmLeaveAsync()).ReturnsAsync(LeaveDecision.Cancel); Assert.False(await vm.CanLeaveAsync()); Assert.Same(vm, main.Content);
        alerts.Setup(x => x.ConfirmLeaveAsync()).ReturnsAsync(LeaveDecision.Save); books.Setup(x => x.SaveAsync(It.IsAny<Book>())).ThrowsAsync(new IOException("模拟保存失败"));
        Assert.False(await vm.CanLeaveAsync()); Assert.True(vm.IsDirty); Assert.Equal("未保存", vm.Title); Assert.Contains("模拟保存失败", vm.Error); Assert.Same(vm, main.Content);
        await vm.OnLoadedCommand.ExecuteAsync(null); Assert.Equal("未保存", vm.Title); Assert.True(vm.IsDirty);
        alerts.Setup(x => x.ConfirmLeaveAsync()).ReturnsAsync(LeaveDecision.Discard); Assert.True(await vm.CanLeaveAsync());
    }
    [Fact]
    public async Task SaveThenLeavePersistsAndMarksClean()
    {
        var books = new Mock<IBookStorage>(); var alerts = new Mock<IAlertService>(); var main = new MainViewModel(Mock.Of<IMenuNavigationService>());
        books.Setup(x => x.SaveAsync(It.IsAny<Book>())).ReturnsAsync(1); alerts.Setup(x => x.ConfirmLeaveAsync()).ReturnsAsync(LeaveDecision.Save);
        var vm = new BookEditViewModel(books.Object, alerts.Object, main); vm.SetParameter(null!); await vm.OnLoadedCommand.ExecuteAsync(null); vm.Title = "保存"; vm.TotalPages = "1";
        Assert.True(await vm.CanLeaveAsync()); Assert.False(vm.IsDirty); books.Verify(x => x.SaveAsync(It.Is<Book>(b => b.Title == "保存" && b.TotalPages == 1)), Times.Once);
    }
}
