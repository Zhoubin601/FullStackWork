"""Drive P1's real Windows window using physical mouse/key input at measured DPI."""
import ctypes
import json
from pathlib import Path
import subprocess
import sys
import time
import win32con
import win32gui
import win32process
from PIL import ImageGrab

ctypes.windll.user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
u32=ctypes.windll.user32
root=Path.cwd()
e=root/'docs/evidence/reader/windows-20261023'
label=sys.argv[1] if len(sys.argv)>1 else 'native-p1'
assert '/' not in label and '\\' not in label
out=e/label
samples=e/'isolated-data/samples'
assert not out.exists(), 'Use a new evidence directory'
log=(e/(label+'-process.log')).open('wb')
p=subprocess.Popen([str(root/'docs/experiments/ReaderPrototype/bin/Release/net8.0/ReaderPrototype.exe'),
    '--file',str(samples/'million.txt'),'--samples',str(samples),'--probe-output',str(out),
    '--await-input','--await-drag'],stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)

def window():
    found=[]
    win32gui.EnumWindows(lambda h,_: found.append(h) if win32process.GetWindowThreadProcessId(h)[1]==p.pid and win32gui.GetWindowText(h)=='阅记 P1 · 原生分页实验' else None,None)
    return found[0] if found else None

def mouse(x,y,flag=0):
    u32.SetCursorPos(round(x),round(y))
    if flag: u32.mouse_event(flag,0,0,0,0)

def key(*codes):
    for c in codes:
        u32.keybd_event(c,u32.MapVirtualKeyW(c,0),1 if c in (0x25,0x27) else 0,0)
        time.sleep(.1)
    time.sleep(.15)
    for c in reversed(codes):
        u32.keybd_event(c,u32.MapVirtualKeyW(c,0),2 | (1 if c in (0x25,0x27) else 0),0)
        time.sleep(.1)

deadline=time.monotonic()+240
handled=set()
initial=False
actions=[]
while p.poll() is None:
    assert time.monotonic()<deadline,'Real Windows P1 probe timed out'
    hwnd=window()
    if not hwnd:
        time.sleep(.05)
        continue
    if win32gui.IsIconic(hwnd): win32gui.ShowWindow(hwnd,win32con.SW_RESTORE)
    try: win32gui.SetForegroundWindow(hwnd)
    except Exception: pass
    scale=u32.GetDpiForWindow(hwnd)/96
    x,y=win32gui.ClientToScreen(hwnd,(0,0))
    if not initial:
        time.sleep(.3)
        mouse(x+700*scale,y+400*scale,2)
        mouse(x+700*scale,y+400*scale,4)
        initial=True
    for signal in ['drag-ready.json','cross-page-ready.json','focus-ready.json','resize-ready.json']:
        path=out/signal
        if signal in handled or not path.exists(): continue
        try: g=json.loads(path.read_text(encoding='utf-8'))
        except json.JSONDecodeError: continue
        if signal=='resize-ready.json':
            wr=win32gui.GetWindowRect(hwnd);cr=win32gui.GetClientRect(hwnd)
            win32gui.MoveWindow(hwnd,wr[0],wr[1],round(g['width']*scale+wr[2]-wr[0]-cr[2]),round(g['height']*scale+wr[3]-wr[1]-cr[3]),True)
        elif signal=='focus-ready.json':
            mouse(x+g['inputX']*scale,y+g['inputY']*scale,2)
            mouse(x+g['inputX']*scale,y+g['inputY']*scale,4)
            time.sleep(.1)
            key(0x27)
        else:
            sx=x+g['surfaceX']*scale;sy=y+g['surfaceY']*scale
            if signal=='drag-ready.json':
                mouse(sx+430*scale,sy+210*scale,2)
                for step in range(1,11):
                    mouse(sx+(430-step*30)*scale,sy+(210-step*13)*scale)
                    time.sleep(.02)
                mouse(sx+130*scale,sy+80*scale,4)
            else: key(0x10,0x27)
            time.sleep(.1)
            ImageGrab.grab(bbox=win32gui.GetWindowRect(hwnd),all_screens=True).save(e/(label+'-'+signal+'.png'))
            key(0x11,0x43)
        handled.add(signal)
        actions.append({'signal':signal,'actualScale':scale,'physicalWindowsInput':True})
    time.sleep(.03)
log.close()
(e/(label+'-driver.json')).write_text(json.dumps({'exitCode':p.returncode,'actions':actions},indent=2),encoding='utf-8')
assert p.returncode==0,'Inspect native-p1/probe.json and native-p1-process.log'
assert len(handled)==4
probe=json.loads((out/'probe.json').read_text(encoding='utf-8'))
assert probe['passedLocalProbe']
print(json.dumps({'passed':True,'actualScale':probe['renderScaling'],'signals':sorted(handled)}))
