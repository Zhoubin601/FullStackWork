present=any(e.Current.Name.startswith("已配置会话密钥") for e in elements() if e.Current.ControlType.ProgrammaticName=="ControlType.Text")
print(json.dumps({"sessionCredentialConfigured":present}))
