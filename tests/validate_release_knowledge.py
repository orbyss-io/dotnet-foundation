"""Publisher knowledge includes schemas, interfaces, capabilities and exact payload bindings."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('knowledge',ROOT/'scripts/build_release_knowledge.py')
m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m)


class KnowledgeTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name); (self.root/'README.md').write_text('Exact publisher usage guidance')
        self.packages = self.root/'packages'; self.packages.mkdir()

    def package(self, version='0.3.0'):
        path = self.packages/'orbyss.foundation.sample.nupkg'
        with zipfile.ZipFile(path,'w') as archive:
            archive.writestr('sample.nuspec','<package><metadata><id>Orbyss.Foundation.Sample</id><version>'+version+'</version><license>MIT</license></metadata></package>')
            archive.writestr('orbyss-foundation/feature.json','{"identity":"Sample","settings":{"type":"object"}}')
            archive.writestr('schemas/settings.json','{"type":"object","properties":{"limit":{"type":"integer"}}}')
            archive.writestr('lib/net10.0/Sample.xml','<doc><members><member name="T:Sample.Interface"/></members></doc>')
            archive.writestr('.signature.p7s','registry signature')
        return path

    def test_exact_metadata_interfaces_schemas_and_source_guidance_are_exposed(self):
        self.package(); value=m.build(self.root,self.packages,'0.3.0','a'*40)
        package=value['packages'][0]
        self.assertIn('schemas/settings.json',package['facts'])
        self.assertIn('lib/net10.0/Sample.xml',package['facts'])
        self.assertIn('orbyss-foundation/feature.json',package['facts'])
        self.assertNotIn('.signature.p7s',package['payloadFiles'])
        self.assertEqual('Exact publisher usage guidance',value['documents'][0]['text'])
        self.assertIn('a'*40,value['documents'][0]['url'])

    def test_wrong_release_and_empty_package_set_cannot_claim_knowledge(self):
        with self.assertRaisesRegex(ValueError,'No publisher'): m.build(self.root,self.packages,'0.3.0','a'*40)
        self.package('0.2.4')
        with self.assertRaisesRegex(ValueError,'exact selected'): m.build(self.root,self.packages,'0.3.0','a'*40)


if __name__=='__main__': unittest.main()
