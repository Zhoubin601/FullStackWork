from pathlib import Path
import hashlib, json, zipfile
from docx import Document
from lxml import etree

ROOT = Path(__file__).resolve().parents[3]
WORK = Path(__file__).resolve().parent

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

protected = {p.relative_to(ROOT).as_posix(): sha(p) for folder in ['raw', 'output'] for p in (ROOT/folder).rglob('*') if p.is_file()}
target = WORK/'input-hashes.json'
if not target.exists():
    target.write_text(json.dumps(protected, ensure_ascii=False, indent=2), encoding='utf-8')
for label, path in [('guidance', ROOT/'raw/期末项目与报告要求.docx'), ('baseline', ROOT/'output/提交材料/阅记-项目报告.docx')]:
    doc = Document(path)
    info = {'path': str(path), 'sha256': sha(path), 'sections': [], 'paragraphs': [], 'tables': []}
    for s in doc.sections:
        info['sections'].append({'size_twips': [s.page_width.twips, s.page_height.twips], 'margins_twips': [s.top_margin.twips, s.bottom_margin.twips, s.left_margin.twips, s.right_margin.twips], 'xml': s._sectPr.xml})
    for i, p in enumerate(doc.paragraphs):
        info['paragraphs'].append({'index': i, 'text': p.text, 'style': p.style.name, 'style_id': p.style.style_id, 'ppr': p._p.pPr.xml if p._p.pPr is not None else None, 'runs': [{'text':r.text, 'rpr':r._r.rPr.xml if r._r.rPr is not None else None} for r in p.runs]})
    info['tables'] = [[[c.text for c in row.cells] for row in t.rows] for t in doc.tables]
    with zipfile.ZipFile(path) as z:
        info['parts'] = {n: {'bytes':len(z.read(n)), 'sha256':hashlib.sha256(z.read(n)).hexdigest()} for n in z.namelist()}
        (WORK/f'{label}-styles.xml').write_bytes(etree.tostring(etree.fromstring(z.read('word/styles.xml')), pretty_print=True, encoding='utf-8'))
    (WORK/f'{label}-inspection.json').write_text(json.dumps(info, ensure_ascii=False, indent=2), encoding='utf-8')
    (WORK/f'{label}-text.txt').write_text('\n'.join(f'P{i}: [{p.style.name}] {p.text}' for i,p in enumerate(doc.paragraphs))+'\n\n'+'\n\n'.join(f'TABLE {i}\n'+'\n'.join(' | '.join(c.text for c in row.cells) for row in t.rows) for i,t in enumerate(doc.tables)), encoding='utf-8')
    print(json.dumps({'label':label,'paragraphs':len(doc.paragraphs),'tables':len(doc.tables),'images':len(doc.inline_shapes),'sha256':sha(path)},ensure_ascii=False))
