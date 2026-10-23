import re,zipfile,hashlib,threading,psutil
expected=json.loads((EVIDENCE/"14-before-backup-delete.json").read_text(encoding="utf-8"))
actual=snapshot("16-restored-verified")
for table in ["Book","EbookFile","EbookProgress","EbookReadingSession","Quote","QuoteOrigin","ReadingPlan","ReadingRecord","AiGenerationBatch","AiGenerationBatchItem"]:
 assert sorted(json.dumps(r,sort_keys=True) for r in actual[table])==sorted(json.dumps(r,sort_keys=True) for r in expected[table]),table
backup=ROOT/"docs/evidence/reader/P10/isolated-data/ui-complete-v2.zip"
with zipfile.ZipFile(backup) as archive:
 for member in archive.namelist():assert not re.search(rb"fictional-p10-[0-9a-f]{32}",archive.read(member))
find("已配置会话密钥，退出应用后自动清除","Text")
click("删除会话密钥");find("会话密钥已删除","Text")
shot("16-complete-restored-key-deleted");close()
for p in Path(state()["data"]).rglob("*"):
 if p.is_file() and p.suffix!=".lock":assert not re.search(rb"fictional-p10-[0-9a-f]{32}",p.read_bytes())
start(state()["data"])
net={"samples":0,"maxTcpConnections":0,"remoteObserved":False};stop=threading.Event()
def monitor():
 process=psutil.Process(state()["pid"])
 while not stop.is_set():
  try:
   rows=process.net_connections(kind="tcp");net["samples"]+=1;net["maxTcpConnections"]=max(net["maxTcpConnections"],len(rows));net["remoteObserved"]|=any(row.raddr and row.raddr.ip not in ["127.0.0.1","::1"] for row in rows)
  except psutil.NoSuchProcess:break
  stop.wait(.05)
thread=threading.Thread(target=monitor,daemon=True);thread.start()
click("设置与数据");assert any(e.Current.Name.startswith("未配置会话密钥") for e in elements())
click("书摘收藏");click("回到原文");click("复制选区 Ctrl+C");find("已复制选区正文。","Text")
shot("17-restored-quote-reopened");click("阅读统计");shot("18-restored-statistics")
close();stop.set();thread.join(2)
assert not net["remoteObserved"]
row={"booksRestored":3,"ebooksRestored":2,"businessTablesExactlyMatchBackup":True,"fakeCredentialAbsentFromZipAndDataFiles":True,"restoreRetainedSessionCredential":True,"credentialDeleted":True,"restartHasNoCredential":True,"restoredQuoteReturnAndCopy":True,"normalCloses":2,"mockAnalyzeSaveUndo":True,"zipBytes":backup.stat().st_size,"zipSha256":hashlib.sha256(backup.read_bytes()).hexdigest(),"networkSamples":net}
(ROOT/"docs/evidence/reader/P10/ui-closed-loop.json").write_text(json.dumps(row,indent=2),encoding="utf-8")
record("closed-loop-result",**row)
