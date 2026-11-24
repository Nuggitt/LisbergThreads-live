#!/usr/bin/env python3
"""Create a source archive without Git history, private settings, uploads or build output."""
from datetime import datetime, timezone
from pathlib import Path
import json
import re
import subprocess
import zipfile

api_root = Path(__file__).resolve().parents[1]
repo_root = api_root.parent
excluded_parts = {'.git', '.idea', '.vs', '.vscode', '.agents', '.codex', 'bin', 'obj',
                  'artifacts', 'node_modules', 'App_Data', 'TestResults'}
secret_extensions = {'.pem', '.key', '.pfx', '.p12', '.publishsettings', '.bak', '.mdf', '.ldf'}
secret_patterns = [
    re.compile(r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'),
    re.compile(r'(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|sk-(?:proj-)?[A-Za-z0-9_-]{24,}|AKIA[A-Z0-9]{16})'),
    re.compile(r'[A-Za-z0-9._%+-]+@(?:gmail|hotmail|outlook|live|yahoo)\.[A-Za-z]{2,}', re.I),
]

raw = subprocess.check_output(['git', '-C', str(repo_root), 'ls-files', '--cached', '--others',
                               '--exclude-standard', '-z'])
files = []
for name in sorted(set(raw.decode().split('\0')) - {''}):
    path = Path(name)
    source = repo_root / path
    if (set(path.parts) & excluded_parts or source.is_symlink() or not source.is_file()
            or path.suffix.lower() in secret_extensions or path.name.startswith('.env')
            or 'wwwroot/uploads/' in name
            or (path.name.startswith('appsettings') and path.name not in {
                'appsettings.Production.json', 'appsettings.Development.json', 'appsettings.Local.example.json'})):
        continue
    data = source.read_bytes()
    if b'\0' not in data:
        content = data.decode('utf-8-sig', errors='replace')
        if any(pattern.search(content) for pattern in secret_patterns):
            raise SystemExit(f'Eksport afbrudt: mulig nøgle eller privat mailadresse i {name}. Værdien vises ikke.')
        if path.suffix.lower() == '.json':
            try:
                document = json.loads(content)
            except json.JSONDecodeError:
                document = None

            def check_settings(value):
                if isinstance(value, dict):
                    for key, item in value.items():
                        if key.lower() in {'password', 'clientsecret', 'subscriptionkey', 'key'} and isinstance(item, str) and item:
                            raise SystemExit(f'Eksport afbrudt: udfyldt hemmelighed i {name}. Værdien vises ikke.')
                        if key.lower() == 'connectionstrings' and isinstance(item, dict) and any(item.values()):
                            raise SystemExit(f'Eksport afbrudt: udfyldt databaseforbindelse i {name}.')
                        check_settings(item)
                elif isinstance(value, list):
                    for item in value:
                        check_settings(item)
            check_settings(document)
    files.append((name, data))

output = api_root / 'artifacts'
output.mkdir(exist_ok=True)
stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')
archive = output / f'lisbergthreads-public-source-{stamp}.zip'
with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as target:
    for name, data in files:
        target.writestr(name, data)
print(f'Eksporteret {len(files)} filer til {archive}')
print('Ingen Git-historik medfølger. Det oprindelige repository er uændret.')
print('Kontrollen finder udvalgte nøglemønstre; gennemgå altid indholdet før offentliggørelse.')
