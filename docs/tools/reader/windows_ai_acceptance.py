"""Reuse the real Windows UI driver with a separate P8 evidence directory.

Never enter real credentials through this driver: edit() records its argument.
Real P9 credentials must be entered by the user in the masked application field.
"""
import sys
from pathlib import Path
import windows_acceptance as driver

driver.EVIDENCE = Path.cwd() / "docs/evidence/reader/windows-p8-20261023/desktop"
driver.EVIDENCE.mkdir(parents=True, exist_ok=True)
driver.STATE = driver.EVIDENCE / "desktop-state.json"
action = sys.argv[1]
if action == "start":
    driver.start(sys.argv[2])
elif action == "dump":
    driver.dump()
elif action == "close":
    driver.close()
elif action == "recipe":
    path = Path(sys.argv[2])
    exec(compile(path.read_text(encoding="utf-8"), str(path), "exec"), vars(driver))
else:
    raise ValueError(action)
