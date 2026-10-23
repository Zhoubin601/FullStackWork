backup=EVIDENCE/"isolated-data/epub-desktop-complete.zip"
find("文件名:","Edit").GetCurrentPattern(ValuePattern.Pattern).SetValue(str(backup))
click("保存(S)")
for attempt in range(50):
    if backup.exists(): break
    time.sleep(.2)
assert backup.exists() and backup.stat().st_size>0
shot("33-epub-complete-backup-saved")
click("我的书库")
click("打开 →")
click("删除书籍")
dump()
