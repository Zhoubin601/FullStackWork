import hashlib,zipfile
before_books=rows('Book')
click('载入虚构演示数据'); click('确定')
find('演示数据仅可载入空业务库，已有数据不会混合导入。','Text'); assert rows('Book')==before_books
click('刷新 / 重试')
theme=find(kind='ComboBox',index=0).Current.BoundingRectangle
tap(theme.X+theme.Width/2,theme.Y+theme.Height/2)
find('深色','ListItem').GetCurrentPattern(SelectionItemPattern.Pattern).Select(); click('应用主题')
shot('16-settings-dark')
backup=ROOT/'docs/evidence/integrated-audit-20261024/ui-complete.zip'
click('导出完整 ZIP 备份')
for name in ['文件名:','文件名(N):']:
    try:entry=find(name,'Edit',timeout=4);break
    except AssertionError:pass
entry.GetCurrentPattern(ValuePattern.Pattern).SetValue(str(backup)); click('保存(S)')
expect(lambda: backup.exists() and zipfile.is_zipfile(backup),'完整ZIP实际窗口导出')
expected=snapshot('09-before-backup-delete')
data=Path(state()['data'])
files_before={str(p.relative_to(data)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (data/'books'/'managed').rglob('*') if p.is_file()}
assert files_before
(EVIDENCE.parent/'managed-before.json').write_text(json.dumps(files_before,indent=2),encoding='utf-8')
click('我的书库'); edit(0,'large-image'); click('搜索 / 刷新'); click('打开 →'); click('删除书籍'); click('确定')
expect(lambda: len(rows('Book'))==3,'EPUB删除及关联数据级联')
assert len(rows('EbookFile'))==2 and len(rows('QuoteOrigin'))==1
snapshot('10-after-delete')
click('设置与数据'); click('从 ZIP / 旧 JSON 恢复')
find('文件名(N):','Edit').GetCurrentPattern(ValuePattern.Pattern).SetValue(str(backup)); click('打开(O)'); click('确定')
expect(lambda: any('恢复成功' in e.Current.Name for e in elements()),'完整ZIP实际窗口恢复',timeout=50)
actual=snapshot('11-after-restore')
tables=['Book','EbookFile','EbookProgress','EbookReadingSession','Quote','QuoteOrigin','ReadingPlan','ReadingRecord','AiGenerationBatch','AiGenerationBatchItem']
canonical=lambda rows:sorted(json.dumps(r,sort_keys=True,ensure_ascii=False) for r in rows)
for table in tables: assert canonical(actual[table])==canonical(expected[table]),table
files_after={str(p.relative_to(data)):hashlib.sha256(p.read_bytes()).hexdigest() for p in (data/'books'/'managed').rglob('*') if p.is_file()}
assert files_after==files_before
assert json.loads((data/'preferences.json').read_text(encoding='utf-8'))['Theme']=='深色'
(EVIDENCE.parent/'backup-loop.json').write_text(json.dumps({'passed':True,'businessTablesCompared':len(tables),'managedFilesCompared':len(files_before),'allRowsIdentical':True,'allFileHashesIdentical':True,'localThemePreserved':True},indent=2),encoding='utf-8')
shot('17-restored-dark')
close(); start(state()['data'])
click('我的书库'); edit(0,'large-image'); click('搜索 / 刷新'); click('打开 →'); click('开始 / 继续阅读')
expect(lambda: find('生成并自动保存书摘','Button').Current.IsEnabled,'恢复并重启后的阅读器可用')
shot('18-restored-reader-dark')
click('书摘收藏'); click('回到原文',0); click('复制选区 Ctrl+C'); find('已复制选区正文。','Text')
assert clipboard() in [q['Text'] for q in rows('Quote')]
click('设置与数据')
theme=find(kind='ComboBox',index=0).Current.BoundingRectangle; tap(theme.X+theme.Width/2,theme.Y+theme.Height/2)
find('浅色','ListItem').GetCurrentPattern(SelectionItemPattern.Pattern).Select(); click('应用主题')
click('删除会话密钥'); find('会话密钥已删除','Text')
snapshot('12-final-closed-loop')
close()
