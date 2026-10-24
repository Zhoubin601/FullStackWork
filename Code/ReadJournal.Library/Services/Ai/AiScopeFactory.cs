using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.Services.Ai;

public sealed class AiScopeFactory(IReaderDocumentService documents, IReaderLayoutService layouts)
{
    public Task<AiScopeSnapshot> SelectionAsync(ReaderContext context, DocumentRange range, CancellationToken ct) => CreateAsync(context, null, "当前选区", [range], AiScopeKind.Selection, ct);
    public Task<AiScopeSnapshot> CurrentPageAsync(ReaderContext context, ReaderPage page, CancellationToken ct) => CreateAsync(context, page.Ref.Layout, "当前页", page.ContentRanges, AiScopeKind.CurrentPage, ct);
    public async Task<AiScopeSnapshot> PagesAsync(ReaderContext context, LayoutHandle layout, PageSpan pages, CancellationToken ct)
    {
        if (pages.StartInclusive < 1 || pages.EndInclusive < pages.StartInclusive || pages.EndInclusive - (long)pages.StartInclusive >= 200)
            throw new AiException("LimitExceeded", "请输入有效全书页码，一次最多选择200页。");
        // No provider request exists until the cancellable index and range resolution finish.
        var resolved = await layouts.ResolveGlobalPagesAsync(layout, pages, null, ct);
        ct.ThrowIfCancellationRequested();
        if (resolved.Layout != layout.Id || layout.Document != context.Identity.Document) throw new AiException("StaleContext", "排版已变化，请重新选择页码。");
        return await CreateAsync(context, layout.Id, $"全书第{pages.StartInclusive}～{pages.EndInclusive}页", resolved.ResolvedPages.SelectMany(p => p.ContentRanges).ToArray(), AiScopeKind.PageRange, ct);
    }
    private async Task<AiScopeSnapshot> CreateAsync(ReaderContext context, LayoutId? layout, string display, IReadOnlyList<DocumentRange> ranges, AiScopeKind kind, CancellationToken ct)
    {
        if (ranges.Count is 0 or > 2048) throw new AiException("LimitExceeded", "范围为空或过大。");
        var document = await documents.DescribeAsync(context.Identity.Document, ct);
        var spans = new List<TextSpan>(); long budget = 0;
        foreach (var range in ranges)
        {
            ct.ThrowIfCancellationRequested(); ReaderContractValidator.Validate(range);
            if (range.Start.Document != document.Key) throw new AiException("StaleContext", "正文身份已变化。");
            var a = document.Chapters.First(c => c.Key == range.Start.ChapterKey); var b = document.Chapters.First(c => c.Key == range.End.ChapterKey);
            budget += document.Chapters.Skip(a.Order).Take(b.Order - a.Order + 1).Sum(c => (long)c.Utf16Length) - range.Start.Utf16Offset - (b.Utf16Length - range.End.Utf16Offset);
            if (budget > AiIntegrity.MaxInput || budget < 0) throw new AiException("LimitExceeded", "正文超过限额，请缩小范围。");
            foreach (var span in (await documents.ExtractAsync(range, ct)).Spans)
            {
                if (spans.Count > 0 && spans[^1].Range.End == span.Range.Start)
                { var last = spans[^1]; spans[^1] = new(new(last.Range.Start, span.Range.End), last.Text + span.Text); }
                else spans.Add(span);
            }
        }
        var original = new TextSnapshot(document.Key, spans.AsReadOnly(), AiIntegrity.Hash(string.Concat(spans.Select(s => s.Text))));
        var snapshot = new AiScopeSnapshot(context, layout, display, original, "", kind);
        snapshot = snapshot with { ScopeSha256 = AiIntegrity.ScopeHash(snapshot) };
        await AiIntegrity.ValidateOriginalAsync(snapshot, documents, ct); return snapshot;
    }
}
