import re
evidence=ROOT/'docs/evidence/recheck-20261024'
def query(sql):
    with sqlite3.connect((Path(state()['data'])/'readjournal.sqlite3').as_uri()+'?mode=ro',uri=True) as connection:
        return connection.execute(sql).fetchall()
def names():return [e.Current.Name for e in elements() if e.Current.ControlType.ProgrammaticName=='ControlType.Text']
def usage():
    for e in elements():
        if e.Current.ControlType.ProgrammaticName=='ControlType.Edit':
            value=e.GetCurrentPattern(ValuePattern.Pattern).Current
            if value.IsReadOnly:
                match=re.search(r'服务报告 token：输入(\d+)，输出(\d+)',value.Value)
                if match:return {'inputTokens':int(match[1]),'outputTokens':int(match[2])}
    raise AssertionError('Provider usage absent')
def send(button,operation,scope):
    click(button)
    confirmation=next((n for n in names() if '真实服务，将上传所选正文' in n and scope in n),None)
    assert confirmation
    characters=int(re.search(r'，(\d+)字符',confirmation)[1]);assert 0<characters<=600
    started=time.monotonic();click('确定')
    deadline=time.monotonic()+55
    while time.monotonic()<deadline:
        rows=names()
        if any(n==scope+'：分析完成，未写入书摘。' or n.startswith('已自动保存') for n in rows):break
        if any(any(x in n for x in ['未自动重发','服务拒绝','密钥无效','连接不可用','返回内容无效','请求超时']) for n in rows):
            record('real-ai-failure',operation=operation,success=False)
            raise AssertionError('Real API returned a classified application failure; no automatic retry')
        time.sleep(.2)
    else:raise AssertionError('Real API observation timed out; no automatic retry')
    row={'operation':operation,'success':True,'mode':'real','characters':characters,'elapsedMs':round((time.monotonic()-started)*1000),**usage()}
    (evidence/('real-'+operation+'.json')).write_text(json.dumps(row,indent=2),encoding='utf-8')
    record('real-ai-result',**row)
    return row

click('我的书库');edit(0,'recheck-ai');click('搜索 / 刷新');click('打开 →');click('开始 / 继续阅读')
edit(1,'22');click('应用排版')
baseline=query('select count(*) from Quote')[0][0]
send('分析所选范围','analysis','当前页')
assert query('select count(*) from Quote')[0][0]==baseline
hwnd=foreground();left,top,right,bottom=win32gui.GetWindowRect(hwnd)
toc=find('第一章 春日河岸','Button').Current.BoundingRectangle
drag(toc.Right+60,toc.Y+100,toc.Right+400,toc.Y+100)
send('解释选区','explanation','当前选区')
assert query('select count(*) from Quote')[0][0]==baseline
row=send('生成并自动保存书摘','extraction','当前页')
summary=next(n for n in names() if n.startswith('已自动保存'))
match=re.fullmatch(r'已自动保存(\d+)条，重复(\d+)条，拒绝(\d+)条。',summary);assert match
inserted=int(match[1]);assert inserted>0
source=(evidence/'samples/recheck-ai.txt').read_text(encoding='utf-8')
newquotes=query("select q.Text from Quote q join QuoteOrigin o on o.QuoteId=q.Id join Book b on b.Id=q.BookId where b.Title='recheck-ai'")
assert len(newquotes)==inserted and all(text[0] in source for text in newquotes)
row.update(saved=inserted,duplicates=int(match[2]),rejected=int(match[3]),allOriginalQuotesExactlyMatchSource=True)
(evidence/'real-extraction.json').write_text(json.dumps(row,indent=2),encoding='utf-8')
click('书摘收藏');click('回到原文');click('复制选区 Ctrl+C');find('已复制选区正文。','Text')
assert clipboard() in [q[0] for q in newquotes]
click('书摘收藏');click('撤销AI批次');click('确定')
for i in range(50):
    if query('select count(*) from Quote')[0][0]==baseline:break
    time.sleep(.2)
assert query('select count(*) from Quote')[0][0]==baseline
row={'returnToOriginal':True,'copiedSelectionExactlyMatchesSavedQuote':True,'undoDeleted':inserted,'baselineQuotesPreserved':baseline}
(evidence/'real-locate-undo.json').write_text(json.dumps(row,indent=2),encoding='utf-8')
record('real-locate-undo',**row)
