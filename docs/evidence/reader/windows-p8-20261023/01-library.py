click("我的书库")
for e in elements():
 if e.Current.ControlType.ProgrammaticName in ["ControlType.Button","ControlType.Text"]: print(e.Current.Name)
