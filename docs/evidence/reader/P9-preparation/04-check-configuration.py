config=json.loads((Path(state()["data"])/"ai-settings.json").read_text(encoding="utf-8"))
print(json.dumps({k:config[k] for k in ["UseMock","HasAcknowledgedTextUpload","Model","MaxInputUtf16Units","MaxOutputTokens"]}))
for e in elements():
 if e.Current.ControlType.ProgrammaticName=="ControlType.Text" and e.Current.Name.startswith(("已配置会话密钥","未配置会话密钥","会话密钥已删除")):
  print(e.Current.Name)
for name,target in [("使用本地模拟（Mock）",0),("我了解真实模式会将确认范围正文发送至 DeepSeek",1)]:
 e=find(name,"CheckBox")
 pattern=e.GetCurrentPattern(TogglePattern.Pattern)
 if int(pattern.Current.ToggleState)!=target:pattern.Toggle()
click("保存 AI 配置")
