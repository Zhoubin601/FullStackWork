using CommunityToolkit.Mvvm.Input;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Ai;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.ViewModels;

public sealed partial class ReaderViewModel
{
    private AiScopeFactory? _aiScopes; private IReaderAiService? _ai; private IAiSettingsStorage? _aiSettings;
    private CancellationTokenSource? _aiCancellation; private int _aiVersion; private bool _aiBusy, _aiPreparing;
    private string _aiStatus = "请先在设置中配置 DeepSeek 和会话密钥。生成书摘会自动入库，分析与解释只显示结果。", _aiResult = "", _aiScope = "当前页", _aiFirst = "", _aiLast = "";
    private Guid? _aiBatch, _aiPending; private DataEpoch _aiBatchEpoch;
    public string[] AiScopes { get; } = ["当前页", "当前选区", "全书页范围"];
    public string AiScope { get => _aiScope; set => SetProperty(ref _aiScope, value); }
    public string AiFirstPage { get => _aiFirst; set => SetProperty(ref _aiFirst, value); }
    public string AiLastPage { get => _aiLast; set => SetProperty(ref _aiLast, value); }
    public bool IsAiBusy { get => _aiBusy; private set => SetProperty(ref _aiBusy, value); }
    public string AiStatus { get => _aiStatus; private set => SetProperty(ref _aiStatus, value); }
    public string AiResult { get => _aiResult; private set => SetProperty(ref _aiResult, value); }
    public bool CanRetryAiSave => _aiPending is not null && !IsAiBusy;
    public bool CanUndoAi => _aiBatch is not null && !IsAiBusy;
    public IAsyncRelayCommand AnalyzeCommand { get; private set; } = null!;
    public IAsyncRelayCommand ExplainCommand { get; private set; } = null!;
    public IAsyncRelayCommand GenerateQuotesCommand { get; private set; } = null!;
    public IRelayCommand CancelAiCommand { get; private set; } = null!;
    public IAsyncRelayCommand RetryAiSaveCommand { get; private set; } = null!;
    public IAsyncRelayCommand UndoAiCommand { get; private set; } = null!;
    private void InitializeAi(AiScopeFactory? scopes, IReaderAiService? ai, IAiSettingsStorage? settings)
    {
        _aiScopes = scopes; _ai = ai; _aiSettings = settings;
        AnalyzeCommand = new AsyncRelayCommand(() => RunAiAsync(AiOperation.Analyze));
        ExplainCommand = new AsyncRelayCommand(() => RunAiAsync(AiOperation.ExplainSelection));
        GenerateQuotesCommand = new AsyncRelayCommand(() => RunAiAsync(AiOperation.ExtractQuotes));
        CancelAiCommand = new RelayCommand(CancelAi);
        RetryAiSaveCommand = new AsyncRelayCommand(() => RunAiActionAsync(async ct =>
        { if (_aiPending is not Guid batch || _context is null || _ai is null) return; var context = _context; CompleteBatch(await _ai.RetrySaveAsync(batch, context, ct), context, ct); }));
        UndoAiCommand = new AsyncRelayCommand(() => RunAiActionAsync(async ct =>
        {
            if (_aiBatch is not Guid batch || _ai is null) return;
            var result = await _ai.UndoBatchAsync(batch, _aiBatchEpoch, ct);
            ct.ThrowIfCancellationRequested(); AiStatus = $"撤销完成：删除{result.DeletedCount}条，保留已修改或收藏{result.RetainedModifiedCount}条。"; _aiBatch = null;
        }));
    }
    private void AiButtons() { OnPropertyChanged(nameof(CanRetryAiSave)); OnPropertyChanged(nameof(CanUndoAi)); }
    private void CompleteBatch(QuoteBatchResult batch, ReaderContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); if (_context != context || Phase != ReaderPhase.Active) return;
        _aiPending = null; _aiBatch = batch.InsertedQuoteIds.Count > 0 && !batch.WasAlreadyUndone ? batch.BatchId : null;
        _aiBatchEpoch = _context!.Identity.Epoch;
        AiStatus = batch.WasAlreadyUndone ? "此批次已撤销，未重新入库。" : $"已自动保存{batch.InsertedQuoteIds.Count}条，重复{batch.DuplicateCount}条，拒绝{batch.RejectedCount}条。";
        AiResult = "已保存原句与所选正文逐字一致；AI解读请自行判断。可在书摘收藏中编辑或回跳。\n" + UsageText(batch.InputTokens, batch.OutputTokens) + (batch.RejectionReasons is { Count: > 0 } reasons ? "\n拒绝原因：\n" + string.Join("\n", reasons.GroupBy(r => r).Select(g => $"{g.Key}（{g.Count()}条）")) : "");
    }
    private static string UsageText(int? input, int? output) =>
        $"服务报告 token：输入{input?.ToString() ?? "未提供"}，输出{output?.ToString() ?? "未提供"}；费用以供应商账单为准。";
    private Task RunAiAsync(AiOperation operation) => RunAiActionAsync(async ct =>
    {
        if (_context is null || _aiScopes is null || _ai is null || _aiSettings is null || _layout is null || Page is null || Phase != ReaderPhase.Active) return;
        var aiVersion = _aiVersion; var context = _context; var layout = _layout; var page = Page; var selection = Selection; var mode = AiScope;
        _aiPreparing = true; AiStatus = "正在冻结正文范围；页索引未就绪时可取消。";
        AiScopeSnapshot scope;
        try
        {
            if (operation == AiOperation.ExplainSelection || mode == "当前选区")
                scope = await _aiScopes.SelectionAsync(context, selection ?? throw new AiException("SelectionRequired", "请先选择正文。"), ct);
            else if (mode == "全书页范围")
            {
                if (!int.TryParse(AiFirstPage, out var first) || !int.TryParse(AiLastPage, out var last)) throw new AiException("InvalidPages", "请输入起止全书页码，重排后须重新填写。");
                scope = await _aiScopes.PagesAsync(context, layout, new(first, last), ct);
            }
            else scope = await _aiScopes.CurrentPageAsync(context, page, ct);
        }
        finally { if (aiVersion == _aiVersion) _aiPreparing = false; }
        ct.ThrowIfCancellationRequested(); var config = await _aiSettings.GetAsync(ct); ct.ThrowIfCancellationRequested();
        const string modeLabel = "将上传所选正文至 DeepSeek，可能产生费用";
        AiStatus = $"{scope.DisplayScope}，{scope.Original.Spans.Sum(s => s.Text.Length)}字符，模型{config.Model}，输入上限{config.MaxInputUtf16Units}字符；{modeLabel}。";
        var confirmed = _generalAlerts is not null && await _generalAlerts.ConfirmAsync(operation == AiOperation.ExtractQuotes ? "生成并自动保存书摘" : "确认分析范围", AiStatus + (operation == AiOperation.ExtractQuotes ? "\n合法原句将自动保存，重复项跳过；可撤销本批未修改且未收藏的新增项。是否继续？" : "\n只显示结果，不写入书摘。是否继续？"));
        ct.ThrowIfCancellationRequested();
        if (!confirmed) { AiStatus = "已取消本次生成。"; return; }
        ct.ThrowIfCancellationRequested();
        if (context != _context || Phase != ReaderPhase.Active) throw new OperationCanceledException(ct);
        AiResult = "";
        if (operation == AiOperation.ExtractQuotes) CompleteBatch(await _ai.GenerateAndSaveQuotesAsync(scope, ct), context, ct);
        else
        {
            var result = await _ai.AnalyzeAsync(scope, operation, ct); ct.ThrowIfCancellationRequested();
            AiResult = result.Analysis + "\n" + UsageText(result.InputTokens, result.OutputTokens); AiStatus = $"{result.DisplayScope}：分析完成，未写入书摘。";
        }
    });
    private async Task RunAiActionAsync(Func<CancellationToken, Task> action)
    {
        if (IsAiBusy) { AiStatus = "当前已有AI任务，请等待或取消。"; return; }
        if (_context is null || Phase != ReaderPhase.Active) return;
        var version = ++_aiVersion; var context = _context;
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token); _aiCancellation = cancellation;
        IsAiBusy = true; AiButtons();
        try { await action(cancellation.Token); }
        catch (AiSavePendingException pending) { if (version == _aiVersion && context == _context) { _aiPending = pending.BatchId; AiStatus = pending.Message; } }
        catch (Exception ex) { if (version == _aiVersion && context == _context) AiStatus = AiIntegrity.Message(ex); }
        finally
        {
            cancellation.Dispose();
            if (version == _aiVersion) { _aiCancellation = null; IsAiBusy = false; _aiPreparing = false; AiButtons(); }
        }
    }
    private void CancelAi()
    {
        _aiCancellation?.Cancel(); ++_aiVersion; _aiCancellation = null; IsAiBusy = false; _aiPreparing = false;
        if (_context is not null) _ai?.ForgetContext(_context);
        _aiPending = null; AiStatus = "已取消；未完成生成不会保存，已保存批次仍可撤销。"; AiButtons();
    }
    private void ResetAi()
    { CancelAi(); _aiBatch = null; AiResult = ""; AiFirstPage = ""; AiLastPage = ""; AiStatus = "生成书摘会自动入库；分析与解释只显示结果。"; AiButtons(); }
    private void InvalidateAiPageNumbers()
    {
        AiFirstPage = ""; AiLastPage = "";
        if (_aiPreparing) { CancelAi(); AiStatus = "排版已变化，请重新选择页码或正文范围。"; }
    }
}
