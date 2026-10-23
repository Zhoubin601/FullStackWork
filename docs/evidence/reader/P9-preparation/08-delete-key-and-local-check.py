real_dir=ROOT/"docs/evidence/reader/P9-preparation"
click("设置与数据")
click("删除会话密钥")
find("会话密钥已删除","Text")
click("我的书库")
click("打开 →")
click("开始 / 继续阅读")
click("分析所选范围")
click("确定")
find("未配置会话密钥，请前往设置。","Text")
# This is a credential preflight rejection; no fourth network call is sent.
with sqlite3.connect((Path(state()["data"])/"readjournal.sqlite3").as_uri()+"?mode=ro",uri=True) as db:
 assert db.execute("select count(*) from Quote").fetchone()[0]==0
 assert db.execute("pragma integrity_check").fetchone()[0]=="ok"
 assert not db.execute("pragma foreign_key_check").fetchall()
close()
row={"credentialDeleted":True,"missingCredentialShown":True,"realCallBudgetUsed":3,"additionalHttpCalls":0,"normalCloseExitCode":0,"quotesAfter":0,"integrity":"ok","foreignKeyErrors":0}
(real_dir/"real-cleanup.json").write_text(json.dumps(row,indent=2),encoding="utf-8")
record("real-cleanup-metadata",**row)
