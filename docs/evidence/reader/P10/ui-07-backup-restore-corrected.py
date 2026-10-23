import threading,psutil,re
net_stats={"samples":0,"maxTcpConnections":0,"remoteObserved":False}
stop_net=threading.Event()
def monitor():
 process=psutil.Process(state()["pid"])
 while not stop_net.is_set():
  try:
   rows=process.net_connections(kind="tcp");net_stats["samples"]+=1;net_stats["maxTcpConnections"]=max(net_stats["maxTcpConnections"],len(rows))
   net_stats["remoteObserved"]|=any(row.raddr and row.raddr.ip not in ["127.0.0.1","::1"] for row in rows)
  except psutil.NoSuchProcess:break
  stop_net.wait(.05)
thread=threading.Thread(target=monitor,daemon=True);thread.start()
click("我的书库");edit(0,"mixed-lf");click("搜索 / 刷新");click("打开 →");click("开始 / 继续阅读")
click("分析所选范围");click("确定");find("当前页：分析完成，未写入书摘。","Text")
click("生成并自动保存书摘");click("确定");find("已自动保存1条，重复0条，拒绝0条。","Text")
click("撤销本批新增书摘");find("撤销完成：删除1条，保留已修改或收藏0条。","Text")
click("设置与数据")
import hashlib,zipfile,uuid
fake_key="fictional-p10-"+uuid.uuid4().hex
find(kind="Edit",index=5).GetCurrentPattern(ValuePattern.Pattern).SetValue(fake_key)
click("保存会话密钥")
find("已配置会话密钥，退出应用后自动清除","Text")
assert find(kind="Edit",index=5).GetCurrentPattern(ValuePattern.Pattern).Current.Value==""
backup=ROOT/"docs/evidence/reader/P10/isolated-data/ui-complete-v2.zip"
click("导出完整 ZIP 备份")
for name in ["文件名:","文件名(N):"]:
 try:entry=find(name,"Edit",timeout=1);break
 except AssertionError:pass
entry.GetCurrentPattern(ValuePattern.Pattern).SetValue(str(backup));click("保存(S)")
for i in range(60):
 if backup.exists() and zipfile.is_zipfile(backup):break
 time.sleep(.2)
assert backup.exists()
with zipfile.ZipFile(backup) as archive:
 for member in archive.namelist():assert fake_key.encode() not in archive.read(member)
expected=snapshot("14-before-backup-delete")
click("我的书库");edit(0,"large-image");click("搜索 / 刷新");click("打开 →");click("删除书籍");click("确定")
for i in range(60):
 with sqlite3.connect((Path(state()["data"])/"readjournal.sqlite3").as_uri()+"?mode=ro",uri=True) as db:count=db.execute("select count(*) from Book").fetchone()[0]
 if count==2:break
 time.sleep(.2)
assert count==2
click("设置与数据");click("从 ZIP / 旧 JSON 恢复")
for name in ["文件名(N):","文件名:"]:
 try:entry=find(name,"Edit",timeout=1);break
 except AssertionError:pass
entry.GetCurrentPattern(ValuePattern.Pattern).SetValue(str(backup));click("打开(O)");click("确定")
for i in range(75):
 if any("恢复成功" in e.Current.Name for e in elements()):break
 time.sleep(.2)
assert any("恢复成功" in e.Current.Name for e in elements())
actual=snapshot("15-after-restore")
for table in ["Book","EbookFile","EbookProgress","EbookReadingSession","Quote","QuoteOrigin","ReadingPlan","ReadingRecord","AiGenerationBatch","AiGenerationBatchItem"]:assert sorted(json.dumps(r,sort_keys=True) for r in actual[table])==sorted(json.dumps(r,sort_keys=True) for r in expected[table]),table
find("已配置会话密钥，退出应用后自动清除","Text")
click("删除会话密钥");find("会话密钥已删除","Text")
shot("16-complete-restored-key-deleted")
close()
for p in Path(state()["data"]).rglob("*"):
 if p.is_file() and p.suffix!=".lock":assert fake_key.encode() not in p.read_bytes()
start(state()["data"]);click("设置与数据")
assert any(e.Current.Name.startswith("未配置会话密钥") for e in elements())
click("书摘收藏");click("回到原文");click("复制选区 Ctrl+C");find("已复制选区正文。","Text")
shot("17-restored-quote-reopened")
click("阅读统计");shot("18-restored-statistics")
close()
row={"booksRestored":3,"ebooksRestored":2,"businessTablesExactlyMatchBackup":True,"fakeCredentialAbsentFromZipAndDataFiles":True,"restoreRetainedSessionCredential":True,"credentialDeleted":True,"restartHasNoCredential":True,"restoredQuoteReturnAndCopy":True,"normalCloses":2,"zipBytes":backup.stat().st_size,"zipSha256":hashlib.sha256(backup.read_bytes()).hexdigest()}
(ROOT/"docs/evidence/reader/P10/ui-closed-loop.json").write_text(json.dumps(row,indent=2),encoding="utf-8")
stop_net.set();thread.join(2)
for p in Path(state()["data"]).rglob("*"):
 if p.is_file() and p.suffix!=".lock":assert not re.search(rb"fictional-p10-[0-9a-f]{32}",p.read_bytes())
row["networkSamples"]=net_stats;row["mockAnalyzeSaveUndo"]=True
(ROOT/"docs/evidence/reader/P10/ui-closed-loop.json").write_text(json.dumps(row,indent=2),encoding="utf-8")
record("closed-loop-result",**row)
