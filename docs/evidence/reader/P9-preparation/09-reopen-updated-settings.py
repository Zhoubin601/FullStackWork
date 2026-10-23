click("设置与数据")
foreground()
r=find(kind="Edit",index=3).Current.BoundingRectangle
u32.SetCursorPos(int(r.X+r.Width/2),int(r.Y+r.Height/2))
u32.mouse_event(0x800,0,0,ctypes.c_ulong(-750).value,0)
record("updated-key-entry-ready",credentialsPresent=False,model="deepseek-flash",inputLimit=600,outputLimit=1024)
