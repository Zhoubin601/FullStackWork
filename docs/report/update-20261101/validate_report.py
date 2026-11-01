from pathlib import Path
from collections import Counter
import hashlib, json, re, zipfile
from docx import Document
from pypdf import PdfReader
import pdfplumber

ROOT = Path(__file__).resolve().parents[3]
WORK = Path(__file__).resolve().parent
NAME = '20261101-阅记-项目报告-v1'
word = WORK/(NAME+'.docx')
pdf = WORK/(NAME+'.pdf')

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def norm(text): return ''.join(text.split())
def readj(path): return json.loads(path.read_text(encoding='utf-8-sig'))

protected = readj(WORK/'input-hashes.json')
assert all(sha(ROOT/name) == expected for name, expected in protected.items()), 'An original or old delivery changed'
before = Document(ROOT/'output/提交材料/阅记-项目报告.docx')
d = Document(word)
reference = Document(ROOT/'raw/期末项目与报告要求.docx')
headings = [p.text for p in d.paragraphs if p.style.name in ['标题 11','标题 21']]
expected = []
number = 0
for p in reference.paragraphs[33:]:
    if p.style.name == 'Heading 2' and p.text != '项目背景与意义':
        number += 1
        expected.append(f'{number}. {p.text}')
    else: expected.append(p.text)
assert list(map(norm, headings)) == list(map(norm, expected)), (headings, expected)
assert len(headings) == 19
assert d.paragraphs[0].text == '全栈开发技术期末项目报告'
assert [r.cells[0].text for r in d.tables[0].rows] == ['项目名称','姓名','学号','班级']
assert [r.cells[1].text for r in d.tables[0].rows] == [p.text.split('：',1)[1] for p in before.paragraphs[3:7]]
assert len(d.tables) == len(before.tables)+1 == 14
assert len(d.inline_shapes) == len(before.inline_shapes) == 10
assert d.tables[6].cell(8,0).text == 'xUnit / Moq / Avalonia.Headless'
def code_blocks(document):
    paragraphs = document.paragraphs
    return [paragraphs[i+1].text for i,p in enumerate(paragraphs) if re.match(r'^代码\s+\d+',p.text)]
code_before = code_blocks(before)
code_after = code_blocks(d)
assert code_before == code_after and len(code_after) == 7
assert [(s.page_width.twips,s.page_height.twips,s.top_margin.twips,s.bottom_margin.twips,s.left_margin.twips,s.right_margin.twips) for s in d.sections] == [(11906,16838,1247,1077,1304,1304)]
with zipfile.ZipFile(ROOT/'output/提交材料/阅记-项目报告.docx') as a, zipfile.ZipFile(word) as b:
    media = {n:hashlib.sha256(a.read(n)).hexdigest() for n in a.namelist() if n.startswith('word/media/')}
    assert media == {n:hashlib.sha256(b.read(n)).hexdigest() for n in b.namelist() if n.startswith('word/media/')}
    preserve_parts = [n for n in a.namelist() if n.startswith('word/') and any(x in n for x in ['header','footer','numbering'])]
    unchanged_parts = {n:a.read(n)==b.read(n) for n in preserve_parts}
    assert all(unchanged_parts.values()), unchanged_parts

reader = PdfReader(pdf)
native = readj(WORK/'native-render.json')
assert len(reader.pages) == native['pages'] == 28
text = '\n'.join(p.text for p in d.paragraphs)+'\n'+'\n'.join(c.text for t in d.tables for row in t.rows for c in row.cells)
pdf_text = '\n'.join(p.extract_text() or '' for p in reader.pages)
for content in [text,pdf_text]:
    for required in ['351','353','2026','11','137','41','ServiceLocator','ViewLocator','SetParameter']:
        assert required in content, required
    for forbidden in ['待确认','TODO','Codex','ChatGPT','docs/','raw/','output/','底稿','本轮','2026 年 10 月 25 日','Avalonia.Headless.XUnit']:
        assert forbidden not in content, forbidden
    assert '\ufffd' not in content
missing_paragraphs = [p.text for p in d.paragraphs if len(norm(p.text)) > 8 and not re.search(r'\t\d+$',p.text) and norm(p.text) not in norm(pdf_text)]
missing_cells = [c.text for t in d.tables for row in t.rows for c in row.cells if len(norm(c.text)) > 8 and norm(c.text) not in norm(pdf_text)]
assert not missing_paragraphs and not missing_cells, (missing_paragraphs,missing_cells)
assert len(native['headings']) == 19
rows = [r for r in native['tocText'][0].split(chr(13)) if r.strip()]
assert len(rows)==19
for row,h in zip(rows,native['headings']):
    title,page = row.rsplit(chr(9),1)
    assert norm(title)==norm(h['title']) and int(page)==h['page']
    assert norm(title) in norm(reader.pages[h['page']-1].extract_text())
def flatten(items):
    for item in items:
        if isinstance(item,list): yield from flatten(item)
        else: yield item
bookmarks=list(flatten(reader.outline))
assert len(bookmarks)==19
for b,h in zip(bookmarks,native['headings']):
    assert norm(b.title)==norm(h['title']) and reader.get_destination_page_number(b)+1==h['page']
links=[a.get_object() for a in reader.pages[1].get('/Annots',[]) if a.get_object().get('/Subtype')=='/Link']
assert len(links)==19
ids={page.indirect_reference.idnum:i for i,page in enumerate(reader.pages,1)}
for link,h in zip(links,native['headings']):
    destination=link.get('/Dest') or link['/A']['/D']
    assert ids[destination[0].idnum]==h['page']

pages=[]
with pdfplumber.open(pdf) as book:
    for i,page in enumerate(book.pages,1):
        chars=page.chars
        assert len(page.extract_text() or '')>50,i
        outliers=[c['text'] for c in chars if c['x0']<28 or c['x1']>page.width-28 or c['top']<8 or c['bottom']>page.height-8]
        assert not outliers,(i,outliers)
        pages.append({'page':i,'characters':len(chars),'images':len(page.images),'words':len(page.extract_words())})
fonts={}
for page in reader.pages:
    for ref in page['/Resources'].get('/Font',{}).values():
        f=ref.get_object()
        descendants=[x.get_object() for x in f.get('/DescendantFonts',[])] or [f]
        descriptors=[x.get('/FontDescriptor') for x in descendants]
        fonts[str(f.get('/BaseFont',''))]=all(x and any(k in x.get_object() for k in ['/FontFile','/FontFile2','/FontFile3']) for x in descriptors)
assert all(fonts.values()),fonts

# The unchanged source archive still matches every current source member.
with zipfile.ZipFile(ROOT/'output/提交材料/阅记-源码.zip') as archive:
    manifest=(ROOT/'Code/delivery-files.txt').read_text(encoding='utf-8').splitlines()
    assert archive.namelist()==manifest and len(manifest)==208
    assert all(archive.read(n)==(ROOT/'Code'/n).read_bytes() for n in manifest)
test_summary=readj(ROOT/'docs/evidence/branch-review-20261101/final-checks.json')
assert (test_summary['Total'],test_summary['Passed'],test_summary['Failed'],test_summary['Skipped'])==(353,351,0,2)
pngs=sorted((WORK/'pages').glob('page-*.png'))
assert len(pngs)==28
result={'pages':28,'body_headings':19,'toc_links':19,'bookmarks':19,'tables':14,'images':10,'code_blocks':7,'all_original_media_unchanged':True,'original_header_footer_numbering_unchanged':True,'protected_original_files':len(protected),'protected_original_files_unchanged':True,'fonts_embedded':fonts,'source_archive_matches_current_code':True,'source_members':208,'test_counts':{k:test_summary[k] for k in ['Total','Passed','Failed','Skipped']},'page_checks':pages,'docx_sha256':sha(word),'pdf_sha256':sha(pdf),'visual_review_required':True}
review_path=WORK/'visual-review.json'
if review_path.exists():
    review=readj(review_path)
    assert review['docx_sha256']==sha(word) and review['pdf_sha256']==sha(pdf)
    assert review['reviewed_pages']==list(range(1,29)) and review['issues']==[]
    assert all(sha(WORK/'pages-verified'/name)==digest for name,digest in review['page_image_sha256'].items())
    result['visual_review_required']=False
    result['visual_review_completed']=True
(WORK/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ['page_checks','fonts_embedded']},ensure_ascii=False,indent=2))
