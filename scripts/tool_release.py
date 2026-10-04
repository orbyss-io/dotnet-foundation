"""Select and verify one independently versioned immutable Foundation tool release."""
from __future__ import annotations
import argparse
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET
import zipfile

TOOLS = {
    'exporter': ('Orbyss.Foundation.OpenApi.Exporter', 'ExporterVersion'),
    'build': ('Orbyss.Foundation.Build', 'BuildToolsVersion'),
}
TAG = re.compile(r'(exporter|build)-v(\d+\.\d+\.\d+(?:-preview\.\d+)?)')


def plan(root: Path, tag: str) -> dict:
    match = TAG.fullmatch(tag)
    if match is None: raise ValueError('Only independent exporter-v or build-v version tags may publish tools')
    kind, version = match.groups()
    package_id, property_name = TOOLS[kind]
    project = 'src/' + package_id + '/' + package_id + '.csproj'
    source = ET.parse(root / project)
    if source.findtext('.//' + property_name) != version:
        raise ValueError('Independent tool tag differs from its source version')
    if source.findtext('.//Version') != '$(' + property_name + ')' or source.findtext('.//PackageVersion') not in {'$(Version)', '$(' + property_name + ')'}:
        raise ValueError('Tool package/assembly versions must bind their independent source property')
    return {'kind': kind, 'version': version, 'package_id': package_id, 'project': project,
            'package_path': 'artifacts/nuget/' + package_id + '.' + version + '.nupkg'}


def verify(root: Path, selected: dict, packages: Path | None = None) -> None:
    expected = (packages / Path(selected['package_path']).name) if packages is not None else root / selected['package_path']
    artifacts = list(expected.parent.glob('*.nupkg'))
    if artifacts != [expected]:
        raise ValueError('Independent tool release must contain exactly its selected package; no sibling tool or runtime publication')
    with zipfile.ZipFile(expected) as package:
        specs = [name for name in package.namelist() if name.endswith('.nuspec')]
        if len(specs) != 1: raise ValueError('Tool package must contain exactly one NuGet identity')
        spec = ET.fromstring(package.read(specs[0]))
        metadata = next((node for node in spec if node.tag.rsplit('}', 1)[-1] == 'metadata'), None)
        values = {node.tag.rsplit('}', 1)[-1]: node.text for node in metadata} if metadata is not None else {}
        if values.get('id') != selected['package_id'] or values.get('version') != selected['version']:
            raise ValueError('Packed tool identity differs from the exact independent tag')


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('plan', 'verify'))
    parser.add_argument('--tag', required=True)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--github-output', type=Path)
    parser.add_argument('--packages', type=Path, help='Exact private/CI packed-artifact directory; defaults to artifacts/nuget')
    args = parser.parse_args()
    try:
        selected = plan(args.root, args.tag)
        if args.command == 'verify': verify(args.root, selected, args.packages)
        if args.github_output:
            with args.github_output.open('a', encoding='utf-8') as stream:
                for key, value in selected.items(): stream.write(key + '=' + value + '\n')
        print(json.dumps(selected, indent=2))
        return 0
    except (ValueError, OSError, ET.ParseError, zipfile.BadZipFile) as error:
        print(str(error))
        return 2


if __name__ == '__main__': raise SystemExit(main())
