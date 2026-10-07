"""Automatic dependency update and rollback contracts; no network or agents."""
import importlib.util
import json
import hashlib
import io
import subprocess
import tarfile
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('maintenance', ROOT / 'scripts/dependency_maintenance.py')
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)


class UpdateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.policy = {'inputs': ['package.json', 'global.json'], 'additionalPins': []}
        m.write_json(self.root / 'maintenance-policy.json', self.policy)
        m.write_json(self.root / 'package.json', {'devDependencies': {'typescript': '7.0.2'}, 'engines': {'node':'24.20.0'}})
        m.write_json(self.root / 'global.json', {'sdk':{'version':'10.0.202'}})

    def observations(self):
        latest = {'typescript':'7.0.3', 'node':'26.0.0', 'dotnet-sdk':'10.0.401'}
        return m.collect(self.root, self.policy, lambda p, o: {'latest':latest.get(p['name'],p['current']), 'source':'fixture'})

    def test_actual_package_engine_and_sdk_pins_upgrade_together(self):
        changes = m.upgrade(self.root, self.policy, self.observations())
        self.assertEqual(3, len(changes))
        self.assertEqual('7.0.3', m.read_json(self.root / 'package.json')['devDependencies']['typescript'])
        self.assertEqual('26.0.0', m.read_json(self.root / 'package.json')['engines']['node'])
        self.assertEqual('10.0.401', m.read_json(self.root / 'global.json')['sdk']['version'])

    def test_failed_upstream_lookup_does_not_partially_upgrade(self):
        before = (self.root / 'package.json').read_bytes()
        observations = self.observations()
        observations['pins'][-1]['latest'] = None
        with self.assertRaisesRegex(ValueError, 'lookup failed'):
            m.upgrade(self.root, self.policy, observations)
        self.assertEqual(before, (self.root / 'package.json').read_bytes())

    def test_failed_image_resolution_rolls_back_other_upgrades(self):
        path = self.root / 'Dockerfile'
        path.write_text('FROM mcr.microsoft.com/dotnet/sdk:10.0.202@sha256:' + 'a'*64)
        self.policy['inputs'].append('Dockerfile')
        before = {p.name:p.read_bytes() for p in self.root.iterdir() if p.is_file()}
        observations = self.observations()
        for row in observations['pins']:
            if row['kind'] == 'image': row['latest'] = 'sha256:' + 'b'*64
        with patch.object(m, 'observe', side_effect=OSError('registry unavailable')):
            with self.assertRaises(OSError): m.upgrade(self.root, self.policy, observations)
        self.assertEqual(before, {p.name:p.read_bytes() for p in self.root.iterdir() if p.is_file()})

    def test_new_dependency_is_discovered_and_historical_profile_is_preserved(self):
        m.write_json(self.root / 'package.json', {'devDependencies': {'typescript':'7.0.2','vite':'8.0.0'}})
        pins = m.inventory(self.root, self.policy)['pins']
        self.assertTrue(any(p['name']=='vite' for p in pins))
        path = self.root / 'references/dependency-profiles/old.json'
        m.write_json(path, {'immutable':'old'})
        self.policy['inputs'].append('references/dependency-profiles/old.json')
        observations = self.observations()
        observations['pins'].append({'kind':'nuget','name':'Orbyss.Foundation.WebDefaults','current':'0.2.4',
            'latest':'0.3.0','id':'historical','paths':['references/dependency-profiles/old.json']})
        m.upgrade(self.root, self.policy, observations)
        self.assertEqual({'immutable':'old'}, m.read_json(path))

    def test_unavailable_lookup_is_an_explicit_failure_not_a_current_version(self):
        def failure(*args): raise OSError('credential diagnostic must not be recorded')
        observations = m.collect(self.root, self.policy, failure)
        self.assertTrue(all(p['latest'] is None and p['error']=='OSError' for p in observations['pins']))
        self.assertNotIn('credential diagnostic', json.dumps(observations))

    def test_oci_scan_selects_both_real_platforms_from_nested_index(self):
        payloads={}
        def blob(value):
            data=json.dumps(value).encode(); digest=hashlib.sha256(data).hexdigest()
            payloads['blobs/sha256/'+digest]=data
            return {'digest':'sha256:'+digest,'size':len(data)}
        images=[]
        for architecture in ('amd64','arm64'):
            descriptor=blob({'config':{'architecture':architecture},'layers':[]})
            descriptor['platform']={'os':'linux','architecture':architecture}
            images.append(descriptor)
        nested=blob({'schemaVersion':2,'manifests':images})
        payloads['index.json']=json.dumps({'schemaVersion':2,'manifests':[nested]}).encode()
        archive=self.root/'candidate.tar'
        with tarfile.open(archive,'w') as stream:
            for name,data in payloads.items():
                info=tarfile.TarInfo(name); info.size=len(data); stream.addfile(info,io.BytesIO(data))
        observed=[]
        def scanner(command,**kwargs):
            platform=command[command.index('--platform')+1]
            layout=Path(kwargs['cwd'])/command[command.index('--input')+1]
            selected=m.read_json(layout/'index.json')['manifests']
            self.assertEqual(1,len(selected))
            self.assertEqual(platform.split('/')[1],selected[0]['platform']['architecture'])
            observed.append(platform)
            m.write_json(Path(command[command.index('--output')+1]),{'Results':[]})
            return subprocess.CompletedProcess(command,0)
        with patch.object(m.subprocess,'run',side_effect=scanner): m.scan(self.root,str(archive))
        self.assertEqual(['linux/amd64','linux/arm64'],observed)
        result=m.read_json(self.root/'artifacts/dependency-security/result.json')
        self.assertEqual(2,len({row['manifestDigest'] for row in result['scans']}))


if __name__ == '__main__': unittest.main()
