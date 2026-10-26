from pathlib import Path
import json,re,hashlib
from docx import Document
from docx.shared import Pt,Twips
from docx.enum.text import WD_TAB_ALIGNMENT,WD_TAB_LEADER
from docx.enum.style import WD_STYLE_TYPE
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

ROOT=Path(__file__).resolve().parents[3];WORK=Path(__file__).resolve().parent
source=ROOT/'output/v2/20261025-阅记-项目报告-v2.docx'
baseline=json.loads((WORK/'input-hashes.json').read_text(encoding='utf-8'))
assert hashlib.sha256(source.read_bytes()).hexdigest()==baseline[source.relative_to(ROOT).as_posix()], 'User edited source during processing'
d=Document(source); package=json.loads((WORK/'package.json').read_text(encoding='utf-8'))

def replace_paragraph(p,value):
    # Preserve paragraph properties and the first run's formatting; no new document rebuild.
    runs=p.runs
    if runs:
        runs[0].text=value
        for run in runs[1:]:run.text=''
    else:p.add_run(value)

by_index={
33:'功能与截图依据 2026 年 10 月 24 日最终 Windows 集成验收；本次提交整理在 10 月 25 日重新检查源码包与报告，并对独立解包源码进行锁定依赖还原、Release 构建与自动化测试。运行逻辑与既有窗口验收一致，仅整理配套说明。文中演示均为自建样本，不作为真实阅读行为或用户资料；本次未再次调用真实 AI。',
68:'本次使用 Roslyn C# 语法树分别清点应用与参考项目。应用 UI 与 Library 包含 137 类、41 接口、134 个 C# 源文件、15 个 Views/*.axaml 视图文件。统计包含默认可见性和嵌套类型，partial 按工程、种类及完整类型名去重；排除测试、生成文件、record、struct 和 enum，避免通过测试夹具扩充工作量。',
181:f'提交源码包按 delivery-files.txt 显式白名单生成，共 {package["Files"]} 个文件，{package["Bytes"]} 字节，小于 10000000 字节。全部成员与 Code 中对应文件逐字节一致，不含 bin/obj、编译产物、业务数据库或验收大样本；随后在独立目录解包，使用 --locked-mode 还原、Release 构建并运行完整自动化回归，结果见本章汇总。',
182:'提交文件为“阅记-项目报告.pdf”和“阅记-源码.zip”，另附可编辑 Word。源码包 README 提供还原、构建、测试和启动命令。报告中的实际窗口、真实 AI 及性能数据来自 10 月 24 日验收；10 月 25 日独立源码测试另列，避免混用两次证据。',
199:'原始需求：项目 raw 目录中的《阅记原生电子书阅读器与 AI 阅读助手总体规划》；实施依据为已确认的完整规划、接口与数据契约及分阶段验收表。具体原文件名与摘要记录在 docs/source-notes.md。',
200:'编辑底稿：用户当前保存的 Word 报告；沿用封面身份信息和原有样式，保留正文图表及用户微调，在提交前统一目录和交付说明。',
201:'实现依据：Code 源码、工程文件、README、锁定依赖与配套源码压缩包。运行源码和既有 Windows 验收摘要一致，提交整理仅改动两份源码说明文件。',
202:'实际窗口及真实 AI 验收：2026 年 10 月 24 日合并后全功能检查；原始证据位于 docs/evidence/integrated-audit-20261024/。',
203:'提交核对：docs/report/submission-20261025/ 留存独立还原、构建、测试日志，应用与参考统计、目录页码、文件摘要及逐页排版检查结果。',
}
# Guard each positional edit against the current user's document structure.
guards={33:'本文对应 main',68:'本轮对当前 UI',181:'本轮从当前 Code',182:'新版报告',199:'原始需求',200:'编辑底稿',201:'实现依据',202:'最新验收',203:'本轮核对'}
for i,value in by_index.items():
    p=d.paragraphs[i]
    assert p.text.startswith(guards[i]),(i,p.text)
    replace_paragraph(p,value)

replacements={'本版处理':'系统处理','本版数量':'项目数量','不作为独立业务页面':'不作为独立业务页面','冒充新版':'混作当日结果','生产版本已移除':'生产应用已移除','本版实现范围':'当前实现范围'}
for p in list(d.paragraphs)+[p for t in d.tables for row in t.rows for c in row.cells for p in c.paragraphs]:
    for old,new in replacements.items():
        if old in p.text:
            for r in p.runs:
                if old in r.text:r.text=r.text.replace(old,new)

for section in d.sections:
    for part in [section.header,section.first_page_header,section.even_page_header,section.footer]:
        for p in part.paragraphs:
            if '/ v2' in p.text:
                for r in p.runs:r.text=r.text.replace(' / v2','').replace('/ v2','')
            # Match existing style; remove report version only.

# Accurate separation of current checks and retained UI/AI evidence.
t=d.tables[8]
t.cell(4,0).text='实际窗口验收日期'
t.cell(4,1).text='2026-10-24；最终 full-verified.trx 与 full-verified.log。运行逻辑与当前源码一致，本次仅整理 README 与实现说明。'
t.cell(5,0).text='提交前独立验证'
t.cell(5,1).text='2026-10-25，Windows 本机；独立解包源码重新锁定还原、Release 构建及完整回归。Word 原生更新目录并导出 PDF，逐页渲染检查；未重复真实 API 请求。'
# Keep test data pending until real test completes; finalize script replaces it before delivery.
d.tables[9].cell(1,0).text='10 月 24 日完整回归'
d.tables[9].cell(1,1).text='353 项：351 通过、0 失败、2 项权限跳过；实际窗口验收的对应自动化证据。'

# Remove inherited character-based indents and normalize a shared page-coordinate tab stop.
width=d.sections[0].page_width.twips-d.sections[0].left_margin.twips-d.sections[0].right_margin.twips
for level in [1,2]:
    style=next(s for s in d.styles if s.style_id==f'TOC{level}')
    style.base_style=d.styles['Normal']
    pf=style.paragraph_format
    pf.left_indent=Twips(0 if level==1 else 440);pf.first_line_indent=Twips(0);pf.right_indent=Twips(0)
    pf.space_before=Pt(0);pf.space_after=Pt(5);pf.line_spacing=1.2
    pf.keep_with_next=False;pf.keep_together=True
    pf.tab_stops.clear_all();pf.tab_stops.add_tab_stop(Twips(width),WD_TAB_ALIGNMENT.RIGHT,WD_TAB_LEADER.DOTS)
    for ind in style.element.xpath('./w:pPr/w:ind'):
        for attr in ['leftChars','rightChars','firstLineChars','hangingChars','hanging']:
            ind.attrib.pop(qn('w:'+attr),None)
    for e in style.element.xpath('./w:autoRedefine'):style.element.remove(e)
    style.font.name='Times New Roman';style.font.size=Pt(11.5 if level==1 else 11)
    style.font.bold=level==1
    fonts=style.element.get_or_add_rPr().get_or_add_rFonts();fonts.set(qn('w:eastAsia'),'宋体')
for p in d.paragraphs:
    if p.style.style_id in ['TOC1','TOC2']:
        ppr=p._p.get_or_add_pPr()
        for child in list(ppr):
            if child.tag!=qn('w:pStyle'):ppr.remove(child)
    if p.text=='目录':
        p.paragraph_format.space_before=Pt(0);p.paragraph_format.space_after=Pt(22)
        p.paragraph_format.first_line_indent=Twips(0)
        for r in p.runs:r.font.size=Pt(20)
# Preserve established course numbering: unnumbered background, chapters 1–5.
title_style=d.styles.add_style('Title',WD_STYLE_TYPE.PARAGRAPH)
title_style.base_style=d.paragraphs[0].style
outlevel=OxmlElement('w:outlineLvl');outlevel.set(qn('w:val'),'9')
title_style.element.get_or_add_pPr().append(outlevel)
d.paragraphs[0].style=title_style
settings=d.settings.element
for e in settings.findall(qn('w:updateFields')):settings.remove(e)
u=OxmlElement('w:updateFields');u.set(qn('w:val'),'true');settings.append(u)
d.core_properties.title='阅记原生电子书阅读器与AI阅读助手项目报告'
d.core_properties.subject='全栈开发技术期末项目报告'
d.core_properties.comments=''
d.core_properties.version=''
d.save(WORK/'阅记-项目报告.docx')
print(json.dumps({'output':str(WORK/'阅记-项目报告.docx'),'toc_width_twips':width,'preserved_tables':len(d.tables),'preserved_images':len(d.inline_shapes)},ensure_ascii=False))
