click("设置与数据")
for e in elements():
 if e.Current.ControlType.ProgrammaticName in ["ControlType.Edit","ControlType.CheckBox"]:
  print(e.Current.ControlType.ProgrammaticName,e.Current.Name,e.Current.BoundingRectangle)
shot("07-p9-settings-before-key")
