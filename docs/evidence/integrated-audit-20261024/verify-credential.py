"""Read the session credential without echo, check this audit's files, forget it."""
import getpass
import json
from pathlib import Path
import zipfile

evidence = Path(__file__).resolve().parent
secret = getpass.getpass('Session credential for exact cleanup check (not recorded): ')
try:
    assert secret
    needles = [secret.encode('utf-8'), secret.encode('utf-16le')]
    candidates = [p for p in evidence.rglob('*') if p.is_file()]
    candidates += [p for p in Path('Code').rglob('*') if p.is_file() and not {'bin','obj','__pycache__'}.intersection(p.parts)]
    matches, unreadable = [], []
    checked = members = 0
    for file in candidates:
        try:
            data = file.read_bytes()
            checked += 1
            if any(needle in data for needle in needles): matches.append(str(file))
            if zipfile.is_zipfile(file):
                with zipfile.ZipFile(file) as archive:
                    for member in archive.infolist():
                        if not member.is_dir():
                            members += 1
                            if any(needle in archive.read(member) for needle in needles): matches.append(str(file) + '::' + member.filename)
        except (OSError, zipfile.BadZipFile):
            unreadable.append(str(file))
    result = {'scannedFiles': checked, 'scannedArchiveMembers': members, 'exactCredentialMatches': len(matches),
              'unreadableFiles': len(unreadable), 'encodings': ['UTF-8','UTF-16LE'], 'passed': not matches and not unreadable,
              'scope': 'This audit evidence including runtime data/archives and Code sources; not the entire machine'}
    (evidence/'credential-cleanup.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result),flush=True)
    assert result['passed'], 'Credential cleanup verification failed; credential is not printed'
finally:
    secret = None
