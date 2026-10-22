using System.Text.Json;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
namespace ReadJournal.Library.Services;

// Format1 has no durable BookUid, Kind, Revision or managed files. Each restore creates new Manual identities.
public sealed class BackupV1Adapter
{
    private readonly BackupEntityValidator _validator;
    public BackupV1Adapter(BackupEntityValidator validator) { _validator = validator; }
    public BackupDataV2 Upgrade(BackupDocument source)
    {
        _validator.ValidateV1(source);
        var copy = JsonSerializer.Deserialize<BackupDocument>(JsonSerializer.Serialize(source))!;
        foreach (var book in copy.Books) { book.Kind = BookKind.Manual; book.BookUid = Guid.NewGuid(); }
        foreach (var quote in copy.Quotes) quote.Revision = 0;
        return new BackupDataV2(2, copy.Books.Select(b => new BackupBookV2(b, b.BookUid, b.Kind)).ToArray(),
            copy.ReadingRecords, copy.Quotes.Select(q => new BackupQuoteV2(q, 0)).ToArray(), copy.ReadingPlans,
            [], [], [], [], [], []);
    }
}
