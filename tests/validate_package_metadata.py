from __future__ import annotations

import argparse
import json
import zipfile
from pathlib import Path
from xml.etree import ElementTree


ROOT = Path(__file__).resolve().parents[1]
EXPECTED_REPOSITORY = "https://github.com/orbyss-io/dotnet-foundation"


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def child(parent: ElementTree.Element, name: str) -> ElementTree.Element:
    return next(item for item in parent if local_name(item.tag) == name)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--packages", type=Path, default=ROOT / "artifacts/nuget")
    args = parser.parse_args()

    if (ROOT / "src/dotnet").exists() or (ROOT / "eng").exists():
        raise AssertionError("Foundation must keep a flat src layout and repository tooling under scripts/.")

    expected_version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    expected_ids = {
        project.stem
        for project in (ROOT / "src").glob("Orbyss.Foundation*/*.csproj")
        if project.stem not in {"Orbyss.Foundation.Host", "Orbyss.Foundation.Build", "Orbyss.Foundation.OpenApi.Exporter"}
    }
    if len(expected_ids) != 24:
        raise AssertionError(f"Expected 24 Foundation package projects, found {len(expected_ids)}.")

    found: set[str] = set()
    for package in sorted(args.packages.glob("*.nupkg")):
        with zipfile.ZipFile(package) as archive:
            nuspecs = [name for name in archive.namelist() if name.endswith(".nuspec")]
            if len(nuspecs) != 1:
                raise AssertionError(f"{package.name} must contain exactly one nuspec.")
            nuspec = archive.read(nuspecs[0])
            root = ElementTree.fromstring(nuspec)
        metadata = child(root, "metadata")
        package_id = child(metadata, "id").text or ""
        version = child(metadata, "version").text or ""
        repository = child(metadata, "repository")
        if package_id not in expected_ids:
            raise AssertionError(f"Unexpected package ID: {package_id}")
        package_version = expected_version
        if package_id == 'Orbyss.Foundation.OpenApi.Exporter':
            project = ROOT / 'src' / package_id / (package_id + '.csproj')
            package_version = ElementTree.parse(project).findtext('.//ExporterVersion') or expected_version
        if version != package_version:
            raise AssertionError(f"{package_id} has version {version}, expected {package_version}.")
        if repository.attrib.get("url") != EXPECTED_REPOSITORY:
            raise AssertionError(f"{package_id} has the wrong repository URL.")
        if not package_id.startswith("Orbyss.Foundation."):
            raise AssertionError(f"{package_id} is outside the Foundation namespace.")
        if b"ProgramKit" in nuspec:
            raise AssertionError(f"{package_id} still exposes a ProgramKit package identity.")
        source = ROOT / 'src' / package_id / 'feature.json'
        if source.is_file():
            with zipfile.ZipFile(package) as archive:
                assert 'orbyss-foundation/feature.json' in archive.namelist(), 'Publisher descriptor is missing'
                assert 'program-kit/feature.json' not in archive.namelist(), 'New publishers must emit only canonical metadata'
                descriptor = json.loads(archive.read('orbyss-foundation/feature.json'))
                for key, expected in json.loads(source.read_text(encoding='utf-8')).items():
                    assert descriptor.get(key) == expected, 'Packed publisher metadata differs: ' + key
                assert descriptor['packageId'] == package_id
                assert b'Orbyss.Foundation.Build' not in nuspec, 'Private build tooling leaked into runtime dependencies'
        found.add(package_id)

    if found != expected_ids:
        raise AssertionError(f"Package set mismatch. Missing={sorted(expected_ids - found)}, extra={sorted(found - expected_ids)}")
    print("Exact 24-package Orbyss Foundation NuGet metadata contract passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
