for e in elements():
 if e.Current.ControlType.ProgrammaticName=="ControlType.Text" and any(t in e.Current.Name for t in ["失败","校验","恢复成功","恢复失败","重复","无效","不完整","原文","格式","缺失","损坏"]):print(e.Current.Name)
