using System.Text.Json;
using ReadJournal.Library.Models;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services;
using ReadJournal.Library.Services.Ai;
using ReadJournal.Library.Services.Backup;
using ReadJournal.Library.Services.Reader;
using Moq;
using Xunit;
namespace ReadJournal.UnitTest;

public sealed class AiWorkflowTests
{
    [Fact]
    public async Task ProviderSeesReadableChineseAndOptionalHintsHaveDistinctRejectionReasons()
    {
        await using var x = await AiTestFixture.Create(); var scope = await x.Scope();
        await x.Ai.GenerateAndSaveQuotesAsync(scope, default);
        Assert.Contains("春天的河流缓缓流过。", x.Client.Request!.UserText);
        using var body = System.Text.Json.JsonDocument.Parse(x.Client.Request.UserText);
        Assert.Equal(scope.Original.Spans[0].Text, body.RootElement.GetProperty("segments")[0].GetString());
        var result = new AiQuoteVerifier().Verify(scope, [
            new("春天的河流缓缓流过。", "感想", "不存在的上下文", null),
            new("改写的河流。", "感想", null, null),
            new("春天的河流缓缓流过。", "感想", null, null)]);
        Assert.Single(result.Accepted);
        Assert.Contains("候选上下文提示与紧邻原文不一致。", result.RejectionReasons);
        Assert.Contains("未在所选连续原文中找到完整原句。", result.RejectionReasons);
    }
    [Fact]
    public async Task ReportedQuoteUsageSurvivesLocalSaveRetryWithoutAnotherProviderCall()
    {
        await using var x = await AiTestFixture.Create();
        x.Client.Reply = _ => Task.FromResult(new AiProviderResponse(null, [new("春天的河流缓缓流过。", "感想", null, null)], null, 101, 27));
        var scope = await x.Scope(); x.Faults.Point = "ai.before-commit";
        var pending = await Assert.ThrowsAsync<AiSavePendingException>(() => x.Ai.GenerateAndSaveQuotesAsync(scope, default));
        var saved = await x.Ai.RetrySaveAsync(pending.BatchId, x.Context, default);
        Assert.Equal(101, saved.InputTokens); Assert.Equal(27, saved.OutputTokens);
        Assert.Single(saved.InsertedQuoteIds); Assert.Equal(1, x.Client.Count);
    }
    [Fact]
    public async Task SelectionRequestContainsOnlyFrozenTextAndAnalysisNeverWritesQuotes()
    {
        await using var x = await AiTestFixture.Create(); var scope = await x.Scope(0, 10);
        var before = await x.Hash(); var result = await x.Ai.AnalyzeAsync(scope, AiOperation.Analyze, default);
        Assert.Equal("模拟分析", result.Analysis); Assert.Equal(before, await x.Hash());
        using var body = JsonDocument.Parse(x.Client.Request!.UserText);
        Assert.Equal("春天的河流缓缓流过。", body.RootElement.GetProperty("segments")[0].GetString());
        Assert.DoesNotContain("范围外", x.Client.Request.UserText); Assert.DoesNotContain(x.Context.Identity.BookUid.ToString(), x.Client.Request.UserText);
        await Assert.ThrowsAsync<AiException>(() => x.Ai.AnalyzeAsync(scope, AiOperation.ExtractQuotes, default)); Assert.Equal(1, x.Client.Count);
    }
    [Fact]
    public async Task CurrentPageAndGlobalPagesFreezeOrderedMappingAndWaitForIndexWithoutSending()
    {
        await using var x = await AiTestFixture.Create(); var layout = new LayoutHandle(new(Guid.NewGuid()), x.Start.Document);
        var page = new ReaderPage(new(layout.Id, x.Start.ChapterKey, 1), [x.Range(0, 11)], 1, true, false);
        var layouts = new Mock<IReaderLayoutService>(); var ready = new TaskCompletionSource<PageRangeSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        layouts.Setup(l => l.ResolveGlobalPagesAsync(layout, new PageSpan(1, 1), null, It.IsAny<CancellationToken>())).Returns((LayoutHandle _, PageSpan _, IProgress<LayoutIndexStatus>? _, CancellationToken ct) => ready.Task.WaitAsync(ct));
        var factory = new AiScopeFactory(x.Documents, layouts.Object);
        var current = await factory.CurrentPageAsync(x.Context, page, default); Assert.Equal(layout.Id, current.Layout); Assert.Single(current.Original.Spans);
        using var cancel = new CancellationTokenSource(); var pending = factory.PagesAsync(x.Context, layout, new(1, 1), cancel.Token); Assert.False(pending.IsCompleted); Assert.Equal(0, x.Client.Count);
        cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending); Assert.Equal(0, await x.QuoteCount());
        ready.SetResult(new(layout.Id, new(1, 1), [page], current.Original)); var frozen = await factory.PagesAsync(x.Context, layout, new(1, 1), default);
        Assert.Equal(current.Original.Spans, frozen.Original.Spans); Assert.Equal("全书第1～1页", frozen.DisplayScope);
        await Assert.ThrowsAsync<AiException>(() => factory.PagesAsync(x.Context, layout, new(1, 201), default));
    }
    [Fact]
    public async Task VerifierRejectsForgedOutOfScopeAmbiguousGraphemeAndImageJoinsButDisambiguatesExactText()
    {
        await using var x = await AiTestFixture.Create(); var scope = await x.Scope(); var verifier = new AiQuoteVerifier();
        var result = verifier.Verify(scope, [new("伪造原句", "", null, null), new("重复句。", "", null, null), new("e", "", null, null), new("重复句。", "合法", "乙", "\n")]);
        Assert.Single(result.Accepted); Assert.Equal(3, result.RejectionReasons.Count);
        var partial = await x.Scope(0, 11); Assert.Empty(verifier.Verify(partial, [new("范围外的结尾。", "", null, null)]).Accepted);
        var spans = new[] { new TextSpan(x.Range(0, 2), "春天"), new TextSpan(x.Range(3, 5), "河流") };
        var split = scope with { Original = new(x.Start.Document, spans, AiIntegrity.Hash("春天河流")) }; split = split with { ScopeSha256 = AiIntegrity.ScopeHash(split) };
        Assert.Empty(verifier.Verify(split, [new("春天河流", "", null, null)]).Accepted);
        Assert.Equal("重复句。", result.Accepted[0].OriginalText);
    }
    [Fact]
    public async Task GenerationSavesOnlyAcceptedQuotesAndCountsRejectedAndDuplicates()
    {
        await using var x = await AiTestFixture.Create(); var scope = await x.Scope();
        x.Client.Reply = _ => Task.FromResult(new AiProviderResponse(null, [new("春天的河流缓缓流过。", "AI感想", null, null), new("伪造句", "", null, null), new("重复句。", "", null, null)], null, 3, 4));
        var first = await x.Ai.GenerateAndSaveQuotesAsync(scope, default); Assert.Single(first.InsertedQuoteIds); Assert.Equal(2, first.RejectedCount);
        var id = first.InsertedQuoteIds[0]; var item = Assert.Single(await x.Quotes.SearchAsync("", false)); Assert.Equal(QuoteSource.AiGenerated, item.Source); Assert.Equal(first.BatchId, item.BatchId); Assert.Contains("AI", item.SourceLabel);
        Assert.Null(item.Quote.Page); Assert.Equal("AI感想", item.Quote.Thoughts);
        var origin = await x.Db.ReadAsync(c => c.Find<QuoteOrigin>(id)); Assert.Equal(scope.Context.Identity.Document, ReaderContractValidator.ReadDocument(origin.DocumentKeyJson!));
        var second = await x.Ai.GenerateAndSaveQuotesAsync(scope, default); Assert.Empty(second.InsertedQuoteIds); Assert.Equal(1, second.DuplicateCount); Assert.Equal(1, await x.QuoteCount());
        var resolve = new ReaderQuoteService(x.Db, x.Db.Coordinator, x.Documents, x.Sessions, new ReadingService(), new()); Assert.True((await resolve.ResolveQuoteAsync(id, default)).IsExact);
    }
    [Theory]
    [InlineData("ai.after-quote")][InlineData("ai.after-receipt")][InlineData("ai.before-commit")]
    public async Task EntireBatchRollsBackAndLocalRetryDoesNotSendAnotherRequest(string point)
    {
        await using var x = await AiTestFixture.Create(); var scope = await x.Scope(); var before = await x.Hash(); x.Faults.Point = point;
        var error = await Assert.ThrowsAsync<AiSavePendingException>(() => x.Ai.GenerateAndSaveQuotesAsync(scope, default)); Assert.Equal(before, await x.Hash());
        Assert.Equal(1, x.Client.Count); var saved = await x.Ai.RetrySaveAsync(error.BatchId, x.Context, default); Assert.Single(saved.InsertedQuoteIds); Assert.Equal(1, x.Client.Count);
    }
    [Fact]
    public async Task ImmutableReceiptSurvivesDeletedItemsAndReplayAfterUndoNeverReinserts()
    {
        await using var x = await AiTestFixture.Create(); var scope = await x.Scope(); var command = x.Command(scope);
        var first = await x.Quotes.SaveAiBatchAsync(command, default); await x.Quotes.DeleteAsync(first.InsertedQuoteIds[0]);
        var repeated = await x.Quotes.SaveAiBatchAsync(command, default); Assert.Equal(first.InsertedQuoteIds, repeated.InsertedQuoteIds); Assert.Equal(0, await x.QuoteCount());
        await x.Quotes.UndoAiBatchAsync(first.BatchId, x.Db.Coordinator.CurrentEpoch, default);
        Assert.True((await x.Quotes.SaveAiBatchAsync(command, default)).WasAlreadyUndone); Assert.Equal(0, await x.QuoteCount());
        await Assert.ThrowsAnyAsync<Exception>(() => x.Db.WriteAsync(c => c.Execute("UPDATE AiGenerationBatch SET InsertedQuoteIdsJson='[]' WHERE BatchId=?", first.BatchId)));
    }
    [Fact]
    public async Task UndoPreservesEarlierDuplicatesEditedFavoriteAndEvenUnversionedContentChanges()
    {
        await using var x = await AiTestFixture.Create(); var scope = await x.Scope();
        var texts = new[] { "春天的河流缓缓流过。", "甲重复句。", "乙重复句。", "范围外的结尾。", "组合e\u0301和🙂。" };
        var existing = await x.Quotes.SaveAiBatchAsync(x.Command(scope, null, new AiQuoteCandidate(texts[0], "旧批", null, null)), default);
        var command = x.Command(scope, null, texts.Select(t => new AiQuoteCandidate(t, "新批", null, null)).ToArray());
        var result = await x.Quotes.SaveAiBatchAsync(command, default); Assert.Equal(1, result.DuplicateCount); Assert.Equal(4, result.InsertedQuoteIds.Count);
        var edited = (await x.Quotes.GetAsync(result.InsertedQuoteIds[0]))!; edited.Thoughts = "用户改动"; await x.Quotes.SaveAsync(edited);
        await x.Quotes.ToggleFavoriteAsync(result.InsertedQuoteIds[1]);
        await x.Db.WriteAsync(c => c.Execute("UPDATE Quote SET Thoughts='external change' WHERE Id=?", result.InsertedQuoteIds[2]));
        var undo = await x.Ai.UndoBatchAsync(result.BatchId, x.Db.Coordinator.CurrentEpoch, default); Assert.Equal(1, undo.DeletedCount); Assert.Equal(3, undo.RetainedModifiedCount);
        Assert.NotNull(await x.Quotes.GetAsync(existing.InsertedQuoteIds[0])); Assert.Equal(4, await x.QuoteCount());
        Assert.True((await x.Ai.UndoBatchAsync(result.BatchId, x.Db.Coordinator.CurrentEpoch, default)).WasAlreadyUndone);
    }
    [Theory][InlineData("ai.undo-after-delete")][InlineData("ai.undo-before-commit")]
    public async Task UndoIsAtomicAndCanRetry(string point)
    {
        await using var x = await AiTestFixture.Create(); var result = await x.Ai.GenerateAndSaveQuotesAsync(await x.Scope(), default); var before = await x.Hash(); x.Faults.Point = point;
        await Assert.ThrowsAsync<IOException>(() => x.Ai.UndoBatchAsync(result.BatchId, x.Db.Coordinator.CurrentEpoch, default)); Assert.Equal(before, await x.Hash());
        Assert.Equal(1, (await x.Ai.UndoBatchAsync(result.BatchId, x.Db.Coordinator.CurrentEpoch, default)).DeletedCount);
    }
    [Fact]
    public async Task AllRejectedLeavesNoBatchAndForgedVerifiedCommandsCannotBypassStorageChecks()
    {
        await using var x = await AiTestFixture.Create(); var scope = await x.Scope();
        x.Client.Reply = _ => Task.FromResult(new AiProviderResponse(null, [new("不存在的原句", "", null, null)], null, null, null));
        var before = await x.Hash(); var result = await x.Ai.GenerateAndSaveQuotesAsync(scope, default); Assert.Empty(result.InsertedQuoteIds); Assert.Equal(1, result.RejectedCount); Assert.Equal(before, await x.Hash());
        var command = x.Command(scope) with { Quotes = [new(x.Range(0, 11), "伪造", "")] };
        await Assert.ThrowsAsync<AiException>(() => x.Quotes.SaveAiBatchAsync(command, default)); Assert.Equal(before, await x.Hash());
        var fake = scope with { Original = scope.Original with { Spans = [new(x.Range(0, 11), "伪造春天河流缓缓流过")] } };
        fake = fake with { Original = fake.Original with { TextSha256 = AiIntegrity.Hash(string.Concat(fake.Original.Spans.Select(s => s.Text))) } }; fake = fake with { ScopeSha256 = AiIntegrity.ScopeHash(fake) };
        await Assert.ThrowsAsync<AiException>(() => x.Ai.AnalyzeAsync(fake, AiOperation.Analyze, default)); Assert.Equal(1, x.Client.Count);
    }
    [Fact]
    public async Task ConcurrentSecondRequestIsRejectedAndCancellationAfterResponseDoesNotSave()
    {
        await using var x = await AiTestFixture.Create(); var scope = await x.Scope();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var reply = new TaskCompletionSource<AiProviderResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.Client.Reply = _ => { started.SetResult(); return reply.Task; }; using var cancel = new CancellationTokenSource();
        var pending = x.Ai.GenerateAndSaveQuotesAsync(scope, cancel.Token); await started.Task;
        Assert.Equal("AiBusy", (await Assert.ThrowsAsync<AiException>(() => x.Ai.AnalyzeAsync(scope, AiOperation.Analyze, default))).Code);
        // A noncooperative provider deliberately returns success after cancellation.
        cancel.Cancel(); reply.SetResult(new(null, [new("春天的河流缓缓流过。", "", null, null)], null, null, null));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending); Assert.Equal(0, await x.QuoteCount()); Assert.Equal(1, x.Client.Count);
    }
    [Theory][InlineData("close")][InlineData("restore")][InlineData("reset")][InlineData("reuse-id")]
    public async Task OldResponseCannotCommitAfterLeavingRestoringResettingOrReusingIntegerId(string mutation)
    {
        await using var x = await AiTestFixture.Create(); var scope = await x.Scope();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var reply = new TaskCompletionSource<AiProviderResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.Client.Reply = _ => { started.SetResult(); return reply.Task; }; var pending = x.Ai.GenerateAndSaveQuotesAsync(scope, default); await started.Task;
        await x.Close();
        if (mutation == "restore")
        { var backup = BackupRuntime.CreateService(x.Db, new ReadingService(), x.Db.Paths); var path = Path.Combine(x.Root, "restore.zip"); await backup.ExportAsync(path, default); await backup.RestoreAsync(path, default); }
        if (mutation == "reset") await x.Sessions.ResetProgressAsync(x.Context.Identity.BookId, x.Context.Identity.BookUid, scope.Context.Identity.Epoch, default);
        if (mutation == "reuse-id")
        {
            await x.Db.WriteAsync(c => { c.Delete<Book>(x.Context.Identity.BookId); c.Insert(new Book { Id = x.Context.Identity.BookId, Title = "重新使用整数ID的手动书", TotalPages = 10, BookUid = Guid.NewGuid(), CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now }); return 0; });
        }
        var before = await x.Hash(); reply.SetResult(new(null, [new("春天的河流缓缓流过。", "", null, null)], null, null, null));
        await Assert.ThrowsAnyAsync<Exception>(() => pending); Assert.Equal(before, await x.Hash()); Assert.Equal(0, await x.QuoteCount());
        if (mutation is "restore" or "reset") await Assert.ThrowsAnyAsync<Exception>(() => x.Ai.UndoBatchAsync(Guid.NewGuid(), scope.Context.Identity.Epoch, default));
    }
    [Fact]
    public async Task InputLimitsPreventProviderCallsAndSnapshotsCannotBeMutatedDuringAwait()
    {
        await using var x = await AiTestFixture.Create(); await x.Settings.SaveAsync(AiSettingsStorage.Default with { MaxInputUtf16Units = 3 }, default);
        var scope = await x.Scope(); await Assert.ThrowsAsync<AiException>(() => x.Ai.GenerateAndSaveQuotesAsync(scope, default)); Assert.Equal(0, x.Client.Count);
        await x.Settings.SaveAsync(AiSettingsStorage.Default, default);
        var mutable = scope.Original.Spans.ToList(); var owned = scope with { Original = scope.Original with { Spans = mutable } }; owned = owned with { ScopeSha256 = AiIntegrity.ScopeHash(owned) };
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var reply = new TaskCompletionSource<AiProviderResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.Client.Reply = _ => { started.SetResult(); return reply.Task; }; var pending = x.Ai.GenerateAndSaveQuotesAsync(owned, default); await started.Task; mutable.Clear();
        reply.SetResult(new(null, [new("春天的河流缓缓流过。", "", null, null)], null, null, null)); Assert.Single((await pending).InsertedQuoteIds);
    }
    [Fact]
    public async Task BackupRoundTripPreservesReceiptAndUndoAndNeverContainsSessionCredentials()
    {
        await using var x = await AiTestFixture.Create(); var key = "fictional-" + Guid.NewGuid().ToString("N"); await x.Secrets.WriteApiKeyAsync(key, default);
        await x.Settings.SaveAsync(AiSettingsStorage.Default, default); var saved = await x.Ai.GenerateAndSaveQuotesAsync(await x.Scope(), default); await x.Close();
        var backup = BackupRuntime.CreateService(x.Db, new ReadingService(), x.Db.Paths); var path = Path.Combine(x.Root, "complete.zip"); var before = await x.Hash(); await backup.ExportAsync(path, default);
        await x.Ai.UndoBatchAsync(saved.BatchId, x.Db.Coordinator.CurrentEpoch, default); await backup.RestoreAsync(path, default); Assert.Equal(before, await x.Hash());
        Assert.True(await x.Secrets.HasApiKeyAsync(default)); Assert.Equal(1, (await x.Ai.UndoBatchAsync(saved.BatchId, x.Db.Coordinator.CurrentEpoch, default)).DeletedCount);
        using (var zip = System.IO.Compression.ZipFile.OpenRead(path)) foreach (var entry in zip.Entries) { using var reader = new StreamReader(entry.Open()); Assert.False((await reader.ReadToEndAsync()).Contains(key, StringComparison.Ordinal)); }
        Assert.False(await new SessionSecretStore().HasApiKeyAsync(default)); await x.Secrets.DeleteApiKeyAsync(default); Assert.False(await x.Secrets.HasApiKeyAsync(default));
        Assert.Equal("ok", await x.Db.ReadAsync(c => c.ExecuteScalar<string>("pragma integrity_check"))); Assert.Empty(await x.Db.ReadAsync(c => c.Query<ForeignKeyError>("pragma foreign_key_check")));
        // SQLite holds an exclusive Windows file handle. Close it before scanning the
        // on-disk database as well as the remaining files; do not omit the DB from QA.
        x.Db.Dispose();
        foreach (var file in Directory.EnumerateFiles(x.Root, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))) Assert.False(System.Text.Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file)).Contains(key, StringComparison.Ordinal));
    }
    [Fact]
    public async Task CancellationInsideTransactionRollsBackReceiptOriginAndAllQuotes()
    {
        await using var x = await AiTestFixture.Create(); var command = x.Command(await x.Scope()); var before = await x.Hash();
        using var cancel = new CancellationTokenSource(); x.Faults.Action = point => { if (point == "ai.before-commit") cancel.Cancel(); };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => x.Quotes.SaveAiBatchAsync(command, cancel.Token)); Assert.Equal(before, await x.Hash());
    }
    [Fact]
    public async Task ReceiptReplayRejectsDifferentScopeAndDirectInvalidRanges()
    {
        await using var x = await AiTestFixture.Create(); var command = x.Command(await x.Scope()); await x.Quotes.SaveAiBatchAsync(command, default); var before = await x.Hash();
        var anotherScope = await x.Scope(0, 10); var conflict = x.Command(anotherScope, command.BatchId);
        await Assert.ThrowsAsync<AiException>(() => x.Quotes.SaveAiBatchAsync(conflict, default)); Assert.Equal(before, await x.Hash());
        var forged = x.Command(await x.Scope()) with { Quotes = [new(x.Range(0, 11), "春天的河流缓缓流过。", "")] };
        await Assert.ThrowsAsync<AiException>(() => x.Quotes.SaveAiBatchAsync(forged, default)); Assert.Equal(before, await x.Hash());
    }
    [Fact]
    public async Task ActualEpubImagesSplitAiSegmentsAndCannotBecomeJoinedOriginalQuotes()
    {
        var root = Path.Combine(Path.GetTempPath(), "ReadJournalP8Images", Guid.NewGuid().ToString("N"));
        try
        {
            var source = EpubTestBooks.Write(Path.Combine(root, "source"), "large-image"); using var db = new DatabaseService(new DataPaths(Path.Combine(root, "data")));
            var imported = await EbookRuntime.CreateLibrary(db).ImportAsync(new(source), null, default); Assert.Equal(ImportState.Imported, imported.State);
            var rows = await db.ReadAsync(BackupDataStore.Snapshot); var book = rows.Books.Single().Book; var key = ReaderContractValidator.ReadDocument(rows.Progress.Single().DocumentKeyJson);
            var docs = new ReaderDocumentService(db, db.Coordinator); var identity = new ReaderOpenIdentity(book.Id, book.BookUid, key, db.Coordinator.CurrentEpoch, Guid.NewGuid(), 1); var document = await docs.OpenAsync(identity, default);
            var sessions = new ReaderSessionService(db, db.Coordinator, docs, new EbookProgressMaintenance(db, db.Coordinator), new(), new()); var now = DateTimeOffset.Now;
            var start = new ReaderPosition(key, document.Chapters[0].Key, 0); var context = await sessions.BeginAsync(identity, start, now, default);
            try
            {
                var chapter = document.Chapters[0]; var factory = new AiScopeFactory(docs, Mock.Of<IReaderLayoutService>());
                var scope = await factory.SelectionAsync(context, new(start, start with { Utf16Offset = chapter.Utf16Length }), default);
                Assert.True(scope.Original.Spans.Count > 1); Assert.All(scope.Original.Spans, span => Assert.DoesNotContain("\ufffc", span.Text));
                var crossing = "图片前的真实原文。\n图片后的真实原文。";
                var result = new AiQuoteVerifier().Verify(scope, [new(crossing, "不应接受", null, null), new("图片后的真实原文。", "合法", null, null)]);
                Assert.Single(result.Accepted); Assert.Single(result.RejectionReasons);
                var storage = new QuoteStorage(db, new ReadingService(), sessions, docs, db.Coordinator); var batch = await storage.SaveAiBatchAsync(new(Guid.NewGuid(), context, scope.ScopeSha256, result.Accepted, result.RejectionReasons.Count, scope), default);
                Assert.Single(batch.InsertedQuoteIds); var quoteService = new ReaderQuoteService(db, db.Coordinator, docs, sessions, new ReadingService(), new()); Assert.True((await quoteService.ResolveQuoteAsync(batch.InsertedQuoteIds[0], default)).IsExact);
            }
            finally { await sessions.CloseAsync(new(context, start, start, now, DateTimeOffset.Now, false, ReaderExitReason.Back), default); }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public async Task LostCommitAcknowledgementRetriesSameReceiptWithoutAnotherHttpCall()
    {
        await using var x = await AiTestFixture.Create(); var wrapper = new Mock<IQuoteStorage>(); var fail = true;
        wrapper.Setup(q => q.SaveAiBatchAsync(It.IsAny<VerifiedAiBatchCommand>(), It.IsAny<CancellationToken>())).Returns(async (VerifiedAiBatchCommand command, CancellationToken ct) =>
        { var result = await x.Quotes.SaveAiBatchAsync(command, ct); if (fail) { fail = false; throw new IOException("synthetic lost acknowledgement"); } return result; });
        var service = new ReaderAiService(x.Client, x.Settings, new AiQuoteVerifier(), wrapper.Object, x.Sessions, x.Db.Coordinator, x.Documents);
        var scope = await x.Scope(); var pending = await Assert.ThrowsAsync<AiSavePendingException>(() => service.GenerateAndSaveQuotesAsync(scope, default));
        Assert.Equal(1, await x.QuoteCount()); Assert.Equal(1, x.Client.Count);
        var retried = await service.RetrySaveAsync(pending.BatchId, x.Context, default); Assert.Single(retried.InsertedQuoteIds); Assert.Equal(1, await x.QuoteCount()); Assert.Equal(1, x.Client.Count);
    }
    private sealed class ForeignKeyError { public string table { get; set; } = ""; }
}
