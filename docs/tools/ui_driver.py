"""Local X11 evidence helper; never substitutes for application data or rendering."""
import os, subprocess, time
from pathlib import Path
ENV=os.environ.copy()
ENV['DISPLAY']=':99'
ENV['LD_LIBRARY_PATH']='/workspace/scratch/x11/root/usr/lib/x86_64-linux-gnu'
XDO='/workspace/scratch/x11/root/usr/bin/xdotool'
def xdo(*args):
    subprocess.run([XDO,*map(str,args)],env=ENV,check=True)
def click(x,y):
    xdo('mousemove',x,y,'click',1);time.sleep(.45)
def fill(x,y,value):
    click(x,y);xdo('key','ctrl+a');xdo('type','--clearmodifiers','--delay','12',value)
def shot(name):
    time.sleep(.5)
    subprocess.run(['import','-window','root',str(Path('docs/evidence')/name)],env=ENV,check=True)
