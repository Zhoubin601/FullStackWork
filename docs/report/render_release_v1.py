"""Update only the process copy through a dedicated invisible Word instance."""
from pathlib import Path
import json
import pythoncom
import win32com.client
import fitz
from PIL import Image, ImageOps, ImageDraw

WORK=Path(__file__).resolve().parent/'release-v1'
source=WORK/'report.docx'
pdf=WORK/'report.pdf'
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
    for i in range(1,doc.TablesOfContents.Count+1):doc.TablesOfContents.Item(i).Update()
    doc.Repaginate()
    for i in range(1,doc.TablesOfContents.Count+1):doc.TablesOfContents.Item(i).UpdatePageNumbers()
    doc.Save()
    doc.ExportAsFixedFormat(str(pdf.resolve()),17,OpenAfterExport=False)
    meta={'renderer':'Microsoft Word native COM','word_version':app.Version,'pages':doc.ComputeStatistics(2),'tables_of_contents':doc.TablesOfContents.Count,'fields':doc.Fields.Count,'protection_type':doc.ProtectionType}
    (WORK/'native-render.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(meta,ensure_ascii=False,indent=2))
finally:
    if doc is not None:doc.Close(False)
    if app is not None:app.Quit()
    pythoncom.CoUninitialize()

pages=WORK/'pages'
pages.mkdir(exist_ok=True)
with fitz.open(pdf) as rendered:
    for i,page in enumerate(rendered,1):page.get_pixmap(matrix=fitz.Matrix(1.6,1.6),alpha=False).save(pages/f'page-{i:02}.png')
    for batch,start in enumerate(range(0,len(rendered),6),1):
        sheet=Image.new('RGB',(1240,3*900),(228,232,230))
        draw=ImageDraw.Draw(sheet)
        for n in range(start,min(start+6,len(rendered))):
            thumb=Image.open(pages/f'page-{n+1:02}.png').convert('RGB')
            thumb.thumbnail((590,842))
            col,row=(n-start)%2,(n-start)//2
            x,y=col*620+(620-thumb.width)//2,row*900+28
            sheet.paste(thumb,(x,y))
            draw.text((col*620+15,row*900+8),str(n+1),fill='black')
        sheet.save(WORK/f'contact-{batch}.jpg',quality=91)
print('Rendered all pages and contact sheets for visual QA.')
