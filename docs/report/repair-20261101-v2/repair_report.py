from pathlib import Path
from copy import deepcopy
from hashlib import sha256
from zipfile import ZipFile, ZIP_DEFLATED
import json, math, re, textwrap
import pymupdf as fitz
from lxml import etree
from docx import Document
from docx.shared import Pt, Twips
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

ROOT=Path(__file__).resolve().parents[3]
WORK=Path(__file__).resolve().parent
SOURCE=ROOT/'output/提交材料/20261101-阅记-项目报告-v1.docx'
NAME='20261101-阅记-项目报告-v2'
TARGET=WORK/(NAME+'.docx')
def dump(name,o):(WORK/name).write_text(json.dumps(o,ensure_ascii=False,indent=2),encoding='utf-8')
def sha(p):return sha256(p.read_bytes()).hexdigest()
protected={str(p.relative_to(ROOT)):sha(p) for folder in [ROOT/'raw',ROOT/'output/提交材料'] for p in folder.rglob('*') if p.is_file()}
dump('input-hashes.json',protected)
d=Document(SOURCE)
changes=[]

def replace(p,t):
    old=p.text
    if old==t:return
    if p.runs:
        p.runs[0].text=t
        for run in p.runs[1:]:run.text=''
    else:p.add_run(t)
    changes.append({'old':old,'new':t})

def match(s):
    values=[p for p in d.paragraphs if s in p.text and not p.style.name.lower().startswith('toc')]
    assert len(values)==1,(s,len(values))
    return values[0]

def setfont(r,west='Times New Roman',east='宋体',size=10.5):
    r.font.name=west;r.font.size=Pt(size)
    fonts=r._r.get_or_add_rPr().get_or_add_rFonts()
    for attr in list(fonts.attrib):
        if attr.endswith('Theme'):del fonts.attrib[attr]
    for attr,value in [('ascii',west),('hAnsi',west),('cs',west),('eastAsia',east)]:fonts.set(qn('w:'+attr),value)

# Restore the three rows' actual cell properties, including their left labels.
fixed=[]
for table,label in [(d.tables[9],'测试日期'),(d.tables[9],'构建与运行'),(d.tables[10],'完整自动化回归')]:
    row=next(r for r in table.rows if r.cells[0].text==label)
    sample=table.rows[2]
    for c,ref in zip(row.cells,sample.cells):
        for p in c.paragraphs:
            ppr=p._p.get_or_add_pPr()
            for el in list(ppr):ppr.remove(el)
            for el in ref.paragraphs[0]._p.get_or_add_pPr():ppr.append(deepcopy(el))
            pf=p.paragraph_format;pf.first_line_indent=Pt(0);pf.left_indent=Pt(0);pf.right_indent=Pt(0)
            for run in p.runs:setfont(run,size=9)
    fixed.append(label)

# Concise, factual descriptions; keep original scope, dates and test limits.
replacements={
'个人阅读中，书籍资料': '个人阅读中，书籍资料、阅读活动与摘录通常分散记录。阅记以本地桌面应用统一管理这些信息，使用户能够阅读电子书、追踪阅读活动、整理感想并恢复自己的数据。项目在原有阅读与书摘管理基础上，加入 TXT/EPUB 原生阅读器及 DeepSeek AI 阅读辅助。用户可在同一应用中导入电子书、继续阅读、保存书摘、安排阅读计划和查看统计，并通过备份恢复书库。',
'业务数据保存在当前用户': '业务数据保存在当前用户本地应用数据目录。断网后可阅读已有书籍、保存手动书摘、查看计划与统计；只有用户主动确认的 AI 操作联网。首次构建仍需从 NuGet 还原依赖，或准备完整缓存。工程保留 UI、Library 与 UnitTest 的职责划分，业务通过接口注入，以便替换平台能力和隔离测试。',
'依赖版本以工程文件': '依赖版本以工程文件和 packages.lock.json 为准，首次还原采用 --locked-mode。旧 SQLite 原生依赖可能产生 NETSDK1206 的旧 Alpine RID 提示；当前验收覆盖 Windows，Alpine 与 macOS 尚未测试。第三方依赖及许可信息见配套源码 DEPENDENCIES.md、THIRD-PARTY-NOTICES.md，许可状态按各条目分别列明。',
'不是网页截图': '正文由原生 ReaderSurface 绘制，支持文字选择、复制和书摘定位。Ctrl+S 将有效原文位置、原文内容和来源信息保存为书摘，同一位置再次保存可去重。书摘列表中的原文入口能恢复对应书籍、章节和位置，复制回跳后高亮区得到的文字与原文逐字一致。',
'阅记已从阅读活动管理': '阅记已从阅读活动管理扩展为原生 TXT/EPUB 阅读与 AI 辅助应用，保留手动书籍、阅读记录、书摘收藏、计划和统计等原有功能。DI、三层导航与 UI/Library 分层将原生排版、平台操作与业务逻辑衔接；阅读产生的会话时长和书摘可在原有记录、统计与备份功能中统一管理。',
'系统实现的重点是保持': '实现中主要处理了阅读位置、会话状态与持久化数据之间的一致性问题。手动页码与电子书百分比分开计算，稳定正文锚点使阅读位置能随重排恢复；统一导航在切换页面前结束活动会话，事务与操作日志共同处理数据库和文件恢复。AI 仅处理用户确认的范围，逐字核验原文，并通过本地保存重试减少错误摘录和重复请求。',
'后续优先补充 Windows': '后续优先补充 Windows 100%/200% 与跨屏实测、具备权限环境下的符号链接用例和授权商业 EPUB 样本，再逐步扩大复杂排版兼容与大书库压力测试。AI 功能将继续改善解读的可读性和拒绝原因提示，保留原文核验、范围确认及本地保存重试机制。账号、同步与在线书源不在当前实现范围。',
'核验通过后，以事务保存': '核验通过后，以事务保存 Quote、QuoteOrigin、批次项及不可变回执。去重键避免相同原文位置重复生成；本地保存失败仅重试已有批次，不重新请求模型。撤销只删除该批仍未改动、未收藏的新书摘，保留先前存在、用户编辑或收藏的内容。',
'AI 本地落库失败': 'AI 本地落库失败与远端生成失败分别处理。前者保留当前会话内的待保存批次，可在本地重试；后者显示错误，不自动重发。事务回滚和提交确认丢失的情况均有回归覆盖，以验证重试时不会重复请求或重复插入。',
}
for needle,t in replacements.items():replace(match(needle),t)

# Use readable monospaced code. Only whitespace changes, not any code token.
consolas=fitz.Font(fontfile='C:/Windows/Fonts/consola.ttf')
def width(s):return consolas.text_length(s,fontsize=8.5)
def safe_positions(line):
    quote=None;escape=False;positions=[];i=0
    while i<len(line):
        ch=line[i]
        if quote:
            if escape:escape=False
            elif ch=='\\':escape=True
            elif ch==quote:quote=None
        elif ch in ['"',"'"]:quote=ch
        elif line[i:i+2]=='//':
            if i>0:positions.append(i)
            break
        elif ch.isspace() and i>0 and not line[i-1].isspace():positions.append(i)
        i+=1
    return positions
def wrap_code(code):
    output=[]
    source_lines=[]
    for line in textwrap.dedent(code).splitlines():
        # Separate consecutive statements before wrapping a long condition.
        indent=' '*(len(line)-len(line.lstrip()))
        source_lines.extend(line.replace('; if (',';\n'+indent+'if (').splitlines())
    for line in source_lines:
        base=len(line)-len(line.lstrip())
        while width(line)>448:
            possible=[p for p in safe_positions(line) if p>base+4 and width(line[:p])<=448]
            if not possible:raise ValueError('No safe code break: '+line)
            cut=max(possible)
            output.append(line[:cut].rstrip())
            line=' '*(base+4)+line[cut:].lstrip()
        output.append(line)
    return '\n'.join(output)
codes=[]
ps=list(d.paragraphs)
for idx,p in enumerate(ps):
    if re.match(r'^代码\s+\d+',p.text):
        block=ps[idx+1];before=block.text;after=wrap_code(before)
        assert re.sub(r'\s+','',before)==re.sub(r'\s+','',after)
        replace(block,after)
        pf=block.paragraph_format
        pf.first_line_indent=Pt(0);pf.left_indent=Pt(5);pf.right_indent=Pt(5)
        pf.line_spacing=Pt(10.5);pf.space_before=Pt(4);pf.space_after=Pt(5)
        pf.keep_together=True;pf.keep_with_next=True
        for r in block.runs:setfont(r,west='Consolas',size=8.5)
        codes.append({'caption':p.text,'before_lines':len(before.splitlines()),'after_lines':len(after.splitlines()),'max_line_width_pt':max(map(width,after.splitlines())),'tokens_unchanged':True})

# Keep the performance subsection with the preceding AI/recovery cases.
match('性能测量与范围说明').paragraph_format.page_break_before=False

# Stable vector diagrams: measure and outline the actual installed font.
DARK=(23/255,61/255,54/255);LINE=(90/255,109/255,101/255)
LIGHT=(237/255,244/255,240/255);GREEN=(214/255,231/255,222/255)
yahei=fitz.Font(fontfile='C:/Windows/Fonts/msyh.ttc')
def begin(w,h):
    doc=fitz.open();page=doc.new_page(width=w,height=h)
    page.insert_font(fontname='yh',fontfile='C:/Windows/Fonts/msyh.ttc')
    return doc,page
def center(page,line,x,y,size=9.2,color=DARK):
    length=yahei.text_length(line,fontsize=size)
    page.insert_text((x-length/2,y),line,fontname='yh',fontsize=size,color=color)
def box(page,x,y,w,h,lines,fill=LIGHT,size=9.2,leading=13):
    page.draw_rect(fitz.Rect(x,y,x+w,y+h),color=DARK,fill=fill,width=.65)
    for t in lines:assert yahei.text_length(t,fontsize=size)<=w-12,(t,w)
    baseline=y+(h-len(lines)*leading)/2+leading*.8
    for i,t in enumerate(lines):center(page,t,x+w/2,baseline+i*leading,size)
def edge(page,pts,dashed=False,arrow=True):
    shape=page.new_shape()
    for a,b in zip(pts,pts[1:]):shape.draw_line(a,b)
    shape.finish(color=LINE,width=.75,dashes='[3 2] 0' if dashed else None,closePath=False);shape.commit()
    if arrow:
        a,b=pts[-2:];dx=b[0]-a[0];dy=b[1]-a[1];length=math.hypot(dx,dy);dx/=length;dy/=length
        left=(b[0]-4*dx+1.8*dy,b[1]-4*dy-1.8*dx);right=(b[0]-4*dx-1.8*dy,b[1]-4*dy+1.8*dx)
        shape=page.new_shape();shape.draw_polyline([left,b,right,left]);shape.finish(color=LINE,fill=LINE,width=.3);shape.commit()
def export(doc,page,name):
    doc.subset_fonts()
    doc.save(WORK/(name+'.pdf'),garbage=4,deflate=True)
    page.get_pixmap(matrix=fitz.Matrix(3,3),alpha=False).save(WORK/(name+'.png'))
    (WORK/(name+'.svg')).write_text(page.get_svg_image(text_as_path=True),encoding='utf-8')

adoc,a=begin(465,370)
box(a,32,5,401,66,['ReadJournal · Avalonia 桌面层','Views / ReaderSurface / 原生布局','ServiceLocator 组装 DI，ViewLocator 缓存视图','字体与平台服务；Root / Menu / Content 三层导航'],fill=GREEN)
a.draw_rect(fitz.Rect(10,113,455,229),color=(.56,.67,.62),width=.65)
center(a,'ReadJournal.Library · 业务核心',130,126,9.2)
box(a,122,135,221,34,['ViewModels','命令、页面状态与阅读器生命周期'])
box(a,22,186,202,36,['Services / 接口','进度、会话、AI 核验、备份与恢复'],size=9)
box(a,251,186,192,36,['Models / Contracts','BookUid、DocumentKey、正文位置'],size=9)
edge(a,[(232.5,71),(232.5,135)])
center(a,'命令绑定、统一参数入口、构造注入',350,95,8.5)
edge(a,[(180,169),(123,186)]);edge(a,[(285,169),(347,186)],True)
edge(a,[(251,204),(224,204)],True)
box(a,10,259,141,37,['SQLite · schema 2','10 张业务表及元数据表'],size=9)
box(a,162,259,141,37,['受管书库 / 操作日志','原件、正文、图片、ZIP'],size=9)
box(a,314,259,141,37,['DeepSeek HTTPS','仅发送用户确认的正文'],size=9)
edge(a,[(80,222),(80,259)]);edge(a,[(123,222),(232.5,249),(232.5,259)]);edge(a,[(180,222),(384.5,249),(384.5,259)])
box(a,86,328,293,32,['ReadJournal.UnitTest','xUnit / Moq / Headless / 临时 SQLite'],fill=(1,1,1),size=9)
edge(a,[(86,344),(3,344),(3,204),(22,204)],True)
center(a,'规则、导航与存储验证',232.5,316,8.2)
export(adoc,a,'architecture')

edoc,e=begin(465,440)
box(e,159,4,147,44,['Book','PK Id / UNIQUE BookUid','Kind / TotalPages'],fill=GREEN,size=9.2,leading=12)
edge(e,[(232.5,48),(232.5,332)],arrow=False)
center(e,'1',239,62,8.2)
nodes=[
 (5,85,'ReadingRecord',['PK Id / FK BookId','StartPage / EndPage'],'0..*'),
 (276,85,'ReadingPlan',['PK Id / FK BookId','TargetDate'],'0..*'),
 (5,160,'EbookFile',['PK / FK BookId','FileId / Status'],'0..1'),
 (276,160,'EbookProgress',['PK / FK BookId','正文位置 / CurrentPercent'],'0..1'),
 (5,235,'EbookReadingSession',['PK SessionId / FK BookId','起止位置 / 阅读时长'],'0..*'),
 (276,235,'Quote',['PK Id / FK BookId','Text / Revision'],'0..*'),
 (5,310,'AiGenerationBatch',['PK BatchId / FK BookId','ScopeSha256 / Status'],'0..*'),
]
for x,y,name,fields,card in nodes:
    box(e,x,y,184,44,[name,*fields],size=8.8,leading=12)
    left=x<200;end=189 if left else 276
    edge(e,[(232.5,y+22),(end,y+22)])
    center(e,card,210 if left else 254,y+17,8)
box(e,5,377,184,38,['AiGenerationBatchItem','PK (BatchId, QuoteId) / 双 FK'],size=8.8,leading=12)
box(e,276,377,184,38,['QuoteOrigin','PK / FK QuoteId；BatchId 可空'],size=8.8,leading=12)
edge(e,[(97,354),(97,377)]);center(e,'0..*',121,370,8)
edge(e,[(368,279),(368,377)]);center(e,'1 : 0..1',395,335,8)
edge(e,[(276,269),(257,269),(257,363),(170,363),(170,377)])
center(e,'0..*',216,359,8)
center(e,'QuoteOrigin.BatchId → AiGenerationBatch（可空，删除批次时置空）',232.5,434,8)
export(edoc,e,'entities')

patches={}
for shape,name,w,h in [(d.inline_shapes[0],'architecture',465,370),(d.inline_shapes[1],'entities',465,440)]:
    shape.width=Pt(w);shape.height=Pt(h)
    for el in shape._inline.iter():
        for key,value in el.attrib.items():
            if key.endswith('}embed'):
                part=d.part.rels[value].target_part
                ext='svg' if el.tag.endswith('svgBlip') else 'png'
                patches[str(part.partname).lstrip('/')]=(WORK/(name+'.'+ext)).read_bytes()
    props=shape._inline.docPr
    props.set('descr','三工程分层、接口依赖和外部资源' if name=='architecture' else '十张业务表主外键关系；Book主干为1，支线标注子表基数')
replace(match('ON DELETE SET NULL'), '图中 Book 主干基数为 1，支线标注子表的 0..1 或 0..*；仅列出关键字段，完整规则见下表。实际外键默认 ON DELETE CASCADE，QuoteOrigin.BatchId 为可空关联，删除批次时 ON DELETE SET NULL。AppMetadata 保存恢复事务标记，不参与业务实体关系图。')

update=d.settings.element.find(qn('w:updateFields'))
if update is None:update=OxmlElement('w:updateFields');d.settings.element.append(update)
update.set(qn('w:val'),'true')
patches['word/document.xml']=etree.tostring(d._element,xml_declaration=True,encoding='UTF-8',standalone=True)
patches['word/settings.xml']=etree.tostring(d.settings.element,xml_declaration=True,encoding='UTF-8',standalone=True)
with ZipFile(SOURCE) as z,ZipFile(TARGET,'w',ZIP_DEFLATED) as out:
    for member in z.infolist():out.writestr(member,patches.get(member.filename,z.read(member.filename)))
dump('edit-summary.json',{'source':str(SOURCE),'target':str(TARGET),'changes':changes,'fixed_rows':fixed,'code_formatting':codes,'changed_package_parts':list(patches),'kept_application_screenshots':8})
assert all(sha(ROOT/p)==h for p,h in protected.items())
print(json.dumps({'target':str(TARGET),'fixed_rows':fixed,'codes':codes,'changed_parts':list(patches)},ensure_ascii=False,indent=2))
