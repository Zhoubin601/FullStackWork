click("书摘收藏")
click("回到原文")
click("复制选区 Ctrl+C")
find("已复制选区正文。","Text")
selected=clipboard()
with sqlite3.connect((Path(state()["data"])/"readjournal.sqlite3").as_uri()+"?mode=ro",uri=True) as db:
 originals=[row[0] for row in db.execute("select Text from Quote")]
 assert selected in originals
 located_chars=len(selected)
click("书摘收藏")
click("撤销AI批次")
click("确定")
deadline=time.monotonic()+5
while time.monotonic()<deadline:
 with sqlite3.connect((Path(state()["data"])/"readjournal.sqlite3").as_uri()+"?mode=ro",uri=True) as db:
  count=db.execute("select count(*) from Quote").fetchone()[0]
 if count==0:break
 time.sleep(.2)
assert count==0
with sqlite3.connect((Path(state()["data"])/"readjournal.sqlite3").as_uri()+"?mode=ro",uri=True) as db:
 origins=db.execute("select count(*) from QuoteOrigin").fetchone()[0]
 items=db.execute("select count(*) from AiGenerationBatchItem").fetchone()[0]
 statuses=[row[0] for row in db.execute("select Status from AiGenerationBatch")]
 assert origins==items==0 and statuses==[1,1]
row={"returnToOriginal":True,"copiedSelectionExactlyMatchesSavedQuote":True,"locatedCharacters":located_chars,"undoDeleted":2,"quotesAfter":0,"originsAfter":origins,"itemsAfter":items,"bothReceiptsUndone":True}
(ROOT/"docs/evidence/reader/P9-preparation/real-locate-undo.json").write_text(json.dumps(row,indent=2),encoding="utf-8")
record("real-locate-undo-metadata",**row)
