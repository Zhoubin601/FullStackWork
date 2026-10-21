"""Replace only the two table diagrams in the user's saved v2 document."""
from pathlib import Path
from hashlib import sha256
from io import BytesIO
import json
import math
import re
import fitz
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib.colors import HexColor, Color, white
from docx import Document
from docx.shared import Mm, Pt
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.opc.part import Part
from docx.opc.packuri import PackURI
from docx.opc.constants import RELATIONSHIP_TYPE as RT
from lxml import etree

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
WORK = HERE / 'word-v3'
WORK.mkdir(exist_ok=True)
SOURCE = ROOT / 'output/20261021-阅记-项目报告-v2.docx'
RAW = ROOT / 'raw/期末项目与报告要求.docx'
DOC = WORK / 'report.docx'
GREEN, INK, MUTED = '#173D36', '#172720', '#586A62'
BORDER, LIGHT = '#9AAFA6', '#F1F6F3'
pdfmetrics.registerFont(TTFont('Song', r'C:\Windows\Fonts\simsun.ttc', subfontIndex=0))
pdfmetrics.registerFont(TTFont('Hei', r'C:\Windows\Fonts\simhei.ttf'))


def digest(path):
    return sha256(path.read_bytes()).hexdigest()


class Diagram:
    def __init__(self, name, width, height):
        self.name, self.w, self.h = name, width, height
        self.pdf = WORK / f'{name}.pdf'
        self.c = canvas.Canvas(str(self.pdf), pagesize=(width, height))
        self.c.setTitle(name)

    def box(self, x, y, w, h, fill='FFFFFF', stroke=BORDER, radius=8, dashed=False):
        c = self.c
        c.saveState()
        c.setFillColor(HexColor(fill if fill.startswith('#') else '#' + fill))
        c.setStrokeColor(HexColor(stroke))
        c.setLineWidth(1.4)
        if dashed:
            c.setDash(6, 4)
        c.roundRect(x, self.h-y-h, w, h, radius, stroke=1, fill=1)
        c.restoreState()

    def text(self, x, y, text, size=20, bold=False, color=INK, center=False):
        c = self.c
        c.setFont('Hei' if bold else 'Song', size)
        c.setFillColor(HexColor(color))
        (c.drawCentredString if center else c.drawString)(x, self.h-y-size*.83, text)

    def line(self, points, arrow=False, dashed=False, color=GREEN, width=1.8):
        c = self.c
        c.saveState()
        c.setStrokeColor(HexColor(color))
        c.setFillColor(HexColor(color))
        c.setLineWidth(width)
        if dashed:
            c.setDash(6, 4)
        path = c.beginPath()
        path.moveTo(points[0][0], self.h-points[0][1])
        for x, y in points[1:]:
            path.lineTo(x, self.h-y)
        c.drawPath(path)
        if arrow:
            (x0,y0),(x,y)=points[-2:]
            a = math.atan2(y-y0,x-x0)
            p = c.beginPath()
            p.moveTo(x,self.h-y)
            for side in [-1,1]:
                p.lineTo(x-10*math.cos(a)+side*4.5*math.sin(a),self.h-(y-10*math.sin(a)-side*4.5*math.cos(a)))
            p.close()
            c.drawPath(p,stroke=0,fill=1)
        c.restoreState()

    def save(self):
        self.c.showPage()
        self.c.save()
        with fitz.open(self.pdf) as pdf:
            page = pdf[0]
            # Glyph outlines keep the SVG independent of installed fonts.
            (WORK/f'{self.name}.svg').write_text(page.get_svg_image(text_as_path=True),encoding='utf-8')
            page.get_pixmap(matrix=fitz.Matrix(3,3),alpha=False).save(WORK/f'{self.name}.png')


def architecture():
    d = Diagram('architecture',960,560)
    # Separate boxes with real connections, rather than a tabular layout.
    d.box(10,12,710,150,fill=LIGHT)
    d.text(30,27,'ReadJournal · UI 工程',23,True,color=GREEN)
    d.text(30,61,'App 资源 / ServiceLocator：注册 → 构建容器 → 属性解析',19)
    for x,w,title,sub in [(30,190,'Views','XAML 与交互呈现'),(235,205,'ViewLocator','反射创建 · 按类型缓存'),(455,245,'平台服务','导航 · 提示 · 文件选择')]:
        d.box(x,96,w,49,fill='FFFFFF',radius=5)
        d.text(x+w/2,101,title,20,True,center=True)
        d.text(x+w/2,125,sub,17,center=True)
    d.line([(365,162),(365,204)],arrow=True)
    d.text(383,174,'构造注入 / 接口协作',18,color=MUTED)
    d.box(10,207,710,237,fill=LIGHT)
    d.text(30,222,'ReadJournal.Library · 业务工程',23,True,color=GREEN)
    d.text(30,251,'Models / Services / ViewModels，不依赖 Avalonia 控件',19)
    d.box(30,284,670,63,fill='FFFFFF',radius=5)
    d.text(365,292,'ViewModels：命令、状态与统一参数入口',21,True,center=True)
    d.text(365,321,'SetParameter → Loaded；MainViewModel 管理返回栈',18,center=True)
    d.line([(365,347),(365,371)],arrow=True)
    d.box(30,374,670,53,fill='FFFFFF',radius=5)
    d.text(365,382,'Services / Models：阅读规则、存储、统计与备份',20,True,center=True)
    d.text(365,408,'通过接口协作；平台相关接口由 UI 工程实现',18,center=True)
    d.line([(365,444),(365,485)],arrow=True)
    d.text(383,456,'异步 IO / 串行写入',18,color=MUTED)
    d.box(95,488,540,58,fill=LIGHT,radius=15)
    d.text(365,495,'SQLite · 本地持久化',22,True,color=GREEN,center=True)
    d.text(365,523,'四类业务表 · 外键 · 版本 · 事务',19,center=True)
    d.box(752,12,198,534,fill='FFFFFF',dashed=True)
    d.text(851,36,'测试工程',23,True,color=GREEN,center=True)
    for y,t in [(81,'ReadJournal.'),(108,'UnitTest'),(170,'xUnit'),(204,'Moq'),(238,'临时真实 SQLite'),(360,'验证业务规则'),(393,'与工程架构')]:
        d.text(851,y,t,19,center=True)
    d.line([(752,305),(720,305)],arrow=True,dashed=True)
    d.text(851,477,'虚线：测试依赖',17,color=MUTED,center=True)
    d.save()


def entities():
    d = Diagram('entities',960,668)
    def entity(x,y,w,title,cn,fields):
        h=62+len(fields)*25+12
        d.box(x,y,w,h,fill='FFFFFF',stroke=GREEN,radius=3)
        d.box(x,y,w,59,fill=LIGHT,stroke=GREEN,radius=3)
        d.text(x+w/2,y+7,title,23,True,color=GREEN,center=True)
        d.text(x+w/2,y+33,cn,19,center=True)
        for i,(name,typ,mark) in enumerate(fields):
            d.text(x+12,y+68+i*25,f'{name}: {typ}'+(f'  [{mark}]' if mark else ''),19)
        return h
    book=[('Id','int','PK'),('Title','string',''),('Author','string',''),('Category','string',''),('TotalPages','int',''),('Description','string',''),('CreatedAt','DateTime',''),('UpdatedAt','DateTime','')]
    record=[('Id','int','PK'),('BookId','int','FK'),('ReadingDate','DateTime',''),('StartPage','int',''),('EndPage','int',''),('Minutes','int','')]
    quote=[('Id','int','PK'),('BookId','int','FK'),('Text','string',''),('Thoughts','string',''),('Page','int?',''),('IsFavorite','bool',''),('CreatedAt','DateTime',''),('UpdatedAt','DateTime','')]
    plan=[('Id','int','PK'),('BookId','int','FK'),('StartDate','DateTime',''),('TargetDate','DateTime','')]
    h=entity(325,12,310,'Book','书籍',book)
    # Three individually identifiable associations, with optional-many children.
    start=12+h
    for sx,ex in [(385,160),(480,480),(575,800)]:
        d.line([(sx,start),(sx,319),(ex,319),(ex,365)],width=1.7)
        d.text(sx+8,start+4,'1',18)
        d.text(ex+9,340,'0..*',18)
    entity(10,365,300,'ReadingRecord','阅读记录',record)
    entity(330,365,300,'Quote','书摘',quote)
    entity(650,365,300,'ReadingPlan','阅读计划',plan)
    d.text(480,650,'PK：主键；FK：BookId → Book.Id；删除书籍时级联删除子记录',18,color=MUTED,center=True)
    d.save()


def add_diagram(doc,table,name,description):
    p = doc.add_paragraph()
    p.alignment=WD_ALIGN_PARAGRAPH.CENTER
    pf=p.paragraph_format
    pf.first_line_indent=Pt(0)
    pf.space_before=Pt(4)
    pf.space_after=Pt(3)
    pf.line_spacing=1
    pf.keep_with_next=True
    pf.keep_together=True
    pic=p.add_run().add_picture(str(WORK/f'{name}.png'),width=Mm(164))
    pic._inline.docPr.set('descr',description)
    pic._inline.docPr.set('title',description)
    # Use vector artwork in modern Word; retain a 3x PNG for older viewers.
    part=Part(PackURI(f'/word/media/{name}.svg'),'image/svg+xml',(WORK/f'{name}.svg').read_bytes(),doc.part.package)
    rel=doc.part.relate_to(part,RT.IMAGE)
    blip=pic._inline.xpath('.//a:blip')[0]
    extlst=OxmlElement('a:extLst')
    ext=OxmlElement('a:ext')
    ext.set('uri','{96DAC541-7B7A-43D3-8B79-37D633B846F1}')
    svg=etree.Element('{http://schemas.microsoft.com/office/drawing/2016/SVG/main}svgBlip',nsmap={'asvg':'http://schemas.microsoft.com/office/drawing/2016/SVG/main'})
    svg.set(qn('r:embed'),rel)
    ext.append(svg)
    extlst.append(ext)
    blip.append(extlst)
    table._tbl.addprevious(p._p)
    table._tbl.getparent().remove(table._tbl)


def main():
    hashes={'source_v2':digest(SOURCE),'raw':digest(RAW)}
    architecture()
    entities()
    doc=Document(SOURCE)
    source=Document(SOURCE)
    original_p=[p.text for p in source.paragraphs]
    selected=[]
    for table in list(doc.tables):
        texts='\n'.join(c.text for r in table.rows for c in r.cells)
        if len(table.columns)==1 and 'Library ViewModels' in texts and 'SQLite' in texts:
            selected.append('architecture')
            add_diagram(doc,table,'architecture','图1：ReadJournal 的工程分层、接口协作与测试依赖')
        elif 'Book' in texts and '1:N' in texts and 'ReadingRecord' in texts:
            selected.append('entities')
            add_diagram(doc,table,'entities','图2：Book、ReadingRecord、Quote、ReadingPlan 的字段与一对多关系')
    assert selected==['architecture','entities'],selected
    # The larger real diagrams alter pagination. Keep each existing code excerpt
    # together so the new pagination cannot strand its closing brace on a page.
    code_layout_adjustments=0
    paragraphs=list(doc.paragraphs)
    for i,p in enumerate(paragraphs):
        if '\n' in p.text and any(token in p.text for token in ['serviceCollection.','ViewModelBase target =','BookProgress Calculate']):
            p.paragraph_format.keep_together=True
            if i:
                paragraphs[i-1].paragraph_format.keep_with_next=True
            code_layout_adjustments+=1
    # Preserve the user's saved changes and modify only explicit report versions.
    parts=[doc.part]+[p for p in doc.part.package.parts if p.partname.startswith(('/word/header','/word/footer'))]
    replaced=0
    for part in parts:
        if not hasattr(part,'element'):
            continue
        for t in part.element.iter(qn('w:t')):
            if t.text and 'v2' in t.text:
                t.text=t.text.replace('v2','v3')
                replaced+=1
    doc.core_properties.subject='报告v3：架构图与实体图改为矢量绘图，配套源码v5'
    doc.save(DOC)
    check=Document(DOC)
    assert len(check.tables)==len(source.tables)-2==5
    assert len(check.inline_shapes)==len(source.inline_shapes)+2==21
    # Removing the two new picture paragraphs restores every original paragraph.
    texts=[p.text for p in check.paragraphs]
    cursor=0
    for expected in original_p:
        expected=expected.replace('v2','v3')
        while cursor<len(texts) and texts[cursor]!=expected:
            cursor+=1
        assert cursor<len(texts),expected
        cursor+=1
    source_media={sha256(p.blob).hexdigest() for p in source.part.package.parts if p.partname.startswith('/word/media/')}
    new_media={sha256(p.blob).hexdigest() for p in check.part.package.parts if p.partname.startswith('/word/media/')}
    assert source_media<=new_media
    assert hashes=={'source_v2':digest(SOURCE),'raw':digest(RAW)}
    meta={'source':str(SOURCE),'baseline_sha256':hashes,'diagrams_replaced':selected,'native_tables_remaining':5,'inline_pictures':21,'all_original_body_paragraphs_preserved':True,'all_original_media_preserved':True,'explicit_version_nodes_updated':replaced,'code_paragraphs_kept_together':code_layout_adjustments,'svg_with_png_fallback':True,'no_new_external_sources':True}
    (WORK/'content-checks.json').write_text(json.dumps(meta,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(meta,ensure_ascii=False,indent=2))


if __name__=='__main__':
    main()
