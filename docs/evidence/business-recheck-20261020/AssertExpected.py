"""Regression assertion: a completed restore must retain all restored quote fields."""
import json
import sys
from pathlib import Path

result = json.loads(Path(sys.argv[1]).read_text())
assert result["NavigatedWhileOldOperationBusy"] is True
assert result["CanLeaveWhileBusy"] is True
assert result["RestoreError"] == result["ToggleError"] == ""
assert result["RestoreStatus"].startswith("恢复成功")
assert result["ActualBookTitle"] == "RESTORED book"
assert result["ActualQuoteText"] == result["ExpectedQuoteText"], (
    "P1: restore reports success, but the earlier favorite operation overwrites restored text: "
    + repr(result["ActualQuoteText"])
)
assert result["ActualThoughts"] == "RESTORED thoughts"
assert result["ActualPage"] == 2
