import re
row={}
for e in elements():
 if e.Current.ControlType.ProgrammaticName=="ControlType.Text" and e.Current.Name.startswith("已自动保存"):
  row["summary"]=e.Current.Name
 if e.Current.ControlType.ProgrammaticName=="ControlType.Edit":
  v=e.GetCurrentPattern(ValuePattern.Pattern).Current
  if v.IsReadOnly:
   m=re.search(r"服务报告 token：输入(\d+)，输出(\d+)",v.Value)
   if m: row.update(inputTokens=int(m[1]),outputTokens=int(m[2]))
   if "拒绝原因：" in v.Value: row["localRejectionReasons"]=v.Value.split("拒绝原因：",1)[1]
with sqlite3.connect((Path(state()["data"])/"readjournal.sqlite3").as_uri()+"?mode=ro",uri=True) as db:
 row["counts"]={name:db.execute("select count(*) from "+name).fetchone()[0] for name in ["Quote","QuoteOrigin","AiGenerationBatch","AiGenerationBatchItem"]}
(ROOT/"docs/evidence/reader/P9-preparation/real-extraction-diagnostics.json").write_text(json.dumps(row,ensure_ascii=False,indent=2),encoding="utf-8")
print(json.dumps(row,ensure_ascii=False))
