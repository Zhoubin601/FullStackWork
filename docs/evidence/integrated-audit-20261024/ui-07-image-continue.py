
click('我的书库'); edit(0,'large-image'); click('搜索 / 刷新'); click('打开 →'); click('开始 / 继续阅读')
click('下一页 →'); shot('13b-epub-image-actual'); close()
a=snapshot('08b-epub-image-closed')
ebook=next(b for b in a['Book'] if 'large-image' in b['Title'])
position=next(p for p in a['EbookProgress'] if p['BookId']==ebook['Id'])['LastPositionJson']
assert json.loads(position)['Value'].get('NonTextBlockKey')
epub_selected=next(q['Text'] for q in a['Quote'] if q['BookId']==ebook['Id'])
start(state()['data'])
click('我的书库'); edit(0,'large-image'); click('搜索 / 刷新'); click('打开 →'); click('开始 / 继续阅读')
expect(lambda: find('生成并自动保存书摘','Button').Current.IsEnabled,'EPUB重启后图片页会话激活')
shot('14-epub-image-resumed')
click('书摘收藏'); click('回到原文',0); click('复制选区 Ctrl+C'); find('已复制选区正文。','Text')
assert clipboard()==epub_selected
shot('15-epub-quote-return')
click('设置与数据')
