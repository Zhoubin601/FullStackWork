"""Deterministic, wholly synthetic reader fixtures. Never overwrites a directory."""
import argparse
import hashlib
import json
from pathlib import Path
import random
import struct
import zipfile
import zlib


def png():
    def chunk(kind, payload):
        return struct.pack('>I', len(payload)) + kind + payload + struct.pack('>I', zlib.crc32(kind + payload))
    return (b'\x89PNG\r\n\x1a\n'
            + chunk(b'IHDR', struct.pack('>IIBBBBB', 2, 2, 8, 2, 0, 0, 0))
            + chunk(b'IDAT', zlib.compress(b'\0' + b'\x90\xc0\xe0' * 2 + b'\0' + b'\x90\xc0\xe0' * 2))
            + chunk(b'IEND', b''))


def epub(path, variant):
    duplicate = variant == 'duplicate-titles'
    nested = variant == 'nested-toc'
    image = variant == 'image-styles'
    titles = ['同名章节', '同名章节'] if duplicate else ['第一章 星光', '第二章 归途']
    nav_items = [f'<li><a href="chapter{i + 1}.xhtml">{title}</a></li>' for i, title in enumerate(titles)]
    if variant == 'toc-order':
        nav_items.reverse()  # Spine is deliberately different from TOC order.
    if nested:
        nav_items = [f'<li><a href="chapter1.xhtml">卷一</a><ol>{"".join(nav_items)}</ol></li>']
    files = {
        'mimetype': b'application/epub+zip',
        'META-INF/container.xml': b'<?xml version="1.0"?><container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container"><rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/></rootfiles></container>',
        'OEBPS/content.opf': (f'''<?xml version="1.0" encoding="utf-8"?>
<package version="3.0" unique-identifier="bookid" xmlns="http://www.idpf.org/2007/opf">
<metadata xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:identifier id="bookid">urn:readjournal:synthetic:{variant}</dc:identifier><dc:title>[合成] {variant}</dc:title><dc:language>zh-CN</dc:language><dc:creator>P0 generator</dc:creator><meta property="dcterms:modified">2024-01-02T00:00:00Z</meta></metadata>
<manifest><item id="nav" href="nav.xhtml" media-type="application/xhtml+xml" properties="nav"/><item id="c1" href="chapter1.xhtml" media-type="application/xhtml+xml"/><item id="c2" href="chapter2.xhtml" media-type="application/xhtml+xml"/>{'<item id="picture" href="picture.png" media-type="image/png"/>' if image else ''}</manifest>
<spine><itemref idref="c1"/><itemref idref="c2"/></spine></package>''').encode(),
        'OEBPS/nav.xhtml': (f'''<html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops"><head><title>目录</title></head><body><nav epub:type="toc"><ol>{''.join(nav_items)}</ol></nav></body></html>''').encode(),
    }
    for i, title in enumerate(titles, 1):
        body = f'<h1 id="heading">{title}</h1><p>第{i}章合成正文。星光落在书页上。</p><p>English text, café and 中文标点。</p>'
        if image:
            body += '<p><strong>粗体</strong>与<em>斜体</em>。</p><img src="picture.png" alt="合成图片"/><p>图片后的真实正文。</p>'
        files[f'OEBPS/chapter{i}.xhtml'] = f'<html xmlns="http://www.w3.org/1999/xhtml"><head><title>{title}</title></head><body>{body}</body></html>'.encode()
    if image:
        files['OEBPS/picture.png'] = png()
    with zipfile.ZipFile(path, 'x', compression=zipfile.ZIP_STORED) as book:
        for name, content in files.items():
            info = zipfile.ZipInfo(name, date_time=(2024, 1, 2, 0, 0, 0))
            info.create_system = 3
            info.external_attr = 0o100644 << 16
            info.compress_type = zipfile.ZIP_STORED
            book.writestr(info, content)


def generate(output, seed, characters):
    if output.exists():
        raise FileExistsError('Choose a new output directory; existing data will not be overwritten.')
    output.mkdir(parents=True)
    rng = random.Random(seed)
    lines = ['第一章 星光', '', '　　星光落在书页上。“今天读到哪里？”她问。',
             'English text with punctuation: one, two; three!',
             '第二章 归途', '重复的句子。', '重复的句子。', '合成编号：' + str(rng.randrange(10**9))]
    mixed = '\n'.join(lines) + '\n'
    samples = {
        'mixed-lf.txt': mixed.encode('utf-8'),
        'mixed-crlf.txt': mixed.replace('\n', '\r\n').encode('utf-8'),
        'mixed-cr.txt': mixed.replace('\n', '\r').encode('utf-8'),
        'mixed-bom.txt': mixed.encode('utf-8-sig'),
        'gb18030.txt': mixed.encode('gb18030'),
        'unicode.txt': ('第一章 字符边界\nemoji 📖👩🏽\u200d🚀，组合字符 e\u0301，变体 ❤️，家庭 👨\u200d👩\u200d👧\u200d👦。\n非断行空格\u00a0与\t制表符。\n').encode(),
        'long-paragraph.txt': ('无章节长段落。' * 20000).encode(),
        'empty.txt': b'',
        'invalid-utf8.txt': b'chapter\n\xff\xfe\x80invalid',
    }
    unit = '这是确定性合成长篇小说正文，包含标点和 English 0123456789。\n'
    samples['million.txt'] = ((unit * (characters // len(unit) + 1))[:characters]).encode()
    for name, content in samples.items():
        with (output / name).open('xb') as file:
            file.write(content)
    for variant in ['basic', 'toc-order', 'duplicate-titles', 'nested-toc', 'image-styles']:
        epub(output / f'{variant}.epub', variant)
    manifest = {'generator_version': 1, 'seed': seed, 'million_text_characters': characters,
                'content': 'Synthetic fixtures only; no external books or copyrighted text.', 'files': []}
    for path in sorted(output.iterdir()):
        data = path.read_bytes()
        manifest['files'].append({'name': path.name, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()})
    (output / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--seed', default=20261022, type=int)
    parser.add_argument('--characters', default=1_000_000, type=int)
    args = parser.parse_args()
    if args.characters < 1 or args.characters > 20_000_000:
        parser.error('characters must be between 1 and 20,000,000')
    manifest = generate(args.output.resolve(), args.seed, args.characters)
    print(json.dumps({'files': len(manifest['files']), 'output': str(args.output.resolve()), 'seed': args.seed}))
