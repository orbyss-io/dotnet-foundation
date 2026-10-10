"""Foundation can maintain its dependencies without any consumer checkout."""
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]


class IndependentUpdateTests(unittest.TestCase):
    def test_update_is_self_contained_and_validates_before_opening_pr(self):
        workflow=(ROOT/'.github/workflows/update-dependencies.yml').read_text()
        stages=['dependency_maintenance.py upgrade','update_dependencies.py --prepare-source','git commit',
                'update_dependencies.py --export-knowledge','dependency_maintenance.py scan','git push','gh pr create']
        offsets=[workflow.index(stage) for stage in stages]
        self.assertEqual(sorted(offsets),offsets)
        script=(ROOT/'scripts/update_dependencies.py').read_text()+(ROOT/'scripts/validate_installed_tools.py').read_text()
        self.assertNotIn('program-kit',script.lower())
        for name in ('refresh_vendor_metadata','refresh_test_locks','validate_foundation',
                     'run_contract_package_qualification','validate_hosted_package_consumption',
                     'validate_feature_build','validate_settings_no_build','validate_legacy_assurance_export',
                     'packaged-web-policies','tests/validate_web_policies.py','build_release_knowledge.py',
                     'foundation-knowledge-candidate.json','Commit prepared source'):
            self.assertIn(name,script)

    def test_runtime_dockerfiles_share_the_same_exact_base(self):
        lines=[]
        for name in ('Dockerfile','Dockerfile.qualified'):
            text=(ROOT/'src/Orbyss.Foundation.Host'/name).read_text()
            lines.append(next(line for line in text.splitlines() if line.startswith('FROM mcr.microsoft.com/dotnet/aspnet:')))
        self.assertEqual(lines[0].split(' AS ')[0],lines[1].split(' AS ')[0])
        self.assertIn('@sha256:',lines[0])


if __name__=='__main__': unittest.main()
