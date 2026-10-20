using SQLite;
namespace ReadJournal.Library.Models;
[Table("ReadingPlan")]
public class ReadingPlan
{
    [System.Text.Json.Serialization.JsonRequired][PrimaryKey, AutoIncrement] public int Id { get; set; }
    [System.Text.Json.Serialization.JsonRequired][Indexed] public int BookId { get; set; }
    [System.Text.Json.Serialization.JsonRequired] public DateTime StartDate { get; set; } = DateTime.Today;
    [System.Text.Json.Serialization.JsonRequired] public DateTime TargetDate { get; set; } = DateTime.Today;
}
