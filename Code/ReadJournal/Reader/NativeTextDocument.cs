using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ReadJournal.Reader;

internal sealed record Chapter(string Key, string Title, int Start, int End);
internal sealed record Section(int Index, int ChapterIndex, int Start, int End);

// Experimental canonical v1: immutable LF text and UTF-16 offsets, independent of UI pages.
internal sealed class NativeTextDocument
{
    public string Text { get; }
    public string Hash { get; }
    public int[] Boundaries { get; }
    public IReadOnlyList<Chapter> Chapters { get; }
    public IReadOnlyList<Section> Sections { get; }

    public NativeTextDocument(string text, IReadOnlyList<Chapter> canonicalChapters)
    {
        Text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        if (Text.Length == 0) throw new ArgumentException("正文为空。");
        if (Text.Length > 20_000_000) throw new ArgumentException("实验正文上限为 2000 万 UTF-16 单元。");
        Hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Text))).ToLowerInvariant();
        Boundaries = StringInfo.ParseCombiningCharacters(Text).Append(Text.Length).ToArray();
        for (var i = 1; i < Boundaries.Length; i++)
            if (Boundaries[i] - Boundaries[i - 1] > 1024) throw new ArgumentException("单个字符簇过长，无法安全排版。");
        Chapters = canonicalChapters;
        if (Chapters.Count == 0 || Chapters.First().Start != 0 || Chapters.Last().End != Text.Length ||
            Chapters.Zip(Chapters.Skip(1), (a, b) => a.End == b.Start).Any(x => !x))
            throw new ArgumentException("章节范围不连续。");
        var sections = new List<Section>();
        for (var chapter = 0; chapter < Chapters.Count; chapter++)
        {
            var start = Chapters[chapter].Start;
            while (start < Chapters[chapter].End)
            {
                var end = Snap(Math.Min(Chapters[chapter].End, start + 4096));
                // Prefer a paragraph boundary in the latter half; long paragraphs stay bounded.
                if (end < Chapters[chapter].End)
                {
                    var newline = Text.LastIndexOf('\n', end - 1, end - start);
                    if (newline >= start + 2048) end = newline + 1;
                }
                if (end <= start) throw new InvalidOperationException("正文分块未推进。");
                sections.Add(new Section(sections.Count, chapter, start, end));
                start = end;
            }
        }
        Sections = sections;
    }

    public int Snap(int offset, bool forward = false)
    {
        offset = Math.Clamp(offset, 0, Text.Length);
        var i = Array.BinarySearch(Boundaries, offset);
        return i >= 0 ? offset : Boundaries[forward ? ~i : Math.Max(0, ~i - 1)];
    }
    public bool IsBoundary(int offset) => Array.BinarySearch(Boundaries, offset) >= 0;
    public int SectionAt(int offset)
    {
        offset = Math.Clamp(offset, 0, Text.Length - 1);
        var lo = 0; var hi = Sections.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (Sections[mid].End <= offset) lo = mid + 1; else hi = mid;
        }
        return lo;
    }
    public string Extract(int a, int b) => Text[Math.Min(Snap(a), Snap(b))..Math.Max(Snap(a), Snap(b))];

}
