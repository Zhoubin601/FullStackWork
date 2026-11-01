from pathlib import Path
from hashlib import sha256
import json,shutil

ROOT=Path(__file__).resolve().parents[3]
WORK=Path(__file__).resolve().parent
OUT=ROOT/'output/提交材料'
NAME='20261101-阅记-项目报告-v2'
def sha(p):return sha256(p.read_bytes()).hexdigest()
def readj(p):return json.loads(p.read_text(encoding='utf-8-sig'))
validation=readj(WORK/'validation.json')
visual=readj(WORK/'visual-review.json')
assert validation['passed'] and validation['visual_review_completed'] and not validation['visual_review_required']
assert visual['reviewed_pages']==list(range(1,validation['pages']+1)) and visual['issues']==[]
assert all(sha(WORK/'pages'/n)==h for n,h in visual['page_image_sha256'].items())
assert all(sha(ROOT/n)==h for n,h in readj(WORK/'input-hashes.json').items())
targets=[OUT/(NAME+ext) for ext in ['.docx','.pdf']]
assert not any(p.exists() for p in targets),'A v2 deliverable already exists; do not overwrite it.'
deliveries=[]
for target in targets:
    source=WORK/target.name
    expected=validation['docx_sha256' if target.suffix=='.docx' else 'pdf_sha256']
    assert sha(source)==expected
    shutil.copy2(source,target)
    assert sha(target)==expected
    deliveries.append({'path':str(target),'bytes':target.stat().st_size,'sha256':expected})
assert all(sha(ROOT/n)==h for n,h in readj(WORK/'input-hashes.json').items())
(WORK/'deliveries.json').write_text(json.dumps(deliveries,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(deliveries,ensure_ascii=False,indent=2))
