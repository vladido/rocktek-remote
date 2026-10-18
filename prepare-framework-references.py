#!/usr/bin/env python3
"""Fetch build-only .NET reference assemblies; never bundle framework DLLs."""
from pathlib import Path
import hashlib, io, urllib.request, zipfile
root = Path(__file__).resolve().parent / '.build' / 'net48'
files = ['UIAutomationClient.dll', 'UIAutomationTypes.dll', 'WindowsBase.dll']
expected = '8a7e348538e7eb91351696911689f49e3d4f63f8bab517432bbe159b8b1104a2'
url = ('https://api.nuget.org/v3-flatcontainer/microsoft.netframework.referenceassemblies.net48/'
       '1.0.3/microsoft.netframework.referenceassemblies.net48.1.0.3.nupkg')
root.mkdir(parents=True, exist_ok=True)
with urllib.request.urlopen(url, timeout=120) as response:
    data = response.read()
if hashlib.sha256(data).hexdigest() != expected:
    raise RuntimeError('Framework reference package hash mismatch')
with zipfile.ZipFile(io.BytesIO(data)) as archive:
    for name in files:
        (root / name).write_bytes(archive.read('build/.NETFramework/v4.8/' + name))
print('Verified .NET 4.8 UI Automation compile-time references')
