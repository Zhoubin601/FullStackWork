import zipfile
backup=ROOT/"docs/evidence/reader/P10/isolated-data/migrated-complete.zip"
click("设置与数据");click("导出完整 ZIP 备份")
for name in ["文件名:","文件名(N):"]:
 try:entry=find(name,"Edit",timeout=1);break
 except AssertionError:pass
entry.GetCurrentPattern(ValuePattern.Pattern).SetValue(str(backup));click("保存(S)")
for i in range(300):
 if backup.exists() and zipfile.is_zipfile(backup):break
 time.sleep(.2)
assert backup.exists()
expected=snapshot("17-migrated-before-delete")
ebook_ids={f["BookId"] for f in expected["EbookFile"]}
title=next(b["Title"] for b in expected["Book"] if b["Id"] in ebook_ids and b["Title"]!="mixed-lf")
click("我的书库");edit(0,title);click("搜索 / 刷新");click("打开 →");click("删除书籍");click("确定")
for i in range(150):
 with sqlite3.connect((Path(state()["data"])/"readjournal.sqlite3").as_uri()+"?mode=ro",uri=True) as db:count=db.execute("select count(*) from Book").fetchone()[0]
 if count==4:break
 time.sleep(.2)
assert count==4
click("设置与数据");click("从 ZIP / 旧 JSON 恢复")
for name in ["文件名(N):","文件名:"]:
 try:entry=find(name,"Edit",timeout=1);break
 except AssertionError:pass
entry.GetCurrentPattern(ValuePattern.Pattern).SetValue(str(backup));click("打开(O)");click("确定")
deadline=time.monotonic()+60
while time.monotonic()<deadline:
 if any("恢复成功" in e.Current.Name for e in elements()):break
 time.sleep(.2)
assert any("恢复成功" in e.Current.Name for e in elements())
actual=snapshot("18-migrated-after-restore")
for table in ["Book","EbookFile","EbookProgress","EbookReadingSession","Quote","QuoteOrigin","ReadingPlan","ReadingRecord","AiGenerationBatch","AiGenerationBatchItem"]:
 assert sorted(json.dumps(r,sort_keys=True) for r in actual[table])==sorted(json.dumps(r,sort_keys=True) for r in expected[table]),table
shot("22-final-package-migrated-restored");close()
start(state()["data"],state()["app"])
click("书摘收藏");click("回到原文");click("复制选区 Ctrl+C");find("已复制选区正文。","Text")
shot("23-final-package-migrated-reopened");close()
row={"finalPackageApp":True,"schema1MigrationAndContinuedReading":True,"txtAndEpub":True,"completeBackupDeleteRestoreReopen":True,"businessTablesExact":True,"booksRestored":5,"quoteReturnAndCopy":True,"normalCloses":2}
(ROOT/"docs/evidence/reader/P10/final-package-ui.json").write_text(json.dumps(row,indent=2),encoding="utf-8")
record("final-package-result",**row)
