click('我的书库'); edit(0,'integrated'); click('搜索 / 刷新'); click('打开 →'); click('开始 / 继续阅读')
expect(lambda: find('生成并自动保存书摘','Button').Current.IsEnabled,'重置前阅读器激活')
history=len(rows('EbookReadingSession')); quote_ids={q['Id'] for q in rows('Quote')}
click('重置进度'); click('确定')
expect(lambda: find('生成并自动保存书摘','Button').Current.IsEnabled,'重置后重新激活阅读器')
click('← 返回')
p=next(p for p in rows('EbookProgress') if p['BookId']==2)
assert p['CompletedUtcTicks'] is None
position=json.loads(p['LastPositionJson'])['Value']; assert position['Utf16Offset']==0 and position['ChapterKey']=='ch-0001'
assert len(rows('EbookReadingSession'))>=history+2 and {q['Id'] for q in rows('Quote')}==quote_ids
click('我的书库'); edit(0,'')
inbox=Path(state()['data'])/'books'/'scan-audit.txt'
inbox.write_text('第一章 扫描验收\n这是独立收件区扫描样本，不涉及用户文件。\n',encoding='utf-8')
count=len(rows('EbookFile'))
click('扫描收件区'); expect(lambda: len(rows('EbookFile'))==count+1,'收件区手动扫描导入')
click('扫描收件区'); time.sleep(1); assert len(rows('EbookFile'))==count+1
snapshot('13-reset-and-scan')
shot('19-scan-and-reset-complete')
close()
