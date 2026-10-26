hwnd=foreground();left,top,right,bottom=win32gui.GetWindowRect(hwnd)
drag(left+700,top+510,left+1030,top+510);keys(0x11,0x43)
find('已复制选区正文。','Text');copied=clipboard()
assert '星光落在' in copied and len(copied)>2
keys(0x11,0x53);time.sleep(.5)
a=snapshot('04-txt-quote');assert len(a['QuoteOrigin'])==1 and len(a['Quote'])==2
click('保存书摘 Ctrl+S');assert len(snapshot('05-txt-quote-deduped')['QuoteOrigin'])==1
edit(1,'28');find(kind='Edit',index=1).SetFocus();keys(0x27);click('应用排版')
click('全屏 F11');shot('03-txt-fullscreen');keys(0x7A)
click('← 返回');click('＋ 阅读计划');click('保存并返回');click('开始 / 继续阅读')
click('标记读完');click('下一页 →');click('← 上一页')
a=snapshot('06-txt-completed');assert len(a['ReadingPlan'])==2
click('阅读统计');shot('04-combined-statistics')
click('我的书库');edit(0,'');click('搜索 / 刷新');click('导入 TXT / EPUB')
sample=ROOT/'docs/evidence/recheck-20261024/p7-compatibility/sources/large-image.epub'
assert sample.exists()
find('文件名(N):','Edit').GetCurrentPattern(ValuePattern.Pattern).SetValue(str(sample));click('打开(O)')
for attempt in range(100):
    with sqlite3.connect((Path(state()['data'])/'readjournal.sqlite3').as_uri()+'?mode=ro',uri=True) as db:
        count=db.execute('select count(*) from EbookFile').fetchone()[0]
    if count==2:break
    time.sleep(.2)
assert count==2
edit(0,'large-image');click('搜索 / 刷新');click('打开 →');click('开始 / 继续阅读')
shot('05-native-epub')
