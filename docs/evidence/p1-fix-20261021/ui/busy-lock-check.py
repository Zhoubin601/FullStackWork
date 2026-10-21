"""Real UI input while an external no-change SQLite lock delays a favorite write."""
from pathlib import Path
import json,runpy,sqlite3,time
u=runpy.run_path(str(Path(__file__).with_name('ui-driver.py')))
p=Path('/workspace/scratch/p1-ui/data/readjournal.sqlite3')
with sqlite3.connect(f'file:{p}?mode=ro',uri=True) as c:
 c.row_factory=sqlite3.Row
 before=dict(c.execute('SELECT * FROM Quote WHERE Id=1').fetchone())
connection=sqlite3.connect(p)
connection.execute('BEGIN EXCLUSIVE')
start=time.monotonic()
try:
 u['click'](391,402)
 u['click'](100,441)
 u['shot']('11-busy-menu-blocked')
 u['click'](1060,131)
 u['shot']('12-busy-content-blocked')
finally:
 connection.rollback();connection.close()
elapsed=time.monotonic()-start
u['pause'](1.2)
u['shot']('13-favorite-completed')
with sqlite3.connect(f'file:{p}?mode=ro',uri=True) as c:
 c.row_factory=sqlite3.Row
 after=dict(c.execute('SELECT * FROM Quote WHERE Id=1').fetchone())
assert elapsed<5,elapsed
assert before['IsFavorite']==1 and after['IsFavorite']==0,(before,after)
for key in before:
 if key not in ('IsFavorite','UpdatedAt'):assert before[key]==after[key],key
Path(__file__).with_name('favorite-result.json').write_text(json.dumps({'lock_seconds':elapsed,'before':before,'after':after,'favorite_changed_only':True},ensure_ascii=False,indent=2)+'\n')
print('PASS: favorite completed; only favorite/time changed; inspect busy navigation screenshots.')
