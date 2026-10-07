"""Refresh Host vendor snapshots from the exact restored publisher commits."""
from __future__ import annotations
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import urllib.request
import xml.etree.ElementTree as ET
import zipfile


def sha(data): return hashlib.sha256(data).hexdigest()


def download(url):
    headers = {'User-Agent':'foundation-vendor-metadata'}
    if url.startswith('https://api.github.com/') and os.environ.get('GH_TOKEN'):
        headers['Authorization'] = 'Bearer ' + os.environ['GH_TOKEN']
    with urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=30) as response:
        data = response.read(2_097_153)
    if len(data) > 2_097_152: raise ValueError('Vendor metadata exceeds two MiB')
    return data


def main():
    root = Path(__file__).resolve().parents[1]
    host = root / 'src/Orbyss.Foundation.Host'
    resources = host / 'metadata/vendor-settings'
    assets = json.loads((host / 'obj/project.assets.json').read_text())
    document = json.loads((resources / 'inputs.json').read_text())
    trees, sources, pending, removed_types, removed_paths = {}, {}, {}, [], []
    for origin in document['origins']:
        identity = origin['packageId']
        matches = [key for key in assets['libraries'] if key.split('/')[0].casefold() == identity.casefold()]
        if len(matches) != 1: raise ValueError('Missing/ambiguous restored vendor package: ' + identity)
        version = matches[0].split('/')[1]
        archives = [Path(folder) / identity.lower() / version / (identity.lower()+'.'+version+'.nupkg') for folder in assets['packageFolders']]
        archives = [p for p in archives if p.is_file()]
        if len(archives) != 1: raise ValueError('Missing/ambiguous native archive: ' + identity)
        package = archives[0]
        with zipfile.ZipFile(package) as archive:
            spec = ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith('.nuspec'))))
            repo = next(n for n in spec.iter() if n.tag.rsplit('}',1)[-1] == 'repository')
            repository, commit = repo.get('url'), repo.get('commit')
            if not repository.startswith('https://github.com/') or not commit or len(commit) != 40:
                raise ValueError('Vendor requires immutable GitHub source provenance')
            repository = repository.removesuffix('.git').rstrip('/')
            name = repository.removeprefix('https://github.com/')
            origin.update(packageVersion=version)
            origin['archive'] = {'name':package.name,'sha256':sha(package.read_bytes())}
            assembly = 'lib/net10.0/' + identity + '.dll'
            origin['assembly'] = {'name':identity+'.dll','sha256':sha(archive.read(assembly))}
        key = name + '@' + commit
        if key not in trees:
            tree = json.loads(download('https://api.github.com/repos/'+name+'/git/trees/'+commit+'?recursive=1'))
            if tree.get('truncated'): raise ValueError('Incomplete publisher source inventory')
            trees[key] = [item['path'] for item in tree['tree'] if item['type']=='blob']
        files = {}
        for relative in origin['source']['files']:
            suffix = relative.removeprefix(identity+'/').removesuffix('.txt')
            preferred = 'src/'+identity+'/'+suffix
            candidates = ([preferred] if preferred in trees[key] else [suffix] if suffix in trees[key]
                          else [p for p in trees[key] if p.endswith('/'+suffix) and not p.startswith('tests/')])
            if not candidates:
                previous = resources / 'sources' / relative
                text = previous.read_text(encoding='utf-8-sig')
                namespace = re.search(r'namespace\s+([\w.]+)', text)
                if namespace:
                    removed_types.extend(namespace[1]+'.'+t for t in re.findall(r'public\s+(?:sealed\s+)?(?:enum|class|record)\s+(\w+)',text))
                removed_paths.append(previous)
                continue
            if len(candidates) != 1:
                raise ValueError('Ambiguous publisher settings source: '+identity+'/'+suffix)
            source_key = key + '/' + candidates[0]
            if source_key not in sources:
                data = download('https://raw.githubusercontent.com/'+name+'/'+commit+'/'+candidates[0])
                sources[source_key] = data.decode('utf-8-sig').replace('\r\n','\n').replace('\r','\n').encode('utf-8')
            destination = resources / 'sources' / relative
            pending[destination] = sources[source_key]
            files[relative] = sha(sources[source_key])
        origin['source'] = {'repository':repository,'commit':commit,'files':files}
    for destination, content in pending.items():
        destination.parent.mkdir(parents=True,exist_ok=True)
        destination.write_bytes(content)
    for path in removed_paths:
        if not path.resolve().is_relative_to(resources.resolve()): raise ValueError('Source path escapes vendor inventory')
        path.unlink()
    integration = json.loads((resources/'integration.json').read_text())
    integration['types'] = [t for t in integration['types'] if t not in removed_types]
    for boundary in integration['boundaries']:
        boundary['bindings'] = [b for b in boundary.get('bindings',[]) if b['typeName'] not in removed_types]
    if removed_types: integration['removedPublisherTypes'] = sorted(set(removed_types))
    (resources/'integration.json').write_text(json.dumps(integration,indent=2)+'\n',encoding='utf-8',newline='\n')
    (resources/'inputs.json').write_text(json.dumps(document,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Refreshed exact native package, assembly and source inventories; build/tests validate owning settings semantics.')


if __name__ == '__main__': main()
