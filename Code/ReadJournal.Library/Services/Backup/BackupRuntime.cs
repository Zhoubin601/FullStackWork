using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.Services.Backup;

// Compatibility construction for existing standalone storage tests/tools. The app uses explicit singleton DI registrations.
public static class BackupRuntime
{
    public static RestoreRecoveryService CreateRecovery(DataPaths paths)
    {
        var reading = new ReadingService(); var validator = new BackupV2Validator(reading, new ReadingStateCalculator());
        var files = new ManagedBookFiles(); var limits = new Models.Reader.BackupLimits();
        var archive = new CompleteBackupArchive(validator, new BackupV1Adapter(new BackupEntityValidator(reading)), limits, files);
        return new(paths, files, archive, validator, limits);
    }
    public static CompleteBackupService CreateService(DatabaseService database, IReadingService reading, DataPaths paths,
        ManagedBookFiles? files = null, Models.Reader.BackupLimits? limits = null)
    {
        files ??= new ManagedBookFiles(); limits ??= new(); var validator = new BackupV2Validator(reading, new ReadingStateCalculator());
        var archive = new CompleteBackupArchive(validator, new BackupV1Adapter(new BackupEntityValidator(reading)), limits, files);
        var recovery = new RestoreRecoveryService(paths, files, archive, validator, limits);
        return new(database, database.Coordinator, paths, archive, recovery, files, validator, limits, []);
    }
}
