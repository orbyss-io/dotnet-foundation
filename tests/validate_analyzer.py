from __future__ import annotations

import subprocess
import json
import tempfile
import zipfile
from pathlib import Path


def run(root: Path, arguments: list[str]) -> str:
    result = subprocess.run(arguments, cwd=root, capture_output=True, text=True)
    output = result.stdout + result.stderr
    if result.returncode:
        raise AssertionError(f"Command failed: {arguments}\n{output}")
    return output


def validate_readability(root: Path) -> None:
    """Prove semantic controls and actual packaged opt-in wiring independently."""
    probe = root / "tests/analyzer-readability-probe/AnalyzerReadabilityProbe.csproj"
    run(root, ["dotnet", "restore", str(probe), "--locked-mode", "--nologo"])
    print(run(root, ["dotnet", "run", "--project", str(probe), "-c", "Release", "--no-restore"]).strip())
    with tempfile.TemporaryDirectory(prefix="foundation-readability-") as directory:
        temporary = Path(directory)
        feed = temporary / "feed"
        feed.mkdir()
        analyzer = root / "src/Orbyss.Foundation.Analyzers/Orbyss.Foundation.Analyzers.csproj"
        run(root, ["dotnet", "pack", str(analyzer), "-c", "Release", "--no-build", "--no-restore", "-o", str(feed), "--nologo"])
        packages = list(feed.glob("*.nupkg"))
        if len(packages) != 1:
            raise AssertionError(f"Expected one local analyzer package, found {packages}")
        with zipfile.ZipFile(packages[0]) as archive:
            marker = json.loads(archive.read("buildTransitive/source-readability-contract.json"))
            if marker["contractId"] != "source-readability-v1" or marker["requiredDiagnosticIds"] != ["ORB1007", "ORB1008", "ORB1009", "ORB1010"]:
                raise AssertionError(f"Unexpected analyzer capability marker: {marker}")
            # A local candidate package is tested at its existing source version, never published.
            import xml.etree.ElementTree as ET
            metadata = ET.fromstring(archive.read("Orbyss.Foundation.Analyzers.nuspec"))
            version = next(element.text for element in metadata.iter() if element.tag.endswith("}version"))
        (temporary / "NuGet.Config").write_text(
            f'<configuration><packageSources><clear/><add key="local" value="{feed.as_posix()}"/></packageSources></configuration>',
            encoding="utf-8",
        )
        project = temporary / "Probe.csproj"
        # Isolate the package cache: an installed public version with the same number cannot replace this source candidate.
        project.write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework>'
            '<FoundationSourceReadability>true</FoundationSourceReadability>'
            '<GenerateDocumentationFile>true</GenerateDocumentationFile>'
            f'<RestorePackagesPath>{(temporary / "packages").as_posix()}</RestorePackagesPath>'
            '</PropertyGroup><ItemGroup>'
            f'<PackageReference Include="Orbyss.Foundation.Analyzers" Version="{version}" PrivateAssets="all"/>'
            '</ItemGroup><Target Name="CheckReadabilityCapability" BeforeTargets="CoreCompile">'
            '<Error Condition="\'$(FoundationSourceReadabilityContract)\' != \'source-readability-v1\'" Text="Missing actual package capability"/>'
            '</Target></Project>', encoding="utf-8",
        )
        source = temporary / "Sample.cs"
        source.write_text('''/// <summary>Native package wiring control.</summary>
class Sample {
    /// <summary>Intentionally invalid call composition.</summary>
    void Run() { this.Next().Next(); Take(Next(), this, this); }
    /// <summary>Intentionally misplaced state.</summary>
    int value;
    /// <summary>Returns this control.</summary>
    Sample Next() => this;
    /// <summary>Accepts a prepared sequence.</summary>
    void Take(params object[] values) { }
}
''', encoding="utf-8")
        run(temporary, ["dotnet", "restore", str(project), "--nologo"])
        rejected = subprocess.run(["dotnet", "build", str(project), "--no-restore", "--nologo"], cwd=temporary, capture_output=True, text=True)
        output = rejected.stdout + rejected.stderr
        if rejected.returncode == 0 or any(rule not in output for rule in marker["requiredDiagnosticIds"]):
            raise AssertionError(f"The packaged native build missed mandatory diagnostics.\n{output}")
        if "error CS" in output or "AD0001" in output:
            raise AssertionError(f"Compiler/analyzer execution failure cannot count as the intended rejection.\n{output}")
        bad_source = source.read_text(encoding="utf-8")
        editorconfig = temporary / ".editorconfig"
        editorconfig.write_text("root = true\n[*.cs]\n" + "".join(
            f"dotnet_diagnostic.{identity}.severity = none\n" for identity in marker["requiredDiagnosticIds"]), encoding="utf-8")
        for mode, arguments in (("editorconfig", []), ("nowarn", ["-p:NoWarn=ORB1007%3BORB1008%3BORB1009%3BORB1010"]), ("pragma", [])):
            if mode != "editorconfig":
                editorconfig.write_text("root = true\n", encoding="utf-8")
            source.write_text(("#pragma warning disable ORB1007, ORB1008, ORB1009, ORB1010\n" if mode == "pragma" else "") + bad_source, encoding="utf-8")
            suppression = subprocess.run(["dotnet", "build", str(project), "--no-restore", "--nologo", *arguments], cwd=temporary, capture_output=True, text=True)
            suppressed_output = suppression.stdout + suppression.stderr
            if suppression.returncode == 0 or any(identity not in suppressed_output for identity in marker["requiredDiagnosticIds"]) or "error CS" in suppressed_output or "AD0001" in suppressed_output:
                raise AssertionError(f"The selected mandatory contract was waived through {mode}, or failed before its intended diagnostics.\n{suppressed_output}")
        source.write_text('''/// <summary>Native conformance control.</summary>
class Sample {
    /// <summary>Prepared authored state.</summary>
    int value;
    /// <summary>Runs a correctly laid out chain with prepared arguments.</summary>
    void Run()
    {
        this
            .Next()
            .Next();
        var prepared = Next();
        Take(
            prepared,
            this,
            this
        );
    }
    /// <summary>Returns this control.</summary>
    Sample Next() => this;
    /// <summary>Accepts a prepared sequence.</summary>
    void Take(params object[] values) { }
}
''', encoding="utf-8")
        run(temporary, ["dotnet", "build", str(project), "--no-restore", "--nologo"])
        print("Packaged source-readability opt-in rejected authored violations and accepted the repaired source.")


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    project = root / "tests/analyzer-probe/AnalyzerProbe.csproj"
    restore = subprocess.run(
        ["dotnet", "restore", str(project), "--locked-mode", "--nologo"],
        cwd=root,
        capture_output=True,
        text=True,
    )
    if restore.returncode != 0:
        raise AssertionError(f"The analyzer probe did not restore in locked mode.\n{restore.stdout}{restore.stderr}")

    result = subprocess.run(
        ["dotnet", "build", str(project), "--no-restore", "--nologo"],
        cwd=root,
        capture_output=True,
        text=True,
    )
    output = result.stdout + result.stderr
    if result.returncode == 0:
        raise AssertionError("The invalid analyzer probe unexpectedly compiled.")
    for rule_id in ("ORB1003", "ORB1004", "ORB1005"):
        if rule_id not in output:
            raise AssertionError(f"The analyzer probe did not report {rule_id}.\n{output}")

    feature_project = root / "tests/analyzer-feature-probe/AnalyzerFeatureProbe.csproj"
    feature = subprocess.run(
        ["dotnet", "build", str(feature_project), "--nologo"],
        cwd=root,
        capture_output=True,
        text=True,
    )
    feature_output = feature.stdout + feature.stderr
    if feature.returncode == 0 or "ORB1006" not in feature_output:
        raise AssertionError(
            "The analyzer did not reject a dotted FoundationFeatureIdentity whose CLR "
            f"[ShellFeature] name diverges.\n{feature_output}"
        )

    validate_readability(root)
    print("Orbyss Foundation C# structure, documentation, feature-identity and source-readability analyzers passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
