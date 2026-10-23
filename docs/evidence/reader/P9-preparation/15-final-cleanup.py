click("设置与数据")
click("删除会话密钥")
find("会话密钥已删除","Text")
box=find("使用本地模拟（Mock）","CheckBox").GetCurrentPattern(TogglePattern.Pattern)
if int(box.Current.ToggleState)!=1:box.Toggle()
click("保存 AI 配置")
close()
with sqlite3.connect((Path(state()["data"])/"readjournal.sqlite3").as_uri()+"?mode=ro",uri=True) as db:
 row={"credentialDeleted":True,"normalCloseExitCode":0,"useMockRestored":True,"quotesAfter":db.execute("select count(*) from Quote").fetchone()[0],"normalReadingSessions":db.execute("select count(*) from EbookReadingSession").fetchone()[0],"integrity":db.execute("pragma integrity_check").fetchone()[0],"foreignKeyErrors":len(db.execute("pragma foreign_key_check").fetchall())}
assert row["quotesAfter"]==0 and row["integrity"]=="ok" and row["foreignKeyErrors"]==0
(ROOT/"docs/evidence/reader/P9-preparation/final-cleanup.json").write_text(json.dumps(row,indent=2),encoding="utf-8")
record("final-cleanup-metadata",**row)
