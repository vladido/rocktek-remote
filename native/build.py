#!/usr/bin/env python3
"""Build CFG-compatible x64 artifacts with a hash-pinned portable compiler."""
from pathlib import Path
import hashlib, os, subprocess, tarfile, urllib.request
root = Path(__file__).resolve().parent
release = 'llvm-mingw-20261006-ucrt-ubuntu-22.04-x86_64'
expected = '5f9c6ed95b2d4bdb2869a488c5fd5857fbdabcf288a0aa3eb1da43f6a08d8ab4'
tool = Path(os.environ.get('LLVM_MINGW', str(root / '.toolchain' / release)))
cc = tool / 'bin/x86_64-w64-mingw32-clang'
if not cc.is_file():
    cache = root / '.toolchain'
    cache.mkdir(exist_ok=True)
    archive = cache / (release + '.tar.xz')
    url = 'https://github.com/mstorsjo/llvm-mingw/releases/download/20261006/' + archive.name
    urllib.request.urlretrieve(url, archive)
    if hashlib.sha256(archive.read_bytes()).hexdigest() != expected:
        raise RuntimeError('Compiler archive hash mismatch')
    with tarfile.open(archive) as tf:
        tf.extractall(cache, filter='data')
flags = ['-O2', '-Wall', '-Wextra', '-Werror', '-Wno-unknown-pragmas', '-mguard=cf', '-D_WIN32_WINNT=0x0A00']
link = ['-Wl,--guard-cf,--dynamicbase,--nxcompat,--no-insert-timestamp']
commands = [
    ['-shared', 'provider.c', '-o', 'provider.dll', '-Wl,--out-implib,provider.lib'],
    ['-shared', 'timedhook.c', '-o', 'timedhook.dll', '-lbcrypt'],
    ['fixture.c', 'provider.lib', '-o', 'fixture.exe'],
    ['native-test.c', '-o', 'native-test.exe'],
    ['loader-fixture.c', 'provider.lib', '-o', 'loader-fixture.exe'],
]
for args in commands:
    subprocess.run([str(cc), *flags, *args, *link], cwd=root, check=True)
frozen = {key: hashlib.sha256((root / name).read_bytes()).hexdigest() for key, name in [('HOOK_HASH', 'timedhook.dll'), ('FIXTURE_HASH', 'loader-fixture.exe'), ('PROVIDER_HASH', 'provider.dll')]}
(root / 'runner-frozen.h').write_text('/* Generated build identities. */\n' + ''.join(f'#define {key} "{value}"\n' for key, value in frozen.items()))
subprocess.run([str(cc), *flags, 'service-runner.c', '-o', 'service-runner.exe', '-municode', '-lbcrypt', '-ladvapi32', *link], cwd=root, check=True)
print('Built native x64 CFG helper and fixtures')
