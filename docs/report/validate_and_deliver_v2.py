"""Validate final PDF and paired ZIP, then exclusively copy the reviewed PDF."""
from pathlib import Path
import hashlib,json,re,zipfile,xml.etree.ElementTree as ET
import fitz
from fontTools.ttLib import TTFont

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
draft=HERE/'revision-v2/20261021-阅记-项目报告-v2.pdf'
target=ROOT/'output'/draft.name
pdf=fitz.open(draft)
texts=[p.get_text() for p in pdf]
full='\n'.join(texts)
compact=re.sub(r'\s+','',full)
required=['全栈开发技术期末项目报告','项目背景与意义','1.1功能需求','1.2非功能需求','2.1架构设计','2.2功能模块设计','2.3数据/数据库设计','2.4接口与类设计','2.5技术选型说明','3.1关键模块的核心实现','3.2核心功能与UI截图','3.3难点和解决方案','4.1测试环境','4.2测试用例','4.3测试截图','5总结与展望']
missing=[s for s in required if s not in compact]
assert not missing,missing
assert len(pdf)==21
assert all(s in compact for s in ['周宇斌','20246085','软件2401','119个白名单文件','113557字节','93通过','71项断言全部通过','仅README变化'])
assert not any(s in compact for s in ['Windows桌面尚未实测','Windows桌面验收未执行','102816字节','114个文件'])
assert not any(s in full for s in ['待确认','TODO','�'])

allowed=set()
for filename,num in [(r'C:\Windows\Fonts\simsun.ttc',0),(r'C:\Windows\Fonts\simhei.ttf',None)]:
    font=TTFont(filename,fontNumber=num if num is not None else -1,lazy=True)
    allowed.update(font.getBestCmap());font.close()
missing_glyphs=sorted({ch for ch in full if not ch.isspace() and ord(ch) not in allowed})
assert not missing_glyphs,missing_glyphs
overflow=[]
for i,page in enumerate(pdf):
    for block in page.get_text('dict')['blocks']:
        if block['type']!=0:continue
        x0,y0,x1,y1=block['bbox']
        if x0<0 or y0<0 or x1>page.rect.width+.5 or y1>page.rect.height+.5:overflow.append({'page':i+1,'bbox':block['bbox']})
assert not overflow,overflow
assert len(pdf.get_toc())==19
assert all(len(page.get_text().strip())>60 or page.get_images() for page in pdf)

source=ROOT/'output/20261021-阅记-源码-v5.zip'
oldzip=ROOT/'output/20261021-阅记-源码-v4.zip'
manifest=(ROOT/'Code/delivery-files.txt').read_text(encoding='utf-8-sig').splitlines()
with zipfile.ZipFile(source) as z,zipfile.ZipFile(oldzip) as old:
    assert z.namelist()==manifest and z.testzip() is None
    assert all(z.read(n)==(ROOT/'Code'/n).read_bytes() for n in manifest)
    changed=[n for n in manifest if z.read(n)!=old.read(n)]
    assert changed==['README.md'],changed
assert source.stat().st_size==113557
assert hashlib.sha256(source.read_bytes()).hexdigest() in compact
assert hashlib.sha256((ROOT/'raw/期末项目与报告要求.docx').read_bytes()).hexdigest()=='352e94bc9126cc0ef9b5e9f0d293ad8b562bb07b7e17e85aacd7972f918d4d1d'
assert hashlib.sha256((ROOT/'output/20261020-阅记-项目报告-v1.pdf').read_bytes()).hexdigest()=='2ef350f2e76dcd88c4afbb24429d7d096a6a697617e1b9ff5976327df9569034'
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
test_counts={}
for name in ['windows-full.trx','windows-clean.trx']:
    counters=ET.parse(ROOT/'docs/evidence/local-windows-20261021'/name).find('.//t:Counters',ns).attrib
    assert counters['total']=='93' and counters['passed']=='93' and counters['failed']=='0' and counters['notExecuted']=='0'
    test_counts[name]=counters

with target.open('xb') as f:f.write(draft.read_bytes())
with fitz.open(target) as final:assert len(final)==21 and final[0].get_text()==texts[0]
validation={'path':str(target),'pages':len(pdf),'bytes':target.stat().st_size,'sha256':hashlib.sha256(target.read_bytes()).hexdigest(),'paired_source':source.name,'source_files':len(manifest),'source_bytes':source.stat().st_size,'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'source_matches_current_code':True,'differences_from_tested_v4':changed,'required_sections_present':True,'identity_present':True,'missing_glyphs':missing_glyphs,'text_outside_page':overflow,'bookmark_count':len(pdf.get_toc()),'rendered_pages':len(list((HERE/'revision-v2/pages-verified').glob('page-*.png'))),'visual_review':'All 21 final pages rendered using Poppler; both contact sheets and cover, environment, cases, closing pages checked.','test_counts':test_counts,'application_tests_rerun_for_readme_only_change':False,'old_pdf_and_raw_unchanged':True,'status':'Formal PDF v2 paired with source v5; Windows validation included.'}
(HERE/'revision-v2/validation.json').write_text(json.dumps(validation,ensure_ascii=False,indent=2),encoding='utf-8')
(HERE/'revision-v2/report-text.txt').write_text(full,encoding='utf-8')
print(json.dumps({k:v for k,v in validation.items() if k not in ['test_counts']},ensure_ascii=True,indent=2))
