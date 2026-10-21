using ReadJournal.Library.ViewModels;
namespace ReadJournal.Library.Services;
public enum LeaveDecision { Save, Discard, Cancel }
public interface IAlertService
{
    Task<bool> ConfirmAsync(string title, string message);
    Task<LeaveDecision> ConfirmLeaveAsync();
    Task<bool> EditRecordAsync(ReadingRecordEditViewModel editor);
}
