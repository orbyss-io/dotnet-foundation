"""Independent tool tags cannot publish a sibling tool or runtime artifact."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import zipfile
import xml.etree.ElementTree as ET
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'scripts'))
import tool_release as release
ROOT = Path(__file__).resolve().parents[1]


class ToolReleaseTests(unittest.TestCase):
    def test_each_tag_selects_only_its_independent_source_identity(self):
        for kind in release.TOOLS:
            project = ROOT / ('src/' + release.TOOLS[kind][0])
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
        for tag in ('v0.2.3', 'exporter-v9.9.9', 'build-v9.9.9', 'analyzer-v9.9.9',
                    'build-v0.1.0; echo leaked', 'analyzer-v0.3.8; echo leaked',
                    'exporter-v0.2.4\npackage_id=Runtime', 'analyzer-v0.3.8\npackage_id=Runtime',
                    'build-v../0.1.0', 'analyzer-v../0.3.8'):
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

    def test_independent_tools_are_excluded_from_runtime_family_publication(self):
        for identity, _ in release.TOOLS.values():
            with self.subTest(package=identity):
                project = ET.parse(ROOT / 'src' / identity / (identity + '.csproj'))
                settings = {node.attrib.get('Condition'): node.text for node in project.findall('.//IsPackable')}
                self.assertEqual('false', settings.get("'$(FoundationRuntimeOnly)' == 'true'"))
                self.assertEqual('true', settings.get("'$(FoundationRuntimeOnly)' != 'true'"))

    def test_analyzer_workflow_validates_selected_bytes_before_publication(self):
        workflow = (ROOT / '.github/workflows/release-tools.yml').read_text()
        self.assertIn('"analyzer-v*.*.*"', workflow)
        self.assertIn("if: steps.selected.outputs.kind == 'analyzer'", workflow)
        self.assertIn('python tests/validate_analyzer.py --package "$PACKAGE"', workflow)
        self.assertLess(workflow.index('python tests/validate_analyzer.py --package'), workflow.index('dotnet nuget push'))
        self.assertLess(workflow.index('tool_release.py verify --tag'), workflow.index('dotnet nuget push'))
        self.assertIn('environment: nuget-production', workflow)

    def test_independent_analyzer_knowledge_preserves_exact_source_and_capability(self):
        import build_release_knowledge
        identity, property_name = release.TOOLS['analyzer']
        version = ET.parse(ROOT / 'src' / identity / (identity + '.csproj')).findtext('.//' + property_name)
        selected = release.plan(ROOT, 'analyzer-v' + version)
        marker = 'buildTransitive/source-readability-contract.json'
        content = (ROOT / 'src' / selected['package_id'] / marker).read_bytes()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            packages = root / 'packages'; packages.mkdir()
            archive_path = packages / Path(selected['package_path']).name
            with zipfile.ZipFile(archive_path, 'w') as archive:
                archive.writestr('analyzer.nuspec', '<package><metadata><id>' + selected['package_id']
                    + '</id><version>' + selected['version'] + '</version><repository commit="' + 'a'*40
                    + '"/></metadata></package>')
                archive.writestr(marker, content)
            release.verify(root, selected, packages)
            knowledge = build_release_knowledge.build(root, packages, selected['version'], 'a'*40)
            self.assertEqual([selected['package_id']], [package['id'] for package in knowledge['packages']])
            self.assertEqual(content.decode(), knowledge['packages'][0]['facts'][marker])
            self.assertEqual('a'*40, knowledge['sourceCommit'])

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
