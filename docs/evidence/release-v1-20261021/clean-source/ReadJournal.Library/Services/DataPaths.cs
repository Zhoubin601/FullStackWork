namespace ReadJournal.Library.Services;
public class DataPaths
{
    public string DirectoryPath { get; }
    public string DatabasePath => Path.Combine(DirectoryPath, "readjournal.sqlite3");
    public DataPaths() : this(Environment.GetEnvironmentVariable("READJOURNAL_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReadJournal")) { }
    public DataPaths(string directory) { DirectoryPath = Path.GetFullPath(directory); }
}
