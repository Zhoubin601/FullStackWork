from pathlib import Path
from hashlib import sha256
from zipfile import ZipFile
from collections import Counter
import json,re,subprocess
import pymupdf as fitz
from docx import Document
from docx.oxml.ns import qn
from pypdf import PdfReader
from lxml import etree
from PIL import Image,ImageDraw

ROOT=Path(__file__).resolve().parents[3]
WORK=Path(__file__).resolve().parent
NAME='20261101-阅记-项目报告-v2'
DOCX=WORK/(NAME+'.docx');PDF=WORK/(NAME+'.pdf')
def readj(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def dump(n,o):(WORK/n).write_text(json.dumps(o,ensure_ascii=False,indent=2,default=str),encoding='utf-8')
def sha(p):return sha256(p.read_bytes()).hexdigest()
def norm(t):return re.sub(r'[\s.\u00ad\u200b\ufeff]','',t)
old=Document(ROOT/'output/提交材料/20261101-阅记-项目报告-v1.docx')
d=Document(DOCX)
headings=[p.text for p in d.paragraphs if p.style.name in ['标题 11','标题 21']]
assert len(headings)==19
assert headings==[p.text for p in old.paragraphs if p.style.name in ['标题 11','标题 21']]
assert [[c.text for c in r.cells] for r in d.tables[0].rows]==[[c.text for c in r.cells] for r in old.tables[0].rows]
assert len(d.tables)==14 and len(d.inline_shapes)==10
def blocks(doc):
    ps=doc.paragraphs
    return [ps[i+1] for i,p in enumerate(ps) if re.match(r'^代码\s+\d+',p.text)]
assert len(blocks(d))==len(blocks(old))==7
assert all(re.sub(r'\s+','',a.text)==re.sub(r'\s+','',b.text) for a,b in zip(blocks(d),blocks(old)))
for p in blocks(d):
    assert all(r.font.name=='Consolas' and r.font.size.pt==8.5 for r in p.runs if r.text)
for ti,label in [(9,'测试日期'),(9,'构建与运行'),(10,'完整自动化回归')]:
    row=next(r for r in d.tables[ti].rows if r.cells[0].text==label)
    for c in row.cells:
        for p in c.paragraphs:
            assert p.paragraph_format.first_line_indent.pt==0
            for r in p.runs:
                if r.text:assert r.font.name=='Times New Roman' and r.font.size.pt==9

with ZipFile(ROOT/'output/提交材料/20261101-阅记-项目报告-v1.docx') as a,ZipFile(DOCX) as b:
    screenparts=[f'word/media/image{i}.png' for i in range(5,13)]
    assert all(a.read(n)==b.read(n) for n in screenparts)
    ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
    fields=[]
    for n in b.namelist():
        if re.fullmatch('word/footer[0-9]+.xml',n):fields+=etree.fromstring(b.read(n)).xpath('//w:instrText/text()',namespaces=ns)
    assert 'PAGE' in fields

pdf=fitz.open(PDF)
native=readj(WORK/'native-render.json')
assert len(pdf)==native['pages']
text='\n'.join(p.text for p in d.paragraphs)+'\n'+'\n'.join(c.text for t in d.tables for r in t.rows for c in r.cells)
for value in ['351','353','137','41','153','2026','ServiceLocator','ViewLocator','SetParameter']:assert value in text
for value in ['待确认','TODO','ChatGPT','Codex','底稿','本轮','docs/','raw/','output/','业务闭环','不是网页截图','不能借用其他包结论']:assert value not in text,value
pdftext=norm(''.join(p.get_text() for p in pdf))
paras=[p.text for p in d.paragraphs]+[p.text for t in d.tables for r in t.rows for c in r.cells for p in c.paragraphs]
missing=[p for p in paras if norm(p) and norm(p) not in pdftext]
assert not missing,missing
toc=pdf.get_toc();links=pdf[1].get_links()
assert len(toc)==len(links)==19
assert [x[2] for x in toc]==[x['page']+1 for x in links]
assert [norm(x[1]) for x in toc]==list(map(norm,headings))
assert all(norm(t) in norm(pdf[p-1].get_text()) for level,t,p in toc)
reader=PdfReader(PDF);fonts={}
for page in reader.pages:
    for f in page['/Resources'].get('/Font',{}).values():
        f=f.get_object();desc=f.get('/FontDescriptor')
        if desc is None and '/DescendantFonts' in f:desc=f['/DescendantFonts'][0].get_object().get('/FontDescriptor')
        fonts[str(f.get('/BaseFont'))]=bool(desc and any(k in desc.get_object() for k in ['/FontFile','/FontFile2','/FontFile3']))
assert all(fonts.values()),fonts

pages=WORK/'pages';pages.mkdir(exist_ok=True)
metrics=[]
for i,p in enumerate(pdf,1):
    p.get_pixmap(matrix=fitz.Matrix(1.6,1.6),alpha=False).save(pages/f'page-{i:02}.png')
    spans=[s for b in p.get_text('dict')['blocks'] if 'lines' in b for l in b['lines'] for s in l['spans']]
    bad=[s for s in spans if s['bbox'][0]<25 or s['bbox'][1]<8 or s['bbox'][2]>p.rect.width-25 or s['bbox'][3]>p.rect.height-8]
    assert not bad,(i,bad)
    assert len(p.get_text().strip())>50,(i,'possible blank')
    metrics.append({'page':i,'text':p.get_text(),'images':len(p.get_images()),'bottom_body_y':max((s['bbox'][3] for s in spans if 70<s['bbox'][1]<790),default=None)})
for start in range(0,len(pdf),4):
    sheet=Image.new('RGB',(1000,1460),'#ddd')
    for k in range(4):
        i=start+k+1
        if i>len(pdf):break
        im=Image.open(pages/f'page-{i:02}.png');im.thumbnail((490,695))
        x=k%2*500+(500-im.width)//2;y=k//2*730+28
        sheet.paste(im,(x,y));ImageDraw.Draw(sheet).text((x,y-20),f'Page {i}',fill='black')
    sheet.save(WORK/f'contact-{start+1:02}-{min(start+4,len(pdf)):02}.png')
for mode in ['outline','issues','stats']:
    r=subprocess.run(['officecli','view',str(DOCX),mode],capture_output=True)
    (WORK/f'officecli-{mode}.log').write_bytes(r.stdout+r.stderr)
r=subprocess.run(['officecli','validate',str(DOCX)],capture_output=True)
(WORK/'officecli-validation.log').write_bytes(r.stdout+r.stderr)
assert r.returncode==0 and b'no errors found' in r.stdout.lower(),r.stdout
protected=readj(WORK/'input-hashes.json')
assert all(sha(ROOT/n)==h for n,h in protected.items())
with ZipFile(ROOT/'output/提交材料/阅记-源码.zip') as z:assert all(z.read(n)==(ROOT/'Code'/n).read_bytes() for n in z.namelist())
dump('pdf-pages.json',metrics)
result={'passed':True,'pages':len(pdf),'headings':19,'toc_links_and_bookmarks':19,'tables':14,'images':10,'application_screenshots_unchanged':8,'code_blocks':7,'code_tokens_unchanged':True,'three_table_rows_9pt_and_zero_indent':True,'all_paragraphs_match_pdf':True,'fonts_embedded':fonts,'protected_files_unchanged':True,'source_zip_matches_Code':True,'visual_review_required':True,'docx_sha256':sha(DOCX),'pdf_sha256':sha(PDF)}
dump('validation.json',result)
print(json.dumps({k:v for k,v in result.items() if k!='fonts_embedded'},ensure_ascii=False,indent=2))
