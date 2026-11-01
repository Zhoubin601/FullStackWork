from pathlib import Path, PurePosixPath
from zipfile import ZipFile
from hashlib import sha256
from collections import Counter
from lxml import etree as E
from pypdf import PdfReader
import json, re, fitz

ROOT=Path(__file__).resolve().parents[3]
OUT=Path(__file__).resolve().parent
def readj(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def digest(p):return sha256(p.read_bytes()).hexdigest()
def dump(p,o):p.write_text(json.dumps(o,ensure_ascii=False,indent=2),encoding='utf-8')
result={}
for kind,folder in [('application','Code'),('reference','daily-poetry-a')]:
    inv=readj(ROOT/f'docs/report/submission-20261025/{kind}-inventory.json')
    bad=[x['Path'] for x in inv['SourceFiles'] if digest(ROOT/folder/x['Path']).upper()!=x['Sha256'].upper()]
    actual={p.relative_to(ROOT/folder).as_posix() for proj in (['ReadJournal','ReadJournal.Library'] if kind=='application' else ['DailyPoetryA','DailyPoetryA.Library']) for p in (ROOT/folder/proj).rglob('*.cs') if not {'bin','obj'}.intersection(p.parts)}
    known={x['Path'] for x in inv['SourceFiles']}
    result[kind]={'summary':inv['Summary'],'changed_inventory_files':bad,'added_files':sorted(actual-known),'missing_files':sorted(known-actual)}
    for v in inv['Views']:
        pass
with ZipFile(ROOT/'output/提交材料/阅记-源码.zip') as z:
    names=z.namelist()
    result['package']={'bytes':(ROOT/'output/提交材料/阅记-源码.zip').stat().st_size,'members':len(names),'corrupt_member':z.testzip(),'mismatched_members':[n for n in names if z.read(n)!=(ROOT/'Code'/n).read_bytes()],'forbidden_members':[n for n in names if {'bin','obj','.git','TestResults'}.intersection(PurePosixPath(n).parts) or PurePosixPath(n).suffix.lower() in {'.exe','.dll','.pdb','.db','.sqlite','.sqlite3'}]}
d=E.fromstring((ROOT/'docs/evidence/branch-review-20261101/release.trx').read_bytes())
result['recorded_tests']={'outcomes':dict(Counter(x.get('outcome') for x in d.iter() if E.QName(x).localname=='UnitTestResult')),'times':[dict(x.attrib) for x in d.iter() if E.QName(x).localname=='Times'],'new_test_run':False}
pdf=fitz.open(ROOT/'output/提交材料/20261101-阅记-项目报告-v1.pdf')
norm=lambda s:re.sub(r'[\s.\u00ad\u200b\ufeff]','',s)
pdftext=norm(''.join(p.get_text() for p in pdf))
ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
with ZipFile(ROOT/'output/提交材料/20261101-阅记-项目报告-v1.docx') as z:
    doc=E.fromstring(z.read('word/document.xml'))
    paragraphs=[''.join(p.xpath('.//w:t/text()',namespaces=ns)) for p in doc.xpath('//w:body//w:p',namespaces=ns)]
    missing=[p for p in paragraphs if norm(p) and norm(p) not in pdftext]
    footer_fields=[]
    for n in z.namelist():
        if re.fullmatch('word/footer[0-9]+.xml',n):footer_fields.extend(E.fromstring(z.read(n)).xpath('//w:instrText/text()',namespaces=ns))
    result['docx_pdf']={'paragraphs_checked':sum(bool(norm(p)) for p in paragraphs),'missing_paragraphs_after_removing_leader_dots':missing,'live_page_fields':footer_fields}
toc=pdf.get_toc()
result['toc']={'entries':len(toc),'links':len(pdf[1].get_links()),'heading_matches':all(norm(title) in norm(pdf[page-1].get_text()) for level,title,page in toc),'link_destinations':[x['page']+1 for x in pdf[1].get_links()],'bookmark_destinations':[x[2] for x in toc]}
reader=PdfReader(ROOT/'output/提交材料/20261101-阅记-项目报告-v1.pdf')
fonts={}
for page in reader.pages:
    for f in page['/Resources'].get('/Font',{}).values():
        f=f.get_object();desc=f.get('/FontDescriptor')
        if desc is None and '/DescendantFonts' in f:desc=f['/DescendantFonts'][0].get_object().get('/FontDescriptor')
        if desc is not None:
            desc=desc.get_object();fonts[str(f.get('/BaseFont'))]=any(k in desc for k in ['/FontFile','/FontFile2','/FontFile3'])
result['embedded_fonts']=fonts
text='\n'.join(paragraphs)
patterns=['ChatGPT','Codex','Claude','作为AI','作为一个AI','由AI生成','AI生成的报告','提示词','底稿','本轮','交付物','docs/','raw/','output/','TODO','待确认','资料未提供','综上所述','显著提升','赋能']
result['editorial_scan']={s:[p for p in paragraphs if s in p] for s in patterns if s in text}
result['protected_hashes_unchanged']={name:digest(ROOT/name)==h for name,h in readj(OUT/'input-hashes.json').items()}
dump(OUT/'evidence-verification.json',result)
print(json.dumps({k:v for k,v in result.items() if k not in ['embedded_fonts','protected_hashes_unchanged']},ensure_ascii=False,indent=2))
