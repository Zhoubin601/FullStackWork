using System.Globalization;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.Services.Ai;

public sealed class AiQuoteVerifier : IAiQuoteVerifier
{
    public QuoteValidationResult Verify(AiScopeSnapshot scope, IReadOnlyList<AiQuoteCandidate> candidates)
    {
        AiIntegrity.Check(scope);
        if (candidates.Count > AiIntegrity.MaxCandidates) throw new AiException("InvalidAiResponse", "返回候选数量超过限额。");
        var accepted = new List<VerifiedQuote>(); var rejected = new List<string>();
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.OriginalText) || candidate.OriginalText.Length > 8000 || candidate.Explanation is null || candidate.Explanation.Length > 4000 ||
                candidate.PrefixHint?.Length > 256 || candidate.SuffixHint?.Length > 256)
            { rejected.Add("候选为空或超过限额。"); continue; }
            var matches = new List<DocumentRange>();
            foreach (var span in scope.Original.Spans)
            {
                var boundaries = StringInfo.ParseCombiningCharacters(span.Text).Append(span.Text.Length).ToHashSet();
                var at = 0;
                while (at <= span.Text.Length - candidate.OriginalText.Length)
                {
                    var found = span.Text.IndexOf(candidate.OriginalText, at, StringComparison.Ordinal); if (found < 0) break;
                    var end = found + candidate.OriginalText.Length; at = found + 1;
                    if (!boundaries.Contains(found) || !boundaries.Contains(end)) continue;
                    if (!string.IsNullOrEmpty(candidate.PrefixHint) && !span.Text[..found].EndsWith(candidate.PrefixHint, StringComparison.Ordinal)) continue;
                    if (!string.IsNullOrEmpty(candidate.SuffixHint) && !span.Text[end..].StartsWith(candidate.SuffixHint, StringComparison.Ordinal)) continue;
                    matches.Add(new(span.Range.Start with { Utf16Offset = span.Range.Start.Utf16Offset + found }, span.Range.Start with { Utf16Offset = span.Range.Start.Utf16Offset + end }));
                }
            }
            if (matches.Count != 1) { rejected.Add(matches.Count == 0 ? "未在所选连续原文中找到原句。" : "原句重复，缺少可唯一定位的上下文。"); continue; }
            accepted.Add(new(matches[0], candidate.OriginalText, candidate.Explanation));
        }
        return new(accepted.AsReadOnly(), rejected.AsReadOnly());
    }
}
