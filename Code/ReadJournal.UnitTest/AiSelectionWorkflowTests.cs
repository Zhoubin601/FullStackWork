using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Ai;
using Xunit;

namespace ReadJournal.UnitTest;

public sealed class AiSelectionWorkflowTests
{
    [Fact]
    public async Task WholeSelectionKeepsParagraphsAndUsesOnlyOverallExplanationWithRetryDedupeAndUndo()
    {
        const string text = "“喂，北京！爸爸妈妈怎么还不回来呀？”\n“什么，你现在还不知道……”\n“嗯，没。”\n“啊？！”\n组合e\u0301和🙂。";
        await using var x = await AiTestFixture.Create(text);
        var scope = await x.Factory.SelectionAsync(x.Context, x.Range(0, text.Length), default);
        x.Client.Reply = _ => Task.FromResult(new AiProviderResponse("整段对话表现孩子从困惑到震惊的变化。",
            [new("啊？！", "不应另存的碎片", null, null)], null, 535, 382));
        x.Faults.Point = "ai.before-commit";
        var pending = await Assert.ThrowsAsync<AiSavePendingException>(() => x.Ai.GenerateAndSaveQuotesAsync(scope, default));
        Assert.Equal(0, await x.QuoteCount());
        Assert.Equal(AiOperation.ExplainSelection, x.Client.Request!.Operation);
        Assert.Contains("整体解读", x.Client.Request.SystemPrompt);
        var batch = await x.Ai.RetrySaveAsync(pending.BatchId, x.Context, default);
        Assert.Equal(1, x.Client.Count); Assert.Single(batch.InsertedQuoteIds); Assert.Equal(0, batch.RejectedCount);
        Assert.Equal(535, batch.InputTokens); Assert.Equal(382, batch.OutputTokens);
        var quote = Assert.Single(await x.Quotes.SearchAsync("", false));
        Assert.Equal(text, quote.Quote.Text); Assert.Equal("整段对话表现孩子从困惑到震惊的变化。", quote.Quote.Thoughts);
        Assert.Equal(QuoteSource.AiGenerated, quote.Source);
        var navigation = new ReadJournal.Library.Services.Reader.ReaderQuoteService(x.Db, x.Db.Coordinator, x.Documents, x.Sessions, new ReadJournal.Library.Services.ReadingService(), new());
        Assert.True((await navigation.ResolveQuoteAsync(quote.Quote.Id, default)).IsExact);
        var duplicate = await x.Ai.GenerateAndSaveQuotesAsync(scope, default);
        Assert.Empty(duplicate.InsertedQuoteIds); Assert.Equal(1, duplicate.DuplicateCount); Assert.Equal(1, await x.QuoteCount());
        Assert.Equal(1, (await x.Ai.UndoBatchAsync(batch.BatchId, x.Db.Coordinator.CurrentEpoch, default)).DeletedCount);
        Assert.Equal(0, await x.QuoteCount());
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" ")]
    public async Task MissingExplanationNeverCreatesAnOriginalOnlyQuote(string? explanation)
    {
        await using var x = await AiTestFixture.Create();
        var scope = await x.Factory.SelectionAsync(x.Context, x.Range(0, AiTestFixture.Text.Length), default);
        x.Client.Reply = _ => Task.FromResult(new AiProviderResponse(explanation, [], null, null, null));
        await Assert.ThrowsAsync<AiException>(() => x.Ai.GenerateAndSaveQuotesAsync(scope, default));
        Assert.Equal(0, await x.QuoteCount());
    }

    [Fact]
    public async Task OversizedSelectionIsRejectedBeforeCallingProviderAndDoesNotTruncate()
    {
        await using var x = await AiTestFixture.Create(new string('字', 8001));
        var scope = await x.Factory.SelectionAsync(x.Context, x.Range(0, 8001), default);
        Assert.Equal("InvalidSelectionQuote", (await Assert.ThrowsAsync<AiException>(() => x.Ai.GenerateAndSaveQuotesAsync(scope, default))).Code);
        Assert.Equal(0, x.Client.Count); Assert.Equal(0, await x.QuoteCount());
    }

    [Fact]
    public async Task DiscontinuousSelectionIsRejectedBeforeProviderAndKindIsIntegrityProtected()
    {
        await using var x = await AiTestFixture.Create();
        var a = await x.Factory.SelectionAsync(x.Context, x.Range(0, 3), default);
        var b = await x.Factory.SelectionAsync(x.Context, x.Range(6, 9), default);
        var spans = a.Original.Spans.Concat(b.Original.Spans).ToArray();
        var scope = a with { Original = a.Original with { Spans = spans, TextSha256 = AiIntegrity.Hash(string.Concat(spans.Select(s => s.Text))) } };
        scope = scope with { ScopeSha256 = AiIntegrity.ScopeHash(scope) };
        Assert.Equal("InvalidSelectionQuote", (await Assert.ThrowsAsync<AiException>(() => x.Ai.GenerateAndSaveQuotesAsync(scope, default))).Code);
        Assert.Equal(0, x.Client.Count);
        await Assert.ThrowsAsync<AiException>(() => x.Ai.GenerateAndSaveQuotesAsync(a with { Kind = AiScopeKind.CurrentPage }, default));
        Assert.Equal(0, x.Client.Count);
    }

    [Fact]
    public async Task OversizedExplanationAndCancelledResponseNeverSaveSelection()
    {
        await using var x = await AiTestFixture.Create();
        var scope = await x.Factory.SelectionAsync(x.Context, x.Range(0, AiTestFixture.Text.Length), default);
        x.Client.Reply = _ => Task.FromResult(new AiProviderResponse(new string('字', 4001), [], null, null, null));
        await Assert.ThrowsAsync<AiException>(() => x.Ai.GenerateAndSaveQuotesAsync(scope, default));
        using var cancel = new CancellationTokenSource();
        x.Client.Reply = _ => { cancel.Cancel(); return Task.FromResult(new AiProviderResponse("取消后返回的解读", [], null, null, null)); };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => x.Ai.GenerateAndSaveQuotesAsync(scope, cancel.Token));
        Assert.Equal(0, await x.QuoteCount());
    }

    [Fact]
    public async Task ActualMockHttpProtocolSupportsWholeSelectionWithoutAskingModelToCopyOriginal()
    {
        await using var x = await AiTestFixture.Create();
        using var client = new DeepSeekClient(x.Settings, x.Secrets);
        var service = new ReaderAiService(client, x.Settings, new AiQuoteVerifier(), x.Quotes, x.Sessions, x.Db.Coordinator, x.Documents);
        var scope = await x.Factory.SelectionAsync(x.Context, x.Range(0, AiTestFixture.Text.Length), default);
        Assert.Equal(AiScopeKind.Selection, scope.Kind);
        var batch = await service.GenerateAndSaveQuotesAsync(scope, default);
        Assert.Single(batch.InsertedQuoteIds);
        var quote = Assert.Single(await x.Quotes.SearchAsync("", false));
        Assert.Equal(AiTestFixture.Text, quote.Quote.Text); Assert.Contains("本地模拟", quote.Quote.Thoughts);
    }
}
