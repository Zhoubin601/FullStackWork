inbox=Path(state()["data"])/"books"
inbox.mkdir(parents=True,exist_ok=True)
text="第一章 春日河岸\n春天的河流缓缓流过。林舟在桥边停下脚步，把刚读完的故事记在本子上。\n他写道：阅读让平凡的日子拥有新的光亮。\n傍晚，他带着这句话回家，决定明天再来河边读书。"
(inbox/"p9-synthetic.txt").write_text(text,encoding="utf-8")
record("synthetic-source-created",characters=len(text))
click("扫描收件区")
click("打开 →")
click("开始 / 继续阅读")
shot("01-reader-mock")
click("分析所选范围")
for e in elements():
 if e.Current.ControlType.ProgrammaticName in ["ControlType.Button","ControlType.Text"]: print(e.Current.Name)
