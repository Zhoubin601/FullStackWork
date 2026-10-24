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
combo=find(kind='ComboBox',index=1); r=combo.Current.BoundingRectangle; tap(r.X+r.Width/2,r.Y+r.Height/2)
find('当前页','ListItem').GetCurrentPattern(SelectionItemPattern.Pattern).Select()
baseline=len(rows('Quote')); before_ids={q['Id'] for q in rows('Quote')}
row=send('生成并自动保存书摘','page-extraction','当前页')
new_quotes=[q for q in rows('Quote') if q['Id'] not in before_ids]
source=(evidence/'samples/real-ai.txt').read_text(encoding='utf-8')
assert new_quotes and all(q['Text'] in source for q in new_quotes)
row.update(saved=len(new_quotes),allOriginalQuotesExactlyMatchSource=True)
(evidence/'real-page-extraction.json').write_text(json.dumps(row,indent=2),encoding='utf-8')
click('撤销本批新增书摘'); expect(lambda: len(rows('Quote'))==baseline,'当前页AI书摘撤销')
combo=find(kind='ComboBox',index=1); r=combo.Current.BoundingRectangle; tap(r.X+r.Width/2,r.Y+r.Height/2)
find('全书页范围','ListItem').GetCurrentPattern(SelectionItemPattern.Pattern).Select()
edit(2,'1'); edit(3,'1')
send('AI 解读','page-range-analysis','全书第1～1页'); assert len(rows('Quote'))==baseline
shot('11-real-page-range')
click('设置与数据'); click('删除会话密钥'); find('会话密钥已删除','Text')
record('real-credential-deleted',persisted=False)
click('我的书库'); edit(0,'real-ai'); click('搜索 / 刷新'); click('打开 →'); click('开始 / 继续阅读')
combo=find(kind='ComboBox',index=1); r=combo.Current.BoundingRectangle; tap(r.X+r.Width/2,r.Y+r.Height/2)
find('当前页','ListItem').GetCurrentPattern(SelectionItemPattern.Pattern).Select()
click('AI 解读'); click('确定'); find('未配置会话密钥，请前往设置。','Text')
assert len(rows('Quote'))==baseline
click('下一页 →'); click('← 返回')
snapshot('07-ai-credential-deleted-local-readable')
