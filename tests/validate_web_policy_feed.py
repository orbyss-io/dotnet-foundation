"""Candidate-feed routing preserves partial and full Foundation package families."""
from pathlib import Path
import tempfile
import unittest
import zipfile
import validate_web_policies as policies


class CandidateFeedTests(unittest.TestCase):
    def package(self, root, name, identity):
        with zipfile.ZipFile(root / name, "w") as archive:
            archive.writestr("owner.nuspec", '<package xmlns="urn:nuget"><metadata><id>' + identity + '</id><version>0.3.2</version></metadata></package>')

    def test_private_two_owner_and_full_family_route_only_present_native_identities(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            owners = ["Orbyss.Foundation.WebDefaults", "Orbyss.Foundation.Authentication.BffCookie"]
            for identity in owners:
                self.package(root, identity + ".0.3.2.nupkg", identity)
            self.assertEqual(sorted(owners), policies.candidate_package_ids(root))
            for identity in ["Orbyss.Foundation.Json", "Orbyss.Foundation.Authentication.Core", "Orbyss.Foundation.Analyzers"]:
                self.package(root, identity + ".0.3.2.nupkg", identity)
                owners.append(identity)
            self.assertEqual(sorted(owners), policies.candidate_package_ids(root))
            self.assertNotIn("Orbyss.Foundation.PostgreSql", policies.candidate_package_ids(root))

    def test_empty_mislabeled_and_ambiguous_feeds_fail(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with self.assertRaises(ValueError): policies.candidate_package_ids(root)
            self.package(root, "Orbyss.Foundation.Wrong.0.3.2.nupkg", "Other.Owner")
            with self.assertRaises(ValueError): policies.candidate_package_ids(root)
            self.package(root, "Orbyss.Foundation.Wrong.0.3.2.nupkg", "Orbyss.Foundation.Wrong")
            with zipfile.ZipFile(root / "Orbyss.Foundation.Wrong.0.3.2.nupkg", "a") as archive:
                archive.writestr("second.nuspec", "<package/>")
            with self.assertRaises(ValueError): policies.candidate_package_ids(root)


if __name__ == "__main__": unittest.main()
