click('展开或收起章节目录'); click('展开或收起排版设置'); click('展开或收起AI阅读助手')
shot('06-all-panels-collapsed')
click('全屏 F11'); keys(0x7A)
click('展开或收起章节目录'); click('展开或收起排版设置'); click('展开或收起AI阅读助手')
shot('07-reader-panels-restored')
hwnd=foreground(); left,top,right,bottom=win32gui.GetWindowRect(hwnd)
drag(left+735,top+645,left+1120,top+645); keys(0x11,0x43)
find('已复制选区正文。','Text'); copied=clipboard()
assert len(copied)>3
keys(0x11,0x53); expect(lambda: len(rows('QuoteOrigin'))==1,'拖选快捷键保存书摘')
click('保存书摘 Ctrl+S'); assert len(rows('QuoteOrigin'))==1
snapshot('03-native-txt-quote'); shot('08-native-selection')
click('下一页 →'); click('← 上一页')
click('第二章 山河入卷'); click('← 返回')
snapshot('04-native-txt-closed')
assert len(rows('EbookReadingSession'))>=1
click('＋ 阅读计划'); click('保存并返回')
expect(lambda: len(rows('ReadingPlan'))==2,'电子书阅读计划')
click('开始 / 继续阅读'); click('标记读完'); click('下一页 →'); click('← 上一页'); click('← 返回')
assert next(r for r in rows('EbookProgress') if r['BookId']==2)['IsCompleted']==1
click('阅读统计'); shot('09-mixed-statistics')
