"""Execute locally restored tool packages against a real, packed OpenAPI feature."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import zipfile
from xml.etree import ElementTree
from xml.sax.saxutils import escape


ROOT = Path(__file__).resolve().parents[1]
TOOL_ID = "Orbyss.Foundation.OpenApi.Exporter"
COMMAND = "orbyss-foundation-openapi-export"
FEATURE_ID = "OpenApiVersionProbe"
FEATURE_PACKAGE = "Orbyss.Foundation.OpenApi.VersionProbe"


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def run(arguments: list[str], cwd: Path, log: Path, *, env=None, expected=0) -> str:
    result = subprocess.run(arguments, cwd=cwd, env=env, capture_output=True,
                            text=True, timeout=240)
    output = result.stdout + result.stderr
    log.write_text(output, encoding="utf-8")
    if result.returncode != expected:
        raise AssertionError(f"Expected exit {expected}, got {result.returncode}: {arguments}\n"
                             f"Log: {log}\n{output}")
    return output


def package_version(package: Path) -> str:
    with zipfile.ZipFile(package) as archive:
        nuspec = next(name for name in archive.namelist() if name.endswith(".nuspec"))
        metadata = ElementTree.fromstring(archive.read(nuspec))
    return next(item.text for item in metadata.iter() if item.tag.rsplit("}", 1)[-1] == "version")


def create_feature(work: Path) -> Path:
    fixture = work / "feature"
    fixture.mkdir()
    # Keep this generated project independent of repository build/analyzer injection.
    (work / "Directory.Build.props").write_text("<Project />\n", encoding="utf-8")
    (work / "Directory.Build.targets").write_text("<Project />\n", encoding="utf-8")
    (work / "Directory.Packages.props").write_text("<Project />\n", encoding="utf-8")
    versions = {item.attrib["Include"]: item.attrib["Version"] for item in
                ElementTree.parse(ROOT / "Directory.Packages.props").iter("PackageVersion")}
    references = "\n".join(
        f'<PackageReference Include="{name}" Version="{versions[name]}" />'
        for name in ("CShells.Abstractions", "CShells.AspNetCore.Abstractions", "Microsoft.AspNetCore.OpenApi"))
    project = fixture / f"{FEATURE_PACKAGE}.csproj"
    project.write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Version>1.0.0-preview.1</Version>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    {references}
    <None Include="feature.json" Pack="true" PackagePath="orbyss-foundation/" />
  </ItemGroup>
</Project>
''', encoding="utf-8")
    shutil.copy2(ROOT / "tests/openapi-exporter/VersionProbeFeature.cs", fixture)
    shutil.copy2(ROOT / "tests/openapi-exporter/packages.lock.json", fixture)
    write_json(fixture / "feature.json", {
        "schemaVersion": 1, "identity": FEATURE_ID, "packageId": FEATURE_PACKAGE,
        "featureDependencies": [], "routes": ["/probe"],
    })
    run(["dotnet", "restore", str(project), "--locked-mode", "--configfile", str(ROOT / "NuGet.config")],
        ROOT, work / "feature-restore.log")
    closure = work / "closure"
    run(["dotnet", "pack", str(project), "-c", "Release", "--no-restore", "--output", str(closure)],
        ROOT, work / "feature-pack.log")
    return closure


def check_tool(package: Path, work: Path, closure: Path) -> dict:
    version = package_version(package)
    case = work / version
    case.mkdir()
    feed = case / "feed"
    feed.mkdir()
    shutil.copy2(package, feed)
    config = case / "NuGet.config"
    config.write_text(f'''<configuration>
  <packageSources><clear /><add key="local" value="{escape(str(feed))}" /></packageSources>
  <packageSourceMapping><clear /><packageSource key="local"><package pattern="*" /></packageSource></packageSourceMapping>
</configuration>
''', encoding="utf-8")
    write_json(case / ".config/dotnet-tools.json", {
        "version": 1, "isRoot": True,
        "tools": {TOOL_ID.lower(): {"version": version, "commands": [COMMAND]}},
    })
    env = dict(os.environ, NUGET_PACKAGES=str(case / "nuget-cache"),
               DOTNET_CLI_HOME=str(case / "dotnet-home"), DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1")
    run(["dotnet", "tool", "restore", "--configfile", str(config)], case, case / "tool-restore.log", env=env)
    write_json(case / "shells.json", {"CShells": {"Shells": {"probe": {
        "Features": {FEATURE_ID: True}, "Configuration": {"WebRouting": {"Path": "test"}},
    }}}})
    write_json(case / "hostsettings.json", {})
    contract = {"schemaVersion": 1, "identity": "packed-version-probe", "documentName": "v1",
                "shell": "probe", "producer": {"kind": TOOL_ID, "version": version},
                "features": [FEATURE_ID]}
    arguments = ["dotnet", "tool", "run", COMMAND, "--",
                 "--repository", str(case), "--packages", str(closure),
                 "--shells", str(case / "shells.json"), "--hostsettings", str(case / "hostsettings.json"),
                 "--contract", str(case / "contract.json")]
    write_json(case / "contract.json", contract)
    document = case / "matching.json"
    evidence = case / "matching.evidence.json"
    run(arguments + ["--output", str(document), "--evidence", str(evidence)],
        case, case / "matching.log", env=env)
    generated = json.loads(document.read_text(encoding="utf-8"))
    assert "/test/probe" in generated["paths"], generated
    receipt = json.loads(evidence.read_text(encoding="utf-8"))
    assert receipt["producer"] == {"kind": TOOL_ID, "version": version}, receipt
    assert receipt["rawDocument"]["sha256"] == hashlib.sha256(document.read_bytes()).hexdigest()
    assert receipt["composedFeatures"] == [FEATURE_ID], receipt
    # A wrong version must still reject before writing documents or producer evidence.
    wrong_version = "0.0.0-version-mismatch"
    contract["producer"]["version"] = wrong_version
    write_json(case / "contract.json", contract)
    rejected_document = case / "mismatch.json"
    rejected_evidence = case / "mismatch.evidence.json"
    output = run(arguments + ["--output", str(rejected_document), "--evidence", str(rejected_evidence)],
                 case, case / "mismatch.log", env=env, expected=2)
    assert f"PKO200 contract requires exporter {wrong_version}, but this tool is {version}." in output, output
    assert not rejected_document.exists() and not rejected_evidence.exists()
    print(f"Packed exporter {version}: matching export/evidence and mismatching rejection passed.")
    return {"version": version, "sha256": hashlib.sha256(package.read_bytes()).hexdigest(),
            "matchingExit": 0, "mismatchingExit": 2}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packages", type=Path, help="Also test the exact release tool nupkg before publication.")
    args = parser.parse_args()
    artifacts = ROOT / "artifacts/openapi-exporter"
    artifacts.mkdir(parents=True, exist_ok=True)
    # Preserve actual subprocess failures and successful exports for investigation.
    work = Path(tempfile.mkdtemp(prefix="validation-", dir=artifacts))
    print(f"Packed exporter logs and evidence: {work}", flush=True)
    closure = create_feature(work)
    release_version = ElementTree.parse(ROOT / f'src/{TOOL_ID}/{TOOL_ID}.csproj').findtext('.//ExporterVersion') or (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    results = []
    if args.packages:
        results.append(check_tool(args.packages.resolve() / f"{TOOL_ID}.{release_version}.nupkg", work, closure))
    else:
        feed = work / "release-feed"
        run(["dotnet", "pack", str(ROOT / f"src/{TOOL_ID}/{TOOL_ID}.csproj"), "-c", "Release",
             "--no-restore", "--output", str(feed)], ROOT, work / "release-pack.log")
        results.append(check_tool(feed / f"{TOOL_ID}.{release_version}.nupkg", work, closure))
    prerelease = release_version + "-preview.1"
    feed = work / "prerelease-feed"
    # Override PackageVersion alone, and deliberately separate InformationalVersion.
    # This catches deriving identity from Version, assembly version, or source revision.
    run(["dotnet", "pack", str(ROOT / f"src/{TOOL_ID}/{TOOL_ID}.csproj"), "-c", "Release",
         "--no-restore", "--output", str(feed), f"-p:PackageVersion={prerelease}",
         "-p:InformationalVersion=9.8.7+version-probe"], ROOT, work / "prerelease-pack.log")
    results.append(check_tool(feed / f"{TOOL_ID}.{prerelease}.nupkg", work, closure))
    # Restore the default build identity for subsequent --no-build release packing.
    run(["dotnet", "build", str(ROOT / f"src/{TOOL_ID}/{TOOL_ID}.csproj"), "-c", "Release",
         "--no-restore"], ROOT, work / "restore-release-build.log")
    write_json(work / "results.json", {"packages": results})
    print("Packed OpenAPI exporter version integration checks passed.")


if __name__ == "__main__":
    main()
