"""Independent tool tags cannot publish a sibling tool or runtime artifact."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import zipfile
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'scripts'))
import tool_release as release
ROOT = Path(__file__).resolve().parents[1]


class ToolReleaseTests(unittest.TestCase):
    def test_each_tag_selects_only_its_independent_source_identity(self):
        for kind in release.TOOLS:
            project = ROOT / ('src/' + release.TOOLS[kind][0])
            import xml.etree.ElementTree as ET
            version = ET.parse(project / (project.name + '.csproj')).findtext('.//' + release.TOOLS[kind][1])
            selected = release.plan(ROOT, kind + '-v' + version)
            self.assertEqual(kind, selected['kind'])
            self.assertEqual(release.TOOLS[kind][0], selected['package_id'])
            self.assertEqual(version, selected['version'])
            with tempfile.TemporaryDirectory() as directory:
                output = Path(directory) / 'outputs'
                result = subprocess.run([sys.executable, str(ROOT / 'scripts/tool_release.py'), 'plan',
                    '--tag', kind + '-v' + version, '--github-output', str(output)], capture_output=True, text=True)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertEqual(selected, json.loads(result.stdout))
                self.assertEqual(selected, dict(line.split('=', 1) for line in output.read_text().splitlines()))

    def test_runtime_mismatched_and_untrusted_tags_fail_before_pack(self):
        for tag in ('v0.2.3', 'exporter-v9.9.9', 'build-v9.9.9', 'build-v0.1.0; echo leaked',
                    'exporter-v0.2.4\npackage_id=Runtime', 'build-v../0.1.0'):
            with self.subTest(tag=tag), self.assertRaises(ValueError): release.plan(ROOT, tag)

    def test_tool_versions_do_not_follow_family_version(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'VERSION').write_text('9.9.9')
            for kind, (identity, property_name) in release.TOOLS.items():
                path = root / 'src' / identity / (identity + '.csproj')
                path.parent.mkdir(parents=True)
                path.write_text('<Project><PropertyGroup><' + property_name + '>1.2.3</' + property_name + '>'
                    '<Version>$(' + property_name + ')</Version><PackageVersion>$(Version)</PackageVersion></PropertyGroup></Project>')
                self.assertEqual('1.2.3', release.plan(root, kind + '-v1.2.3')['version'])
                path.write_text(path.read_text().replace('$(' + property_name + ')', '$(FoundationVersion)'))
                with self.assertRaisesRegex(ValueError, 'independent source property'): release.plan(root, kind + '-v1.2.3')

    def test_exact_artifact_check_rejects_sibling_and_wrong_embedded_identity(self):
        for kind, (identity, _) in release.TOOLS.items():
            with self.subTest(kind=kind), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                selected = {'package_id': identity, 'version': '1.2.3',
                            'package_path': 'artifacts/nuget/' + identity + '.1.2.3.nupkg'}
                artifact = root / selected['package_path'];artifact.parent.mkdir(parents=True)
                def package(package_id=identity, version='1.2.3'):
                    with zipfile.ZipFile(artifact, 'w') as archive:
                        archive.writestr('tool.nuspec', '<package xmlns="urn:nuget"><metadata><id>' + package_id + '</id><version>' + version + '</version></metadata></package>')
                with self.assertRaisesRegex(ValueError, 'exactly its selected'): release.verify(root, selected)
                package();release.verify(root, selected)
                sibling = artifact.parent / 'Orbyss.Foundation.Core.1.2.3.nupkg';sibling.write_bytes(b'Runtime')
                with self.assertRaisesRegex(ValueError, 'no sibling tool or runtime'): release.verify(root, selected)
                sibling.unlink()
                for package_id, version in [('Runtime', '1.2.3'), (identity, '1.2.4')]:
                    package(package_id, version)
                    with self.assertRaisesRegex(ValueError, 'identity differs'): release.verify(root, selected)


if __name__ == '__main__': unittest.main()
