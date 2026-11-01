from pathlib import Path
from copy import deepcopy
from collections import Counter
import hashlib, json, re, zipfile
from docx import Document
from docx.enum.style import WD_STYLE_TYPE
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT, WD_ROW_HEIGHT_RULE
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.shared import Pt, Twips
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

ROOT = Path(__file__).resolve().parents[3]
WORK = Path(__file__).resolve().parent
SOURCE = ROOT/'output/提交材料/阅记-项目报告.docx'
REFERENCE = ROOT/'raw/期末项目与报告要求.docx'
TARGET = WORK/'20261101-阅记-项目报告-v1.docx'
inputs = json.loads((WORK/'input-hashes.json').read_text(encoding='utf-8'))
for name, expected in inputs.items():
    assert hashlib.sha256((ROOT/name).read_bytes()).hexdigest() == expected, name

d = Document(SOURCE)
paras = list(d.paragraphs)
tables = list(d.tables)
changes = []

def replace(p, text):
    old = p.text
    if p.runs:
        p.runs[0].text = text
        for r in p.runs[1:]: r.text = ''
    else:
        p.add_run(text)
    changes.append({'old': old, 'new': text})

def font(run, size, bold=False):
    run.font.name = 'Microsoft YaHei'
    run.font.size = Pt(size)
    run.font.bold = bold
    fonts = run._r.get_or_add_rPr().get_or_add_rFonts()
    for key in list(fonts.attrib):
        if key.endswith('Theme'): del fonts.attrib[key]
    for attr in ['ascii', 'hAnsi', 'eastAsia', 'cs']:
        fonts.set(qn('w:'+attr), 'Microsoft YaHei')
    color = run._r.get_or_add_rPr().get_or_add_color()
    color.set(qn('w:val'), '000000')
    for key in list(color.attrib):
        if 'theme' in key: del color.attrib[key]

# The cover follows the retained course template: title, four-row identity table.
title = d.styles['Title'] if 'Title' in d.styles else d.styles.add_style('Title', WD_STYLE_TYPE.PARAGRAPH)
title.base_style = d.styles['Normal']
title.font.name = 'Microsoft YaHei'
title.font.size = Pt(19)
title.font.bold = True
outline = OxmlElement('w:outlineLvl'); outline.set(qn('w:val'), '9')
title.element.get_or_add_pPr().append(outline)
paras[0].style = title
paras[0].paragraph_format.alignment = WD_ALIGN_PARAGRAPH.CENTER
paras[0].paragraph_format.first_line_indent = Twips(0)
paras[0].paragraph_format.space_before = Pt(65)
paras[0].paragraph_format.space_after = Pt(45)
paras[0].paragraph_format.keep_with_next = True
for run in paras[0].runs: font(run, 19, True)

cover_values = [p.text.split('：', 1)[1] for p in paras[3:7]]
cover = deepcopy(Document(REFERENCE).tables[0]._tbl)
paras[0]._p.addnext(cover)
from docx.table import Table
cover_table = Table(cover, d._body)
cover_table.alignment = WD_TABLE_ALIGNMENT.CENTER
cover_table.autofit = False
style_node = cover_table._tbl.tblPr.find(qn('w:tblStyle'))
if style_node is not None: cover_table._tbl.tblPr.remove(style_node)
width = cover_table._tbl.tblPr.find(qn('w:tblW'))
width.set(qn('w:type'), 'dxa'); width.set(qn('w:w'), '9298')
for col, twips in zip(cover_table.columns, [1800, 7498]): col.width = Twips(twips)
for i, row in enumerate(cover_table.rows):
    row.height = Pt(44)
    row.height_rule = WD_ROW_HEIGHT_RULE.AT_LEAST
    for j, cell in enumerate(row.cells):
        cell.width = Twips([1800, 7498][j])
        cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
        p = cell.paragraphs[0]
        replace(p, ['项目名称', '姓名', '学号', '班级'][i] if j == 0 else cover_values[i])
        p.style = d.styles['Normal']
        p.paragraph_format.first_line_indent = Twips(0)
        p.paragraph_format.space_before = Pt(0)
        p.paragraph_format.space_after = Pt(0)
        p.paragraph_format.line_spacing = 1.3
        p.paragraph_format.keep_with_next = i < 3
        for run in p.runs: font(run, 11)
        ppr = p._p.get_or_add_pPr()
        jc = ppr.find(qn('w:jc'))
        if jc is not None: jc.set(qn('w:val'), 'left')
for p in paras[1:7]: p._p.getparent().remove(p._p)
replace(paras[7], '2026 年 11 月 1 日')
paras[7].paragraph_format.alignment = WD_ALIGN_PARAGRAPH.CENTER
paras[7].paragraph_format.first_line_indent = Twips(0)
paras[7].paragraph_format.space_before = Pt(60)
for run in paras[7].runs: font(run, 11)

# Preserve report page geometry and contents, adopting the template heading roles.
for style_name, size, before, line in [('标题 11', 16, 16.5, 26.8), ('标题 21', 15, 15, 24.4)]:
    style = d.styles[style_name]
    style.font.name = 'Microsoft YaHei'
    style.font.size = Pt(size)
    style.font.bold = True
    style.paragraph_format.space_before = Pt(before)
    style.paragraph_format.space_after = Pt(6)
    style.paragraph_format.line_spacing = Pt(line)
    style.paragraph_format.keep_with_next = True
    rpr = style.element.get_or_add_rPr()
    for attr in ['ascii', 'hAnsi', 'eastAsia', 'cs']:
        rpr.get_or_add_rFonts().set(qn('w:'+attr), 'Microsoft YaHei')
    rpr.get_or_add_color().set(qn('w:val'), '000000')
for p in paras:
    if p.style.name in ['标题 11', '标题 21']:
        new_text = re.sub(r'^([1-5])\s+', r'\1. ', p.text) if p.style.name == '标题 11' else re.sub(r'^(\d+\.\d+)\s+', r'\1 ', p.text)
        if new_text != p.text: replace(p, new_text)
        for run in p.runs: font(run, 16 if p.style.name == '标题 11' else 15, True)

def cell(table, row, col, text):
    value = tables[table].cell(row, col)
    replace(value.paragraphs[0], text)
    for extra in value.paragraphs[1:]: extra._p.getparent().remove(extra._p)

cell(5, 8, 0, 'xUnit / Moq / Avalonia.Headless')
cell(8, 4, 1, '2026年10月24日至25日：界面及独立源码测试；11月1日：当前主线完整自动化回归。')
cell(8, 5, 1, '源码包与当前主线均采用 --locked-mode 还原依赖、Release 构建和自动化测试；启动方法见源码包 README。')
cell(9, 1, 1, '2026年11月1日：共353项，351通过、0失败、2项因符号链接权限跳过。')
replace(paras[163], '2026年11月1日的完整自动化回归共353项，351项通过、0项失败。两项符号链接测试因Windows账号缺少创建权限而跳过，不计入通过数量。其余架构、排版、性能与实际窗口结果来自2026年10月24日至25日验收，按各自方法分别统计。')
replace(paras[171], '2026年10月24日的性能测试运行于Windows、.NET 8.0.30和32逻辑处理器环境。使用生产ReaderViewModel与ReaderSurface进行Skia离屏绘制，视口为760×440 DIP、输出1140×660像素。测量区分冷导入、预热打开、翻页和重排，不将离屏采样等同于物理显示缩放测试。')
replace(paras[175], '图10为2026年10月24日界面验收中完整恢复并重启后的阅读界面。测试流程为导出备份、删除电子书及关联数据、恢复备份、关闭并重启应用，再检查内容和原文定位。')
replace(paras[117], '以下界面来自2026年10月24日验收，展示书籍管理、原生阅读、AI辅助和数据恢复功能，对应功能实现与当前代码一致。界面中的书籍与摘录均为自建测试样本，记录数量用于说明操作效果。')
replace(paras[183], '2026年11月1日对当前主线完成锁定依赖还原、Release构建与完整自动化回归：353项测试中351项通过、0项失败、2项因符号链接权限跳过。此前独立源码包测试取得相同计数；实际窗口验收覆盖主要阅读流程，备份恢复前后10张业务表与12个受管文件一致。相关结论限于已测试环境。')

settings = d.settings.element
update = settings.find(qn('w:updateFields'))
if update is None: update = OxmlElement('w:updateFields'); settings.append(update)
update.set(qn('w:val'), 'true')

# Keep every unedited OOXML package part byte-for-byte intact.
from lxml import etree
patches = {'word/document.xml':d._element, 'word/styles.xml':d.styles.element, 'word/settings.xml':settings}
with zipfile.ZipFile(SOURCE) as source, zipfile.ZipFile(TARGET, 'w', zipfile.ZIP_DEFLATED) as out:
    for member in source.infolist():
        payload = etree.tostring(patches[member.filename], xml_declaration=True, encoding='UTF-8', standalone=True) if member.filename in patches else source.read(member.filename)
        out.writestr(member, payload)

result = Document(TARGET)
assert len(result.tables) == 14 and len(result.inline_shapes) == 10
assert [r.cells[0].text for r in result.tables[0].rows] == ['项目名称','姓名','学号','班级']
assert [(s.page_width.twips,s.page_height.twips,s.left_margin.twips,s.right_margin.twips) for s in result.sections] == [(11906,16838,1304,1304)]
with zipfile.ZipFile(SOURCE) as a, zipfile.ZipFile(TARGET) as b:
    changed_parts = [n for n in a.namelist() if a.read(n) != b.read(n)]
    assert set(changed_parts) <= set(patches), changed_parts
    assert all(a.read(n) == b.read(n) for n in a.namelist() if n.startswith('word/media/'))
(WORK/'edit-summary.json').write_text(json.dumps({'target':str(TARGET),'changes':changes,'changed_parts':changed_parts,'tables':len(result.tables),'images':len(result.inline_shapes)},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'paragraphs':len(result.paragraphs),'tables':len(result.tables),'images':len(result.inline_shapes),'changed_parts':changed_parts},ensure_ascii=False))
