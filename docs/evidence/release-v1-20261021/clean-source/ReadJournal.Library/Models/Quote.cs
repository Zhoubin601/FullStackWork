using SQLite;
namespace ReadJournal.Library.Models;
[Table("Quote")]
public class Quote
{
    [System.Text.Json.Serialization.JsonRequired][PrimaryKey, AutoIncrement] public int Id { get; set; }
    [System.Text.Json.Serialization.JsonRequired][Indexed] public int BookId { get; set; }
    [System.Text.Json.Serialization.JsonRequired] public string Text { get; set; } = "";
    [System.Text.Json.Serialization.JsonRequired] public string Thoughts { get; set; } = "";
    [System.Text.Json.Serialization.JsonRequired] public int? Page { get; set; }
    [System.Text.Json.Serialization.JsonRequired] public bool IsFavorite { get; set; }
    [System.Text.Json.Serialization.JsonRequired] public DateTime CreatedAt { get; set; }
    [System.Text.Json.Serialization.JsonRequired] public DateTime UpdatedAt { get; set; }
    public override string ToString() => (IsFavorite ? "★ " : "") + Text;
}
