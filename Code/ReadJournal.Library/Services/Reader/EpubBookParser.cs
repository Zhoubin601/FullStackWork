using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using ReadJournal.Library.Models.Reader;
using ReadJournal.Library.Services.Backup;
using VersOne.Epub;
namespace ReadJournal.Library.Services.Reader;

// All supplier types stay behind this adapter. The ZIP is inspected before the vendor sees it.
public sealed class EpubBookParser(ManagedBookFiles files) : IBookParser
{
    public EbookFormat Format => EbookFormat.Epub;
    private static readonly Regex Spaces = new("[ \\t\\r\\n\\f]+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
    private sealed record RawStyle(int Start, int Length, bool Bold, bool Italic, int Heading);
    public Task<ParsedBook> ParseAsync(string stagedSourcePath, string workspacePath, ParserOptions options, CancellationToken ct)
        => Task.Run(async () =>
        { try { return await ParseAsyncCore(stagedSourcePath, workspacePath, options, ct); } catch (Exception ex) when (ex is InvalidDataException or XmlException or DecoderFallbackException) { throw new ArgumentException("InvalidFormat：EPUB ZIP/XML损坏或编码无效。" + ex.Message, ex); } }, ct);
    private static string ZipPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 1000 || path.StartsWith('/') || path.Contains('\\') || path.Contains(':') || path.Any(c => c < 32) || path.Split('/').Any(p => p is "" or "." or "..")) throw new ArgumentException("UnsafePath：EPUB条目路径无效或越界。");
        return path;
    }
    private static string Resolve(string owner, string href)
    {
        var path = Uri.UnescapeDataString(href.Split('#')[0]);
        if (path.Contains('%') || path.Contains('?') || path.Contains('#') || path.Contains('\\') || path.Contains(':') || path.StartsWith('/')) throw new ArgumentException("UnsafeResource：禁止外部/绝对资源和歧义路径。");
        var parts = owner.Split('/').SkipLast(1).ToList();
        if (path.Length == 0) return owner;
        foreach (var part in path.Split('/'))
        { if (part is "" or ".") continue; if (part == "..") { if (parts.Count == 0) throw new ArgumentException("UnsafePath：资源越过EPUB根目录。"); parts.RemoveAt(parts.Count - 1); } else parts.Add(part); }
        return ZipPath(string.Join('/', parts));
    }
    private static byte[] Read(ZipArchiveEntry entry, long max, CancellationToken ct)
    {
        if (entry.Length > max) throw new ArgumentException("LimitExceeded：EPUB单条目超限。");
        using var input = entry.Open(); using var output = new MemoryStream(); var buffer = new byte[16384]; int count;
        while ((count = input.Read(buffer)) > 0) { ct.ThrowIfCancellationRequested(); if (output.Length + count > max || output.Length + count > entry.Length) throw new ArgumentException("LimitExceeded：EPUB实际展开长度超限。"); output.Write(buffer, 0, count); }
        if (output.Length != entry.Length) throw new ArgumentException("InvalidFormat：EPUB条目长度不一致。"); return output.ToArray();
    }
    private static XDocument Xml(byte[] bytes)
    {
        // Accept the inert HTML5 declaration; all entity definitions/external DTDs remain prohibited.
        var text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\ufeff'); text = Regex.Replace(text, "<!DOCTYPE\\s+html\\s*>", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16777216 });
        while (reader.Read()) if (reader.Depth > 128) throw new ArgumentException("LimitExceeded：XML嵌套过深。");
        using var second = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16777216 });
        return XDocument.Load(second, LoadOptions.PreserveWhitespace);
    }
    private async Task<ParsedBook> ParseAsyncCore(string source, string workspace, ParserOptions options, CancellationToken ct)
    {
        if (options.ParserVersion != 1 || new FileInfo(source).Length > Math.Min(options.MaxSourceBytes, 104857600)) throw new ArgumentException("LimitExceeded：EPUB版本或源文件超限。");
        using var archive = ZipFile.OpenRead(source); var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long expanded = 0;
        if (archive.Entries.Count > options.MaxArchiveEntries) throw new ArgumentException("LimitExceeded：EPUB条目数超限。");
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested(); var name = entry.FullName.TrimEnd('/'); ZipPath(name);
            if (!names.Add(name) || ((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000) throw new ArgumentException("UnsafePath：EPUB重复路径或符号链接。");
            if ((expanded = checked(expanded + entry.Length)) > Math.Min(options.MaxExpandedBytes, 314572800) || entry.Length > 33554432) throw new ArgumentException("LimitExceeded：EPUB展开总量或单文件超限。");
            if (!entry.FullName.EndsWith('/')) entries.Add(name, entry);
        }
        byte[] Bytes(string path) => entries.TryGetValue(path, out var e) ? Read(e, 33554432, ct) : throw new ArgumentException("MissingResource：EPUB缺少资源：" + path);
        if (Encoding.ASCII.GetString(Bytes("mimetype")) != "application/epub+zip") throw new ArgumentException("InvalidFormat：EPUB mimetype不正确。");
        if (entries.ContainsKey("META-INF/encryption.xml") || entries.ContainsKey("META-INF/rights.xml")) throw new ArgumentException("UnsupportedFormat：DRM/加密资源暂不支持。");
        foreach (var item in entries)
            if (new[] { ".xml", ".opf", ".ncx", ".xhtml", ".html", ".htm" }.Contains(Path.GetExtension(item.Key).ToLowerInvariant())) Xml(Bytes(item.Key));
        var container = Xml(Bytes("META-INF/container.xml"));
        var opfPath = ZipPath(container.Descendants().FirstOrDefault(e => e.Name.LocalName == "rootfile")?.Attribute("full-path")?.Value ?? throw new ArgumentException("InvalidFormat：缺少OPF入口。"));
        var opf = Xml(Bytes(opfPath));
        if (opf.Descendants().Any(e => e.Name.LocalName == "meta" && ((e.Attribute("property")?.Value == "rendition:layout" && e.Value.Trim() == "pre-paginated") || e.Attribute("name")?.Value == "fixed-layout" && e.Attribute("content")?.Value == "true"))) throw new ArgumentException("UnsupportedFormat：固定版式EPUB暂不支持。");
        var manifest = opf.Descendants().Where(e => e.Name.LocalName == "item").ToDictionary(e => e.Attribute("id")?.Value ?? throw new ArgumentException("InvalidFormat：manifest缺少id。"), e => (Path: Resolve(opfPath, e.Attribute("href")?.Value ?? ""), Media: e.Attribute("media-type")?.Value ?? ""));
        foreach (var item in manifest.Values) if (!entries.ContainsKey(item.Path)) throw new ArgumentException("MissingResource：manifest资源不存在：" + item.Path);
        var expected = opf.Descendants().Where(e => e.Name.LocalName == "itemref" && e.Attribute("linear")?.Value != "no").Select(e => manifest.TryGetValue(e.Attribute("idref")?.Value ?? "", out var m) ? m.Path : throw new ArgumentException("InvalidFormat：spine引用不存在。")).ToArray();
        if (expected.Length is < 1 or > 2048 || expected.Distinct().Count() != expected.Length) throw new ArgumentException("InvalidFormat：spine为空、重复或超限。");
        if (opf.Descendants().Any(e => e.Attribute("properties")?.Value.Contains("rendition:layout-pre-paginated", StringComparison.Ordinal) == true)) throw new ArgumentException("UnsupportedFormat：固定版式spine暂不支持。");
        var warnings = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in entries.Where(e => e.Key.EndsWith(".css", StringComparison.OrdinalIgnoreCase)))
        {
            var css = Encoding.UTF8.GetString(Bytes(item.Key));
            foreach (Match match in Regex.Matches(css, "url\\(\\s*['\"]?([^)'\"\\s]+)", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200))) { var local = Resolve(item.Key, match.Groups[1].Value); if (!entries.ContainsKey(local)) throw new ArgumentException("MissingResource：CSS资源缺失。"); }
            if (css.Contains("@import", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("UnsupportedFormat：CSS @import暂不支持，禁止隐式资源加载。");
            warnings.Add("外部样式表未复刻；保留语义标签和内联基本粗斜体。");
        }
        using var book = await EpubReader.OpenBookAsync(source); ct.ThrowIfCancellationRequested();
        var order = (await book.GetReadingOrderAsync()).Where(f => expected.Contains(f.FilePath, StringComparer.Ordinal)).ToArray();
        if (!order.Select(f => f.FilePath).SequenceEqual(expected)) throw new ArgumentException("InvalidFormat：供应商阅读顺序与OPF spine不一致，拒绝丢失正文。");
        var prefix = Path.GetRelativePath(workspace, Path.GetDirectoryName(source)!).Replace(Path.DirectorySeparatorChar, '/') + "/"; ManagedBookFiles.ValidateRelative(prefix.TrimEnd('/'));
        var chapters = new List<CanonicalChapter>(); var resources = new Dictionary<string, CanonicalResource>(); var anchors = new Dictionary<string, (string Chapter, Dictionary<string, int> Ids)>();
        long totalText = 0, rawText = 0; var blocksCount = 0; var nodesCount = 0;
        foreach (var item in order)
        {
            ct.ThrowIfCancellationRequested(); var html = await item.ReadContentAsync(); ct.ThrowIfCancellationRequested();
            var dom = new HtmlParser().ParseDocument(html); var body = dom.Body ?? throw new ArgumentException("InvalidFormat：XHTML没有body。");
            foreach (var element in dom.All)
            {
                if (++nodesCount > 100000) throw new ArgumentException("LimitExceeded：HTML节点数量超限。");
                if (element.LocalName is "script" or "iframe" or "object" or "embed" or "svg" or "math" or "audio" or "video" or "canvas" || element.Attributes.Any(a => a.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("UnsupportedFormat：EPUB含脚本/嵌入内容。");
                foreach (var attribute in element.Attributes.Where(a => a.Name is "src" or "poster" or "data" || a.Name == "href" && element.LocalName == "link"))
                { var path = Resolve(item.FilePath, attribute.Value); if (!entries.ContainsKey(path)) throw new ArgumentException("MissingResource：XHTML资源不存在。"); }
                if (element.LocalName == "style" && (element.TextContent.Contains("url(", StringComparison.OrdinalIgnoreCase) || element.TextContent.Contains("@import", StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("UnsafeResource：不支持内嵌CSS资源。");
            }
            var text = new StringBuilder(); var styles = new List<RawStyle>(); var ids = new Dictionary<string, int>(StringComparer.Ordinal); var images = new Dictionary<int, ImageResourceRef>();
            var key = "sp-" + ManagedBookFiles.Sha256(Encoding.UTF8.GetBytes(item.FilePath))[..24];
            void Append(string value, bool bold, bool italic, int heading)
            {
                if (value.Length == 0) return; if (value.Contains('\ufffc') || value.Contains('\ufeff') || value.Any(c => char.IsControl(c) && c is not ('\n' or '\t'))) throw new ArgumentException("InvalidFormat：XHTML包含保留占位符或控制字符。");
                var start = text.Length; text.Append(value); if (text.Length + rawText > 20_000_000) throw new ArgumentException("LimitExceeded：EPUB正文超限。");
                if (bold || italic || heading > 0) styles.Add(new(start, value.Length, bold, italic, heading));
            }
            void Walk(INode node, bool bold, bool italic, int heading, bool pre, int depth)
            {
                ct.ThrowIfCancellationRequested(); if (depth > 128) throw new ArgumentException("LimitExceeded：HTML嵌套过深。");
                if (node is IText content)
                { var value = pre ? content.Data.Replace("\r\n", "\n").Replace('\r', '\n') : Spaces.Replace(content.Data, " "); if (!pre && string.IsNullOrWhiteSpace(value) && (text.Length == 0 || text[^1] == '\n')) return; if (!pre && (text.Length == 0 || text[^1] == '\n')) value = value.TrimStart(' '); Append(value, bold, italic, heading); return; }
                if (node is not IElement element) return; var tag = element.LocalName.ToLowerInvariant();
                if (tag is "script" or "iframe" or "object" or "embed" or "svg" or "math" or "audio" or "video" or "canvas") throw new ArgumentException("UnsupportedFormat：不支持脚本/嵌入内容：" + tag);
                if (element.Attributes.Any(a => a.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("UnsafeResource：禁止脚本事件属性。");
                if (element.HasAttribute("style"))
                {
                    var inline = element.GetAttribute("style")!;
                    if (inline.Contains("url(", StringComparison.OrdinalIgnoreCase) || inline.Contains("@import", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("UnsafeResource：内联CSS资源不支持。");
                    bold |= Regex.IsMatch(inline, "font-weight\\s*:\\s*(bold|[6-9]00)", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)); italic |= Regex.IsMatch(inline, "font-style\\s*:\\s*(italic|oblique)", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
                    warnings.Add("复杂CSS仅保留基本粗斜体，页面布局由阅读偏好决定。");
                }
                var block = tag is "p" or "div" or "section" or "article" or "blockquote" or "li" or "pre" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6";
                if (block && text.Length > 0 && text[^1] != '\n') Append("\n", false, false, 0);
                if (element.Id is { Length: > 0 } id && !ids.TryAdd(id, text.Length)) throw new ArgumentException("InvalidFormat：章内HTML id重复。");
                bold |= tag is "b" or "strong"; italic |= tag is "i" or "em"; if (tag.Length == 2 && tag[0] == 'h' && tag[1] is >= '1' and <= '6') heading = tag[1] - '0'; pre |= tag == "pre";
                if (tag == "img")
                {
                    var path = Resolve(item.FilePath, element.GetAttribute("src") ?? throw new ArgumentException("MissingResource：img缺少src。"));
                    if (!resources.TryGetValue(path, out var resource))
                    {
                        var bytes = Bytes(path); var resourceKey = "r-" + ManagedBookFiles.Sha256(Encoding.UTF8.GetBytes(path))[..24]; var relative = "resources/" + resourceKey + ".image";
                        var full = ManagedBookFiles.SafePath(workspace, prefix + relative); files.WriteDurable(full, bytes); var header = ImageResourceValidator.Inspect(full);
                        if ((long)header.Width * header.Height > options.MaxImagePixels) throw new ArgumentException("LimitExceeded：图片超过像素预算。");
                        resource = new(resourceKey, relative, header.MediaType, header.Width, header.Height, ManagedBookFiles.Sha256(bytes)); resources.Add(path, resource);
                    }
                    images.Add(text.Length, new(resource.Key, resource.PixelWidth, resource.PixelHeight, resource.MediaType, resource.Sha256)); text.Append('\ufffc'); return;
                }
                if (tag == "br") { Append("\n", bold, italic, heading); return; }
                if (tag is "style" or "head" or "link" or "meta") return;
                foreach (var child in element.ChildNodes) Walk(child, bold, italic, heading, pre, depth + 1);
                if (block && (text.Length == 0 || text[^1] != '\n' || tag == "p" && !element.HasChildNodes)) Append("\n", bold, italic, heading);
            }
            Walk(body, false, false, 0, false, 0); var value = text.ToString(); var breaks = StringInfo.ParseCombiningCharacters(value).Append(value.Length).ToArray();
            if (breaks.Zip(breaks.Skip(1), (a, b) => b - a).Any(n => n > 1024)) throw new ArgumentException("LimitExceeded：EPUB单字符簇过长。");
            int Snap(int p, bool forward) { var index = Array.BinarySearch(breaks, p); return index >= 0 ? p : breaks[forward ? ~index : Math.Max(0, ~index - 1)]; }
            var normalized = styles.Select(s => new TextStyleSpan(Snap(s.Start, false), Snap(s.Start + s.Length, true) - Snap(s.Start, false), s.Bold, s.Italic, s.Heading)).ToArray();
            var blocks = new List<ReaderBlockDescriptor>(); var offset = 0;
            while (offset < value.Length)
            {
                if (++blocksCount > 100000) throw new ArgumentException("LimitExceeded：EPUB块数量超限。");
                var isImage = images.TryGetValue(offset, out var image); var nextImage = value.IndexOf('\ufffc', offset); var end = isImage ? offset + 1 : Snap(Math.Min(value.Length, Math.Min(offset + 8192, nextImage < 0 ? value.Length : nextImage)), false);
                if (!isImage) { var newline = value.IndexOf('\n', offset, end - offset); if (newline >= 0) end = newline + 1; }
                if (end <= offset) throw new ArgumentException("InvalidFormat：EPUB分块未推进。");
                var spans = isImage ? [] : normalized.Where(s => s.StartOffset < end && s.StartOffset + s.Utf16Length > offset).Select(s => s with { StartOffset = Math.Max(offset, s.StartOffset), Utf16Length = Math.Min(end, s.StartOffset + s.Utf16Length) - Math.Max(offset, s.StartOffset) }).ToArray();
                blocks.Add(new("b-" + blocksCount, isImage ? ReaderBlockKind.Image : spans.Any(s => s.HeadingLevel > 0) ? ReaderBlockKind.Heading : ReaderBlockKind.Paragraph, offset, end - offset, image, spans)); offset = end;
            }
            var chapterTitle = dom.QuerySelector("h1,h2,title")?.TextContent.Trim() ?? "正文"; if (chapterTitle.Length > 120) chapterTitle = chapterTitle[..SnapTitle(chapterTitle, 120)];
            var textPath = "chapters/" + key + ".utf16"; var bytesText = new UnicodeEncoding(false, false, true).GetBytes(value); files.WriteDurable(ManagedBookFiles.SafePath(workspace, prefix + textPath), bytesText);
            chapters.Add(new(key, chapterTitle, chapters.Count, textPath, value.Length, ManagedBookFiles.Sha256(bytesText), blocks)); anchors.Add(item.FilePath, (key, ids.ToDictionary(i => i.Key, i => Snap(i.Value, false), StringComparer.Ordinal)));
            rawText += value.Length; totalText += value.Length - images.Count; if (rawText > 20_000_000) throw new ArgumentException("LimitExceeded：EPUB正文超限。"); files.Checkpoint("import.parsing");
        }
        if (totalText <= 0) throw new ArgumentException("InvalidFormat：EPUB没有可计量正文。");
        var tocCount = 0;
        CanonicalTocEntry? Toc(EpubNavigationItemRef item, int depth)
        {
            ct.ThrowIfCancellationRequested(); if (depth > 32 || ++tocCount > options.MaxArchiveEntries) throw new ArgumentException("LimitExceeded：目录超限。");
            var tocKey = "toc-" + tocCount;
            var children = item.NestedItems.Select(i => Toc(i, depth + 1)).Where(i => i is not null).Cast<CanonicalTocEntry>().ToArray();
            if (item.Link is null) return children.Length == 0 ? null : new(tocKey, item.Title, children[0].ChapterKey, children[0].Utf16Offset, children[0].NonTextBlockKey, children);
            if (!anchors.TryGetValue(item.Link.ContentFilePath, out var target)) { warnings.Add("目录指向非线性/非正文资源，已跳过。"); return children.Length == 0 ? null : new(tocKey, item.Title, children[0].ChapterKey, children[0].Utf16Offset, children[0].NonTextBlockKey, children); }
            var offset = 0; if (!string.IsNullOrEmpty(item.Link.Anchor) && !target.Ids.TryGetValue(Uri.UnescapeDataString(item.Link.Anchor), out offset)) throw new ArgumentException("MissingResource：目录锚点不存在。");
            var image = chapters.Single(c => c.Key == target.Chapter).Blocks.FirstOrDefault(b => b.Kind == ReaderBlockKind.Image && b.StartOffset == offset);
            return new(tocKey, item.Title, target.Chapter, offset, image?.Key, children);
        }
        var navigation = await book.GetNavigationAsync(); ct.ThrowIfCancellationRequested();
        var toc = navigation?.Select(n => Toc(n, 0)).Where(n => n is not null).Cast<CanonicalTocEntry>().ToArray() ?? [];
        if (toc.Length == 0) { warnings.Add("无可用目录，按spine章节生成目录。"); toc = chapters.Select(c => new CanonicalTocEntry("toc-" + c.Key, c.Title, c.Key, 0, null, [])).ToArray(); }
        var title = string.IsNullOrWhiteSpace(book.Title) ? options.Title ?? "EPUB" : book.Title.Trim(); if (title.Length > 120) title = title[..SnapTitle(title, 120)];
        var document = new CanonicalManifest(1, title, chapters, resources.Values.OrderBy(r => r.Key, StringComparer.Ordinal).ToArray(), totalText, toc);
        var bytesManifest = BackupJson.Write(document); files.WriteDurable(ManagedBookFiles.SafePath(workspace, prefix + "document-v1.json"), bytesManifest);
        return new(EbookFormat.Epub, title, book.Author ?? "", prefix + "document-v1.json", ManagedBookFiles.Sha256(bytesManifest), 1, warnings.Order(StringComparer.Ordinal).ToArray());
    }
    private static int SnapTitle(string title, int max) => StringInfo.ParseCombiningCharacters(title).Where(n => n <= max).DefaultIfEmpty(0).Max();
}
