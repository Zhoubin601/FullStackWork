using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ReadJournal.Reader;

public sealed class ReaderFontFamilyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string name || !FontManager.Current.SystemFonts.Any(font => font.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return FontFamily.Default;
        return new FontFamily($"{name}, Noto Sans CJK SC, Microsoft YaHei, DejaVu Sans");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
