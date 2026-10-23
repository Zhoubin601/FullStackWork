click("我的书库")
for i in range(150):
 with sqlite3.connect((Path(state()["data"])/"readjournal.sqlite3").as_uri()+"?mode=ro",uri=True) as db:count=db.execute("select count(*) from EbookFile").fetchone()[0]
 if count==1:break
 time.sleep(.2)
assert count==1
click("搜索 / 刷新");click("打开 →")
t0=time.monotonic();find("开始 / 继续阅读","Button").GetCurrentPattern(InvokePattern.Pattern).Invoke()
find("复制选区 Ctrl+C","Button",timeout=20)
shot("21-million-first-visible")
first_visible=(time.monotonic()-t0)*1000
building=any(e.Current.Name=="总页数计算中" for e in elements())
font=find(kind="Edit",index=1).GetCurrentPattern(ValuePattern.Pattern)
samples=[]
for i in range(20):
 t=time.monotonic();value=str(22+i%2);font.SetValue(value);assert font.Current.Value==value;samples.append((time.monotonic()-t)*1000)
# Leave while an index may be pending: inspect state and record what was actually observed.
before_leave_building=any(e.Current.Name=="总页数计算中" for e in elements())
t=time.monotonic();find("我的书库","Button").GetCurrentPattern(InvokePattern.Pattern).Invoke();find("导入 TXT / EPUB","Button",timeout=10)
leave_ms=(time.monotonic()-t)*1000
close()
row={"millionCharacterSample":True,"actualWindowDpi":144,"firstVisibleCaptureUpperBoundMs":first_visible,"firstVisibleNote":"After managed import; includes UIAutomation and screenshot overhead, OS caches warm","indexBuildingAtInputStart":building,"indexBuildingBeforeLeave":before_leave_building,"inputRoundTripMs":samples,"maximumInputRoundTripMs":max(samples),"leaveRequestMs":leave_ms,"normalClose":True}
(ROOT/"docs/evidence/reader/P10/desktop-million.json").write_text(json.dumps(row,indent=2),encoding="utf-8")
record("desktop-million-result",**row)
