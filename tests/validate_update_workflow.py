"""Foundation can maintain its dependencies without any consumer checkout."""
from pathlib import Path, PurePosixPath
import importlib.util
import io
import json
import os
import subprocess
import tempfile
import tarfile
import unittest
from unittest.mock import MagicMock, patch
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]


class IndependentUpdateTests(unittest.TestCase):
    def test_keycloak_cleanup_attempts_independent_operations_and_preserves_failure_evidence(self):
        spec = importlib.util.spec_from_file_location('native_keycloak_provider', ROOT/'tests/validate_keycloak_provider.py')
        fixture = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(fixture)
        for failed in ('inspect', 'foreign-owner', 'logs', 'rm', None):
            with self.subTest(failed=failed), tempfile.TemporaryDirectory() as temporary:
                directory = Path(temporary)
                private = directory/'private-import'
                private.mkdir()
                (private/'realm.json').write_text('fictional-secret')
                calls = []

                def command(arguments, **kwargs):
                    calls.append(arguments[1])
                    if arguments[1] == failed:
                        raise OSError('failed fictional-secret')
                    ownership = 'foreign' if failed == 'foreign-owner' else 'owned\n'
                    return subprocess.CompletedProcess(arguments, 0, ownership if arguments[1] == 'inspect' else 'fictional-secret', '')

                removed, failures = fixture.cleanup_fixture(directory, private, 'fixture', 'owned', True, command, 'fictional-secret')
                self.assertFalse(private.exists())
                self.assertEqual(failed is not None, bool(failures))
                self.assertNotIn('fictional-secret', str(failures))
                self.assertEqual(failed not in ('inspect', 'foreign-owner', 'rm'), removed)
                self.assertEqual(['inspect'] if failed in ('inspect', 'foreign-owner') else ['inspect', 'logs', 'rm'], calls)
                if failed not in ('logs', 'inspect', 'foreign-owner'):
                    self.assertEqual('[REDACTED]', (directory/'provider.log').read_text())

    def test_keycloak_primary_and_cleanup_failures_both_survive_in_results(self):
        spec = importlib.util.spec_from_file_location('native_keycloak_provider', ROOT/'tests/validate_keycloak_provider.py')
        fixture = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(fixture)
        with tempfile.TemporaryDirectory() as temporary:
            def command(arguments, **kwargs):
                if arguments[1] == 'cp':
                    raise OSError('primary import failure')
                if arguments[1] == 'inspect':
                    if '{{.Config.User}}' in arguments:
                        return subprocess.CompletedProcess(arguments, 0, '1000', '')
                    raise OSError('secondary ownership failure')
                return subprocess.CompletedProcess(arguments, 0, '', '')
            with patch.object(fixture, 'ROOT', Path(temporary)), patch.object(fixture.subprocess, 'run', side_effect=command):
                with self.assertRaisesRegex(RuntimeError, 'primary import failure'):
                    fixture.main()
            result_file = next(Path(temporary).glob('artifacts/keycloak-provider/*/results.json'))
            result = json.loads(result_file.read_text())
            self.assertEqual('failed', result['status'])
            self.assertIn('primary import failure', result['failure'])
            self.assertIn('secondary ownership failure', result['cleanupFailures'][0]['error'])
            self.assertFalse(result['isolatedContainerRemoved'])
            self.assertFalse(list(Path(temporary).rglob('realm.json')))

    def test_update_is_self_contained_and_validates_before_opening_pr(self):
        workflow=(ROOT/'.github/workflows/update-dependencies.yml').read_text()
        stages=['dependency_maintenance.py upgrade','update_dependencies.py --prepare-source','git commit',
                'update_dependencies.py --export-knowledge','dependency_maintenance.py scan','git push','gh pr create']
        offsets=[workflow.index(stage) for stage in stages]
        self.assertEqual(sorted(offsets),offsets)
        self.assertIn('create_pull_request:', workflow)
        self.assertIn("github.event_name != 'workflow_dispatch' || inputs.create_pull_request", workflow)
        script=(ROOT/'scripts/update_dependencies.py').read_text()+(ROOT/'scripts/validate_installed_tools.py').read_text()
        self.assertNotIn('program-kit',script.lower())
        for name in ('refresh_vendor_metadata','refresh_test_locks','validate_foundation',
                     'run_contract_package_qualification','validate_hosted_package_consumption',
                     'validate_feature_build','validate_settings_no_build','validate_legacy_assurance_export',
                     'packaged-web-policies','tests/validate_web_policies.py','build_release_knowledge.py',
                     'foundation-knowledge-candidate.json','Commit prepared source',
                     'pack-analyzer-tool','AnalyzerVersion','validate_analyzer.py'):
            self.assertIn(name,script)
        for scenario in ('tests/browser/dynamic-pages.cjs', 'tests/browser/recovery-localization.cjs'):
            self.assertIn(scenario, script)

    def test_runtime_dockerfiles_share_the_same_exact_base(self):
        lines=[]
        for name in ('Dockerfile','Dockerfile.qualified'):
            text=(ROOT/'src/Orbyss.Foundation.Host'/name).read_text()
            lines.append(next(line for line in text.splitlines() if line.startswith('FROM mcr.microsoft.com/dotnet/aspnet:')))
        self.assertEqual(lines[0].split(' AS ')[0],lines[1].split(' AS ')[0])
        self.assertIn('@sha256:',lines[0])

    def test_real_keycloak_regression_is_owned_and_required_on_linux_and_release(self):
        policy = json.loads((ROOT/'maintenance-policy.json').read_text())
        self.assertIn('tests/validate_keycloak_provider.py', policy['inputs'])
        self.assertEqual({'github': 'keycloak/keycloak'}, policy['metadataOverrides']['image:quay.io/keycloak/keycloak'])
        qualifier = (ROOT/'scripts/validate_foundation.py').read_text()
        self.assertIn('"validate_keycloak_provider.py"', qualifier)
        self.assertIn('arguments.keycloak_recording_only and validator == "validate_keycloak_provider.py"', qualifier)
        ci = (ROOT/'.github/workflows/ci.yml').read_text()
        self.assertIn('python scripts/validate_foundation.py --postgresql-deadline-only --keycloak-recording-only', ci)
        for path in ('.github/workflows/release.yml', 'scripts/update_dependencies.py'):
            source = (ROOT/path).read_text()
            self.assertIn('validate_foundation.py', source)
            self.assertNotIn('--keycloak-recording-only', source)
        fixture = (ROOT/'tests/validate_keycloak_provider.py').read_text()
        self.assertIn('127.0.0.1::8080', fixture)
        self.assertIn('inspected.stdout.strip() == identity', fixture)
        workflow = (ROOT/'.github/workflows/update-dependencies.yml').read_text()
        self.assertIn('artifacts/keycloak-provider/*/*.log', workflow)
        self.assertIn('artifacts/keycloak-provider/*/results.json', workflow)
        self.assertNotIn('            artifacts/keycloak-provider/\n', workflow)

    def test_uncertain_docker_create_still_inspects_owned_container_and_removes_private_input(self):
        spec = importlib.util.spec_from_file_location('native_keycloak_provider', ROOT/'tests/validate_keycloak_provider.py')
        fixture = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(fixture)
        with tempfile.TemporaryDirectory() as temporary:
            calls = []
            identity = 'a' * 32
            def command(arguments, **kwargs):
                calls.append(arguments[1])
                if arguments[1] == 'create':
                    private = next(Path(temporary).rglob('realm.json'))
                    if os.name != 'nt':
                        self.assertEqual(0o600, private.stat().st_mode & 0o777)
                        self.assertEqual(0o700, private.parent.stat().st_mode & 0o777)
                    raise subprocess.TimeoutExpired(arguments, 120)
                return subprocess.CompletedProcess(arguments, 0, identity if arguments[1] == 'inspect' else '', '')
            with patch.object(fixture, 'ROOT', Path(temporary)), patch.object(fixture.uuid, 'uuid4', return_value=type('Id', (), {'hex': identity})()), patch.object(fixture.subprocess, 'run', side_effect=command):
                with self.assertRaisesRegex(RuntimeError, 'TimeoutExpired'):
                    fixture.main()
            result_file = next(Path(temporary).glob('artifacts/keycloak-provider/*/results.json'))
            result = json.loads(result_file.read_text())
            self.assertEqual(['create', 'inspect', 'logs', 'rm'], calls)
            self.assertEqual('failed', result['status'])
            self.assertTrue(result['createAttempted'])
            self.assertFalse(result['createCompleted'])
            self.assertTrue(result['isolatedContainerRemoved'])
            self.assertEqual([], result['cleanupFailures'])
            self.assertFalse(list(Path(temporary).rglob('realm.json')))

    def test_provider_artifacts_never_select_private_realm_on_cleanup_failure(self):
        public = ('artifacts/keycloak-provider/fictional/provider.log', 'artifacts/keycloak-provider/fictional/results.json')
        private = 'artifacts/keycloak-provider/fictional/private-import/realm.json'
        for name in ('ci.yml', 'release.yml', 'update-dependencies.yml'):
            source = (ROOT/'.github/workflows'/name).read_text()
            patterns = [line.strip() for line in source.splitlines() if line.strip().startswith('artifacts/')]
            with self.subTest(workflow=name):
                self.assertFalse(any(PurePosixPath(private).full_match(pattern) for pattern in patterns))
                for path in public:
                    self.assertTrue(any(PurePosixPath(path).full_match(pattern) for pattern in patterns))

    def test_native_probe_timeout_preserves_redacted_partial_streams_and_cleanup(self):
        spec = importlib.util.spec_from_file_location('native_keycloak_provider', ROOT/'tests/validate_keycloak_provider.py')
        fixture = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(fixture)
        with tempfile.TemporaryDirectory() as temporary:
            identity = 'b' * 32
            def command(arguments, **kwargs):
                if arguments[0] == 'dotnet':
                    secret = kwargs['env']['PROGRAM_KIT_KEYCLOAK_ADMIN_SECRET']
                    raise subprocess.TimeoutExpired(arguments, 120, output=('partial ' + secret).encode(), stderr=('failure ' + secret).encode())
                output = ('1000' if '{{.Config.User}}' in arguments else identity) if arguments[1] == 'inspect' else ('127.0.0.1:12345' if arguments[1] == 'port' else '')
                return subprocess.CompletedProcess(arguments, 0, output, '')
            response = MagicMock()
            response.__enter__.return_value.status = 200
            with patch.object(fixture, 'ROOT', Path(temporary)), patch.object(fixture.uuid, 'uuid4', return_value=type('Id', (), {'hex': identity})()), patch.object(fixture.subprocess, 'run', side_effect=command), patch.object(fixture.urllib.request, 'urlopen', return_value=response):
                with self.assertRaisesRegex(RuntimeError, 'TimeoutExpired'):
                    fixture.main()
            result_file = next(Path(temporary).glob('artifacts/keycloak-provider/*/results.json'))
            result = json.loads(result_file.read_text())
            self.assertEqual('failed', result['status'])
            self.assertTrue(result['isolatedContainerRemoved'])
            self.assertEqual([], result['cleanupFailures'])
            self.assertTrue(result['privateImportReadableAsNativeUser'])
            self.assertEqual('partial [REDACTED]', (result_file.parent/'stdout.log').read_text())
            self.assertEqual('failure [REDACTED]', (result_file.parent/'stderr.log').read_text())
            self.assertFalse(list(Path(temporary).rglob('realm.json')))

    def test_import_archive_preserves_private_modes_for_only_the_selected_nonroot_user(self):
        spec = importlib.util.spec_from_file_location('native_keycloak_provider', ROOT/'tests/validate_keycloak_provider.py')
        fixture = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(fixture)
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary)/'realm.json'
            path.write_bytes(b'fictional-private-realm')
            with tarfile.open(fileobj=io.BytesIO(fixture.import_archive(path, 1000, 0))) as archive:
                self.assertEqual(['import', 'import/realm.json'], archive.getnames())
                directory, realm = archive.getmembers()
                self.assertTrue(directory.isdir())
                self.assertTrue(realm.isfile())
                self.assertEqual([(1000, 0, 0o700), (1000, 0, 0o600)], [(item.uid, item.gid, item.mode) for item in (directory, realm)])
                self.assertEqual(b'fictional-private-realm', archive.extractfile(realm).read())


if __name__=='__main__': unittest.main()
