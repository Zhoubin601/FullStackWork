using System.Text.Json.Serialization;
namespace ReadJournal.Library.Models;
public class BackupDocument
{
    [JsonRequired] public int FormatVersion { get; set; } = 1;
    [JsonRequired] public List<Book> Books { get; set; } = [];
    [JsonRequired] public List<ReadingRecord> ReadingRecords { get; set; } = [];
    [JsonRequired] public List<Quote> Quotes { get; set; } = [];
    [JsonRequired] public List<ReadingPlan> ReadingPlans { get; set; } = [];
}
