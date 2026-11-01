from pathlib import Path
import zipfile, json

ROOT=Path(__file__).resolve().parents[3]
WORK=Path(__file__).resolve().parent
source=ROOT/'output/提交材料/阅记-项目报告.docx'
target=WORK/'20261101-阅记-项目报告-v1.docx'
temporary=WORK/'preserved-parts.docx'
# Word's field update reserializes header/footer XML although their content is
# unchanged. Restore the retained package parts, then export read-only for QA.
with zipfile.ZipFile(source) as a, zipfile.ZipFile(target) as b:
    preserved=[n for n in a.namelist() if n.startswith('word/') and any(s in n for s in ['header','footer','numbering'])]
    changed=[n for n in preserved if a.read(n)!=b.read(n)]
    for name in preserved:
        if name.endswith('.rels'):
            assert a.read(name)==b.read(name), name
    with zipfile.ZipFile(temporary,'w',zipfile.ZIP_DEFLATED) as out:
        for member in b.infolist():
            out.writestr(member,a.read(member.filename) if member.filename in preserved else b.read(member.filename))
temporary.replace(target)
(WORK/'preserved-parts-restored.json').write_text(json.dumps({'restored_parts':changed,'reason':'retain unchanged header/footer XML after native field refresh'},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'restored_parts':changed}))
