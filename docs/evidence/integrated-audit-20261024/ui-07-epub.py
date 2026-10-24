click('我的书库'); click('导入 TXT / EPUB')
sample=ROOT/'docs/evidence/integrated-audit-20261024/p7-compatibility/sources/large-image.epub'
find('文件名(N):','Edit').GetCurrentPattern(ValuePattern.Pattern).SetValue(str(sample)); click('打开(O)')
expect(lambda: len(rows('EbookFile'))==3,'EPUB原生选择器导入')
edit(0,'large-image'); click('搜索 / 刷新'); click('打开 →'); click('开始 / 继续阅读')
expect(lambda: find('生成并自动保存书摘','Button').Current.IsEnabled,'EPUB首屏会话激活')
shot('12-epub-first-page')
# Save a native EPUB selection using its real displayed text coordinates.
chapters=[e for e in elements() if e.Current.ControlType.ProgrammaticName=='ControlType.Button' and e.Current.Name.startswith('第一')]
assert chapters
toc=chapters[0].Current.BoundingRectangle
drag(toc.Right+60,toc.Y+65,toc.Right+350,toc.Y+65)
click('复制选区 Ctrl+C'); find('已复制选区正文。','Text'); epub_selected=clipboard(); assert len(epub_selected)>2
click('保存书摘 Ctrl+S'); expect(lambda: len(rows('QuoteOrigin'))==2,'EPUB书摘原生选区保存')
keys(0x7A); keys(0x7A)
for i in range(7): click('下一页 →')
shot('13-epub-image')
close()
a=snapshot('08-epub-closed')
ebook=next(b for b in a['Book'] if 'large-image' in b['Title'])
position=next(p for p in a['EbookProgress'] if p['BookId']==ebook['Id'])['LastPositionJson']
assert json.loads(position)['Value'].get('NonTextBlockKey')
start(state()['data'])
click('我的书库'); edit(0,'large-image'); click('搜索 / 刷新'); click('打开 →'); click('开始 / 继续阅读')
expect(lambda: find('生成并自动保存书摘','Button').Current.IsEnabled,'EPUB重启后图片页会话激活')
shot('14-epub-image-resumed')
click('书摘收藏'); click('回到原文',0); click('复制选区 Ctrl+C'); find('已复制选区正文。','Text')
assert clipboard()==epub_selected
shot('15-epub-quote-return')
click('设置与数据')
