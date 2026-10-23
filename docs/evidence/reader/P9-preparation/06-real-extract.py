import re
real_dir=ROOT/"docs/evidence/reader/P9-preparation";budget=real_dir/"real-call-budget.json"
b=json.loads(budget.read_text(encoding="utf-8"));assert b["started"]==2
click("生成并自动保存书摘")
assert any("真实服务，将上传所选正文" in e.Current.Name and "当前页，86字符" in e.Current.Name for e in elements())
b["started"]=3;budget.write_text(json.dumps(b,indent=2),encoding="utf-8")
t0=time.monotonic();click("确定")
deadline=time.monotonic()+55;summary=None
while time.monotonic()<deadline:
 names=[e.Current.Name for e in elements() if e.Current.ControlType.ProgrammaticName=="ControlType.Text"]
 summary=next((n for n in names if n.startswith("已自动保存")),None)
 if summary:break
 if any("未自动重发" in n or "服务拒绝" in n or "密钥无效" in n or "连接不可用" in n or "返回内容无效" in n for n in names):raise AssertionError("Real extraction application error; no automatic retry")
 time.sleep(.3)
assert summary,"No completed batch; do not automatically retry"
m=re.fullmatch(r"已自动保存(\d+)条，重复(\d+)条，拒绝(\d+)条。",summary);assert m
usage=None
for e in elements():
 if e.Current.ControlType.ProgrammaticName=="ControlType.Edit":
  v=e.GetCurrentPattern(ValuePattern.Pattern).Current
  if v.IsReadOnly:
   tokens=re.search(r"服务报告 token：输入(\d+)，输出(\d+)",v.Value)
   if tokens:usage={"inputTokens":int(tokens[1]),"outputTokens":int(tokens[2])}
assert usage
with sqlite3.connect((Path(state()["data"])/"readjournal.sqlite3").as_uri()+"?mode=ro",uri=True) as db:
 quotes=db.execute("select count(*) from Quote").fetchone()[0]
 origins=db.execute("select count(*) from QuoteOrigin").fetchone()[0]
 assert quotes==int(m[1]) and quotes>0 and origins==quotes
 # Compare original quote strings in memory only; do not write content to the record.
 text=(Path(state()["data"])/"books"/"p9-synthetic.txt").read_text(encoding="utf-8")
 exact=all(row[0] in text for row in db.execute("select Text from Quote"))
 assert exact
row={"operation":"ExtractQuotes","mode":"real","characters":86,"elapsedMs":round((time.monotonic()-t0)*1000),**usage,"saved":int(m[1]),"duplicates":int(m[2]),"rejected":int(m[3]),"origins":origins,"allOriginalQuotesExactlyMatchSource":exact,"success":True}
(real_dir/"real-extraction.json").write_text(json.dumps(row,indent=2),encoding="utf-8")
record("real-extraction-metadata",**row)
