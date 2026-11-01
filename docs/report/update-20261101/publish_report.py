from pathlib import Path
import hashlib, json, shutil

ROOT=Path(__file__).resolve().parents[3]
WORK=Path(__file__).resolve().parent
OUT=ROOT/'output/提交材料'
NAME='20261101-阅记-项目报告-v1'
validation=json.loads((WORK/'validation.json').read_text(encoding='utf-8'))
assert validation['visual_review_completed'] and not validation['visual_review_required']
originals=json.loads((WORK/'input-hashes.json').read_text(encoding='utf-8'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert all(sha(ROOT/n)==v for n,v in originals.items())
targets=[OUT/(NAME+ext) for ext in ['.docx','.pdf']]
assert not any(p.exists() for p in targets),'Do not overwrite an existing delivery'
deliveries=[]
for target in targets:
    source=WORK/target.name
    expected=validation['docx_sha256' if target.suffix=='.docx' else 'pdf_sha256']
    assert sha(source)==expected
    shutil.copy2(source,target)
    assert sha(target)==expected
    deliveries.append({'path':str(target),'bytes':target.stat().st_size,'sha256':expected})
assert all(sha(ROOT/n)==v for n,v in originals.items())
(WORK/'deliveries.json').write_text(json.dumps(deliveries,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(deliveries,ensure_ascii=False,indent=2))
