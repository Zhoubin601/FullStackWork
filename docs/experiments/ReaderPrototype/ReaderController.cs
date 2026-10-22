using Avalonia.Threading;

namespace ReaderPrototype;

public sealed class ReaderController : IDisposable
{
    public NativeLayout? Layout { get; private set; }
    public ReaderDocument? Document => Layout?.Document;
    public NativePage? Page { get; private set; }
    public int Anchor { get; private set; }
    public int SelectionAnchor { get; set; }
    public int SelectionFocus { get; set; }
    public string Status { get; private set; } = "等待打开合成文本";
    public Task IndexTask { get; private set; } = Task.CompletedTask;
    public event Action? Changed;
    private CancellationTokenSource _work = new();
    private int _openVersion;
    private bool _disposed;
    public int IgnoredOpenResults { get; private set; }

    public async Task<bool> OpenAsync(Func<CancellationToken, Task<ReaderDocument>> load, LayoutOptions options)
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        var version = ++_openVersion;
        CancelIndex();
        Status = "正在读取正文"; Changed?.Invoke();
        var token = _work.Token;
        ReaderDocument document;
        try { document = await load(token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return false; }
        if (_disposed || version != _openVersion || token.IsCancellationRequested)
        { IgnoredOpenResults++; return false; }
        var candidate = new NativeLayout(document, options);
        NativePage page;
        try { page = candidate.Locate(0); }
        catch { candidate.Dispose(); throw; }
        var old = Layout;
        Layout = candidate; Page = page; Anchor = SelectionAnchor = SelectionFocus = 0;
        candidate.PinnedSection = page.Key.Section;
        old?.Dispose(); Status = "总页数计算中"; Changed?.Invoke();
        return true;
    }
    public void Reflow(LayoutOptions options)
    {
        if (Layout is null || Layout.Options == options) return;
        var current = Layout;
        var next = new NativeLayout(current.Document, options);
        NativePage page;
        try { page = next.Locate(Anchor); }
        catch { next.Dispose(); throw; }
        CancelIndex(); Layout = next; Page = page; next.PinnedSection = page.Key.Section;
        current.Dispose(); Status = "总页数计算中"; Changed?.Invoke();
    }
    public void Locate(int offset)
    {
        if (Layout is null) return;
        Anchor = Layout.Document.Snap(offset);
        Page = Layout.Locate(Anchor); Layout.PinnedSection = Page.Key.Section;
        Changed?.Invoke();
    }
    public bool Flip(int direction, bool extendSelection = false)
    {
        if (Layout is null || Page is null) return false;
        var next = Layout.Adjacent(Page, direction);
        if (next is null) return false;
        if (extendSelection)
        {
            if (SelectionAnchor == SelectionFocus) SelectionAnchor = direction > 0 ? Page.Start : Page.End;
            SelectionFocus = direction > 0 ? next.End : next.Start;
        }
        else SelectionAnchor = SelectionFocus = next.Start;
        Page = next; Anchor = next.Start; Layout.PinnedSection = next.Key.Section;
        Changed?.Invoke(); return true;
    }
    public string SelectedText => Layout?.Document.Extract(SelectionAnchor, SelectionFocus) ?? "";
    public void NotifySelection() => Changed?.Invoke();
    public void StartIndex()
    {
        if (Layout is null || !IndexTask.IsCompleted) return;
        var layout = Layout; var token = _work.Token;
        IndexTask = IndexAsync(layout, token);
    }
    private async Task IndexAsync(NativeLayout layout, CancellationToken token)
    {
        try
        {
            await layout.BuildIndexAsync(new InlineProgress<IndexStatus>(value =>
            {
                if (!token.IsCancellationRequested && ReferenceEquals(Layout, layout))
                {
                    Status = value.ExactPages is int pages ? $"共 {pages} 页" : $"总页数计算中 {value.IndexedSections}/{value.TotalSections}";
                    Changed?.Invoke();
                }
            }), token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        { if (!_disposed && ReferenceEquals(Layout, layout)) { Status = "索引失败：" + ex.Message; Changed?.Invoke(); } }
    }
    public void CancelIndex()
    {
        _work.Cancel(); _work.Dispose(); _work = new CancellationTokenSource();
        // Each old task observes its cancelled token before doing any more UI work.
        IndexTask = Task.CompletedTask;
        Status = "页索引已取消";
    }
    public string PageLabel
    {
        get
        {
            if (Layout is null || Page is null) return Status;
            var section = Layout.Document.Sections[Page.Key.Section];
            var chapter = Layout.Document.Chapters[section.ChapterIndex];
            var number = Layout.GlobalNumber(Page);
            return $"{chapter.Title} · {(number is int n ? $"第 {n} 页" : "页号计算中")} · 正文位置 {100.0 * Anchor / Layout.Document.Text.Length:0.00}% · {Status}";
        }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _openVersion++; _work.Cancel(); _work.Dispose();
        Layout?.Dispose(); Layout = null; Page = null; Changed = null;
    }
    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    { public void Report(T value) => report(value); }
}
