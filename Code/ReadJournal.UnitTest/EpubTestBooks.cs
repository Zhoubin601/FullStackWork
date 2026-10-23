using System.IO.Compression;
using System.Text;
namespace ReadJournal.UnitTest;
public static class EpubTestBooks
{
    public static readonly string[] Supported = ["basic", "toc-order", "duplicate-titles", "nested-toc", "image-styles", "large-image", "inline-style", "unicode-path", "epub2-ncx", "long-chapter", "many-images"];
    public static readonly string[] Rejected = ["missing-image", "traversal", "remote-image", "external-css", "dtd", "script", "fixed-layout", "drm", "broken-xhtml", "oversize-image", "bad-image", "encoded-parent", "duplicate-path", "symlink", "entry-limit"];
    public static string Write(string root, string variant)
    {
        Directory.CreateDirectory(root); var path = Path.Combine(root, variant + ".epub");
        var epub2 = variant == "epub2-ncx"; var image = variant is "image-styles" or "large-image" or "missing-image" or "oversize-image" or "bad-image" or "many-images";
        var name1 = variant == "unicode-path" ? "第一章.xhtml" : "chapter1.xhtml";
        var title1 = variant == "duplicate-titles" ? "同名章节" : "第一章 起点"; var title2 = variant == "duplicate-titles" ? "同名章节" : "第二章 归途";
        var nav1 = $"<li><a href=\"{name1}#heading\">{title1}</a></li>"; var nav2 = $"<li><a href=\"chapter2.xhtml#detail\">{title2}</a></li>";
        var nav = variant == "toc-order" ? nav2 + nav1 : variant == "nested-toc" ? $"<li><a href=\"nav.xhtml\">卷一</a><ol>{nav1}{nav2}</ol></li>" : nav1 + nav2;
        var files = new Dictionary<string, byte[]>(); void Text(string name, string content) => files.Add(name, Encoding.UTF8.GetBytes(content));
        Text("mimetype", "application/epub+zip");
        Text("META-INF/container.xml", "<container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\"><rootfiles><rootfile full-path=\"OEBPS/content.opf\" media-type=\"application/oebps-package+xml\"/></rootfiles></container>");
        var imageManifest = image ? "<item id=\"picture\" href=\"picture.png\" media-type=\"image/png\"/>" : "";
        if (variant == "many-images") imageManifest += string.Concat(Enumerable.Range(1, 11).Select(i => $"<item id=\"picture{i}\" href=\"picture{i}.png\" media-type=\"image/png\"/>"));
        var navManifest = epub2 ? "<item id=\"ncx\" href=\"toc.ncx\" media-type=\"application/x-dtbncx+xml\"/>" : "<item id=\"nav\" href=\"nav.xhtml\" media-type=\"application/xhtml+xml\" properties=\"nav\"/>";
        var fixedMeta = variant == "fixed-layout" ? "<meta property=\"rendition:layout\">pre-paginated</meta>" : "";
        Text("OEBPS/content.opf", $"<package version=\"{(epub2 ? "2.0" : "3.0")}\" unique-identifier=\"bookid\" xmlns=\"http://www.idpf.org/2007/opf\"><metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:identifier id=\"bookid\">urn:readjournal:p7:{variant}</dc:identifier><dc:title>[合成P7] {variant}</dc:title><dc:language>zh-CN</dc:language><dc:creator>ReadJournal deterministic fixtures</dc:creator><meta property=\"dcterms:modified\">2026-10-22T00:00:00Z</meta>{fixedMeta}</metadata><manifest>{navManifest}<item id=\"c1\" href=\"{name1}\" media-type=\"application/xhtml+xml\"/><item id=\"c2\" href=\"chapter2.xhtml\" media-type=\"application/xhtml+xml\"/>{imageManifest}</manifest><spine{(epub2 ? " toc=\"ncx\"" : "")}><itemref idref=\"c1\"/><itemref idref=\"c2\"/></spine></package>");
        if (epub2) Text("OEBPS/toc.ncx", $"<ncx xmlns=\"http://www.daisy.org/z3986/2005/ncx/\" version=\"2005-1\"><head><meta name=\"dtb:uid\" content=\"urn:readjournal:p7:{variant}\"/></head><docTitle><text>EPUB2</text></docTitle><navMap><navPoint id=\"one\" playOrder=\"1\"><navLabel><text>第一章</text></navLabel><content src=\"{name1}#heading\"/></navPoint><navPoint id=\"two\" playOrder=\"2\"><navLabel><text>第二章</text></navLabel><content src=\"chapter2.xhtml#detail\"/></navPoint></navMap></ncx>");
        else Text("OEBPS/nav.xhtml", $"<html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:epub=\"http://www.idpf.org/2007/ops\"><head><title>目录</title></head><body><nav epub:type=\"toc\"><ol>{nav}</ol></nav></body></html>");
        var paragraph = "<p>正文星光落在书页上。相同句子。相同句子。中文标点、English café，组合 e&#x301; 与 👩‍💻。</p>";
        var extra = variant == "long-chapter" ? string.Concat(Enumerable.Repeat(paragraph, 800)) : string.Concat(Enumerable.Repeat(paragraph, 12));
        var styles = variant is "image-styles" or "inline-style" or "large-image" ? "<p><strong>粗体与<em>粗斜体</em></strong>，<i>斜体</i>。</p><p style=\"font-weight:700;font-style:italic\">内联强调</p>" : "";
        var picture = image ? "<p>图片前的真实原文。</p><img id=\"picture\" src=\"picture.png\" alt=\"确定性测试图片\"/><p>图片后的真实原文。</p>" : "";
        if (variant == "many-images") picture += string.Concat(Enumerable.Range(1, 11).Select(i => $"<img src=\"picture{i}.png\"/><p>图片{i}后的正文。</p>"));
        var payload = variant == "remote-image" ? "<img src=\"https://example.invalid/book.png\"/>" : variant == "encoded-parent" ? "<img src=\"%2e%2e/%2e%2e/%2e%2e/etc/passwd\"/>" : "";
        var head = variant == "script" ? "<script>alert('never execute')</script>" : variant == "external-css" ? "<link rel=\"stylesheet\" href=\"https://example.invalid/a.css\"/>" : "";
        var declaration = variant == "dtd" ? "<!DOCTYPE html [<!ENTITY x SYSTEM 'file:///etc/passwd'>]>" : "";
        Text("OEBPS/" + name1, declaration + $"<html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>{title1}</title>{head}</head><body><h1 id=\"heading\">{title1}</h1>{extra}{styles}{picture}{payload}</body></html>");
        Text("OEBPS/chapter2.xhtml", $"<html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>{title2}</title></head><body><h1>{title2}</h1><p id=\"detail\">第二章的真实正文，目录顺序不改变正文顺序。</p>{paragraph}</body></html>");
        if (image && variant != "missing-image") files.Add("OEBPS/picture.png", variant == "bad-image" ? [1, 2, 3] : Png(variant == "oversize-image" ? 6000 : variant == "large-image" ? 2400 : 240, variant == "oversize-image" ? 6000 : variant == "large-image" ? 1800 : 180, variant == "oversize-image"));
        if (variant == "many-images") for (var i = 1; i <= 11; i++) files.Add($"OEBPS/picture{i}.png", files["OEBPS/picture.png"]);
        if (variant == "traversal") Text("../escape.txt", "must not escape");
        if (variant == "drm") Text("META-INF/encryption.xml", "<encryption/>");
        if (variant == "broken-xhtml") files["OEBPS/" + name1] = Encoding.UTF8.GetBytes("<html><body><p>损坏</body></html>");
        using var zip = new ZipArchive(File.Open(path, FileMode.CreateNew), ZipArchiveMode.Create);
        foreach (var item in files)
        { var entry = zip.CreateEntry(item.Key, item.Key == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Optimal); entry.LastWriteTime = new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero); entry.ExternalAttributes = unchecked((int)0x81a40000u); using var output = entry.Open(); output.Write(item.Value); }
        if (variant == "duplicate-path") { var duplicate = zip.CreateEntry("mimetype"); duplicate.LastWriteTime = new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero); using var stream = duplicate.Open(); stream.Write(Encoding.ASCII.GetBytes("application/epub+zip")); }
        if (variant == "symlink") { var link = zip.CreateEntry("OEBPS/link"); link.LastWriteTime = new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero); link.ExternalAttributes = unchecked((int)0xa1ff0000); using var stream = link.Open(); stream.Write(Encoding.ASCII.GetBytes("/etc/passwd")); }
        if (variant == "entry-limit") for (var i = 0; i < 20001; i++) { var entry = zip.CreateEntry("extra/" + i, CompressionLevel.NoCompression); entry.LastWriteTime = new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero); }
        return path;
    }
    public static byte[] Png(int width, int height, bool headerOnly = false)
    {
        using var output = new MemoryStream(); output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        static byte[] Big(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
        void Chunk(string name, byte[] value)
        {
            output.Write(Big(value.Length)); var type = Encoding.ASCII.GetBytes(name); output.Write(type); output.Write(value); uint crc = uint.MaxValue;
            foreach (var b in type.Concat(value)) { crc ^= b; for (var i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1; } output.Write(Big(unchecked((int)~crc)));
        }
        Chunk("IHDR", Big(width).Concat(Big(height)).Concat(new byte[] { 8, 2, 0, 0, 0 }).ToArray());
        if (!headerOnly)
        {
            using var compressed = new MemoryStream(); using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, true))
            { var row = new byte[width * 3 + 1]; for (var y = 0; y < height; y++) { for (var x = 0; x < width; x++) { row[x * 3 + 1] = (byte)(x * 255 / width); row[x * 3 + 2] = (byte)(y * 255 / height); row[x * 3 + 3] = 150; } zlib.Write(row); } }
            Chunk("IDAT", compressed.ToArray());
        }
        Chunk("IEND", []); return output.ToArray();
    }
}
