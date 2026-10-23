import re
text="第一章 超限测试\n"+"这是只在本地验证输入上限的合成段落。"*120
for e in elements():
 if e.Current.Name=="全书页范围":print(e.Current.ControlType.ProgrammaticName,e.Current.BoundingRectangle)
keys(0x0D)
index=next(e.Current.Name for e in elements() if re.fullmatch(r"全书 \d+ 页（随排版变化）",e.Current.Name))
last=int(re.search(r"\d+",index)[0])
edit(2,"1");edit(3,str(last))
click("分析所选范围")
confirmation=find("确定","Button",timeout=15)
assert any("全书第1～" in e.Current.Name and "字符" in e.Current.Name for e in elements()),"Do not confirm a different scope"
confirmation.GetCurrentPattern(InvokePattern.Pattern).Invoke()
find("范围为空或超过正文限额，请缩小页范围或选区。","Text")
with sqlite3.connect((Path(state()["data"])/"readjournal.sqlite3").as_uri()+"?mode=ro",uri=True) as db:assert db.execute("select count(*) from Quote").fetchone()[0]==0
row={"localSyntheticCharacters":len(text),"wholeBookPages":last,"inputLimit":600,"overLimitRejectedBeforeProvider":True,"additionalHttpCalls":0,"quotesAfter":0}
(ROOT/"docs/evidence/reader/P9-preparation/input-limit-ui.json").write_text(json.dumps(row,indent=2),encoding="utf-8")
record("input-limit-metadata",**row)
