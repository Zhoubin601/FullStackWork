"""P10 real Windows UI driver; isolate evidence and optionally launch an unpacked app."""
import sys
from pathlib import Path
import windows_acceptance as driver

driver.EVIDENCE = Path.cwd() / "docs/evidence/reader/P10/desktop"
driver.EVIDENCE.mkdir(parents=True, exist_ok=True)
driver.STATE = driver.EVIDENCE / "desktop-state.json"
_elements = driver.elements

def stable_elements():
    # Native picker teardown can briefly invalidate UIAutomation nodes.
    for attempt in range(10):
        try:
            return _elements()
        except Exception:
            if attempt == 9:
                raise
            driver.time.sleep(.15)

driver.elements = stable_elements

def start(data, app=None):
    data = str(Path(data).resolve())
    executable = str(Path(app).resolve()) if app else str(driver.ROOT / "Code/ReadJournal/bin/Release/net8.0/ReadJournal.exe")
    env = dict(driver.os.environ, READJOURNAL_DATA_DIR=data, READJOURNAL_LOG_BINDINGS="1")
    log = driver.EVIDENCE / ("app-" + str(driver.time.time_ns()) + ".log")
    with log.open("wb") as stream:
        process = driver.subprocess.Popen([executable], env=env, stdout=stream, stderr=stream, creationflags=driver.subprocess.CREATE_NO_WINDOW)
    driver.STATE.write_text(driver.json.dumps({"pid": process.pid, "data": data, "log": str(log), "app": executable}), encoding="utf-8")
    driver.find("阅记 · 阅读与书摘", "Window", timeout=20)
    driver.time.sleep(1)
    driver.record("start", pid=process.pid, data=data, app=executable)

driver.start = start
action = sys.argv[1]
if action == "start":
    start(sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else None)
elif action == "close":
    driver.close()
elif action == "dump":
    driver.dump()
elif action == "recipe":
    path = Path(sys.argv[2])
    exec(compile(path.read_text(encoding="utf-8"), str(path), "exec"), vars(driver))
else:
    raise ValueError(action)
