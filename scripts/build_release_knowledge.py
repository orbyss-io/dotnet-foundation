"""Export versioned publisher facts and source guidance; never consumer authority."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
import zipfile


def sha(data):
    return hashlib.sha256(data).hexdigest()


def build(root, packages, version, commit):
    if not packages.is_dir():
        raise ValueError('Published package input directory is missing')
    records = []
    for path in sorted(packages.glob('*.nupkg')):
        with zipfile.ZipFile(path) as archive:
            specs = [name for name in archive.namelist() if name.endswith('.nuspec')]
            if len(specs) != 1:
                raise ValueError('Expected one package identity')
            metadata = ET.fromstring(archive.read(specs[0]))
            fields = {node.tag.rsplit('}', 1)[-1]: node.text for node in metadata.iter()}
            if fields['version'] != version or not fields['id'].startswith('Orbyss.Foundation.'):
                raise ValueError('Knowledge must cover the exact selected publisher/version')
            files = {}
            facts = {}
            for item in archive.infolist():
                if item.is_dir() or item.filename == '.signature.p7s':
                    continue
                if item.file_size > 64 * 1024 * 1024 or len(files) >= 4096:
                    raise ValueError('Package knowledge input exceeds bounded archive policy')
                data = archive.read(item)
                files[item.filename] = sha(data)
                if (item.filename.startswith(('orbyss-foundation/','schemas/')) and item.filename.endswith('.json')
                        or item.filename.endswith('.xml') and item.filename.startswith('lib/')
                        or item.filename.rsplit('/',1)[-1].casefold() == 'readme.md'):
                    if len(data) > 2 * 1024 * 1024:
                        raise ValueError('Publisher fact exceeds two MiB')
                    facts[item.filename] = data.decode('utf-8')
            repository=next((n.attrib for n in metadata.iter() if n.tag.rsplit('}',1)[-1]=='repository'),None)
            if repository and repository.get('commit')!=commit:
                raise ValueError('Package source commit differs from publisher knowledge source')
            records.append({'id': fields['id'], 'version': version,
                'license': fields.get('license'), 'repository': repository,
                'payloadFiles': files, 'facts': facts})
    if not records:
        raise ValueError('No publisher packages for knowledge export')
    documents = []
    for path in sorted({*root.glob('README.md'), *root.glob('LICENSE'), *root.glob('THIRD-PARTY-NOTICES.md'),
                        *root.glob('src/Orbyss.Foundation.Host/NOTICE.md'), *root.glob('docs/**/*.md')}):
        if path.is_file():
            data = path.read_bytes()
            if len(data) > 1024 * 1024:
                raise ValueError('Source guidance exceeds one MiB')
            relative = path.relative_to(root).as_posix()
            documents.append({'path': relative, 'sha256': sha(data), 'text': data.decode('utf-8'),
                'url': 'https://raw.githubusercontent.com/orbyss-io/dotnet-foundation/' + commit + '/' + relative})
    return {'schemaVersion': 1, 'publisher': 'orbyss-io/dotnet-foundation',
        'releaseVersion': version, 'sourceCommit': commit, 'packages': records, 'documents': documents,
        'boundary': 'Versioned publisher facts and guidance. Package payload hashes exclude registry signatures. Consumers must verify public bytes, reviewed capability mapping and compatibility; no consumer selection, legal clearance or future support is granted.'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--packages', type=Path, required=True)
    parser.add_argument('--version', required=True)
    parser.add_argument('--source-commit', required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--host', type=Path)
    parser.add_argument('--host-image')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    actual = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
    if actual != args.source_commit:
        raise ValueError('Source commit must be the actual checked-out publisher commit')
    if subprocess.check_output(['git','status','--porcelain'],cwd=root,text=True).strip():
        raise ValueError('Commit the candidate source before exporting its immutable publisher knowledge')
    result = build(root, args.packages, args.version, actual)
    if args.host:
        facts = {}
        for path in sorted((args.host/'.orbyss-foundation').rglob('*')):
            if path.is_file():
                data = path.read_bytes()
                if len(data)>2*1024*1024: raise ValueError('Host knowledge fact exceeds two MiB')
                facts[path.relative_to(args.host).as_posix()] = {'sha256':sha(data),'text':data.decode('utf-8')}
        result['host'] = {'image':args.host_image,'facts':facts}
    if args.output.exists():
        raise ValueError('Preserve existing publisher knowledge; choose a new output')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8', newline='\n')


if __name__ == '__main__':
    main()
