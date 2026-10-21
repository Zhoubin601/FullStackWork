using SQLite;
namespace ReadJournal.Library.Models;
[Table("ReadingRecord")]
public class ReadingRecord
{
    [System.Text.Json.Serialization.JsonRequired][PrimaryKey, AutoIncrement] public int Id { get; set; }
    [System.Text.Json.Serialization.JsonRequired][Indexed] public int BookId { get; set; }
    [System.Text.Json.Serialization.JsonRequired] public DateTime ReadingDate { get; set; } = DateTime.Today;
    [System.Text.Json.Serialization.JsonRequired] public int StartPage { get; set; } = 1;
    [System.Text.Json.Serialization.JsonRequired] public int EndPage { get; set; } = 1;
    [System.Text.Json.Serialization.JsonRequired] public int Minutes { get; set; } = 1;
    [Ignore, System.Text.Json.Serialization.JsonIgnore] public int Pages => EndPage - StartPage + 1;
    public override string ToString() => $"{ReadingDate:yyyy-MM-dd} · 第 {StartPage}～{EndPage} 页 · {Minutes} 分钟";
}
