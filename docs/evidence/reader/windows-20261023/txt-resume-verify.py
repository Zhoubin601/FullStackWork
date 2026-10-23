data = snapshot("txt-after-restart")
assert len(data["Book"]) == 1 and len(data["QuoteOrigin"]) == 1
assert len(data["EbookReadingSession"]) == 2
progress = data["EbookProgress"][0]
position = json.loads(progress["LastPositionJson"])["Value"]
assert position["ChapterKey"] == "ch-0002" and position["Utf16Offset"] == 0
assert progress["CompletedUtcTicks"] is None
assert data["QuoteOrigin"][0]["OriginalText"] == json.loads((EVIDENCE/"txt-clipboard.json").read_text(encoding="utf-8"))["text"]
assert find(kind="Edit",index=1).GetCurrentPattern(ValuePattern.Pattern).Current.Value == "28"
click("书摘收藏")
shot("20-txt-quote-list-after-menu-save")
click("回到原文")
shot("21-txt-quote-source-highlight")
click("全屏 F11")
time.sleep(.5)
shot("22-txt-fullscreen")
click("阅读统计")
shot("23-txt-session-statistics-normal-window")
data = snapshot("txt-final")
assert len(data["EbookReadingSession"]) == 4
close()
