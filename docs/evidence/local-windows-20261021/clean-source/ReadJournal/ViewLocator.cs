using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ReadJournal.Library.ViewModels;
namespace ReadJournal;
public class ViewLocator : IDataTemplate
{
    private readonly Dictionary<Type, Control> _cache = [];
    public bool Match(object? data) => data is ViewModelBase;
    public Control? Build(object? data)
    {
        if (data is null) return null;
        var name = data.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal).Replace("ReadJournal.Library.", "ReadJournal.", StringComparison.Ordinal);
        var type = Type.GetType(name);
        if (type is null) return new TextBlock { Text = "未找到视图：" + name };
        var view = _cache.TryGetValue(type, out var cached) ? cached : _cache[type] = (Control)Activator.CreateInstance(type)!;
        view.DataContext = data; return view;
    }
}
