"""P0 real X11 startup/WM_DELETE_WINDOW/reopen in isolated data directories.

Usage: python smoke_desktop.py --dotnet <dotnet> --xvfb <Xvfb> --app <dll>
  --fixture <schema1.sqlite3> --work <new-temp-directory> --evidence <directory>
Only processes started here are closed. This is Linux evidence, never Windows evidence.
"""
import argparse
import ctypes as C
import hashlib
import json
import os
from pathlib import Path
import re
import select
import shutil
import sqlite3
import subprocess
import time


def directory_state(path):
    if not path.exists():
        return {'exists': False, 'files': []}
    return {'exists': True, 'files': [
        {'name': str(p.relative_to(path)), 'sha256': hashlib.sha256(p.read_bytes()).hexdigest(),
         'bytes': p.stat().st_size, 'mtime_ns': p.stat().st_mtime_ns}
        for p in sorted(path.rglob('*')) if p.is_file()]}


def data_state(path):
    with sqlite3.connect(f'{path.as_uri()}?mode=ro', uri=True) as db:
        data = {t: db.execute(f'SELECT * FROM {t} ORDER BY Id').fetchall()
                for t in ['Book', 'ReadingRecord', 'Quote', 'ReadingPlan']}
        legacy = {t: db.execute(f"SELECT {','.join(row[1] for row in db.execute(f'PRAGMA table_info({t})') if row[1] not in ['BookUid','Kind','Revision'])} FROM {t} ORDER BY Id").fetchall()
                  for t in ['Book', 'ReadingRecord', 'Quote', 'ReadingPlan']}
        return {'legacy_field_sha256': hashlib.sha256(json.dumps(legacy, ensure_ascii=False).encode()).hexdigest(), 'schema': db.execute('PRAGMA user_version').fetchone()[0],
                'integrity': db.execute('PRAGMA integrity_check').fetchone()[0],
                'foreign_key_violations': db.execute('PRAGMA foreign_key_check').fetchall(),
                'counts': {t: len(rows) for t, rows in data.items()},
                'field_sha256': hashlib.sha256(json.dumps(data, ensure_ascii=False).encode()).hexdigest()}


def close_window(display_name, window):
    x = C.CDLL('libX11.so.6')
    x.XOpenDisplay.argtypes = [C.c_char_p]
    x.XOpenDisplay.restype = C.c_void_p
    x.XKeysymToKeycode.argtypes = [C.c_void_p, C.c_ulong]
    x.XKeysymToKeycode.restype = C.c_uint
    x.XSetInputFocus.argtypes = [C.c_void_p, C.c_ulong, C.c_int, C.c_ulong]
    x.XFlush.argtypes = [C.c_void_p]
    x.XCloseDisplay.argtypes = [C.c_void_p]
    display = x.XOpenDisplay(display_name.encode())
    if not display:
        raise RuntimeError('Cannot open X11 display')
    try:
        xtst = C.CDLL('libXtst.so.6')
        xtst.XTestFakeKeyEvent.argtypes = [C.c_void_p, C.c_uint, C.c_int, C.c_ulong]
        x.XSetInputFocus(display, window, 2, 0)
        alt = x.XKeysymToKeycode(display, 0xffe9)
        f4 = x.XKeysymToKeycode(display, 0xffc1)
        for key, down in [(alt, 1), (f4, 1), (f4, 0), (alt, 0)]:
            if not xtst.XTestFakeKeyEvent(display, key, down, 0):
                raise RuntimeError('XTest Alt+F4 failed')
        x.XFlush(display)
    finally:
        x.XCloseDisplay(display)


def main(args):
    work = args.work.resolve()
    if work.exists():
        raise FileExistsError('work must be a new directory')
    work.mkdir(parents=True)
    evidence = args.evidence.resolve()
    evidence.mkdir(parents=True, exist_ok=True)
    default_data = Path(os.environ.get('XDG_DATA_HOME', str(Path.home() / '.local/share'))) / 'ReadJournal'
    before = directory_state(default_data)
    read_fd, write_fd = os.pipe()
    with (evidence / 'xvfb.log').open('w') as log:
        server = subprocess.Popen([str(args.xvfb.resolve()), '-displayfd', str(write_fd), '-screen', '0', '1200x850x24', '-nolisten', 'tcp', '-ac'], pass_fds=[write_fd], stdout=log, stderr=log)
    os.close(write_fd)
    results = []
    manager = None
    try:
        if not select.select([read_fd], [], [], 10)[0]:
            raise RuntimeError('Xvfb failed to publish its display')
        display_name = ':' + os.read(read_fd, 64).decode().strip()
        env = os.environ.copy()
        env.update(DISPLAY=display_name, DOTNET_ROOT=str(args.dotnet.resolve().parent),
                   DOTNET_CLI_TELEMETRY_OPTOUT='1', READJOURNAL_LOG_BINDINGS='1')
        with (evidence / 'window-manager.log').open('w') as log:
            manager = subprocess.Popen([str(args.wm.resolve()), '--sm-disable'], env=env, stdout=log, stderr=log)
        deadline = time.monotonic() + 10
        while True:
            if manager.poll() is not None:
                raise RuntimeError('Window manager failed to start')
            status = subprocess.check_output(['xprop', '-root', '_NET_SUPPORTING_WM_CHECK'], env=env, text=True)
            if 'window id #' in status:
                break
            if time.monotonic() > deadline:
                raise TimeoutError('Window manager not ready')
            time.sleep(.2)
        for scenario in ['empty', 'schema1']:
            data = work / scenario
            data.mkdir()
            if scenario == 'schema1':
                shutil.copy2(args.fixture, data / 'readjournal.sqlite3')
            env['READJOURNAL_DATA_DIR'] = str(data)
            previous = data_state(data / 'readjournal.sqlite3') if scenario == 'schema1' else None
            for attempt in [1, 2]:
                label = f'{scenario}-{attempt}'
                with (evidence / f'{label}.log').open('w') as log:
                    process = subprocess.Popen([str(args.dotnet.resolve()), str(args.app.resolve())], env=env, stdout=log, stderr=log)
                try:
                    deadline = time.monotonic() + 20
                    while True:
                        if process.poll() is not None:
                            raise RuntimeError(f'{label}: app exited before startup: {process.returncode}')
                        tree = subprocess.check_output(['xwininfo', '-root', '-tree'], env=env, text=True)
                        match = re.search(r'(0x[0-9a-fA-F]+) "阅记 · 阅读与书摘"', tree)
                        if match and (data / 'readjournal.sqlite3').exists():
                            break
                        if time.monotonic() > deadline:
                            raise TimeoutError(f'{label}: no application window/database')
                        time.sleep(.2)
                    time.sleep(1)  # Allow asynchronous overview rendering before capture.
                    subprocess.run(['import', '-window', 'root', str(evidence / f'{label}.png')], env=env, check=True, timeout=10)
                    close_window(display_name, int(match.group(1), 16))
                    code = process.wait(timeout=10)
                    if code != 0:
                        raise RuntimeError(f'{label}: nonzero close exit: {code}')
                    state = data_state(data / 'readjournal.sqlite3')
                    assert state['schema'] == args.expected_schema and state['integrity'] == 'ok'
                    assert not state['foreign_key_violations']
                    assert state['counts'] == ({'Book': 0, 'ReadingRecord': 0, 'Quote': 0, 'ReadingPlan': 0} if scenario == 'empty' else {'Book': 3, 'ReadingRecord': 3, 'Quote': 2, 'ReadingPlan': 3})
                    if previous:
                        if previous['schema'] != args.expected_schema:
                            assert state['legacy_field_sha256'] == previous['legacy_field_sha256'], f'{label}: migration changed legacy business fields'
                        else:
                            assert state == previous, f'{label}: business fields changed on read-only startup'
                    previous = state
                    results.append({'scenario': scenario, 'attempt': attempt, 'close_method': 'XTest Alt+F4 via Openbox (normal window close)', 'normal_close_exit_code': code, 'data': state, 'screenshot': f'{label}.png'})
                finally:
                    if process.poll() is None:
                        process.terminate()
                        process.wait(timeout=10)
        after = directory_state(default_data)
        assert before == after, 'Default user data changed'
        result = {'platform': 'Linux X11/Xvfb, not Windows', 'default_directory': str(default_data),
                  'default_directory_unchanged': before == after, 'default_before': before,
                  'default_after': after, 'runs': results}
        (evidence / 'desktop-smoke.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
        print('4 real desktop launches/normal closes passed; default data unchanged.')
    finally:
        os.close(read_fd)
        if manager is not None and manager.poll() is None:
            manager.terminate()
            manager.wait(timeout=10)
        server.terminate()
        server.wait(timeout=10)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['dotnet', 'xvfb', 'wm', 'app', 'fixture', 'work', 'evidence']:
        parser.add_argument('--' + name, required=True, type=Path)
    parser.add_argument('--expected-schema', type=int, choices=[1, 2], default=1)
    main(parser.parse_args())
