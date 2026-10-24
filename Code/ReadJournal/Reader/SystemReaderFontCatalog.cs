using Avalonia.Media;
using ReadJournal.Library.Services.Reader;

namespace ReadJournal.Reader;

public sealed class SystemReaderFontCatalog : IReaderFontCatalog
{
    private static readonly string[] Recommended =
    ["Noto Sans CJK SC", "Noto Serif CJK SC", "Microsoft YaHei", "SimSun", "SimHei", "KaiTi", "FangSong", "Segoe UI", "Arial"];

    public IReadOnlyList<string> FontFamilies { get; } = FontManager.Current.SystemFonts
        .Select(font => font.Name)
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(name => { var index = Array.FindIndex(Recommended, font => font.Equals(name, StringComparison.OrdinalIgnoreCase)); return index < 0 ? int.MaxValue : index; })
        .ThenBy(name => name, StringComparer.CurrentCultureIgnoreCase)
        .ToArray();
}
