from pathlib import Path
from zipfile import ZipFile
from collections import Counter
from hashlib import sha256
import json, re, subprocess
import fitz
from lxml import etree as E
from PIL import Image, ImageOps, ImageDraw

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
NS = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
TARGET = ROOT / 'output/提交材料/20261101-阅记-项目报告-v1.docx'
PDF = TARGET.with_suffix('.pdf')

def dump(name, value):
    (OUT / name).write_text(json.dumps(value, ensure_ascii=False, indent=2, default=str), encoding='utf-8')

def text(e):
    return ''.join(e.xpath('.//w:t/text()', namespaces=NS))

def paragraphs(path):
    with ZipFile(path) as z:
        d = E.fromstring(z.read('word/document.xml'))
        return [text(x) for x in d.xpath('//w:body//w:p', namespaces=NS) if text(x)]

files = [*ROOT.joinpath('raw').glob('*'), TARGET, PDF, ROOT/'output/提交材料/阅记-源码.zip']
dump('input-hashes.json', {str(p.relative_to(ROOT)): sha256(p.read_bytes()).hexdigest() for p in files if p.is_file()})
with ZipFile(TARGET) as z:
    d = E.fromstring(z.read('word/document.xml'))
    styles = E.fromstring(z.read('word/styles.xml'))
    sd = {x.get('{'+NS['w']+'}styleId'): x for x in styles.xpath('//w:style', namespaces=NS)}
    rows = []
    for idx, p in enumerate(d.xpath('//w:body/w:p|//w:body/w:tbl', namespaces=NS)):
        if p.tag.endswith('tbl'):
            row = {'index': idx, 'table': [[text(c) for c in tr.xpath('./w:tc',namespaces=NS)] for tr in p.xpath('./w:tr',namespaces=NS)], 'xml_properties': E.tostring(p.find('w:tblPr',NS),encoding='unicode')}
        else:
            style = p.xpath('./w:pPr/w:pStyle/@w:val',namespaces=NS)
            row = {'index': idx, 'style':style[0] if style else 'Normal','text':text(p), 'pPr': E.tostring(p.find('w:pPr',NS),encoding='unicode') if p.find('w:pPr',NS) is not None else '', 'runs': [{'text':text(r),'rPr':E.tostring(r.find('w:rPr',NS),encoding='unicode') if r.find('w:rPr',NS) is not None else ''} for r in p.xpath('./w:r',namespaces=NS)]}
        rows.append(row)
    dump('docx-structure.json', rows)
    lines=[]
    for x in rows:
        if 'table' in x:
            lines.append(f"\n[{x['index']}] TABLE")
            lines.extend(' | '.join(r) for r in x['table'])
        else:
            lines.append(f"[{x['index']}] ({x['style']}) {x['text']}")
    (OUT/'report-text.txt').write_text('\n'.join(lines),encoding='utf-8')
    dump('styles.json',{k: E.tostring(v, encoding='unicode') for k,v in sd.items() if k in {x.get('style') for x in rows}})
    dump('structure-summary.json',{
        'paragraph_styles':dict(Counter(x.get('style') for x in rows if 'style' in x)),
        'tables':len(d.xpath('//w:tbl',namespaces=NS)),
        'drawings':len(d.xpath('//w:drawing',namespaces=NS)),
        'sections':[E.tostring(x,encoding='unicode') for x in d.xpath('//w:sectPr',namespaces=NS)],
        'fields':d.xpath('//w:instrText/text()',namespaces=NS),
        'footers':{n:z.read(n).decode('utf-8') for n in z.namelist() if re.match(r'word/(footer|header)\d+\.xml$',n)},
        'core':z.read('docProps/core.xml').decode('utf-8'),
        'app':z.read('docProps/app.xml').decode('utf-8'),
        'comments':[n for n in z.namelist() if 'comment' in n.lower()],
        'tracked_changes':len(d.xpath('//w:ins|//w:del',namespaces=NS)),
    })

pages = OUT/'pages'; pages.mkdir(exist_ok=True)
doc = fitz.open(PDF)
metrics = []
for i,page in enumerate(doc):
    page.get_pixmap(matrix=fitz.Matrix(1.5,1.5),alpha=False).save(pages/f'page-{i+1:02}.png')
    spans=[s for b in page.get_text('dict')['blocks'] if 'lines' in b for l in b['lines'] for s in l['spans']]
    metrics.append({'page':i+1,'width':page.rect.width,'height':page.rect.height,'text':page.get_text(),'spans':spans,'links':page.get_links(),'images':page.get_images(),'outside':[s for s in spans if s['bbox'][0]<0 or s['bbox'][1]<0 or s['bbox'][2]>page.rect.width+1 or s['bbox'][3]>page.rect.height+1]})
dump('pdf-pages.json',metrics)
dump('bookmarks.json',doc.get_toc())
for start in range(0,len(doc),4):
    sheet=Image.new('RGB',(1000,1460),'#ddd')
    for k in range(4):
        p=start+k+1
        if p>len(doc):break
        im=Image.open(pages/f'page-{p:02}.png'); im.thumbnail((490,695))
        x=(k%2)*500+(500-im.width)//2; y=(k//2)*730+28
        sheet.paste(im,(x,y)); ImageDraw.Draw(sheet).text((x,y-20),f'Page {p}',fill='black')
    sheet.save(OUT/f'contact-{start+1:02}-{min(start+4,len(doc)):02}.png')
for mode in ['outline','issues','stats']:
    r=subprocess.run(['officecli','view',str(TARGET),mode],capture_output=True)
    (OUT/f'officecli-{mode}.log').write_bytes(r.stdout+r.stderr)
r=subprocess.run(['officecli','validate',str(TARGET)],capture_output=True)
(OUT/'officecli-validate.log').write_bytes(r.stdout+r.stderr)
word=''.join(paragraphs(TARGET))
pdf=''.join(p['text'] for p in metrics)
norm=lambda s:re.sub(r'\s+','',s)
missing=[p for p in paragraphs(TARGET) if norm(p) not in norm(pdf)]
dump('comparison.json',{'docx_paragraphs_missing_from_pdf':missing,'out_of_page_spans':sum(len(p['outside']) for p in metrics),'pages':len(doc)})
print(json.dumps({'pages':len(doc),'missing_paragraphs':len(missing),'contact_sheets':7,'output':str(OUT)},ensure_ascii=False))
