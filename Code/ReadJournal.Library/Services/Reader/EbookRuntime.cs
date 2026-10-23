using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Backup;
namespace ReadJournal.Library.Services.Reader;

// Composition for standalone consumers/tests. The application uses the explicit singleton registrations.
public static class EbookRuntime
{
    public static BookDeletionService CreateDeletion(DatabaseService db, ManagedBookFiles? files = null)
    { files ??= new(); return new(db, db.Coordinator, files, new(db.Paths, files, new()), [], []); }
    public static EbookLibraryService CreateLibrary(DatabaseService db, ManagedBookFiles? files = null, TxtParserLimits? txtLimits = null)
    {
        files ??= new(); var limits = new BackupLimits(); var ops = new BookFileOperations(db.Paths, files, limits);
        var validator = new BackupV2Validator(new ReadingService(), new ReadingStateCalculator());
        var archive = new CompleteBackupArchive(validator, new BackupV1Adapter(new BackupEntityValidator(new ReadingService())), limits, files);
        var recovery = new RestoreRecoveryService(db.Paths, files, archive, validator, limits, ops);
        return new(db, db.Coordinator, db.Paths, files, ops, [new TxtBookParser(files, txtLimits ?? new()), new EpubBookParser(files)], validator, limits, recovery, new(db, db.Coordinator, files, ops, [], []));
    }
}
