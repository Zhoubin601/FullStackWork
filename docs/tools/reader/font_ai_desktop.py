"""Replay X11 desktop actions against the actual application, recording screenshots and read-only data snapshots.
Never send a real API key through this recorder: typed text is intentionally recorded.
"""
import argparse, json, os, sqlite3, subprocess, sys, time
from pathlib import Path
parser=argparse.ArgumentParser()
parser.add_argument('--evidence',type=Path,required=True)
parser.add_argument('--data',type=Path,required=True)
parser.add_argument('--xdo',default='xdotool')
args=parser.parse_args(); args.evidence.mkdir(parents=True,exist_ok=True)
env=os.environ.copy()
def xdo(*cmd): subprocess.run([args.xdo,*map(str,cmd)],env=env,check=True)
for action in json.load(sys.stdin):
    op=action['op']
    if op=='click': xdo('mousemove',action['x'],action['y'],'click',action.get('button',1))
    elif op=='double': xdo('mousemove',action['x'],action['y'],'click','--repeat','2','--delay','100',1)
    elif op=='fill':
        xdo('mousemove',action['x'],action['y'],'click',1,'key','ctrl+a')
        xdo('type','--clearmodifiers','--delay','2',action['text'])
    elif op=='type': xdo('type','--clearmodifiers','--delay','2',action['text'])
    elif op=='keys': xdo('key',*action['keys'].split())
    elif op=='drag':
        xdo('mousemove',action['x1'],action['y1'],'mousedown',1)
        for i in range(1,21):
            xdo('mousemove',round(action['x1']+(action['x2']-action['x1'])*i/20),round(action['y1']+(action['y2']-action['y1'])*i/20));time.sleep(.01)
        xdo('mouseup',1)
    elif op=='capture':
        time.sleep(.4);subprocess.run(['import','-window','root',str(args.evidence/(action['name']+'.png'))],env=env,check=True)
    elif op=='snapshot':
        db=sqlite3.connect('file:'+str(args.data/'readjournal.sqlite3')+'?mode=ro',uri=True)
        tables=['Book','ReadingRecord','Quote','ReadingPlan','EbookFile','EbookProgress','EbookReadingSession','QuoteOrigin','AiGenerationBatch','AiGenerationBatchItem']
        result={'integrity':db.execute('pragma integrity_check').fetchone()[0],'foreign_key_errors':db.execute('pragma foreign_key_check').fetchall(),'tables':{}}
        for table in tables:
            if db.execute("select 1 from sqlite_master where type='table' and name=?",(table,)).fetchone():
                db.row_factory=sqlite3.Row;result['tables'][table]=[dict(row) for row in db.execute('select * from '+table)]
        db.close();(args.evidence/(action['name']+'.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
    elif op!='wait': raise ValueError(op)
    time.sleep(action.get('wait',.3))
    with (args.evidence/'actions.jsonl').open('a') as log: log.write(json.dumps(action,ensure_ascii=False)+'\n')
