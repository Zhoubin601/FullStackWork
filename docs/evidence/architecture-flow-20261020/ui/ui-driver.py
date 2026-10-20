import os,subprocess,time,sys,json
from pathlib import Path
ENV=os.environ.copy(); ENV['DISPLAY']=':118'; ENV['LD_LIBRARY_PATH']='/workspace/scratch/ui-audit/root/usr/lib/x86_64-linux-gnu'
XDO='/workspace/scratch/ui-audit/root/usr/bin/xdotool'
OUT=Path('/workspace/FullStackWork/docs/evidence/architecture-flow-20261020/ui')
def xdo(*args): subprocess.run([XDO,*map(str,args)],env=ENV,check=True)
def click(x,y): xdo('mousemove',x,y,'click',1);time.sleep(.35)
def key(*args): xdo('key',*args);time.sleep(.2)
def fill(x,y,value): click(x,y);key('ctrl+a');xdo('type','--clearmodifiers','--delay','12',value);time.sleep(.2)
def shot(name):
 time.sleep(.6);subprocess.run(['import','-window','root',str(OUT/(name+'.png'))],env=ENV,check=True)
def tree(): print(subprocess.check_output(['xwininfo','-root','-tree'],env=ENV).decode())
if __name__=='__main__':
 for action in json.loads(sys.argv[1]):
  print(action,flush=True)
  globals()[action[0]](*action[1:])
