using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Ai;
using ReadJournal.Library.Services.Backup;
using ReadJournal.Library.Services.Reader;
using Moq;
using Xunit;
namespace ReadJournal.UnitTest;

internal sealed class AiTestFaults : AiBatchCheckpoints
{
    public string? Point; public Action<string>? Action { get; set; }
    public override void Checkpoint(string point) { Action?.Invoke(point); if (Point == point) { Point = null; throw new IOException("synthetic storage fault"); } }
}
internal sealed class AiTestClient : IDeepSeekClient
{
    public int Count; public AiProviderRequest? Request;
    public Func<CancellationToken, Task<AiProviderResponse>> Reply = _ => Task.FromResult(new AiProviderResponse("模拟分析", [new("春天的河流缓缓流过。", "模拟感想", null, null)], null, null, null));
    public Task<AiProviderResponse> SendAsync(AiProviderRequest request, CancellationToken ct) { Count++; Request = request; return Reply(ct); }
}
internal sealed class AiTestFixture : IAsyncDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "ReadJournalP8", Guid.NewGuid().ToString("N"));
    public const string Text = "春天的河流缓缓流过。\n甲重复句。乙重复句。\n组合e\u0301和🙂。\n范围外的结尾。";
    public DatabaseService Db { get; private set; } = null!;
    public ReaderDocumentService Documents { get; private set; } = null!;
    public ReaderSessionService Sessions { get; private set; } = null!;
    public QuoteStorage Quotes { get; private set; } = null!;
    public ReaderContext Context { get; private set; } = null!;
    public ReaderPosition Start { get; private set; } = null!;
    public AiTestFaults Faults { get; } = new();
    public AiSettingsStorage Settings { get; private set; } = null!;
    public SessionSecretStore Secrets { get; } = new();
    public AiTestClient Client { get; } = new();
    public AiScopeFactory Factory { get; private set; } = null!;
    public ReaderAiService Ai { get; private set; } = null!;
    private DateTimeOffset _opened; private bool _closed;
    public static async Task<AiTestFixture> Create(string? text = null)
    {
        var x = new AiTestFixture(); Directory.CreateDirectory(x.Root); var path = Path.Combine(x.Root, "input.txt"); await File.WriteAllTextAsync(path, text ?? Text);
        x.Db = new(new DataPaths(Path.Combine(x.Root, "data"))); var imported = await EbookRuntime.CreateLibrary(x.Db).ImportAsync(new(path), null, default); Assert.Equal(ImportState.Imported, imported.State);
        var info = await x.Db.ReadAsync(c => (c.Find<Book>(imported.BookId), c.Find<EbookProgress>(imported.BookId)));
        x.Start = ReaderContractValidator.ReadPosition(info.Item2.LastPositionJson); var identity = new ReaderOpenIdentity(info.Item1.Id, info.Item1.BookUid, x.Start.Document, x.Db.Coordinator.CurrentEpoch, Guid.NewGuid(), 1);
        x.Documents = new(x.Db, x.Db.Coordinator); await x.Documents.OpenAsync(identity, default);
        x.Sessions = new(x.Db, x.Db.Coordinator, x.Documents, new EbookProgressMaintenance(x.Db, x.Db.Coordinator), new(), new());
        x._opened = DateTimeOffset.Now; x.Context = await x.Sessions.BeginAsync(identity, x.Start, x._opened, default);
        x.Quotes = new(x.Db, new ReadingService(), x.Sessions, x.Documents, x.Db.Coordinator, x.Faults);
        x.Settings = new(x.Db.Paths); x.Factory = new(x.Documents, Mock.Of<IReaderLayoutService>());
        x.Ai = new(x.Client, x.Settings, new AiQuoteVerifier(), x.Quotes, x.Sessions, x.Db.Coordinator, x.Documents); return x;
    }
    public DocumentRange Range(int first, int last) => new(Start with { Utf16Offset = first }, Start with { Utf16Offset = last });
    public Task<AiScopeSnapshot> Scope(int start = 0, int? end = null) => Factory.SelectionAsync(Context, Range(start, end ?? Text.Length), default);
    public VerifiedAiBatchCommand Command(AiScopeSnapshot scope, Guid? id = null, params AiQuoteCandidate[] candidates)
    {
        var result = new AiQuoteVerifier().Verify(scope, candidates.Length == 0 ? [new("春天的河流缓缓流过。", "感想", null, null)] : candidates);
        return new(id ?? Guid.NewGuid(), Context, scope.ScopeSha256, result.Accepted, result.RejectionReasons.Count, scope);
    }
    public Task<int> QuoteCount() => Db.ReadAsync(c => c.Table<Quote>().Count());
    public Task<string> Hash() => Db.ReadAsync(c => BackupDataStore.Hash(BackupDataStore.Snapshot(c)));
    public async Task Close()
    { if (_closed) return; await Sessions.CloseAsync(new(Context, Start, Start, _opened, DateTimeOffset.Now, false, ReaderExitReason.Back), default); _closed = true; }
    public async ValueTask DisposeAsync() { await Close(); await Secrets.DeleteApiKeyAsync(default); Db.Dispose(); if (Directory.Exists(Root)) Directory.Delete(Root, true); }
}
