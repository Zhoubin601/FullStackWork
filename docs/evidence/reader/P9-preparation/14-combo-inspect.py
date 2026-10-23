for e in elements():
 if e.Current.Name in ["全书页范围","当前页","当前选区"]:
  print(e.Current.Name,e.Current.ControlType.ProgrammaticName,e.Current.BoundingRectangle)
keys(0x24);keys(0x28);keys(0x28);keys(0x0D)
