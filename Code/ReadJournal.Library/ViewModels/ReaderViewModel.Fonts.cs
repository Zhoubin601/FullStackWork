using CommunityToolkit.Mvvm.Input;
using ReadJournal.Library.Services.Reader;

namespace ReadJournal.Library.ViewModels;

public sealed partial class ReaderViewModel
{
    public IReadOnlyList<string> FontFamilies { get; private set; } = [];
    public string[] SuggestedFontSizes { get; } = ["16", "18", "20", "22", "24", "28", "32", "36", "48", "60", "72"];
    public string FontHint => FontFamilies.Count == 0 ? "请输入已安装字体名称；缺字时使用后备字体。"
        : FontFamilies.Contains(FontFamily, StringComparer.OrdinalIgnoreCase)
            ? $"本机 {FontFamilies.Count} 种字体，输入名称可搜索；中文缺字时使用后备字体。"
            : "未找到同名已安装字体，请继续搜索或选择候选字体。";
    public IAsyncRelayCommand ResetTypographyCommand { get; private set; } = null!;

    private void InitializeFonts(IReaderFontCatalog? catalog)
    {
        FontFamilies = catalog?.FontFamilies ?? [];
        ResetTypographyCommand = new AsyncRelayCommand(() => Observe(async () =>
        {
            FontFamily = FontFamilies.FirstOrDefault() ?? ReaderPreferencesStorage.Default.FontFamily;
            FontSizeText = "22";
            await ApplyAppearanceAsync();
        }));
    }

    private string InstalledFont(string requested) => FontFamilies.FirstOrDefault(font => font.Equals(requested, StringComparison.OrdinalIgnoreCase))
        ?? (FontFamilies.Count == 0 ? requested : throw new ArgumentException("字体未安装，请从下拉列表选择本机字体后应用。"));
}
