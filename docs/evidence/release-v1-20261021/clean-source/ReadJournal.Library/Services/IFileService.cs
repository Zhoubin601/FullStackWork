namespace ReadJournal.Library.Services;
public interface IFileService
{
    Task<string?> ChooseSavePathAsync();
    Task<string?> ChooseOpenPathAsync();
    Task<string> ReadTextAsync(string path);
    Task WriteTextAsync(string path, string contents);
}
