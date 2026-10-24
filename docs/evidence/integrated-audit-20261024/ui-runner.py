"""Isolated real Windows UI checks; credentials only arrive through stdin."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path.cwd() / 'docs/tools/reader'))
import windows_acceptance as d

d.EVIDENCE = Path.cwd() / 'docs/evidence/integrated-audit-20261024/desktop'
d.EVIDENCE.mkdir(parents=True, exist_ok=True)
d.STATE = d.EVIDENCE / 'desktop-state.json'
original_elements = d.elements

def stable_elements():
    for attempt in range(20):
        try:
            return original_elements()
        except Exception:
            if attempt == 19:
                raise
            d.time.sleep(.15)

d.elements = stable_elements
original_click = d.click

def settled_click(name, index=0):
    deadline = d.time.monotonic() + 30
    while not d.find(name, 'Button', index).Current.IsEnabled:
        if d.time.monotonic() > deadline:
            raise AssertionError('Button remained disabled: ' + name)
        d.time.sleep(.2)
    element = d.find(name, 'Button', index)
    if d.TogglePattern.Pattern.Id in [p.Id for p in element.GetSupportedPatterns()]:
        element.GetCurrentPattern(d.TogglePattern.Pattern).Toggle()
        d.time.sleep(.5)
        d.record('toggle', name=name)
    else:
        original_click(name, index)

d.click = settled_click

def expect(predicate, label, timeout=30):
    deadline = d.time.monotonic() + timeout
    while d.time.monotonic() < deadline:
        if predicate():
            d.record('assert', label=label, passed=True)
            return
        d.time.sleep(.2)
    raise AssertionError(label)

def rows(table):
    database = Path(d.state()['data']) / 'readjournal.sqlite3'
    with d.sqlite3.connect(database.as_uri() + '?mode=ro', uri=True) as connection:
        connection.row_factory = d.sqlite3.Row
        return [dict(row) for row in connection.execute('select * from "' + table + '"')]

d.expect = expect
d.rows = rows
action = sys.argv[1]
if action == 'start':
    d.start(sys.argv[2])
elif action == 'dump':
    d.dump()
elif action == 'close':
    d.close()
elif action == 'credential':
    import getpass
    secret = getpass.getpass('Session credential (not recorded): ') if sys.stdin.isatty() else sys.stdin.readline().strip()
    try:
        assert secret
        box = next(e for e in d.elements() if e.Current.AutomationId == 'ApiKeyBox')
        box.GetCurrentPattern(d.ValuePattern.Pattern).SetValue(secret)
        d.click('保存会话密钥')
        assert box.GetCurrentPattern(d.ValuePattern.Pattern).Current.Value == ''
        d.find('已配置会话密钥，退出应用后自动清除', 'Text')
        d.record('credential-loaded-in-memory', inputCleared=True, persisted=False)
    finally:
        secret = None
elif action == 'recipe':
    recipe = Path(sys.argv[2])
    exec(compile(recipe.read_text(encoding='utf-8'), str(recipe), 'exec'), vars(d))
else:
    raise ValueError(action)
