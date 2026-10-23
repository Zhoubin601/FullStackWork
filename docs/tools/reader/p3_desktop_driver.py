"""Interactive real X11 evidence driver: JSON commands on stdin; never hooks into product code."""
import argparse
import ctypes as C
import json
import os
from pathlib import Path
import re
import select
import subprocess
import sys
import time


def main(args):
    evidence=args.evidence.resolve()
    if evidence.exists(): raise FileExistsError('Choose a new evidence directory')
    evidence.mkdir(parents=True)
    default=Path.home()/'.local/share/ReadJournal';before=default.exists()
    env=os.environ.copy();env.update(DOTNET_ROOT=str(args.dotnet.resolve().parent),READJOURNAL_DATA_DIR=str(args.data.resolve()),READJOURNAL_LOG_BINDINGS='1')
    read_fd,write_fd=os.pipe()
    log=(evidence/'xvfb.log').open('w')
    server=subprocess.Popen([str(args.xvfb),'-displayfd',str(write_fd),'-screen','0','1200x850x24','-nolisten','tcp','-ac'],pass_fds=[write_fd],stdout=log,stderr=log);os.close(write_fd)
    manager=app=None;display=None;records=[];run=0
    try:
        if not select.select([read_fd],[],[],10)[0]:raise TimeoutError('Xvfb not ready')
        env['DISPLAY']=':'+os.read(read_fd,64).decode().strip()
        manager=subprocess.Popen([str(args.wm),'--sm-disable'],env=env,stdout=(evidence/'wm.log').open('w'),stderr=subprocess.STDOUT)
        deadline=time.monotonic()+10
        while 'window id #' not in subprocess.check_output(['xprop','-root','_NET_SUPPORTING_WM_CHECK'],env=env,text=True):
            if time.monotonic()>deadline:raise TimeoutError('Openbox not ready')
            time.sleep(.1)
        x=C.CDLL('libX11.so.6');xt=C.CDLL('libXtst.so.6')
        x.XOpenDisplay.argtypes=[C.c_char_p];x.XOpenDisplay.restype=C.c_void_p;x.XCloseDisplay.argtypes=[C.c_void_p]
        x.XKeysymToKeycode.argtypes=[C.c_void_p,C.c_ulong];x.XKeysymToKeycode.restype=C.c_uint
        x.XStringToKeysym.argtypes=[C.c_char_p];x.XStringToKeysym.restype=C.c_ulong;x.XFlush.argtypes=[C.c_void_p]
        x.XSetInputFocus.argtypes=[C.c_void_p,C.c_ulong,C.c_int,C.c_ulong]
        xt.XTestFakeKeyEvent.argtypes=[C.c_void_p,C.c_uint,C.c_int,C.c_ulong]
        xt.XTestFakeButtonEvent.argtypes=[C.c_void_p,C.c_uint,C.c_int,C.c_ulong]
        xt.XTestFakeMotionEvent.argtypes=[C.c_void_p,C.c_int,C.c_int,C.c_int,C.c_ulong]
        display=x.XOpenDisplay(env['DISPLAY'].encode())
        x.XInternAtom.argtypes=[C.c_void_p,C.c_char_p,C.c_int];x.XInternAtom.restype=C.c_ulong
        names=['CLIPBOARD','TARGETS','UTF8_STRING','UTF16_STRING','MULTIPLE','SAVE_TARGETS','CLIPBOARD_MANAGER']
        atoms_before={name:x.XInternAtom(display,name.encode(),1) for name in names}
        atoms_after={name:x.XInternAtom(display,name.encode(),0) for name in names};x.XFlush(display)
        (evidence/'x11-clipboard-bootstrap.json').write_text(json.dumps({'before':atoms_before,'after':atoms_after,'scope':'private test display protocol identifiers only, no owner/data set'},indent=2)+'\n')
        def key(name,down):
            symbol=ord(name) if len(name)==1 else x.XStringToKeysym(name.encode());code=x.XKeysymToKeycode(display,symbol)
            if not code:raise ValueError('No keyboard mapping for '+name)
            xt.XTestFakeKeyEvent(display,code,int(down),0);x.XFlush(display)
        def chord(names):
            for name in names:key(name,True)
            for name in reversed(names):key(name,False)
        def window():
            tree=subprocess.check_output(['xwininfo','-root','-tree'],env=env,text=True)
            match=re.search(r'(0x[0-9a-fA-F]+) "阅记 · 阅读与书摘"',tree)
            return int(match.group(1),16) if match else None
        def launch():
            nonlocal app,run
            run+=1;stream=(evidence/f'app-{run}.log').open('w')
            app=subprocess.Popen([str(args.dotnet),str(args.app.resolve())],env=env,stdout=stream,stderr=stream)
            until=time.monotonic()+20
            while window() is None:
                if app.poll() is not None:raise RuntimeError('App exited before window')
                if time.monotonic()>until:raise TimeoutError('No window')
                time.sleep(.1)
            time.sleep(1);return window()
        def close():
            if app is None or app.poll() is not None:return
            x.XSetInputFocus(display,window(),2,0);chord(['Alt_L','F4']);code=app.wait(timeout=15)
            records.append({'op':'normal-close','run':run,'exit_code':code})
            if code:raise RuntimeError('Nonzero exit on normal close')
        print(json.dumps({'ready':True,'display':env['DISPLAY'],'window':launch()}),flush=True)
        for line in sys.stdin:
            command=json.loads(line);op=command['op'];records.append(command)
            if op=='click':
                xt.XTestFakeMotionEvent(display,0,int(command['x']),int(command['y']),0);xt.XTestFakeButtonEvent(display,1,1,0);xt.XTestFakeButtonEvent(display,1,0,0);x.XFlush(display)
            elif op=='drag':
                x1,y1,x2,y2=[int(command[k]) for k in ['x1','y1','x2','y2']]
                xt.XTestFakeMotionEvent(display,0,x1,y1,0);xt.XTestFakeButtonEvent(display,1,1,0);x.XFlush(display);time.sleep(.1)
                for step in range(1,31):
                    xt.XTestFakeMotionEvent(display,0,x1+(x2-x1)*step//30,y1+(y2-y1)*step//30,0);x.XFlush(display);time.sleep(.015)
                xt.XTestFakeButtonEvent(display,1,0,0);x.XFlush(display)
            elif op=='keys':chord(command['keys'])
            elif op=='type':
                for ch in command['text']:
                    shifted=ch.isupper() or ch=='_'
                    if shifted:key('Shift_L',True)
                    key(ch,True);key(ch,False)
                    if shifted:key('Shift_L',False)
                    time.sleep(.012)
            elif op=='capture':
                # Force a real X11 exposure after GTK closes; otherwise root capture can catch a partial repaint.
                subprocess.run(['xrefresh'],env=env,check=True,timeout=10)
                time.sleep(.4);subprocess.run(['import','-window','root',str(evidence/(command['name']+'.png'))],env=env,check=True,timeout=10)
            elif op=='tree':
                tree=subprocess.check_output(['xwininfo','-root','-tree'],env=env,text=True);(evidence/(command['name']+'.txt')).write_text(tree);print(tree,flush=True)
            elif op=='reopen':close();launch()
            elif op=='finish':close();break
            else:raise ValueError('Unknown command '+op)
            (evidence/'actions.json').write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n');print(json.dumps({'done':op}),flush=True)
        (evidence/'actions.json').write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n')
        assert before==default.exists(),'Default data directory changed'
        (evidence/'process.json').write_text(json.dumps({'normal_closes':[r for r in records if r['op']=='normal-close'],'default_directory_existence_unchanged':True,'default_existed':before},indent=2)+'\n')
    finally:
        os.close(read_fd)
        if display:x.XCloseDisplay(display)
        for child in [app,manager,server]:
            if child is not None and child.poll() is None:child.terminate();child.wait(timeout=10)


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ['dotnet','xvfb','wm','app','data','evidence']:p.add_argument('--'+name,type=Path,required=True)
    main(p.parse_args())
