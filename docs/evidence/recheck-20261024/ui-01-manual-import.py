click('我的书库');click('＋ 添加书籍');click('保存并返回')
assert len(snapshot('01-invalid-manual')['Book']) == 0
edit(0,'20261024 合成手动书');edit(1,'合成作者');edit(2,'复验');edit(3,'280');edit(4,'隔离功能验收')
click('保存并返回');click('打开 →');click('＋ 阅读记录')
edit(0,'1');edit(1,'35');edit(2,'40');click('保存阅读记录')
click('＋ 书摘');edit(0,'本次复验使用的合成手动书摘。');edit(1,'合成感想');edit(2,'15')
find('加入收藏','CheckBox').GetCurrentPattern(TogglePattern.Pattern).Toggle()
click('保存并返回');click('＋ 阅读计划');click('保存并返回')
a=snapshot('02-manual-complete')
assert len(a['Book'])==len(a['ReadingRecord'])==len(a['Quote'])==len(a['ReadingPlan'])==1
click('阅读统计');shot('01-manual-statistics')
click('我的书库');click('导入 TXT / EPUB')
sample=ROOT/'docs/evidence/recheck-20261024/samples/mixed-lf.txt'
find('文件名(N):','Edit').GetCurrentPattern(ValuePattern.Pattern).SetValue(str(sample));click('打开(O)')
for attempt in range(80):
    with sqlite3.connect((Path(state()['data'])/'readjournal.sqlite3').as_uri()+'?mode=ro',uri=True) as db:
        count=db.execute('select count(*) from EbookFile').fetchone()[0]
    if count==1:break
    time.sleep(.2)
assert count==1
click('导入 TXT / EPUB')
find('文件名(N):','Edit').GetCurrentPattern(ValuePattern.Pattern).SetValue(str(sample));click('打开(O)')
time.sleep(1)
assert len(snapshot('03-deduplicated')['Book'])==2
edit(0,'mixed-lf');click('搜索 / 刷新');click('打开 →');click('开始 / 继续阅读')
shot('02-native-txt')
