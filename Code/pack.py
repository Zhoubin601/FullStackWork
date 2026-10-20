"""Package only reviewed, explicit source paths. Python 3 standard library only."""
from pathlib import Path, PurePosixPath
import hashlib, json, sys, tempfile, shutil, zipfile
root = Path(__file__).resolve().parent
if len(sys.argv) != 2:
    raise SystemExit('Usage: python pack.py <new-output.zip>')
output = Path(sys.argv[1]).resolve()
if output.exists():
    raise SystemExit('Refusing to overwrite an existing delivery; choose a new version.')
entries = (root / 'delivery-files.txt').read_text(encoding='utf-8').splitlines()
if len(entries) != len(set(entries)):
    raise SystemExit('Duplicate manifest path')
for entry in entries:
    path = PurePosixPath(entry)
    if path.is_absolute() or '..' in path.parts or any(p in {'bin','obj','.git','TestResults','docs','raw','memory-bank','assets','output'} for p in path.parts):
        raise SystemExit('Forbidden path: '+entry)
    source = root / entry
    if not source.is_file() or source.is_symlink():
        raise SystemExit('Missing or symlink: '+entry)
output.parent.mkdir(parents=True, exist_ok=True)
with tempfile.TemporaryDirectory(prefix='readjournal-package-') as stage:
    stage = Path(stage)
    for entry in entries:
        target = stage / entry
        target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(root / entry,target)
    with zipfile.ZipFile(output,'x',compression=zipfile.ZIP_DEFLATED,compresslevel=9) as archive:
        for entry in entries:
            archive.write(stage/entry,entry)
with zipfile.ZipFile(output) as archive:
    if archive.namelist() != entries or archive.testzip() is not None:
        raise SystemExit('Archive content verification failed')
    for entry in entries:
        if hashlib.sha256(archive.read(entry)).digest() != hashlib.sha256((root/entry).read_bytes()).digest():
            raise SystemExit('Archive hash mismatch: '+entry)
if output.stat().st_size > 10_000_000:
    raise SystemExit('Archive exceeds 10,000,000 bytes')
print(json.dumps({'Path':str(output),'Bytes':output.stat().st_size,'Files':len(entries),'Sha256':hashlib.sha256(output.read_bytes()).hexdigest()},ensure_ascii=False,indent=2))
