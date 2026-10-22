using System.Globalization;
using System.Text;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Reader;
namespace ReadJournal.Library.Services.Backup;

public static class ManagedBookInventory
{
    public static IReadOnlyList<BackupFileEntry> Inspect(string root, BackupDataV2 data, BackupLimits limits, bool entireManagedRoot = true)
    {
        var inventory = new Dictionary<string, BackupFileEntry>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        void Add(string relative, string? hash = null, long? size = null)
        {
            var full = ManagedBookFiles.SafePath(root, relative);
            if (!File.Exists(full)) throw new ArgumentException("FileMissing：受管文件缺失：" + relative);
            var length = new FileInfo(full).Length;
            if (length > limits.MaxEntryBytes || length < 0 || (size.HasValue && length != size)) throw new ArgumentException("LimitExceeded：文件长度无效。");
            var actual = ManagedBookFiles.HashFile(full);
            if (hash is not null && actual != hash) throw new ArgumentException("DocumentMismatch：文件摘要不一致：" + relative);
            if (!inventory.TryAdd(relative, new(relative, length, actual))) throw new ArgumentException("InvalidFormat：重复文件路径。");
            total = checked(total + length);
            if (total > limits.MaxExpandedBytes || inventory.Count > limits.MaxEntries - 2) throw new ArgumentException("LimitExceeded：受管文件总量超限。");
        }
        foreach (var file in data.Files)
        {
            var prefix = ManagedBookFiles.BookPrefix(file.FileId);
            Add(file.SourceRelativePath, file.SourceSha256, file.SourceBytes);
            var manifestPath = ManagedBookFiles.SafePath(root, file.CanonicalRelativePath);
            if (!File.Exists(manifestPath) || new FileInfo(manifestPath).Length > limits.MaxJsonBytes) throw new ArgumentException("InvalidFormat：规范化清单缺失或超限。");
            var manifest = BackupJson.Read<CanonicalManifest>(File.ReadAllBytes(manifestPath));
            if (manifest.ContractVersion != 1 || manifest.Title is null || manifest.Chapters is null || manifest.Resources is null || manifest.TableOfContents is null ||
                manifest.Chapters.Count == 0 || manifest.Chapters.Count > limits.MaxEntries || manifest.Resources.Count > limits.MaxEntries ||
                manifest.Chapters.Any(c => c is null) || manifest.Resources.Any(r => r is null) ||
                manifest.Chapters.Select(c => c.Key).Distinct().Count() != manifest.Chapters.Count || manifest.Resources.Select(r => r.Key).Distinct().Count() != manifest.Resources.Count ||
                !manifest.Chapters.Select(c => c.Order).SequenceEqual(Enumerable.Range(0, manifest.Chapters.Count))) throw new ArgumentException("InvalidFormat：规范化清单字段无效。");
            if (ManagedBookFiles.Sha256(BackupJson.Write(manifest)) != file.CanonicalSha256) throw new ArgumentException("DocumentMismatch：规范化版本摘要不一致。");
            Add(file.CanonicalRelativePath);
            var texts = new Dictionary<string, string>(); var boundaries = new Dictionary<string, int[]>(); var resources = manifest.Resources.ToDictionary(r => r.Key);
            long units = 0, rawUnits = 0;
            foreach (var chapter in manifest.Chapters)
            {
                if (string.IsNullOrWhiteSpace(chapter.Key) || chapter.Title is null || chapter.Utf16Length < 0 || chapter.Utf16Length > 20_000_000 || !chapter.TextPath.StartsWith("chapters/", StringComparison.Ordinal) || !chapter.TextPath.EndsWith(".utf16", StringComparison.Ordinal) || !ReaderContractValidator.IsSha256(chapter.Sha256)) throw new ArgumentException("InvalidFormat：章节字段无效。");
                rawUnits = checked(rawUnits + chapter.Utf16Length);
                if (rawUnits > 20_000_000) throw new ArgumentException("LimitExceeded：单文档正文超限。");
                Add(prefix + chapter.TextPath, chapter.Sha256, (long)chapter.Utf16Length * 2);
                var text = new UnicodeEncoding(false, false, true).GetString(File.ReadAllBytes(ManagedBookFiles.SafePath(root, prefix + chapter.TextPath)));
                if (text.Contains('\r') || text.StartsWith('\ufeff')) throw new ArgumentException("InvalidFormat：正文不是规范化LF/无BOM文本。");
                var breaks = StringInfo.ParseCombiningCharacters(text).Append(text.Length).ToArray();
                if (breaks.Zip(breaks.Skip(1), (a, b) => b - a).Any(n => n > 1024)) throw new ArgumentException("LimitExceeded：单字符簇超限。");
                texts.Add(chapter.Key, text); boundaries.Add(chapter.Key, breaks); units += text.Length - text.Count(c => c == '\ufffc');
                if (chapter.Blocks is null || chapter.Blocks.Any(b => b is null) || chapter.Blocks.Select(b => b.Key).Distinct().Count() != chapter.Blocks.Count) throw new ArgumentException("InvalidFormat：块清单无效。");
                var end = 0;
                foreach (var block in chapter.Blocks)
                {
                    if (string.IsNullOrWhiteSpace(block.Key) || !Enum.IsDefined(block.Kind) || block.StartOffset != end || block.Utf16Length <= 0 || (long)block.StartOffset + block.Utf16Length > text.Length ||
                        Array.BinarySearch(breaks, block.StartOffset) < 0 || Array.BinarySearch(breaks, block.StartOffset + block.Utf16Length) < 0 || block.Styles is null) throw new ArgumentException("InvalidFormat：块范围不连续或切断字符簇。");
                    end = block.StartOffset + block.Utf16Length;
                    if (block.Kind == ReaderBlockKind.Image)
                    {
                        if (block.Utf16Length != 1 || text[block.StartOffset] != '\ufffc' || block.Image is null || !resources.TryGetValue(block.Image.Key, out var image) || image.Sha256 != block.Image.Sha256 || image.PixelWidth != block.Image.PixelWidth || image.PixelHeight != block.Image.PixelHeight || image.MediaType != block.Image.MediaType) throw new ArgumentException("InvalidFormat：图片块与资源不一致。");
                    }
                    else if (block.Image is not null || text[block.StartOffset..end].Contains('\ufffc')) throw new ArgumentException("InvalidFormat：图片占位未映射。");
                    foreach (var style in block.Styles)
                        if (style is null || style.StartOffset < block.StartOffset || style.Utf16Length <= 0 || (long)style.StartOffset + style.Utf16Length > end || style.HeadingLevel is < 0 or > 6 || Array.BinarySearch(breaks, style.StartOffset) < 0 || Array.BinarySearch(breaks, style.StartOffset + style.Utf16Length) < 0) throw new ArgumentException("InvalidFormat：样式范围无效。");
                }
                if (end != text.Length) throw new ArgumentException("InvalidFormat：块未覆盖完整章节。");
            }
            if (units != manifest.TotalTextUnits || units <= 0) throw new ArgumentException("InvalidFormat：正文字数不一致或为空。");
            foreach (var resource in resources.Values)
            {
                if (string.IsNullOrWhiteSpace(resource.Key) || !resource.Path.StartsWith("resources/", StringComparison.Ordinal) || resource.MediaType is not ("image/png" or "image/jpeg" or "image/gif" or "image/webp") ||
                    resource.PixelWidth <= 0 || resource.PixelHeight <= 0 || (long)resource.PixelWidth * resource.PixelHeight > limits.MaxImagePixels || !ReaderContractValidator.IsSha256(resource.Sha256)) throw new ArgumentException("LimitExceeded：资源格式或像素预算无效。");
                ImageResourceValidator.Validate(ManagedBookFiles.SafePath(root, prefix + resource.Path), resource);
                Add(prefix + resource.Path, resource.Sha256);
            }
            var key = new DocumentKey(file.FileId, file.SourceSha256, file.CanonicalSha256, file.ParserVersion);
            void Position(ReaderPosition p)
            {
                ReaderContractValidator.Validate(p);
                if (p.Document != key || !texts.TryGetValue(p.ChapterKey, out var value) || p.Utf16Offset > value.Length || Array.BinarySearch(boundaries[p.ChapterKey], p.Utf16Offset) < 0) throw new ArgumentException("InvalidFormat：位置超出章节或切断字符簇。");
                if (p.NonTextBlockKey is string blockKey && !manifest.Chapters.Single(c => c.Key == p.ChapterKey).Blocks.Any(b => b.Key == blockKey && b.Kind == ReaderBlockKind.Image && b.StartOffset == p.Utf16Offset)) throw new ArgumentException("InvalidFormat：图片亲和位置无效。");
            }
            int tocCount = 0;
            void Toc(IReadOnlyList<CanonicalTocEntry> entries, int depth)
            {
                if (depth > 32) throw new ArgumentException("LimitExceeded：目录层级过深。");
                foreach (var toc in entries)
                {
                    if (toc is null || string.IsNullOrWhiteSpace(toc.Key) || toc.Title is null || toc.Children is null || ++tocCount > limits.MaxEntries) throw new ArgumentException("InvalidFormat：目录无效。");
                    Position(new(key, toc.ChapterKey, toc.Utf16Offset, toc.NonTextBlockKey)); Toc(toc.Children, depth + 1);
                }
            }
            Toc(manifest.TableOfContents, 0);
            double Percent(ReaderPosition position)
            {
                Position(position); var chapter = manifest.Chapters.Single(c => c.Key == position.ChapterKey);
                var preceding = manifest.Chapters.Take(chapter.Order).Sum(c => (long)texts[c.Key].Length - texts[c.Key].Count(ch => ch == '\ufffc'));
                var prefixText = texts[chapter.Key][..position.Utf16Offset];
                return 100.0 * (preceding + prefixText.Length - prefixText.Count(ch => ch == '\ufffc')) / manifest.TotalTextUnits;
            }
            foreach (var p in data.Progress.Where(p => p.BookId == file.BookId))
                if (Math.Abs(Percent(ReaderContractValidator.ReadPosition(p.LastPositionJson)) - p.CurrentPercent) > .000001) throw new ArgumentException("InvalidFormat：正文位置与百分比不一致。");
            foreach (var s in data.Sessions.Where(s => s.BookId == file.BookId))
                if (Math.Abs(Percent(ReaderContractValidator.ReadPosition(s.StartPositionJson)) - s.StartPercent) > .000001 || Math.Abs(Percent(ReaderContractValidator.ReadPosition(s.EndPositionJson)) - s.EndPercent) > .000001) throw new ArgumentException("InvalidFormat：会话位置与百分比不一致。");
            var quoteIds = data.Quotes.Where(q => q.Quote.BookId == file.BookId).Select(q => q.Quote.Id).ToHashSet();
            foreach (var origin in data.Origins.Where(o => quoteIds.Contains(o.QuoteId) && o.DocumentKeyJson is not null && ReaderContractValidator.ReadDocument(o.DocumentKeyJson) == key))
            {
                var a = ReaderContractValidator.ReadPosition(origin.StartPositionJson!); var b = ReaderContractValidator.ReadPosition(origin.EndPositionJson!); Position(a); Position(b);
                var first = manifest.Chapters.Single(c => c.Key == a.ChapterKey).Order; var last = manifest.Chapters.Single(c => c.Key == b.ChapterKey).Order;
                if (first > last || (first == last && a.Utf16Offset >= b.Utf16Offset)) throw new ArgumentException("InvalidFormat：书摘范围逆序。");
                var original = string.Concat(manifest.Chapters.Skip(first).Take(last - first + 1).Select(c => texts[c.Key][(c.Order == first ? a.Utf16Offset : 0)..(c.Order == last ? b.Utf16Offset : texts[c.Key].Length)])).Replace("\ufffc", "");
                if (original != origin.OriginalText) throw new ArgumentException("DocumentMismatch：书摘原句与范围不一致。");
            }
        }
        // Refuse orphan content instead of silently discarding it during whole-root replacement.
        var managed = ManagedBookFiles.SafePath(root, "books/managed");
        var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Walk(string directory)
        {
            ManagedBookFiles.RejectLink(directory);
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                ManagedBookFiles.RejectLink(entry);
                if (Directory.Exists(entry)) Walk(entry);
                else { actual.Add(Path.GetRelativePath(root, entry).Replace(Path.DirectorySeparatorChar, '/')); if (actual.Count > limits.MaxEntries) throw new ArgumentException("LimitExceeded：文件数超限。"); }
            }
        }
        if (entireManagedRoot) { if (Directory.Exists(managed)) Walk(managed); }
        else foreach (var file in data.Files)
        { var directory = ManagedBookFiles.SafePath(root, ManagedBookFiles.BookPrefix(file.FileId).TrimEnd('/')); if (Directory.Exists(directory)) Walk(directory); }
        if (!actual.SetEquals(inventory.Keys)) throw new ArgumentException("InvalidFormat：受管目录含未登记文件，需先修复文件清单。");
        return inventory.Values.OrderBy(x => x.Path, StringComparer.Ordinal).ToArray();
    }
    public static bool Same(IReadOnlyList<BackupFileEntry> a, IReadOnlyList<BackupFileEntry> b) => a.OrderBy(x => x.Path, StringComparer.Ordinal).SequenceEqual(b.OrderBy(x => x.Path, StringComparer.Ordinal));
    public static void Verify(string root, IReadOnlyList<BackupFileEntry> files)
    {
        foreach (var f in files)
        {
            var path = ManagedBookFiles.SafePath(root, f.Path);
            if (!File.Exists(path) || new FileInfo(path).Length != f.Bytes || ManagedBookFiles.HashFile(path) != f.Sha256) throw new IOException("RecoveryRequired：文件摘要无法确认，请保留 staging/trash/backups 后重试。");
        }
    }
}
