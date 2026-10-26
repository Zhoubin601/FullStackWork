"""Run the existing Windows UI driver with isolated evidence for this recheck.

Credentials, if supplied, are read from stdin and set without the logging edit helper.
Never pass a credential to a recipe, file, command-line argument or screenshot.
"""
import sys
from pathlib import Path
sys.path.insert(0, str(Path.cwd() / 'docs/tools/reader'))
import windows_acceptance as d
d.EVIDENCE = Path.cwd() / 'docs/evidence/recheck-20261024/desktop'
d.EVIDENCE.mkdir(parents=True, exist_ok=True)
d.STATE = d.EVIDENCE / 'desktop-state.json'
original_elements = d.elements
def stable_elements():
    for attempt in range(10):
        try:
            return original_elements()
        except Exception:
            if attempt == 9:
                raise
            d.time.sleep(.15)
d.elements = stable_elements
original_click = d.click
def settled_click(name,index=0):
    deadline = d.time.monotonic()+30
    while not d.find(name,'Button',index).Current.IsEnabled:
        if d.time.monotonic()>deadline:
            raise AssertionError('Button remained disabled: '+name)
        d.time.sleep(.2)
    original_click(name,index)
d.click = settled_click
action = sys.argv[1]
if action == 'start': d.start(sys.argv[2])
elif action == 'dump': d.dump()
elif action == 'close': d.close()
elif action == 'key':
    secret = sys.stdin.readline().strip()
    try:
        assert secret
        d.find(kind='Edit',index=5).GetCurrentPattern(d.ValuePattern.Pattern).SetValue(secret)
        d.click('保存会话密钥')
        d.find('已配置会话密钥，退出应用后自动清除','Text')
        assert d.find(kind='Edit',index=5).GetCurrentPattern(d.ValuePattern.Pattern).Current.Value == ''
    finally:
        secret = None
elif action == 'real':
    secret = sys.stdin.readline().strip()
    try:
        assert secret
        d.find(kind='Edit',index=5).GetCurrentPattern(d.ValuePattern.Pattern).SetValue(secret)
        d.click('保存会话密钥')
        d.find('已配置会话密钥，退出应用后自动清除','Text')
        assert d.find(kind='Edit',index=5).GetCurrentPattern(d.ValuePattern.Pattern).Current.Value == ''
        p=Path(sys.argv[2])
        exec(compile(p.read_text(encoding='utf-8'),str(p),'exec'),vars(d))
    finally:
        try:
            d.click('设置与数据');d.click('删除会话密钥')
            d.find('会话密钥已删除','Text')
            mock=d.find('使用本地模拟（Mock）','CheckBox').GetCurrentPattern(d.TogglePattern.Pattern)
            if str(mock.Current.ToggleState)!='On':mock.Toggle()
            d.click('保存 AI 配置')
            d.close()
            import zipfile
            needles=[secret.encode('utf-8'),secret.encode('utf-16le')] if secret else []
            scanned=0
            for file in d.EVIDENCE.parent.rglob('*'):
                if not file.is_file() or file.suffix=='.lock':continue
                data=file.read_bytes();scanned+=1
                assert all(needle not in data for needle in needles), 'Credential found in '+file.name
                if zipfile.is_zipfile(file):
                    with zipfile.ZipFile(file) as archive:
                        for member in archive.namelist():
                            assert all(needle not in archive.read(member) for needle in needles), 'Credential found in archive'
            row={'credentialDeleted':True,'mockRestored':True,'normalClose':True,'exactCredentialScanFiles':scanned,'exactCredentialMatches':0,'encodingChecks':['UTF-8','UTF-16LE'],'archivesDecompressed':True}
            (d.EVIDENCE.parent/'real-cleanup.json').write_text(d.json.dumps(row,indent=2),encoding='utf-8')
            d.record('real-credential-cleanup',**row)
        finally:secret=None
elif action == 'recipe':
    p = Path(sys.argv[2])
    exec(compile(p.read_text(encoding='utf-8'),str(p),'exec'), vars(d))
else: raise ValueError(action)
