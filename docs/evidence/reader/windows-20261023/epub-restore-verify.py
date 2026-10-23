import hashlib
import zipfile
backup=EVIDENCE/"isolated-data/epub-desktop-complete.zip"
restored=json.loads((EVIDENCE/"epub-after-restore.json").read_text(encoding="utf-8"))
original=json.loads((EVIDENCE/"epub-before-export.json").read_text(encoding="utf-8"))
canonical=lambda rows: sorted(json.dumps(row,sort_keys=True) for row in rows)
for table in original:
    if table != "AppMetadata": assert canonical(restored[table])==canonical(original[table]),table
inventory=[]
with zipfile.ZipFile(backup) as archive:
    for name in archive.namelist():
        if name.startswith("books/managed/"):
            actual=(Path(state()["data"])/name).read_bytes()
            assert actual==archive.read(name),name
            inventory.append({"path":name,"sha256":hashlib.sha256(actual).hexdigest(),"bytes":len(actual)})
assert inventory
(EVIDENCE/"epub-restored-files.json").write_text(json.dumps(inventory,ensure_ascii=False,indent=2),encoding="utf-8")
automatic=list((Path(state()["data"])/"backups").glob("auto-backup-*.zip"))
assert len(automatic)==1
with zipfile.ZipFile(automatic[0]) as archive: assert not json.loads(archive.read("data.json"))["Books"]
click("书摘收藏")
click("回到原文")
shot("36-epub-restored-quote-exact-highlight")
close()
