from pathlib import Path
import json
from docx import Document

W=Path(__file__).resolve().parent;p=W/'阅记-项目报告.docx';d=Document(p)
paras=list(d.paragraphs);removed=[]
for i,q in enumerate(paras):
    if q.text.startswith('图 10  完整 ZIP 恢复成功'):
        assert len(paras[i-1]._p.xpath('.//w:drawing'))==1
        for target in [paras[i-1],q]:
            target._p.getparent().remove(target._p)
        removed.append('Restore success screenshot with a developer-local docs/evidence path')
    elif q.text.startswith('图10和图11展示'):
        q.runs[0].text='图10展示完整恢复并重启后的阅读界面。测试流程为导出备份、删除电子书及关联数据、恢复备份、关闭并重启应用，再检查内容和原文定位。'
        for run in q.runs[1:]:run.text=''
    elif q.text=='恢复后重新阅读与原文定位':
        q._p.getparent().remove(q._p)
    elif q.text.startswith('图 11  恢复并重启后的 EPUB'):
        for run in q.runs:run.text=run.text.replace('图 11','图 10')
d.save(p)
(W/'image-cleanup.json').write_text(json.dumps({'removed':removed,'remaining_figures':len(d.inline_shapes),'preserved_actual_screenshot_content':True},ensure_ascii=False,indent=2),encoding='utf-8')
assert len(removed)==1 and len(d.inline_shapes)==10
print('Removed one duplicate screenshot containing a local process path; all other screenshots remain unedited.')
