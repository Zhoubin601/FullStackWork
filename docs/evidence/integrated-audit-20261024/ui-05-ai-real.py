import re
evidence=EVIDENCE.parent

def text_names():
    return [e.Current.Name for e in elements() if e.Current.ControlType.ProgrammaticName=='ControlType.Text']

def usage():
    for e in elements():
        if e.Current.ControlType.ProgrammaticName=='ControlType.Edit':
            value=e.GetCurrentPattern(ValuePattern.Pattern).Current
            if value.IsReadOnly:
                match=re.search(r'服务报告 token：输入(\d+)，输出(\d+)',value.Value)
                if match:return {'inputTokens':int(match[1]),'outputTokens':int(match[2])}
    raise AssertionError('Provider usage absent')

def send(button, operation, scope):
    click(button)
    confirmation=next(n for n in text_names() if '将上传所选正文至 DeepSeek' in n and scope in n)
    characters=int(re.search(r'，(\d+)字符',confirmation)[1]); assert 0<characters<400
    started=time.monotonic(); click('确定')
    deadline=time.monotonic()+55
    while time.monotonic()<deadline:
        names=text_names()
        if any('解读完成，未写入书摘。' in n or n.startswith(('生成完成：','部分成功：','原文已存在，无新增：')) for n in names):break
        failures=[n for n in names if any(x in n for x in ['未自动重发','服务拒绝请求','密钥无效','连接不可用','返回内容无效','请求超时','未通过原文核验：'])]
        if failures:
            record('real-ai-failure', operation=operation, message=failures[0], automaticRetry=False)
            raise AssertionError('Real service failed; request not repeated automatically')
        time.sleep(.2)
    else:raise AssertionError('Real service observation timed out; request not repeated automatically')
    result={'operation':operation,'scope':scope,'characters':characters,'elapsedMs':round((time.monotonic()-started)*1000),**usage(),'passed':True}
    (evidence/('real-'+operation+'.json')).write_text(json.dumps(result,indent=2),encoding='utf-8')
    record('real-ai-result',**result)
    return result

click('我的书库'); edit(0,'real-ai'); click('搜索 / 刷新'); click('打开 →'); click('开始 / 继续阅读')
expect(lambda: find('AI 解读','Button').Current.IsEnabled,'真实AI阅读器激活')
baseline=len(rows('Quote'))
send('AI 解读','page-analysis','当前页'); assert len(rows('Quote'))==baseline
toc=find('第一章 自建短文','Button').Current.BoundingRectangle
drag(toc.Right+60,toc.Y+65,toc.Right+390,toc.Y+65)
click('复制选区 Ctrl+C'); find('已复制选区正文。','Text'); selected=clipboard(); assert len(selected)>3
scope=find(kind='ComboBox',index=1)
rect=scope.Current.BoundingRectangle; tap(rect.X+rect.Width/2,rect.Y+rect.Height/2)
find('当前选区','ListItem').GetCurrentPattern(SelectionItemPattern.Pattern).Select()
row=send('生成并自动保存书摘','whole-selection','当前选区')
assert len(rows('Quote'))==baseline+1
quote=rows('Quote')[-1]
assert quote['Text']==selected and quote['Thoughts'].strip()
row.update(saved=1,originalExactlyMatchesSelection=True)
(evidence/'real-whole-selection.json').write_text(json.dumps(row,indent=2),encoding='utf-8')
shot('10-real-whole-selection')
send('生成并自动保存书摘','selection-deduplication','当前选区')
assert len(rows('Quote'))==baseline+1
click('书摘收藏'); edit(0,''); click('搜索 / 刷新')
origins=[r for r in rows('QuoteOrigin') if r['QuoteId']==quote['Id']]; assert len(origins)==1
# Newest quote is shown first by the production query.
click('回到原文',0); click('复制选区 Ctrl+C'); find('已复制选区正文。','Text')
assert clipboard()==selected
click('书摘收藏'); click('撤销AI批次',0); click('确定')
expect(lambda: len(rows('Quote'))==baseline,'真实AI整段书摘撤销保留原有书摘')
snapshot('06-real-ai-undone')
(evidence/'real-quote-loop.json').write_text(json.dumps({'passed':True,'wholeSelectionExactlyPreserved':True,'deduplicated':True,'copiedBackToOriginal':True,'undoReturnedToBaseline':True,'baselineQuotes':baseline},indent=2),encoding='utf-8')
