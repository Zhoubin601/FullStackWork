for attempt in range(40):
 with sqlite3.connect((Path(state()["data"])/"readjournal.sqlite3").as_uri()+"?mode=ro",uri=True) as db:count=db.execute("select count(*) from EbookFile").fetchone()[0]
 if count==1:break
 time.sleep(.2)
assert count==1,"Import not complete; inspect application before proceeding"
a=snapshot("05-txt-import-settled")
click("导入 TXT / EPUB")
sample=ROOT/"docs/evidence/reader/P10/isolated-data/samples/mixed-lf.txt"
find("文件名(N):","Edit").GetCurrentPattern(ValuePattern.Pattern).SetValue(str(sample));click("打开(O)")
time.sleep(1)
a=snapshot("06-txt-deduplicated");assert len(a["Book"])==2
edit(0,"mixed-lf");click("搜索 / 刷新");click("打开 →");click("开始 / 继续阅读")
shot("02-native-txt")
