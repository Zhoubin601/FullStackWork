from pathlib import Path
import zipfile,os,json

W=Path(__file__).resolve().parent;ROOT=W.parents[2]
source=ROOT/'output/v2/20261025-阅记-项目报告-v2.docx'
edited=W/'阅记-项目报告.docx';temporary=W/'original-pngs.docx'
changed=[]
with zipfile.ZipFile(source) as original,zipfile.ZipFile(edited) as current,zipfile.ZipFile(temporary,'w',zipfile.ZIP_DEFLATED) as output:
    for info in current.infolist():
        data=current.read(info.filename)
        if info.filename.startswith('word/media/') and info.filename.endswith('.png'):
            assert info.filename in original.namelist()
            before=original.read(info.filename)
            if data!=before:changed.append(info.filename)
            data=before
        output.writestr(info,data)
os.replace(temporary,edited)
(W/'fallback-preservation.json').write_text(json.dumps({'restored_original_raster':changed,'reason':'Native Word rewrote the first SVG raster fallback during save; original pixels restored. SVG diagram, dimensions, layout and PDF unchanged.'},ensure_ascii=False,indent=2),encoding='utf-8')
print(changed)
