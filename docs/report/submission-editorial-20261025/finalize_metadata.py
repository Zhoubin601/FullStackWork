from pathlib import Path
from docx import Document
from docx.oxml.ns import qn

W=Path(__file__).resolve().parent
before=Document(W/'before/阅记-项目报告.docx');d=Document(W/'阅记-项目报告.docx')
for i in [0,1]:
    source_blip=before.inline_shapes[i]._inline.xpath('.//a:blip')[0]
    target_blip=d.inline_shapes[i]._inline.xpath('.//a:blip')[0]
    source_part=before.part.related_parts[source_blip.get(qn('r:embed'))]
    target_part=d.part.related_parts[target_blip.get(qn('r:embed'))]
    target_part._blob=source_part.blob
d.core_properties.author='';d.core_properties.last_modified_by='';d.core_properties.comments='';d.core_properties.keywords='';d.core_properties.identifier=''
d.save(W/'阅记-项目报告.docx')
print('Original diagram fallbacks preserved; editor metadata cleared.')
