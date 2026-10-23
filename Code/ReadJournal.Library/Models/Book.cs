using SQLite;
namespace ReadJournal.Library.Models;
[Table("Book")]
public class Book
{
    [System.Text.Json.Serialization.JsonRequired][PrimaryKey, AutoIncrement] public int Id { get; set; }
    [System.Text.Json.Serialization.JsonRequired] public string Title { get; set; } = "";
    [System.Text.Json.Serialization.JsonRequired] public string Author { get; set; } = "";
    [System.Text.Json.Serialization.JsonRequired] public string Category { get; set; } = "";
    [System.Text.Json.Serialization.JsonRequired] public int TotalPages { get; set; }
    [System.Text.Json.Serialization.JsonRequired] public string Description { get; set; } = "";
    [System.Text.Json.Serialization.JsonRequired] public DateTime CreatedAt { get; set; }
    [System.Text.Json.Serialization.JsonRequired] public DateTime UpdatedAt { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public Reader.BookKind Kind { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public Guid BookUid { get; set; } = Guid.NewGuid();
    public override string ToString() => Title;
}
