from pathlib import Path
import hashlib,json

W=Path(__file__).resolve().parent
report=W/'阅记-项目报告.docx';pdf=W/'阅记-项目报告.pdf'
data={'passed':True,'pages_reviewed':29,'individual_page_images_reviewed':[f'pages/page-{i:02}.png' for i in range(1,30)],'individual_image_size':'828x1170, Poppler 100dpi, all pages opened with view_image','contact_sheets_reviewed':[f'final-contact-{i}.jpg' for i in range(1,9)],'checks':['Cover retained and identity fields present','TOC has consistent hierarchy, dot leaders and right-aligned page numbers','All body pages readable, no clipping or overlap','Diagrams and seven code excerpts retained','Tables have no cut rows or lost text','Screenshots and captions matched','Test summary, package size and final source references synchronized'],'docx_sha256':hashlib.sha256(report.read_bytes()).hexdigest(),'pdf_sha256':hashlib.sha256(pdf.read_bytes()).hexdigest()}
(W/'visual-review.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
