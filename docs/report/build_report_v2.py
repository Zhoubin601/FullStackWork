"""Render the limited, reviewed LaTeX report source with ReportLab on Windows.

This supports only constructs present in report-v2.tex, and fails on unknown markup.
It does not fabricate UI screenshots or terminal output.
"""
from pathlib import Path
from xml.sax.saxutils import escape
import json, re
from PIL import Image as PILImage
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.units import mm
from reportlab.platypus import (BaseDocTemplate, PageTemplate, Frame, Paragraph, Spacer,
    PageBreak, Table, TableStyle, Image, KeepTogether, Flowable, XPreformatted)
from reportlab.platypus.tableofcontents import TableOfContents
from reportlab.graphics.shapes import Drawing, Rect, Line, String, Polygon

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[1]
OUT=HERE/'revision-v2'
OUT.mkdir(exist_ok=True)
PDF=OUT/'20261021-阅记-项目报告-v2.pdf'
pdfmetrics.registerFont(TTFont('CJK',r'C:\Windows\Fonts\simsun.ttc',subfontIndex=0))
pdfmetrics.registerFont(TTFont('CJK-Bold',r'C:\Windows\Fonts\simhei.ttf'))
pdfmetrics.registerFontFamily('CJK',normal='CJK',bold='CJK-Bold',italic='CJK',boldItalic='CJK-Bold')
GREEN=colors.HexColor('#173D36')
LIGHT=colors.HexColor('#F0F5F3')
INK=colors.HexColor('#172720')
MUTED=colors.HexColor('#586A62')
W,H=A4
CW=W-46*mm
styles={
 'body':ParagraphStyle('Body',fontName='CJK',fontSize=10.5,leading=18.2,wordWrap='CJK',textColor=INK,spaceAfter=7,firstLineIndent=21,allowWidows=0,allowOrphans=0),
 'h1':ParagraphStyle('Heading1',fontName='CJK-Bold',fontSize=18,leading=26,textColor=GREEN,spaceBefore=16,spaceAfter=10,keepWithNext=True),
 'h2':ParagraphStyle('Heading2',fontName='CJK-Bold',fontSize=13,leading=21,textColor=GREEN,spaceBefore=12,spaceAfter=8,keepWithNext=True),
 'cell':ParagraphStyle('Cell',fontName='CJK',fontSize=9.2,leading=14.5,wordWrap='CJK',textColor=INK,allowWidows=0,allowOrphans=0),
 'headcell':ParagraphStyle('HeadCell',fontName='CJK-Bold',fontSize=9.4,leading=15,wordWrap='CJK',textColor=colors.white),
 'caption':ParagraphStyle('Caption',fontName='CJK',fontSize=9,leading=14,wordWrap='CJK',alignment=TA_CENTER,textColor=MUTED,spaceBefore=5,spaceAfter=7),
 'code':ParagraphStyle('Code',fontName='CJK',fontSize=7.1,leading=10.3,textColor=INK,spaceBefore=3,spaceAfter=3),
 'toc1':ParagraphStyle('TOC1',fontName='CJK-Bold',fontSize=11,leading=22,textColor=GREEN,spaceBefore=5),
 'toc2':ParagraphStyle('TOC2',fontName='CJK',fontSize=10,leading=18,leftIndent=15,textColor=INK),
}

def clean_tex(s):
    s=re.sub(r'\\path\{([^{}]*)\}',lambda m:m.group(1),s)
    s=s.replace(r'\newline','<br/>').replace(r'\_','_').replace(r'\#','#').replace(r'\%','%').replace(r'\&','&')
    s=s.replace('~',' ')
    if re.search(r'\\[a-zA-Z]+',s):
        raise ValueError('Unknown inline LaTeX: '+s)
    return s

def para(s,style='body'):
    return Paragraph(escape(clean_tex(s)).replace('&lt;br/&gt;','<br/>'),styles[style])

class Cover(Flowable):
    def __init__(self):
        super().__init__()
        self.width=CW
        self.height=232*mm
    def draw(self):
        c=self.canv
        c.setFillColor(GREEN);c.rect(0,221*mm,20*mm,2*mm,fill=1,stroke=0)
        def line(text,y,size=12,font='CJK',color=INK):
            c.setFillColor(color);c.setFont(font,size);c.drawString(0,y*mm,text)
        line('全栈开发技术期末项目报告',204,22,'CJK-Bold',GREEN)
        line('阅记',171,34,'CJK-Bold',GREEN)
        line('阅读与书摘管理应用',155,17)
        line('修订版 v2  /  配套源码 v5',143,11,'CJK',MUTED)
        c.setFillColor(LIGHT);c.roundRect(0,61*mm,CW,65*mm,4*mm,stroke=0,fill=1)
        for i,(key,value) in enumerate([('项目名称','阅记阅读与书摘管理应用'),('姓名','周宇斌'),('学号','20246085'),('班级','软件2401')]):
            c.setFont('CJK',11);c.setFillColor(MUTED);c.drawString(8*mm,(112-i*12)*mm,key)
            c.setFillColor(INK);c.drawString(36*mm,(112-i*12)*mm,value)
        line('2026 年 10 月 21 日',45,12)
        block=Paragraph('依据实际源码、Linux历史记录及Windows本机验收修订。当前代码93项自动化测试、71项独立架构断言通过；本报告配套源码v5，应用与测试源码同已验收的v4，仅README更新。',ParagraphStyle('cover_note',fontName='CJK',fontSize=10,leading=17,wordWrap='CJK',textColor=MUTED))
        aw,ah=block.wrap(CW,100*mm);block.drawOn(c,0,8*mm)

def header(c,doc):
    if doc.page==1:return
    c.saveState();c.setFont('CJK',8);c.setFillColor(MUTED)
    c.drawString(23*mm,H-15*mm,'阅记 · 阅读与书摘管理')
    c.drawRightString(W-23*mm,H-15*mm,'项目报告  /  修订版 v2')
    c.setStrokeColor(colors.HexColor('#D4DDD8'));c.setLineWidth(.5)
    c.line(23*mm,H-18*mm,W-23*mm,H-18*mm)
    c.drawCentredString(W/2,12*mm,str(doc.page-1));c.restoreState()

class Doc(BaseDocTemplate):
    def beforeDocument(self):self.seq=0
    def afterFlowable(self,f):
        if hasattr(f,'heading_level'):
            self.seq+=1;key=f'heading-{self.seq}'
            self.canv.bookmarkPage(key);self.canv.addOutlineEntry(f.getPlainText(),key,level=f.heading_level,closed=False)
            self.notify('TOCEntry',(f.heading_level,f.getPlainText(),self.page-1,key))

def arrow(d,x1,y1,x2,y2):
    d.add(Line(x1,y1,x2,y2,strokeColor=MUTED,strokeWidth=.8))
    import math
    dx=x2-x1;dy=y2-y1;l=math.hypot(dx,dy);ux,uy=dx/l,dy/l
    d.add(Polygon([x2,y2,x2-6*ux+2.5*uy,y2-6*uy-2.5*ux,x2-6*ux-2.5*uy,y2-6*uy+2.5*ux],fillColor=MUTED,strokeColor=MUTED))

def box(d,x,y,w,h,lines):
    d.add(Rect(x,y,w,h,rx=5,ry=5,strokeColor=colors.HexColor('#94A99D'),fillColor=LIGHT,strokeWidth=.7))
    for i,text in enumerate(lines):d.add(String(x+w/2,y+h/2+(len(lines)-1)*7-i*14,text,fontName='CJK',fontSize=10,textAnchor='middle',fillColor=INK))

def diagram(architecture=True):
    if architecture:
        d=Drawing(CW,210)
        labels=[['UI：App资源、ViewLocator、Views、平台提示与文件选择'],['Library ViewModels：构造注入、公开命令、统一参数入口'],['Library Services：14项接口职责、阅读规则、存储、统计、备份'],['SQLite：四类业务表、外键、版本、串行写入与事务']]
        for i,lines in enumerate(labels):
            y=168-i*52;box(d,5,y,CW-10,36,lines)
            if i<3:arrow(d,CW/2,y,CW/2,y-16)
        return d
    d=Drawing(CW,155);bw=132;bh=45
    box(d,CW/2-bw/2,105,bw,bh,['Book','书籍与总页数'])
    for x,lines in [(0,['ReadingRecord','页段、日期、分钟']),(CW/2-bw/2,['Quote','正文、感想、收藏、页码']),(CW-bw,['ReadingPlan','开始日与目标日'])]:
        box(d,x,5,bw,bh,lines);arrow(d,CW/2,105,x+bw/2,50)
        d.add(String((CW/2+x+bw/2)/2+8,78,'1:N',fontName='CJK',fontSize=9,fillColor=MUTED))
    return d

def code_block(text):
    # Wrap by actual embedded-font width so long C# and shell lines cannot overflow.
    lines=[];limit=CW-22
    for line in text.rstrip().splitlines():
        part=''
        for ch in line.expandtabs(4):
            if pdfmetrics.stringWidth(part+ch,'CJK',7.1)>limit:
                lines.append(part);part='    '+ch
            else:part+=ch
        lines.append(part)
    f=XPreformatted(escape('\n'.join(lines)),styles['code'])
    # XPreformatted can split across pages; no oversized enclosing table.
    return [f,Spacer(1,5)]

def table(block):
    widths=[float(x)*mm for x in re.findall(r'p\{([\d.]+)mm\}',block.split('\n')[0])]
    if not widths:raise ValueError('Table widths not found')
    widths=[x*CW/sum(widths) for x in widths]
    body=block.split(r'\toprule',1)[1].split(r'\bottomrule',1)[0]
    body=body.replace(r'\midrule','')
    rows=[]
    for raw in body.split(chr(92)*2):
        raw=raw.strip()
        if not raw:continue
        cells=raw.split(' & ')
        if len(cells)!=len(widths):raise ValueError('Table row mismatch '+repr(raw))
        rows.append([para(cell.strip(),'headcell' if not rows else 'cell') for cell in cells])
    t=Table(rows,colWidths=widths,repeatRows=1,hAlign='LEFT')
    t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),GREEN),('VALIGN',(0,0),(-1,-1),'TOP'),('LEFTPADDING',(0,0),(-1,-1),8),('RIGHTPADDING',(0,0),(-1,-1),8),('TOPPADDING',(0,0),(-1,-1),7),('BOTTOMPADDING',(0,0),(-1,-1),7),('LINEBELOW',(0,0),(-1,0),.7,GREEN),('LINEBELOW',(0,1),(-1,-1),.3,colors.HexColor('#D8E0DB')),('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.white,colors.HexColor('#F7F9F8')])]))
    return [t,Spacer(1,8)]

text=(HERE/'report-v2.tex').read_text(encoding='utf-8')
text=text[text.index(r'\section*{项目背景与意义}'):]
lines=text.splitlines();i=0;story=[Cover(),PageBreak()]
story.append(para('目录','h1'))
toc=TableOfContents();toc.levelStyles=[styles['toc1'],styles['toc2']];toc.dotsMinLevel=0
story += [toc,PageBreak()]
section=subsection=figures=0
paragraph=[]
def flush():
    if paragraph:
        story.append(para(''.join(paragraph)));paragraph.clear()
while i<len(lines):
    line=lines[i].strip()
    if not line:flush();i+=1;continue
    heading=re.match(r'\\(section\*?|subsection)\{([^{}]+)\}',line)
    if heading:
        flush();kind,title=heading.groups()
        if kind.startswith('section'):
            if kind=='section':section+=1;subsection=0;title=f'{section}  {title}'
            f=para(title,'h1');f.heading_level=0
        else:
            subsection+=1;title=f'{section}.{subsection}  {title}';f=para(title,'h2');f.heading_level=1
        story.append(f);i+=1;continue
    if line in [r'\clearpage',r'\newpage']:
        flush()
        if not isinstance(story[-1],PageBreak):story.append(PageBreak())
        i+=1;continue
    if line.startswith(r'\end{document}'):
        flush();break
    if line.startswith(r'\begin{longtable}'):
        flush();block=[line];i+=1
        while r'\end{longtable}' not in block[-1]:block.append(lines[i]);i+=1
        story+=table('\n'.join(block));continue
    if line.startswith(r'\begin{figure}'):
        flush();block=[line];i+=1
        while r'\end{figure}' not in block[-1]:block.append(lines[i]);i+=1
        block='\n'.join(block);architecture='架构图' in block
        caption=re.search(r'\\caption\{([^{}]*)\}',block).group(1);figures+=1
        story.append(KeepTogether([diagram(architecture),para(f'图 {figures}  {caption}','caption')]))
        continue
    screen=re.fullmatch(r'\\screen\{([^{}]+)\}\{([^{}]+)\}',line)
    if screen:
        flush();path,caption=screen.groups();image_path=ROOT/'docs/evidence'/path
        with PILImage.open(image_path) as img:iw,ih=img.size
        maxheight=(90 if figures+1 in (3,4,13,14,17,18) else 110)*mm
        scale=min((CW-4*mm)/iw,maxheight/ih)
        im=Image(str(image_path),width=iw*scale,height=ih*scale);im.hAlign='CENTER';figures+=1
        story.append(KeepTogether([Spacer(1,2*mm),im,para(f'图 {figures}  {caption}','caption')]))
        i+=1;continue
    inp=re.fullmatch(r'\\VerbatimInput\[([^\]]+)\]\{([^{}]+)\}',line)
    if inp:
        flush();opts,path=inp.groups();first=int(re.search(r'firstline=(\d+)',opts).group(1));last=int(re.search(r'lastline=(\d+)',opts).group(1))
        content=(HERE/path).read_text(encoding='utf-8').splitlines()[first-1:last]
        story+=code_block('\n'.join(content));i+=1;continue
    if line.startswith(r'\begin{Verbatim}'):
        flush();i+=1;block=[]
        while not lines[i].strip().startswith(r'\end{Verbatim}'):block.append(lines[i]);i+=1
        story+=code_block('\n'.join(block));i+=1;continue
    if line.startswith('\\'):
        raise ValueError('Unknown block markup: '+line)
    paragraph.append(line);i+=1
flush()
doc=Doc(str(PDF),pagesize=A4,title='阅记阅读与书摘管理应用项目报告（修订版v2）',author='周宇斌',leftMargin=23*mm,rightMargin=23*mm,topMargin=24*mm,bottomMargin=20*mm)
frame=Frame(23*mm,20*mm,CW,H-44*mm,id='normal',leftPadding=0,rightPadding=0,topPadding=0,bottomPadding=0)
doc.addPageTemplates(PageTemplate(id='pages',frames=[frame],onPage=header))
doc.multiBuild(story)
print(json.dumps({'draft':str(PDF),'bytes':PDF.stat().st_size,'figures':figures},ensure_ascii=True))
