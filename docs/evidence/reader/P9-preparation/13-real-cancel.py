real_dir=ROOT/"docs/evidence/reader/P9-preparation"
click("我的书库");click("打开 →");click("开始 / 继续阅读")
click("分析所选范围")
budget=real_dir/"real-call-budget.json";b=json.loads(budget.read_text(encoding="utf-8"));assert b["started"]==4
b["started"]=5;budget.write_text(json.dumps(b,indent=2),encoding="utf-8")
t0=time.monotonic()
find("确定","Button").GetCurrentPattern(InvokePattern.Pattern).Invoke()
time.sleep(.25)
find("取消AI任务","Button",timeout=3).GetCurrentPattern(InvokePattern.Pattern).Invoke()
time.sleep(.5)
assert any(e.Current.Name.startswith("已取消；未完成生成不会保存") for e in elements() if e.Current.ControlType.ProgrammaticName=="ControlType.Text")
with sqlite3.connect((Path(state()["data"])/"readjournal.sqlite3").as_uri()+"?mode=ro",uri=True) as db:
 assert db.execute("select count(*) from Quote").fetchone()[0]==0
edit(1,"24");click("应用排版")
row={"operation":"AnalyzeCancelled","mode":"real","cancelAfterConfirmationMs":250,"elapsedMs":round((time.monotonic()-t0)*1000),"quotesAfter":0,"readingAppearanceStillUsable":True,"usageProvided":False,"automaticRetry":False,"providerReceiptUnknown":True}
(real_dir/"real-cancellation.json").write_text(json.dumps(row,indent=2),encoding="utf-8")
record("real-cancel-metadata",**row)
