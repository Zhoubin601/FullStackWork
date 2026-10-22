"""Run the isolated P1 native experiment on a private Linux X11 desktop."""
import argparse
import ctypes as C
import json
import os
from pathlib import Path
import select
import re
import subprocess
import time


def main(args):
    evidence = args.evidence.resolve()
    if evidence.exists():
        raise FileExistsError('Choose a new evidence directory')
    evidence.mkdir(parents=True)
    read_fd, write_fd = os.pipe()
    env = os.environ.copy()
    env['DOTNET_ROOT'] = str(args.dotnet.resolve().parent)
    env['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
    env['AVALONIA_GLOBAL_SCALE_FACTOR'] = str(args.scale)
    env['READJOURNAL_DATA_DIR'] = str(evidence / 'unused-isolated-data')
    with (evidence / 'xvfb.log').open('w') as log:
        screen = f'{round(1280 * args.scale)}x{round(900 * args.scale)}x24'
        server = subprocess.Popen([str(args.xvfb), '-displayfd', str(write_fd), '-screen', '0', screen, '-nolisten', 'tcp', '-ac'], pass_fds=[write_fd], stdout=log, stderr=log)
    os.close(write_fd)
    manager = process = None
    try:
        if not select.select([read_fd], [], [], 10)[0]:
            raise RuntimeError('Xvfb did not start')
        env['DISPLAY'] = ':' + os.read(read_fd, 64).decode().strip()
        with (evidence / 'wm.log').open('w') as log:
            manager = subprocess.Popen([str(args.wm), '--sm-disable'], env=env, stdout=log, stderr=log)
        deadline = time.monotonic() + 10
        while 'window id #' not in subprocess.check_output(['xprop', '-root', '_NET_SUPPORTING_WM_CHECK'], env=env, text=True):
            if manager.poll() is not None or time.monotonic() > deadline:
                raise RuntimeError('Window manager did not start')
            time.sleep(.1)
        # A bare Xvfb/Openbox server has no clipboard manager to create ICCCM atoms.
        # Avalonia 11.0.10 can cache zero for absent atoms. Create protocol identifiers
        # on this private test display before the toolkit starts; do not set any owner/data.
        x = C.CDLL('libX11.so.6')
        x.XOpenDisplay.argtypes = [C.c_char_p]; x.XOpenDisplay.restype = C.c_void_p
        x.XInternAtom.argtypes = [C.c_void_p, C.c_char_p, C.c_int]; x.XInternAtom.restype = C.c_ulong
        x.XCloseDisplay.argtypes = [C.c_void_p]
        display = x.XOpenDisplay(env['DISPLAY'].encode())
        atom_names = ['CLIPBOARD', 'TARGETS', 'UTF8_STRING', 'UTF16_STRING', 'MULTIPLE', 'SAVE_TARGETS', 'CLIPBOARD_MANAGER']
        atoms_before = {name: x.XInternAtom(display, name.encode(), 1) for name in atom_names}
        atoms_after = {name: x.XInternAtom(display, name.encode(), 0) for name in atom_names}
        x.XCloseDisplay(display)
        (evidence / 'x11-clipboard-bootstrap.json').write_text(json.dumps({'before': atoms_before, 'after': atoms_after,
            'scope': 'private test display protocol atoms only; no clipboard owner or data is set'}, indent=2) + '\n')
        command = [str(args.dotnet), str(args.app), '--file', str(args.samples / 'million.txt'),
                '--samples', str(args.samples), '--probe-output', str(evidence / 'native'), '--await-input', '--await-drag']
        if args.clipboard_reader:
            command += ['--clipboard-reader', str(args.clipboard_reader)]
        with (evidence / 'application.log').open('w') as log:
            process = subprocess.Popen(command, env=env, stdout=log, stderr=log)
        # X11 clipboard ownership uses the last real input timestamp. Exercise the desktop
        # like a user, including focus and pointer input, before the native clipboard probe.
        deadline = time.monotonic() + 15
        while True:
            tree = subprocess.check_output(['xwininfo', '-root', '-tree'], env=env, text=True)
            match = re.search(r'(0x[0-9a-fA-F]+) "阅记 P1 · 原生分页实验"', tree)
            if match and 'Map State: IsViewable' in subprocess.check_output(['xwininfo', '-id', match.group(1)], env=env, text=True):
                x = C.CDLL('libX11.so.6'); xt = C.CDLL('libXtst.so.6')
                x.XOpenDisplay.argtypes = [C.c_char_p]; x.XOpenDisplay.restype = C.c_void_p
                x.XFlush.argtypes = [C.c_void_p]; x.XCloseDisplay.argtypes = [C.c_void_p]
                xt.XTestFakeMotionEvent.argtypes = [C.c_void_p, C.c_int, C.c_int, C.c_int, C.c_ulong]
                xt.XTestFakeButtonEvent.argtypes = [C.c_void_p, C.c_uint, C.c_int, C.c_ulong]
                display = x.XOpenDisplay(env['DISPLAY'].encode())
                info = subprocess.check_output(['xwininfo', '-id', match.group(1)], env=env, text=True)
                wx = int(re.search(r'Absolute upper-left X:\s+(-?\d+)', info).group(1))
                wy = int(re.search(r'Absolute upper-left Y:\s+(-?\d+)', info).group(1))
                xt.XTestFakeMotionEvent(display, 0, wx + round(200 * args.scale), wy + round(120 * args.scale), 0)
                xt.XTestFakeButtonEvent(display, 1, 1, 0); xt.XTestFakeButtonEvent(display, 1, 0, 0)
                x.XFlush(display); x.XCloseDisplay(display)
                break
            if process.poll() is not None or time.monotonic() > deadline:
                raise RuntimeError('Prototype window not available for actual input')
            time.sleep(.05)
        # Native probe signals when it is ready to receive actual pointer/keyboard actions.
        handled = set()
        deadline = time.monotonic() + 240
        while process.poll() is None:
            for signal in ['drag-ready.json', 'cross-page-ready.json', 'focus-ready.json', 'resize-ready.json']:
                path = evidence / 'native' / signal
                if signal in handled or not path.exists():
                    continue
                try:
                    geometry = json.loads(path.read_text())
                except json.JSONDecodeError:
                    continue
                info = subprocess.check_output(['xwininfo', '-id', match.group(1)], env=env, text=True)
                win_x = int(re.search(r'Absolute upper-left X:\s+(-?\d+)', info).group(1))
                win_y = int(re.search(r'Absolute upper-left Y:\s+(-?\d+)', info).group(1))
                display = x.XOpenDisplay(env['DISPLAY'].encode())
                if signal == 'resize-ready.json':
                    x.XResizeWindow.argtypes = [C.c_void_p, C.c_ulong, C.c_uint, C.c_uint]
                    x.XResizeWindow(display, int(match.group(1), 16), round(geometry['width'] * args.scale), round(geometry['height'] * args.scale))
                    x.XFlush(display); x.XCloseDisplay(display); time.sleep(.15)
                    subprocess.run(['import', '-window', 'root', str(evidence / 'actual-window-resize.png')], env=env, check=True, timeout=10)
                    handled.add(signal)
                    continue
                x.XKeysymToKeycode.argtypes = [C.c_void_p, C.c_ulong]; x.XKeysymToKeycode.restype = C.c_uint
                xt.XTestFakeKeyEvent.argtypes = [C.c_void_p, C.c_uint, C.c_int, C.c_ulong]
                def key(symbol, down):
                    xt.XTestFakeKeyEvent(display, x.XKeysymToKeycode(display, symbol), down, 0)
                    x.XFlush(display)
                if signal == 'focus-ready.json':
                    xt.XTestFakeMotionEvent(display, 0, win_x + round(geometry['inputX'] * args.scale), win_y + round(geometry['inputY'] * args.scale), 0)
                    xt.XTestFakeButtonEvent(display, 1, 1, 0); xt.XTestFakeButtonEvent(display, 1, 0, 0)
                    x.XFlush(display); time.sleep(.1)
                    key(0xff53, 1); key(0xff53, 0); x.XCloseDisplay(display)
                    handled.add(signal)
                    continue
                sx = win_x + round(geometry['surfaceX'] * args.scale); sy = win_y + round(geometry['surfaceY'] * args.scale)
                if signal == 'drag-ready.json':
                    xt.XTestFakeMotionEvent(display, 0, sx + round(430 * args.scale), sy + round(210 * args.scale), 0)
                    xt.XTestFakeButtonEvent(display, 1, 1, 0); x.XFlush(display); time.sleep(.1)
                    for step in range(1, 11):
                        xt.XTestFakeMotionEvent(display, 0, sx + round((430 - step * 30) * args.scale), sy + round((210 - step * 13) * args.scale), 0)
                        x.XFlush(display); time.sleep(.02)
                    xt.XTestFakeButtonEvent(display, 1, 0, 0); x.XFlush(display); time.sleep(.1)
                else:
                    key(0xffe1, 1); key(0xff53, 1); key(0xff53, 0); key(0xffe1, 0); time.sleep(.1)
                subprocess.run(['import', '-window', 'root', str(evidence / (signal + '.png'))], env=env, check=True, timeout=10)
                key(0xffe3, 1); key(ord('c'), 1); key(ord('c'), 0); key(0xffe3, 0)
                x.XCloseDisplay(display)
                handled.add(signal)
            if time.monotonic() > deadline:
                raise TimeoutError('Native probe exceeded 240 seconds')
            time.sleep(.05)
        code = process.returncode
        (evidence / 'process.json').write_text(json.dumps({'exit_code': code, 'unused_default_data_override_created': (evidence / 'unused-isolated-data').exists()}) + '\n')
        if code != 0:
            raise RuntimeError(f'Native probe failed: exit {code}; inspect {evidence}')
        native = json.loads((evidence / 'native/probe.json').read_text())
        if abs(native['renderScaling'] - args.scale) > .01:
            raise RuntimeError('Actual native render scaling differs from the requested Linux scale.')
        print((evidence / 'native/probe.json').read_text())
    finally:
        os.close(read_fd)
        for child in [process, manager, server]:
            if child is not None and child.poll() is None:
                child.terminate()
                child.wait(timeout=10)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['dotnet', 'xvfb', 'wm', 'app', 'samples', 'evidence']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--clipboard-reader', type=Path)
    parser.add_argument('--scale', type=float, choices=[1, 1.5, 2], default=1)
    main(parser.parse_args())
