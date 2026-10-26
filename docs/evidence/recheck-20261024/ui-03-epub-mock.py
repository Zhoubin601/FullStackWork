hwnd=foreground();left,top,right,bottom=win32gui.GetWindowRect(hwnd)
drag(left+640,top+460,left+960,top+460)
click('复制选区 Ctrl+C');find('已复制选区正文。','Text');assert len(clipboard())>2
click('保存书摘 Ctrl+S')
a=snapshot('07-epub-quote');assert len(a['QuoteOrigin'])==2
click('分析所选范围');click('确定')
find('当前页：分析完成，未写入书摘。','Text')
assert len(snapshot('08-mock-analysis')['Quote'])==3
click('生成并自动保存书摘');click('确定')
for attempt in range(100):
    names=[e.Current.Name for e in elements()]
    if any(n.startswith('已自动保存') for n in names):break
    time.sleep(.2)
assert any(n.startswith('已自动保存1条') for n in names)
click('撤销本批新增书摘')
assert len(snapshot('09-mock-undone')['Quote'])==3
# Advance until the picture page is shown. At 28pt this fixture uses more pages.
for i in range(6):click('下一页 →')
shot('06-epub-image')
close()
a=snapshot('10-epub-closed')
assert len(a['Book'])==3 and len(a['EbookFile'])==2 and len(a['QuoteOrigin'])==2
assert any(json.loads(p['LastPositionJson'])['Value'].get('NonTextBlockKey') for p in a['EbookProgress'])
start(state()['data'])
click('我的书库');edit(0,'large-image');click('搜索 / 刷新');click('打开 →');click('开始 / 继续阅读')
shot('07-epub-image-resumed')
click('书摘收藏');click('回到原文');click('复制选区 Ctrl+C');find('已复制选区正文。','Text')
shot('08-epub-quote-return')
click('设置与数据')
