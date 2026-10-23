import re
real_dir=ROOT/"docs/evidence/reader/P9-preparation"
budget=real_dir/"real-call-budget.json"
b=json.loads(budget.read_text(encoding="utf-8"));assert b["started"]==1
r=find("第一章 春日河岸","Button").Current.BoundingRectangle
# Physically select part of the first body line; coordinates derive from TOC bounds.
drag(r.Right+60,r.Y+100,r.Right+400,r.Y+100)
click("解释选区")
text=next((e.Current.Name for e in elements() if "真实服务，将上传所选正文" in e.Current.Name and "当前选区" in e.Current.Name),None)
assert text,"No selected-scope confirmation; no network request was made"
m=re.search(r"，(\d+)字符",text);assert m and 0<int(m[1])<=600
characters=int(m[1]);b["started"]=2;budget.write_text(json.dumps(b,indent=2),encoding="utf-8")
t0=time.monotonic();click("确定")
deadline=time.monotonic()+55
while time.monotonic()<deadline:
 names=[e.Current.Name for e in elements() if e.Current.ControlType.ProgrammaticName=="ControlType.Text"]
 if "当前选区：分析完成，未写入书摘。" in names:break
 if any("未自动重发" in n or "服务拒绝" in n or "密钥无效" in n or "连接不可用" in n or "返回内容无效" in n for n in names):raise AssertionError("Real explanation application error; no automatic retry")
 time.sleep(.3)
else:raise AssertionError("Real explanation observation timed out; budget remains spent")
usage=None
for e in elements():
 if e.Current.ControlType.ProgrammaticName=="ControlType.Edit":
  v=e.GetCurrentPattern(ValuePattern.Pattern).Current
  if v.IsReadOnly:
   m=re.search(r"服务报告 token：输入(\d+)，输出(\d+)",v.Value)
   if m:usage={"inputTokens":int(m[1]),"outputTokens":int(m[2])}
assert usage
with sqlite3.connect((Path(state()["data"])/"readjournal.sqlite3").as_uri()+"?mode=ro",uri=True) as db:
 quotes=db.execute("select count(*) from Quote").fetchone()[0];assert quotes==0
row={"operation":"ExplainSelection","mode":"real","characters":characters,"elapsedMs":round((time.monotonic()-t0)*1000),**usage,"quotesAfter":quotes,"success":True}
(real_dir/"real-explanation.json").write_text(json.dumps(row,indent=2),encoding="utf-8")
record("real-explanation-metadata",**row)
