# Resume the open native save dialog after allowing its UIAutomation tree to settle.
import hashlib,zipfile
fake_key='fictional-recheck-'
backup=ROOT/'docs/evidence/recheck-20261024/ui-complete.zip'
p=ROOT/'docs/evidence/recheck-20261024/ui-04-backup.py'
exec(compile('\n'.join(p.read_text(encoding='utf-8').splitlines()[7:]),str(p)+' [resume]','exec'))
