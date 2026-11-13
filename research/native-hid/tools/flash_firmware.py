"""Check or flash the exact experimental native-HID package on Windows."""

import argparse
import os
from pathlib import Path
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('component', choices=('controller', 'receiver'))
    parser.add_argument('--drive', required=True, help='Recovery drive letter shown by Windows')
    parser.add_argument('--patched', required=True, type=Path, help='Exact patched UFW')
    parser.add_argument('--stock', required=True, type=Path, help='Saved original UFW')
    parser.add_argument('--connect-dir', type=Path, help='GameSir Connect installation directory')
    parser.add_argument('--write', action='store_true', help='Flash after all checks. Default: preflight only.')
    args = parser.parse_args()

    if sys.platform != 'win32':
        parser.error('The GameSir updater requires Windows.')
    drive = args.drive.rstrip(':').upper()
    if len(drive) != 1 or drive not in 'DEFGHIJKLMNOPQRSTUVWXYZ':
        parser.error('Supply the recovery drive letter, for example I or I:.')
    install_dir = args.connect_dir or Path(os.environ['LOCALAPPDATA']) / 'Programs' / 'GameSir Connect'
    executable = install_dir.resolve() / 'GameSir Connect.exe'
    if not executable.is_file():
        parser.error('GameSir Connect.exe was not found. Use --connect-dir to select its installation directory.')

    script = Path(__file__).resolve().with_suffix('.js')
    command = [str(executable), str(script), args.component, drive, str(args.patched.resolve()), str(args.stock.resolve())]
    if args.write:
        command.append('--write')
    environment = dict(os.environ, ELECTRON_RUN_AS_NODE='1')
    # Stream output from the GUI executable. Never terminate an update on a timeout.
    with subprocess.Popen(command, cwd=executable.parent, env=environment, stdout=subprocess.PIPE,
                          stderr=subprocess.STDOUT, text=True, encoding='utf-8', errors='replace',
                          creationflags=subprocess.CREATE_NO_WINDOW) as process:
        for line in process.stdout:
            print(line, end='', flush=True)
        return process.wait()


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except OSError as error:
        raise SystemExit(str(error))
