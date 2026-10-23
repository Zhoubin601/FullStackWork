import re
real_dir=ROOT/"docs/evidence/reader/P9-preparation"
config=json.loads((Path(state()["data"])/"ai-settings.json").read_text(encoding="utf-8"))
assert config["UseMock"] is False and config["HasAcknowledgedTextUpload"] is True
assert config["Model"]=="deepseek-flash" and config["MaxInputUtf16Units"]==600 and config["MaxOutputTokens"]==1024
budget=real_dir/"real-call-budget.json"
assert not budget.exists(), "An existing budget must be reviewed before sending again"
budget.write_text(json.dumps({"maxCalls":3,"started":1,"mode":"real","model":"deepseek-flash"},indent=2),encoding="utf-8")
click("我的书库")
click("打开 →")
click("开始 / 继续阅读")
click("分析所选范围")
assert any("真实服务，将上传所选正文" in e.Current.Name and "86字符" in e.Current.Name for e in elements())
t0=time.monotonic()
click("确定")
deadline=time.monotonic()+55
while time.monotonic()<deadline:
 names=[e.Current.Name for e in elements() if e.Current.ControlType.ProgrammaticName=="ControlType.Text"]
 if "当前页：分析完成，未写入书摘。" in names: break
 if any("未自动重发" in n or "服务拒绝" in n or "密钥无效" in n or "连接不可用" in n or "返回内容无效" in n or "未配置会话密钥" in n for n in names):
  raise AssertionError("Real analysis returned an application error; inspect locally without logging response content")
 time.sleep(.3)
else: raise AssertionError("No success within observation limit; budget remains spent; do not retry automatically")
usage=None
for e in elements():
 if e.Current.ControlType.ProgrammaticName=="ControlType.Edit":
  v=e.GetCurrentPattern(ValuePattern.Pattern).Current
  if v.IsReadOnly:
   m=re.search(r"服务报告 token：输入(\d+)，输出(\d+)",v.Value)
   if m: usage={"inputTokens":int(m[1]),"outputTokens":int(m[2])}
assert usage
with sqlite3.connect((Path(state()["data"])/"readjournal.sqlite3").as_uri()+"?mode=ro",uri=True) as db:
 quotes=db.execute("select count(*) from Quote").fetchone()[0]
 assert quotes==0
row={"operation":"Analyze","mode":"real","characters":86,"elapsedMs":round((time.monotonic()-t0)*1000),**usage,"quotesAfter":quotes,"success":True}
(real_dir/"real-analysis.json").write_text(json.dumps(row,indent=2),encoding="utf-8")
record("real-analysis-metadata",**row)
