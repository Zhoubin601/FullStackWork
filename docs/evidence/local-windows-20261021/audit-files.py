"""Read-only requirement, archive, reference and PDF checks for this local audit."""
from pathlib import Path
import hashlib, json, re, zipfile, xml.etree.ElementTree as ET
import fitz

root = Path(__file__).resolve().parents[3]
out = Path(__file__).resolve().parent
raw = root / 'raw/期末项目与报告要求.docx'
sha = lambda b: hashlib.sha256(b).hexdigest()
with zipfile.ZipFile(raw) as z:
    ns = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
    xml = ET.fromstring(z.read('word/document.xml'))
    paragraphs = [''.join(p.itertext()) for p in []]  # text extracted below preserves table paragraphs
    paragraphs = [''.join(t.text or '' for t in p.findall('.//w:t', ns)) for p in xml.findall('.//w:p', ns)]
(out/'requirements.txt').write_text('\n'.join(paragraphs), encoding='utf-8')

archive = root/'output/20261021-阅记-源码-v4.zip'
manifest = (root/'Code/delivery-files.txt').read_text(encoding='utf-8-sig').splitlines()
checks = {'archive': archive.name, 'bytes': archive.stat().st_size, 'sha256': sha(archive.read_bytes()),
          'raw_sha256': sha(raw.read_bytes()), 'under_10MB': archive.stat().st_size <= 10_000_000}
with zipfile.ZipFile(archive) as z:
    checks['files'] = len(z.namelist())
    checks['manifest_matches'] = z.namelist() == manifest
    checks['crc_ok'] = z.testzip() is None
    checks['forbidden_entries'] = [n for n in z.namelist() if set(Path(n).parts) & {'bin','obj','.git','TestResults','docs','raw','memory-bank','output'} or n.endswith(('.sqlite3','.dll','.exe','.pdb','.log'))]
    checks['byte_differences'] = []
    checks['content_differences_after_newline_normalization'] = []
    for n in z.namelist():
        a, b = z.read(n), (root/'Code'/n).read_bytes()
        if a != b:
            checks['byte_differences'].append(n)
            if a.replace(b'\r\n',b'\n') != b.replace(b'\r\n',b'\n'):
                checks['content_differences_after_newline_normalization'].append(n)
    # Archive entries have already been checked against the explicit local manifest.
    target = out/'clean-source'
    if target.exists():
        raise RuntimeError('Refusing to overwrite clean-source')
    z.extractall(target)
(out/'package-checks.json').write_text(json.dumps(checks,ensure_ascii=False,indent=2),encoding='utf-8')

baseline = json.loads((root/'docs/reference-baseline.json').read_text(encoding='utf-8-sig'))
reference_checks = []
for x in baseline['SourceFiles'] + baseline['Views']:
    p = root/'daily-poetry-a'/x['Path']
    reference_checks.append({'path':x['Path'],'exact_hash_match':sha(p.read_bytes()).lower()==x['Sha256'].lower()})
(out/'reference-hashes.json').write_text(json.dumps(reference_checks,ensure_ascii=False,indent=2),encoding='utf-8')

pdf = fitz.open(root/'output/20261020-阅记-项目报告-v1.pdf')
texts = [page.get_text() for page in pdf]
(out/'report-text.txt').write_text('\n'.join(texts),encoding='utf-8')
compact = re.sub(r'\s+','', ''.join(texts))
sections = ['项目背景与意义','1.1功能需求','1.2非功能需求','2.1架构设计','2.2功能模块设计','2.3数据/数据库设计','2.4接口与类设计','2.5技术选型说明','3.1关键模块的核心实现','3.2核心功能与UI截图','3.3难点和解决方案','4.1测试环境','4.2测试用例','4.3测试截图','5总结与展望']
pdf_check = {'pages':len(pdf),'sections':{s:s in compact for s in sections},'version_lines':[line.strip() for t in texts for line in t.splitlines() if any(w in line for w in ['68','93','源码-v2','Windows','Debian'])]}
(out/'report-checks.json').write_text(json.dumps(pdf_check,ensure_ascii=False,indent=2),encoding='utf-8')
thumbs=[]
from PIL import Image, ImageDraw
for i,page in enumerate(pdf):
    pix=page.get_pixmap(matrix=fitz.Matrix(.5,.5))
    img=Image.frombytes('RGB',[pix.width,pix.height],pix.samples)
    tile=Image.new('RGB',(310,450),'#dddddd'); tile.paste(img,((310-img.width)//2,20)); ImageDraw.Draw(tile).text((8,3),str(i+1),fill='black'); thumbs.append(tile)
sheet=Image.new('RGB',(310*5,450*4),'#aaaaaa')
for i,img in enumerate(thumbs): sheet.paste(img,((i%5)*310,(i//5)*450))
sheet.save(out/'report-contact-sheet.png')
print(json.dumps({'package':checks,'reference_hashes':{'total':len(reference_checks),'exact_match':sum(x['exact_hash_match'] for x in reference_checks)},'report':pdf_check},ensure_ascii=False,indent=2))
