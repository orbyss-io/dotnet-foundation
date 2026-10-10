"""Run response-policy probes from source or exact installed candidate archives."""
from __future__ import annotations
import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile
from xml.etree import ElementTree as ET
import zipfile
import validate_openapi_exporter as fixture
import settings_package_bindings as bindings



def candidate_package_ids(feed: Path):
    identities = set()
    for package in feed.glob("Orbyss.Foundation.*.nupkg"):
        with zipfile.ZipFile(package) as archive:
            specs = [name for name in archive.namelist() if "/" not in name and name.endswith(".nuspec")]
            bindings.require(len(specs) == 1, "candidate requires one native nuspec")
            metadata = ET.fromstring(bindings.member(archive, specs[0], bindings.MAX_METADATA))
            values = [node.text for node in metadata.iter() if node.tag.rsplit("}", 1)[-1] == "id"]
            bindings.require(len(values) == 1 and isinstance(values[0], str)
                             and values[0].startswith("Orbyss.Foundation."), "candidate must contain a Foundation identity")
            identities.add(values[0])
    bindings.require(bool(identities), "candidate feed is empty")
    return sorted(identities)


def qualify(package: Path, bff_package: Path | None):
    package = package.resolve()
    inspected = [bindings.inspect(package)]
    artifact = fixture.ROOT / "artifacts/web-policies"
    artifact.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix="packaged-", dir=artifact))
    for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"):
        (work / name).write_text("<Project/>\n", encoding="utf-8")
    config = work / "NuGet.config"
    # Pin every locally supplied Foundation identity, including full stable families.
    # Private two-owner feeds retain public resolution for all other dependencies.
    candidate_ids = candidate_package_ids(package.parent)
    candidate_patterns = "".join('<package pattern="' + fixture.escape(identity) + '"/>'
                                 for identity in candidate_ids)
    config.write_text('<configuration><packageSources><clear/><add key="candidate" value="' + fixture.escape(str(package.parent))
        + '"/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/><add key="cshells-preview" value="https://f.feedz.io/valence-works/cshells/nuget/index.json"/></packageSources>'
        + '<packageSourceMapping><clear/><packageSource key="candidate">' + candidate_patterns + '</packageSource><packageSource key="cshells-preview"><package pattern="CShells"/><package pattern="CShells.*"/></packageSource><packageSource key="nuget.org"><package pattern="*"/></packageSource></packageSourceMapping></configuration>', encoding="utf-8")
    environment = dict(os.environ, NUGET_PACKAGES=str(work / "cache"), DOTNET_CLI_HOME=str(work / "dotnet-home"))
    versions = {item.get("Include"): item.get("Version") for item in ET.parse(fixture.ROOT / "Directory.Packages.props").iter("PackageVersion")}
    stable = (fixture.ROOT / "VERSION").read_text().strip()
    probes = [("Orbyss.Foundation.WebDefaults.Probe", package)]
    if bff_package:
        bff_package = bff_package.resolve()
        assert bff_package.parent == package.parent
        inspected.append(bindings.inspect(bff_package))
        with zipfile.ZipFile(bff_package) as archive:
            bff_metadata = json.loads(archive.read("orbyss-foundation/settings.json"))
        timeout = next(setting for setting in bff_metadata["contracts"][0]["settings"] if setting["path"] == "Foundation:Web:RemoteAuthenticationTimeoutSeconds")
        assert timeout["constraints"] == {"minimum": 1} and timeout["default"] == 10
        probes.append(("Orbyss.Foundation.Authentication.BffCookie.Probe", bff_package))
    for name, selected in probes:
        folder = work / name
        folder.mkdir()
        original = fixture.ROOT / "tests/dotnet" / name
        for source in original.glob("*.cs"):
            shutil.copy2(source, folder / source.name)
        document = ET.parse(original / (name + ".csproj"))
        references = []
        for node in document.iter("PackageReference"):
            references.append('<PackageReference Include="' + node.get("Include") + '" Version="' + versions[node.get("Include")] + '"/>')
        for node in document.iter("ProjectReference"):
            owner = Path(node.get("Include")).parent.name
            version = fixture.package_version(selected) if owner == bindings.inspect(selected)["packageId"] else stable
            references.append('<PackageReference Include="' + owner + '" Version="' + version + '"/>')
        sdk = document.getroot().get("Sdk")
        project = folder / (name + ".csproj")
        project.write_text('<Project Sdk="' + sdk + '"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><RestorePackagesWithLockFile>true</RestorePackagesWithLockFile></PropertyGroup><ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App"/>' + ''.join(references) + '</ItemGroup></Project>', encoding="utf-8")
        fixture.run(["dotnet", "restore", str(project), "--configfile", str(config)], folder, folder / "restore.log", env=environment)
        fixture.run(["dotnet", "restore", str(project), "--locked-mode", "--configfile", str(config)], folder, folder / "locked-restore.log", env=environment)
        fixture.run(["dotnet", "build", str(project), "-c", "Release", "--no-restore"], folder, folder / "build.log", env=environment)
        runtime = folder / "bin/Release/net10.0"
        for record in inspected if bff_package and selected == bff_package else inspected[:1]:
            archive = package if record["packageId"] == "Orbyss.Foundation.WebDefaults" else bff_package
            bindings.verify(record, archive, folder / "obj/project.assets.json", work / "cache", runtime)
        fixture.run(["dotnet", str(runtime / (name + ".dll"))], folder, folder / "runtime.log", env=environment)
        if name == "Orbyss.Foundation.WebDefaults.Probe":
            defaults = json.loads(fixture.run(["dotnet", str(runtime / (name + ".dll")), "--defaults"], folder, folder / "defaults.log", env=environment))
            with zipfile.ZipFile(package) as archive:
                metadata = json.loads(archive.read("orbyss-foundation/settings.json"))
            settings = next(c for c in metadata["contracts"] if c["scope"] == "web-response-policy")["settings"]
            assert defaults == {s["path"].split(":")[-1]: s["default"] for s in settings}
    result = {"status": "passed", "actualPackagedOwners": inspected, "sourceDerivedDefaultsMatch": True,
              "probes": [name for name, _ in probes], "coldNativeRestoreAndRuntimeBinding": True}
    (work / "results.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("Installed candidate response policies and BFF activation passed. Evidence: " + str(work / "results.json"))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path)
    parser.add_argument("--bff-package", type=Path)
    args = parser.parse_args()
    if args.package:
        qualify(args.package, args.bff_package)
    else:
        if args.bff_package:
            parser.error("--bff-package requires --package")
        artifact = fixture.ROOT / "artifacts/web-policies"
        artifact.mkdir(parents=True, exist_ok=True)
        fixture.run(["dotnet", "run", "--project", str(fixture.ROOT / "tests/dotnet/Orbyss.Foundation.WebDefaults.Probe"),
                     "-c", "Release", "--no-restore", "--no-build"], fixture.ROOT, artifact / "source-runtime.log")
        print("Source response policy probe passed. Evidence: " + str(artifact / "source-runtime.log"))


if __name__ == "__main__":
    main()
