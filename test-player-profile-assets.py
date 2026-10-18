#!/usr/bin/env python3
"""Validate shipped player profiles before compiling the installer."""
from pathlib import Path
import json
root = Path(__file__).resolve().parent
for player, bridge in [('CloudMus', 'CloudMusPlayPause.exe'), ('Murglar', 'MurglarRemote.exe')]:
    config = json.loads((root / 'profiles' / (player + '.json')).read_text(encoding='utf-8'))
    assert config['Version'] == 4
    buttons = config['Buttons']
    identities = [(b['InputType'], b['Usage']) for b in buttons.values()]
    assert len(identities) == len(set(identities)), (player, 'duplicate source/code')
    by_code = {b['Usage']: b for b in buttons.values() if b['InputType'] == 'Consumer'}
    power = by_code[48]
    assert power['Action'] == 'LaunchApplication' and power['Scope'] == 'Global' and not power['Repeat']
    assert power['ApplicationPath'] == '%WINDIR%\\System32\\shutdown.exe'
    assert power['Arguments'] == '/s /t 0' and '/f' not in power['Arguments']
    close = by_code[443]
    assert close['Action'] == 'LaunchApplication' and close['Scope'] == 'Global' and not close['Repeat']
    assert close['ApplicationPath'] == '../' + bridge
    assert close['Arguments'] == '--close --application "' + buttons['OK']['ScopeApplicationPath'] + '"'
    assert buttons['Mute']['Action'] == 'Native'
    target = buttons['OK']['ScopeApplicationPath']
    for name, mode in [('Home', '--open '), ('OK', '')]:
        b = buttons[name]
        assert b['Action'] == 'LaunchApplication' and not b['Repeat']
        assert b['ApplicationPath'] == '../' + bridge
        assert b['Arguments'] == mode + '--application "' + target + '"'
    assert buttons['Home']['Scope'] == 'Global'
    assert buttons['OK']['Scope'] == 'Application'
    for usage, mode in [(554, '--like '), (150, '--dislike ')]:
        b = by_code[usage]
        assert b['Scope'] == 'Application' and not b['Repeat']
        if player == 'Murglar':
            assert b['Arguments'] == mode + '--application "' + target + '"'
            assert b['ApplicationPath'] == '../' + bridge
    print('PASS', player, 'unique input identities, explicit shutdown, exact close/play bridge defaults and scopes')
