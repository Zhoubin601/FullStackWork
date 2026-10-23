"""Real Windows UIAutomation driver. Uses isolated data and never edits the running DB.

Requires installed pythonnet, Pillow and pywin32. Run from the repository root.
Evidence and recipes live under docs/evidence/reader/windows-20261023.
"""
import ctypes
import json
import os
from pathlib import Path
import subprocess
import sqlite3
import sys
import time

sys.stdout.reconfigure(encoding="utf-8")
ctypes.windll.user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
import clr
from System import Int32
for assembly in ["WindowsBase", "UIAutomationTypes", "UIAutomationClient"]:
    clr.AddReference(str(Path(os.environ["WINDIR"]) / "Microsoft.NET/Framework64/v4.0.30319/WPF" / (assembly + ".dll")))
from System.Windows.Automation import (AutomationElement as AE, Condition, ControlType,
    PropertyCondition, TreeScope, InvokePattern, ValuePattern, TogglePattern,
    SelectionItemPattern, ExpandCollapsePattern)
import win32clipboard
import win32con
import win32gui
import win32api
from PIL import ImageGrab

ROOT = Path.cwd()
EVIDENCE = ROOT / "docs/evidence/reader/windows-20261023"
STATE = EVIDENCE / "desktop-state.json"
EVIDENCE.mkdir(parents=True, exist_ok=True)
u32 = ctypes.windll.user32

def record(action, **fields):
    row = {"time": time.strftime("%Y-%m-%d %H:%M:%S%z"), "action": action, **fields}
    with (EVIDENCE / "actions.jsonl").open("a", encoding="utf-8") as f:
        f.write(json.dumps(row, ensure_ascii=False) + "\n")
    print(json.dumps(row, ensure_ascii=False), flush=True)

def state():
    return json.loads(STATE.read_text(encoding="utf-8"))

def windows():
    condition = PropertyCondition(AE.ProcessIdProperty, Int32(state()["pid"]))
    return list(AE.RootElement.FindAll(TreeScope.Children, condition))

def elements():
    result = []
    for win in windows():
        result += [win] + list(win.FindAll(TreeScope.Descendants, Condition.TrueCondition))
    return result

def find(name=None, kind=None, index=0, timeout=10):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        matches = [e for e in elements() if (name is None or e.Current.Name == name)
                   and (kind is None or e.Current.ControlType.ProgrammaticName.split(".")[-1] == kind)]
        if len(matches) > index:
            return matches[index]
        time.sleep(.2)
    raise AssertionError(f"UI element missing: {name!r}, {kind}, index {index}")

def dump():
    rows = []
    for e in elements():
        c = e.Current
        r = c.BoundingRectangle
        rows.append({"name": c.Name, "type": c.ControlType.ProgrammaticName,
                     "id": c.AutomationId, "enabled": c.IsEnabled, "offscreen": c.IsOffscreen,
                     "rect": [r.X, r.Y, r.Width, r.Height]})
    (EVIDENCE / "last-ui-tree.json").write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps([r for r in rows if not r["offscreen"]], ensure_ascii=False, indent=2))
    return rows

def click(name, index=0):
    e = find(name, "Button", index)
    assert e.Current.IsEnabled, f"Button disabled: {name}"
    e.GetCurrentPattern(InvokePattern.Pattern).Invoke()
    time.sleep(.5)
    record("click", name=name, index=index)

def edit(index, value):
    e = find(kind="Edit", index=index)
    e.GetCurrentPattern(ValuePattern.Pattern).SetValue(str(value))
    time.sleep(.2)
    record("edit", index=index, value=str(value))

def keys(*codes):
    foreground()
    for code in codes:
        u32.keybd_event(code, 0, 0, 0)
    time.sleep(.06)
    for code in reversed(codes):
        u32.keybd_event(code, 0, 2, 0)
    time.sleep(.25)
    record("keys", codes=codes)

def point(x, y, down=False, up=False):
    u32.SetCursorPos(int(x), int(y))
    if down: u32.mouse_event(2, 0, 0, 0, 0)
    if up: u32.mouse_event(4, 0, 0, 0, 0)

def tap(x, y):
    point(x, y, down=True)
    time.sleep(.08)
    point(x, y, up=True)
    time.sleep(.3)
    record("physical-click", x=x, y=y)

def drag(x1, y1, x2, y2):
    foreground()
    point(x1, y1, down=True)
    for i in range(1, 31):
        point(x1 + (x2-x1)*i/30, y1+(y2-y1)*i/30)
        time.sleep(.02)
    point(x2, y2, up=True)
    time.sleep(.5)
    record("physical-drag", start=[x1,y1], end=[x2,y2])

def clipboard():
    win32clipboard.OpenClipboard()
    try: return win32clipboard.GetClipboardData(win32con.CF_UNICODETEXT)
    finally: win32clipboard.CloseClipboard()

def snapshot(label):
    database = Path(state()["data"]) / "readjournal.sqlite3"
    with sqlite3.connect(database.as_uri()+"?mode=ro", uri=True) as connection:
        connection.row_factory = sqlite3.Row
        tables = [row[0] for row in connection.execute("select name from sqlite_master where type='table' and name not like 'sqlite_%' order by name")]
        data = {table: [dict(row) for row in connection.execute('select * from "'+table+'"')] for table in tables}
        assert connection.execute("pragma integrity_check").fetchone()[0] == "ok"
        assert not connection.execute("pragma foreign_key_check").fetchall()
    (EVIDENCE / (label+".json")).write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding="utf-8")
    record("database-readonly-verification", label=label, integrity="ok", foreignKeyErrors=0,
           rows={key:len(rows) for key,rows in data.items()})
    return data

def foreground():
    hwnd = int(find("阅记 · 阅读与书摘", "Window").Current.NativeWindowHandle)
    if win32gui.IsIconic(hwnd):
        win32gui.ShowWindow(hwnd, win32con.SW_RESTORE)
        time.sleep(.5)
    left,top,right,bottom=win32gui.GetWindowRect(hwnd)
    ml,mt,mr,mb=win32api.GetMonitorInfo(win32api.MonitorFromWindow(hwnd,win32con.MONITOR_DEFAULTTONEAREST))["Monitor"]
    if left<ml or top<mt or right>mr or bottom>mb:
        width,height=min(right-left,mr-ml),min(bottom-top,mb-mt)
        win32gui.MoveWindow(hwnd,max(ml,min(left,mr-width)),max(mt,min(top,mb-height)),width,height,True)
        time.sleep(.4)
    try: win32gui.SetForegroundWindow(hwnd)
    except Exception: pass
    return hwnd

def shot(name):
    hwnd = foreground()
    time.sleep(.3)
    rect = win32gui.GetWindowRect(hwnd)
    assert rect[2]-rect[0] > 500 and rect[3]-rect[1] > 400 and rect[0] > -10000, "Screenshot requires a visible restored window"
    path = EVIDENCE / (name + ".png")
    if path.exists(): raise FileExistsError(path)
    ImageGrab.grab(bbox=rect, all_screens=True).save(path)
    record("screenshot", file=path.name, rect=rect, dpi=u32.GetDpiForWindow(hwnd))

def start(data):
    data = str(Path(data).resolve())
    env = dict(os.environ, READJOURNAL_DATA_DIR=data, READJOURNAL_LOG_BINDINGS="1")
    log = EVIDENCE / ("app-" + str(time.time_ns()) + ".log")
    with log.open("wb") as f:
        p = subprocess.Popen([str(ROOT / "Code/ReadJournal/bin/Release/net8.0/ReadJournal.exe")],
                             env=env, stdout=f, stderr=f, creationflags=subprocess.CREATE_NO_WINDOW)
    STATE.write_text(json.dumps({"pid": p.pid, "data": data, "log": str(log)}), encoding="utf-8")
    find("阅记 · 阅读与书摘", "Window", timeout=20)
    time.sleep(1.5)
    record("start", pid=p.pid, data=data, log=log.name)

def close():
    hwnd = int(find("阅记 · 阅读与书摘", "Window").Current.NativeWindowHandle)
    pid = state()["pid"]
    handle = ctypes.windll.kernel32.OpenProcess(0x1000 | 0x100000, False, pid)
    assert handle
    try:
        u32.PostMessageW(hwnd, 0x10, 0, 0)
        assert ctypes.windll.kernel32.WaitForSingleObject(handle, 15000) == 0, "Normal close timed out"
        code = ctypes.c_ulong()
        assert ctypes.windll.kernel32.GetExitCodeProcess(handle, ctypes.byref(code))
        record("normal-close", pid=pid, exitCode=code.value)
        assert code.value == 0
    finally: ctypes.windll.kernel32.CloseHandle(handle)

if __name__ == "__main__":
    action = sys.argv[1]
    if action == "start": start(sys.argv[2])
    elif action == "dump": dump()
    elif action == "shot": shot(sys.argv[2])
    elif action == "close": close()
    elif action == "click": click(sys.argv[2], int(sys.argv[3]) if len(sys.argv)>3 else 0)
    elif action == "recipe": exec(compile(Path(sys.argv[2]).read_text(encoding="utf-8"), sys.argv[2], "exec"), globals())
    else: raise ValueError(action)
