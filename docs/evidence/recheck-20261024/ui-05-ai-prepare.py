click('我的书库');edit(0,'');click('搜索 / 刷新')
source=ROOT/'docs/evidence/recheck-20261024/samples/recheck-ai.txt'
text='第一章 春日河岸\n春天的河流缓缓流过。小林沿着河岸散步，把途中见到的花草记在本子里。\n阅读让平凡的日子拥有新的光亮。她决定每天留出一刻钟，认真读完一页书。'
source.write_text(text,encoding='utf-8')
click('导入 TXT / EPUB')
find('文件名(N):','Edit',timeout=15).GetCurrentPattern(ValuePattern.Pattern).SetValue(str(source));click('打开(O)')
for attempt in range(100):
    with sqlite3.connect((Path(state()['data'])/'readjournal.sqlite3').as_uri()+'?mode=ro',uri=True) as db:
        count=db.execute('select count(*) from EbookFile').fetchone()[0]
    if count==3:break
    time.sleep(.2)
assert count==3
click('设置与数据')
mock=find('使用本地模拟（Mock）','CheckBox').GetCurrentPattern(TogglePattern.Pattern)
if str(mock.Current.ToggleState)=='On':mock.Toggle()
upload=find('我了解真实模式会将确认范围正文发送至 DeepSeek','CheckBox').GetCurrentPattern(TogglePattern.Pattern)
if str(upload.Current.ToggleState)!='On':upload.Toggle()
edit(1,'deepseek-flash');edit(2,'600');edit(3,'1024');click('保存 AI 配置')
config=json.loads((Path(state()['data'])/'ai-settings.json').read_text(encoding='utf-8'))
assert not config['UseMock'] and config['HasAcknowledgedTextUpload'] and config['Model']=='deepseek-flash'
record('real-ai-ready',maxInputCharacters=600,maxOutputTokens=1024,syntheticSourceCharacters=len(text),plannedFunctionalCalls=3)
