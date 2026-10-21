"""Update only the process v3 document in an isolated invisible Word instance."""
from pathlib import Path
import json
import pythoncom
import win32com.client
import fitz

out=Path(__file__).resolve().parent/'word-v3'
source=out/'report.docx'
pdf=out/'word-render.pdf'
pythoncom.CoInitialize()
app=None
doc=None
try:
    app=win32com.client.DispatchEx('Word.Application')
    app.Visible=False
    app.DisplayAlerts=0
    app.AutomationSecurity=3
    doc=app.Documents.Open(str(source.resolve()),ConfirmConversions=False,ReadOnly=False,AddToRecentFiles=False,Visible=False)
    doc.Repaginate()
    doc.Fields.Update()
    for i in range(1,doc.TablesOfContents.Count+1):
        doc.TablesOfContents.Item(i).Update()
    doc.Repaginate()
    for i in range(1,doc.TablesOfContents.Count+1):
        doc.TablesOfContents.Item(i).UpdatePageNumbers()
    doc.Save()
    doc.ExportAsFixedFormat(str(pdf.resolve()),17,OpenAfterExport=False)
    meta={'renderer':'Microsoft Word native COM','word_version':app.Version,'pages':doc.ComputeStatistics(2),'tables_of_contents':doc.TablesOfContents.Count,'fields':doc.Fields.Count,'protection_type':doc.ProtectionType,'qa_pdf':str(pdf)}
    (out/'native-render.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(meta,ensure_ascii=False,indent=2))
finally:
    if doc is not None:
        doc.Close(False)
    if app is not None:
        app.Quit()
    pythoncom.CoUninitialize()

pages=out/'pages'
pages.mkdir(exist_ok=True)
with fitz.open(pdf) as rendered:
    for index,page in enumerate(rendered,1):
        page.get_pixmap(matrix=fitz.Matrix(1.6,1.6),alpha=False).save(pages/f'page-{index:02}.png')
print('Rendered all pages for visual QA.')
