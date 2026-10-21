"""Real X11 controls for independent ReadJournal UI recheck; no app state writes."""
import os, subprocess, time, sys, json
from pathlib import Path
ENV=os.environ.copy()
ENV['DISPLAY']=':120'
ENV['LD_LIBRARY_PATH']='/workspace/scratch/recheck-ui/root/usr/lib/x86_64-linux-gnu'
XDO='/workspace/scratch/recheck-ui/root/usr/bin/xdotool'
OUT=Path('/workspace/FullStackWork/docs/evidence/p1-fix-20261021/ui')
def xdo(*args): subprocess.run([XDO,*map(str,args)],env=ENV,check=True)
def click(x,y): xdo('mousemove',x,y,'click',1); time.sleep(.45)
def key(*args): xdo('key',*args); time.sleep(.25)
def fill(x,y,value): click(x,y);key('ctrl+a');xdo('type','--clearmodifiers','--delay','12',value);time.sleep(.25)
def shot(name):
    time.sleep(.7)
    subprocess.run(['import','-window','root',str(OUT/(name+'.png'))],env=ENV,check=True)
def pause(seconds): time.sleep(seconds)
if __name__=='__main__':
    for action in json.loads(sys.argv[1]):
        print(action,flush=True)
        globals()[action[0]](*action[1:])
