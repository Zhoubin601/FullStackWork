"""Window-bound Windows UI test helper, standard library + Pillow only."""
from pathlib import Path
import ctypes as c
from ctypes import wintypes as w
import json, os, subprocess, sys, time
from PIL import ImageGrab

out=Path(__file__).resolve().parent
root=out.parents[2]
user=c.windll.user32
user.SetProcessDPIAware()
user.GetForegroundWindow.restype=w.HWND
user.GetWindowRect.argtypes=[w.HWND,c.POINTER(w.RECT)]
user.SetForegroundWindow.argtypes=[w.HWND]
user.PostMessageW.argtypes=[w.HWND,w.UINT,w.WPARAM,w.LPARAM]
def find_window():
    pid=int((out/'ui-pid.txt').read_text())
    found=[]
    @c.WINFUNCTYPE(w.BOOL,w.HWND,w.LPARAM)
    def callback(hwnd,_):
        p=w.DWORD(); user.GetWindowThreadProcessId(hwnd,c.byref(p))
        if p.value==pid and user.IsWindowVisible(hwnd):
            buf=c.create_unicode_buffer(512); user.GetWindowTextW(hwnd,buf,512)
            rect=w.RECT(); user.GetWindowRect(hwnd,c.byref(rect))
            found.append({'hwnd':hwnd,'title':buf.value,'rect':[rect.left,rect.top,rect.right,rect.bottom]})
        return True
    user.EnumWindows(callback,0)
    return found
def main_window():
    windows=find_window()
    if not windows: raise RuntimeError('No visible test-process window')
    return max(windows,key=lambda x:(x['rect'][2]-x['rect'][0])*(x['rect'][3]-x['rect'][1]))
def key(vk):
    user.keybd_event(vk,0,0,0); user.keybd_event(vk,0,2,0)
def foreground():
    hwnd=main_window()['hwnd']; user.ShowWindow(hwnd,9)
    # Windows permits foreground changes following an actual modifier input.
    user.keybd_event(0x12,0,0,0); user.SetForegroundWindow(hwnd); user.keybd_event(0x12,0,2,0); time.sleep(.25)
    if user.GetForegroundWindow()!=hwnd: raise RuntimeError('Test application did not acquire foreground')
    return hwnd
action=sys.argv[1]
if action=='start':
    label=sys.argv[2] if len(sys.argv)>2 else 'first'
    env=os.environ.copy(); env['READJOURNAL_DATA_DIR']=str(out/('clean-ui-data' if label=='clean' else 'ui-data')); env['READJOURNAL_LOG_BINDINGS']='1'
    exe=(out/'clean-source' if label=='clean' else root/'Code')/'ReadJournal/bin/Release/net8.0/ReadJournal.exe'
    with (out/f'ui-{sys.argv[2] if len(sys.argv)>2 else "first"}.log').open('wb') as log:
        p=subprocess.Popen([str(exe)],env=env,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
    (out/'ui-pid.txt').write_text(str(p.pid)); time.sleep(3)
    print(json.dumps(find_window(),ensure_ascii=False))
elif action=='windows': print(json.dumps(find_window(),ensure_ascii=False))
elif action=='shot':
    foreground(); time.sleep(.3); box=main_window()['rect']; ImageGrab.grab(bbox=tuple(box)).save(out/(sys.argv[2]+'.png')); print(box)
elif action=='click':
    foreground(); box=main_window()['rect']; user.SetCursorPos(box[0]+int(sys.argv[2]),box[1]+int(sys.argv[3])); user.mouse_event(2,0,0,0,0); user.mouse_event(4,0,0,0,0); time.sleep(.6)
elif action=='text':
    hwnd=foreground()
    for char in sys.argv[2]: user.PostMessageW(hwnd,0x0102,ord(char),0)
    time.sleep(.3)
elif action=='keys':
    foreground()
    for v in sys.argv[2:]:
        if v=='ctrl+a': user.keybd_event(0x11,0,0,0); key(0x41); user.keybd_event(0x11,0,2,0)
        else: key({'tab':9,'enter':13,'esc':27,'down':40,'up':38,'delete':46,'back':8}[v])
    time.sleep(.3)
elif action=='close':
    for window in find_window(): user.PostMessageW(window['hwnd'],0x10,0,0)
    time.sleep(1); print(json.dumps(find_window(),ensure_ascii=False))
