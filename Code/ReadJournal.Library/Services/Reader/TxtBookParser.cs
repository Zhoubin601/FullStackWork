using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Backup;
namespace ReadJournal.Library.Services.Reader;

// Canonical v1 preserves every decoded character except the source BOM and CR/CRLF -> LF.
// Only a bounded block and chapter metadata are retained; chapter bytes are written incrementally.
public sealed class TxtBookParser(ManagedBookFiles files, TxtParserLimits limits) : IBookParser
{
    public EbookFormat Format => EbookFormat.Txt;
    public Task<ParsedBook> ParseAsync(string stagedSourcePath, string workspacePath, ParserOptions options, CancellationToken ct)
        => Task.Run(() => Parse(stagedSourcePath, workspacePath, options, ct), ct);
    private ParsedBook Parse(string source, string workspace, ParserOptions options, CancellationToken ct)
    {
        if (options.ParserVersion != 1 || new FileInfo(source).Length > Math.Min(options.MaxSourceBytes, 104857600))
            throw new ArgumentException("LimitExceeded：TXT版本或源文件大小不支持。");
        var prefix = Path.GetRelativePath(workspace, Path.GetDirectoryName(source)!).Replace(Path.DirectorySeparatorChar, '/') + "/";
        ManagedBookFiles.ValidateRelative(prefix.TrimEnd('/'));
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var name = options.RequestedEncoding?.ToLowerInvariant() ?? "utf-8";
        var encoding = name switch
        {
            "utf-8" or "utf8" => new UTF8Encoding(false, true),
            "gb18030" => Encoding.GetEncoding("GB18030", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback),
            _ => throw new ArgumentException("EncodingRequired：请选择UTF-8或明确指定GB18030。")
        };
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        var signature = new byte[3]; var signatureSize = input.Read(signature); input.Position = 0;
        if (signatureSize == 3 && signature.SequenceEqual(new byte[] { 0xef, 0xbb, 0xbf }))
        {
            if (name == "gb18030") throw new ArgumentException("EncodingRequired：文件含UTF-8 BOM，请选择UTF-8。");
            input.Position = 3;
        }
        using var reader = new StreamReader(input, encoding, false, 4096, true);
        var title = string.IsNullOrWhiteSpace(options.Title) ? Path.GetFileNameWithoutExtension(source) : options.Title.Trim();
        if (title.Length > 120) title = title[..120];
        var chapters = new List<CanonicalChapter>(); var blocks = new List<ReaderBlockDescriptor>();
        var pending = new StringBuilder(); var chapterLength = 0; var blockCount = 0; long total = 0, expanded = 0;
        string chapterTitle = "正文", chapterPath = "", chapterKey = ""; FileStream? output = null;
        var utf16 = new UnicodeEncoding(false, false, true); var lineStart = true;
        void CloseChapter()
        {
            if (output is null) return;
            output.Flush(true); output.Dispose(); output = null;
            var full = ManagedBookFiles.SafePath(workspace, prefix + chapterPath);
            chapters.Add(new(chapterKey, chapterTitle, chapters.Count, chapterPath, chapterLength, ManagedBookFiles.HashFile(full), blocks.ToArray()));
            blocks.Clear(); chapterLength = 0;
        }
        void WriteBlock(string value, bool endsLine)
        {
            ct.ThrowIfCancellationRequested();
            var heading = lineStart && endsLine && value.Length <= 121 && Regex.IsMatch(value.Trim(), @"^(第[\p{L}\p{N}〇零一二三四五六七八九十百千万]+[章回节卷部].*|[Cc][Hh][Aa][Pp][Tt][Ee][Rr]\s+\S+.*|序章|前言|序言|尾声|后记)$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            if (output is not null && ((heading && chapterLength > 0) || chapterLength + value.Length > limits.MaxChapterUtf16)) CloseChapter();
            if (output is null)
            {
                if (chapters.Count >= limits.MaxChapters) throw new ArgumentException("LimitExceeded：TXT章节数超限。");
                chapterKey = "ch-" + (chapters.Count + 1).ToString("D4", CultureInfo.InvariantCulture);
                chapterTitle = heading ? value.Trim() : chapters.Count == 0 ? "正文" : "正文（续）";
                chapterPath = "chapters/" + chapterKey + ".utf16";
                var full = ManagedBookFiles.SafePath(workspace, prefix + chapterPath); files.CreateDirectory(Path.GetDirectoryName(full)!);
                output = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            }
            if (++blockCount > limits.MaxBlocks || (total += value.Length) > limits.MaxTextUtf16 || (expanded += (long)value.Length * 2) > options.MaxExpandedBytes)
                throw new ArgumentException("LimitExceeded：TXT正文或块数超限。");
            output.Write(utf16.GetBytes(value));
            blocks.Add(new("b-" + blockCount.ToString(CultureInfo.InvariantCulture), heading ? ReaderBlockKind.Heading : ReaderBlockKind.Paragraph,
                chapterLength, value.Length, null, heading ? [new(chapterLength, value.Length, true, false, 1)] : []));
            chapterLength += value.Length; lineStart = endsLine; files.Checkpoint("import.parsing");
        }
        void Flush(bool final)
        {
            while (pending.Length > 0 && (final || pending.Length > limits.MaxBlockUtf16 + 1026))
            {
                var value = pending.ToString(); var boundaries = StringInfo.ParseCombiningCharacters(value).Append(value.Length).ToArray();
                if (boundaries.Zip(boundaries.Skip(1), (a, b) => b - a).Any(n => n > 1024)) throw new ArgumentException("LimitExceeded：TXT单字符簇超限。");
                var take = boundaries.LastOrDefault(n => n <= limits.MaxBlockUtf16);
                if (take == 0) throw new ArgumentException("LimitExceeded：TXT字符簇无法安全分块。");
                WriteBlock(value[..take], value[take - 1] == '\n'); pending.Remove(0, take);
            }
        }
        var buffer = new char[4096]; var previousCr = false;
        try
        {
            int count;
            while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                for (var i = 0; i < count; i++)
                {
                    var c = buffer[i];
                    if (c == '\n' && previousCr) { previousCr = false; continue; }
                    previousCr = c == '\r'; if (previousCr) c = '\n';
                    if (c == '\ufffc' || c == '\ufeff' || char.IsControl(c) && c is not ('\n' or '\t')) throw new ArgumentException("InvalidFormat：TXT包含二进制控制字符或保留占位符。");
                    pending.Append(c);
                    if (c == '\n') Flush(true); else Flush(false);
                }
            }
            Flush(true); CloseChapter();
        }
        catch (DecoderFallbackException ex)
        { throw new ArgumentException(name == "gb18030" ? "InvalidFormat：GB18030字节无效，原件未改变。" : "EncodingRequired：不是合法UTF-8；如为GB18030，请明确选择该编码重试。", ex); }
        finally { output?.Dispose(); }
        if (total == 0) throw new ArgumentException("InvalidFormat：TXT正文为空。");
        var manifest = new CanonicalManifest(1, title, chapters, [], total,
            chapters.Select(c => new CanonicalTocEntry(c.Key, c.Title, c.Key, 0, null, [])).ToArray());
        var bytes = BackupJson.Write(manifest);
        if (bytes.Length > 16777216 || chapters.Count + 2 > options.MaxArchiveEntries) throw new ArgumentException("LimitExceeded：TXT清单或条目数超限。");
        var relative = prefix + "document-v1.json"; files.WriteDurable(ManagedBookFiles.SafePath(workspace, relative), bytes);
        return new(EbookFormat.Txt, title, "", relative, ManagedBookFiles.Sha256(bytes), 1, []);
    }
}
