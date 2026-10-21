"""Send WM_DELETE_WINDOW to the isolated Avalonia window on Xvfb :120."""
import ctypes as C
import runpy,subprocess
from pathlib import Path
u=runpy.run_path(str(Path(__file__).with_name('ui-driver.py')))
window=int(subprocess.check_output([u['XDO'],'search','--name','阅记'],env=u['ENV'],text=True).splitlines()[0])
class Data(C.Union):_fields_=[('b',C.c_char*20),('s',C.c_short*10),('l',C.c_long*5)]
class Message(C.Structure):_fields_=[('type',C.c_int),('serial',C.c_ulong),('send_event',C.c_int),('display',C.c_void_p),('window',C.c_ulong),('message_type',C.c_ulong),('format',C.c_int),('data',Data)]
class Event(C.Union):_fields_=[('xclient',Message),('pad',C.c_long*24)]
x=C.CDLL('libX11.so.6')
x.XOpenDisplay.argtypes=[C.c_char_p];x.XOpenDisplay.restype=C.c_void_p
x.XInternAtom.argtypes=[C.c_void_p,C.c_char_p,C.c_int];x.XInternAtom.restype=C.c_ulong
x.XSendEvent.argtypes=[C.c_void_p,C.c_ulong,C.c_int,C.c_long,C.POINTER(Event)]
x.XFlush.argtypes=[C.c_void_p];x.XCloseDisplay.argtypes=[C.c_void_p]
d=x.XOpenDisplay(b':120');assert d
try:
 e=Event();e.xclient.type=33;e.xclient.display=d;e.xclient.window=window
 e.xclient.message_type=x.XInternAtom(d,b'WM_PROTOCOLS',0);e.xclient.format=32
 e.xclient.data.l[0]=x.XInternAtom(d,b'WM_DELETE_WINDOW',0)
 assert x.XSendEvent(d,window,0,0,C.byref(e))!=0
 x.XFlush(d)
finally:x.XCloseDisplay(d)
print('Sent WM_DELETE_WINDOW to isolated window',window)
