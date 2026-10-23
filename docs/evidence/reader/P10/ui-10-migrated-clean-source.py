import hashlib
before=json.loads((ROOT/"docs/evidence/reader/P10/legacy-before.json").read_text(encoding="utf-8"))
with sqlite3.connect((Path(state()["data"])/"readjournal.sqlite3").as_uri()+"?mode=ro",uri=True) as db:
 db.row_factory=sqlite3.Row
 assert db.execute("pragma user_version").fetchone()[0]==2
 for table,rows in before.items():
  after={r["Id"]:dict(r) for r in db.execute("select * from "+table)}
  for row in rows:assert {k:after[row["Id"]][k] for k in row}==row
click("我的书库");click("导入 TXT / EPUB")
folder=ROOT/"docs/evidence/reader/P10/isolated-data/samples"
paths=[folder/"mixed-lf.txt",folder/"image-styles.epub"]
find("文件名(N):","Edit").GetCurrentPattern(ValuePattern.Pattern).SetValue(" ".join('"'+str(p)+'"' for p in paths));click("打开(O)")
for i in range(150):
 with sqlite3.connect((Path(state()["data"])/"readjournal.sqlite3").as_uri()+"?mode=ro",uri=True) as db:count=db.execute("select count(*) from EbookFile").fetchone()[0]
 if count==2:break
 time.sleep(.2)
assert count==2
edit(0,"mixed-lf");click("搜索 / 刷新");click("打开 →");click("开始 / 继续阅读")
click("生成并自动保存书摘");click("确定");find("已自动保存1条，重复0条，拒绝0条。","Text")
click("阅读统计");shot("19-migrated-clean-app-statistics")
close()
with sqlite3.connect((Path(state()["data"])/"readjournal.sqlite3").as_uri()+"?mode=ro",uri=True) as db:
 assert db.execute("select count(*) from Book").fetchone()[0]==5
 assert db.execute("select count(*) from EbookReadingSession").fetchone()[0]==1
 assert db.execute("select count(*) from QuoteOrigin").fetchone()[0]==1
start(state()["data"],state()["app"])
click("我的书库");edit(0,"mixed-lf");click("搜索 / 刷新");click("打开 →");click("开始 / 继续阅读")
click("书摘收藏");click("回到原文");click("复制选区 Ctrl+C");find("已复制选区正文。","Text")
shot("20-migrated-clean-app-reopened")
close()
row={"unpackedApp":True,"oldSchema":1,"newSchema":2,"legacyBusinessFieldsUnchanged":True,"newTxtAndEpubImported":True,"books":5,"mockQuoteSaved":1,"normalCloseAndReopen":True,"restoredQuoteReturnAndCopy":True,"sourceFilesSha256":{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}}
(ROOT/"docs/evidence/reader/P10/clean-migrated-ui.json").write_text(json.dumps(row,indent=2),encoding="utf-8")
record("clean-migrated-result",**row)
