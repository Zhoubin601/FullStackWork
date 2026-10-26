# The first import had committed while the UI still held the busy guard.
# Continue after waiting for the import button to be reenabled.
p=ROOT/'docs/evidence/recheck-20261024/ui-01-manual-import.py'
lines=p.read_text(encoding='utf-8').splitlines()
sample=ROOT/'docs/evidence/recheck-20261024/samples/mixed-lf.txt'
exec(compile('\n'.join(lines[20:]),str(p)+' [resume]','exec'))
