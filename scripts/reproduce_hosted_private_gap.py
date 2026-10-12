"""Preserve an executable reproduction against exact unmodified main source in an isolated snapshot."""
import argparse
import io
import json
import os
from pathlib import Path
import subprocess
import tarfile
import tempfile

BASELINE = "8e3576d0b8dc25b820e56f4013d92137e410a440"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=Path("artifacts/authenticated-hosted-pages/reproduction"))
    args = parser.parse_args()
    repository = Path(__file__).resolve().parents[1]
    output = (repository / args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    snapshot = Path(tempfile.mkdtemp(prefix="baseline-", dir=output))
    archive = subprocess.check_output(["git", "archive", "--format=tar", BASELINE], cwd=repository)
    with tarfile.open(fileobj=io.BytesIO(archive)) as files:
        files.extractall(snapshot, filter="data")
    probe = snapshot / "tests/dotnet/Orbyss.Foundation.Web.HostedPages.Probe"
    project = probe / "Orbyss.Foundation.Web.HostedPages.Probe.csproj"
    project.write_text(project.read_text().replace("</ItemGroup></Project>", """
<ProjectReference Include="../../../src/Orbyss.Foundation.Authentication.BffCookie/Orbyss.Foundation.Authentication.BffCookie.csproj" />
<ProjectReference Include="../../../src/Orbyss.Foundation.Web.OpenApi/Orbyss.Foundation.Web.OpenApi.csproj" />
</ItemGroup></Project>"""), encoding="utf-8")
    program = probe / "Program.cs"
    code = program.read_text()
    code = code.replace('configuration[$"{key}:Configuration:Foundation:HostedPages:CurrentRevision"] = "release-1";', '''
        configuration[$"{key}:Configuration:Foundation:HostedPages:CurrentRevision"] = "release-1";
        configuration[$"{key}:Configuration:Foundation:HostedPages:PagePath"] = "/private";
        configuration[$"{key}:Features:Orbyss.Foundation.Authentication.BffCookie"] = "true";
        var web = $"{key}:Configuration:Foundation:Web";
        configuration[$"{web}:Authority"] = "https://identity.example/" + shell;
        configuration[$"{web}:ClientId"] = "synthetic-client";
        configuration[$"{web}:ClientSecret"] = "synthetic-secret";
        configuration[$"{web}:Audience"] = "synthetic-api";
        configuration[$"{web}:ResponsePolicies:FeaturePolicies:Orbyss.Foundation.Web.HostedPages"] = "private";
''')
    code = code.replace('await client.GetAsync("/a/")', 'await client.GetAsync("/a/private")')
    marker = '    Require(page.StatusCode == HttpStatusCode.OK, "page failed");'
    start = code.index(marker)
    end = code.index('\n}\nfinally', start)
    code = code[:start] + '''
    Require(page.StatusCode == HttpStatusCode.OK, "Anonymous exposure was not reproduced");
    var bootstrap = WebUtility.HtmlDecode(Regex.Match(html, "data-bootstrap=\\\"([^\\\"]+)\\\"").Groups[1].Value);
    using var exposed = await client.GetAsync(bootstrap);
    Require(exposed.StatusCode == HttpStatusCode.OK, "Anonymous bootstrap exposure was not reproduced");
    Require(typeof(HostedPageOptions).GetProperty("Routes") is null, "Baseline is not the unsupported contract");
    Console.WriteLine("REPRODUCED: selected Foundation BFF + private feature response policy; anonymous HTML=200, bootstrap=200. Response policy does not establish authorization; no private route access option exists.");
    await app.StopAsync();
''' + code[end:]
    program.write_text(code, encoding="utf-8")
    environment = os.environ.copy()
    environment.update(MSBUILDDISABLENODEREUSE="1", DOTNET_CLI_USE_MSBUILD_SERVER="0")
    commands = [
        ["dotnet", "restore", str(project), "--configfile", str(snapshot / "NuGet.config"), "-m:1", "-nr:false"],
        ["dotnet", "restore", str(project), "--locked-mode", "--configfile", str(snapshot / "NuGet.config"), "-m:1", "-nr:false"],
        ["dotnet", "build", str(project), "-c", "Release", "--no-restore", "--disable-build-servers", "-m:1"],
        ["dotnet", str(probe / "bin/Release/net10.0/Orbyss.Foundation.Web.HostedPages.Probe.dll")]
    ]
    for index, command in enumerate(commands):
        with (snapshot / f"command-{index}.log").open("w", encoding="utf-8") as log:
            subprocess.run(command, cwd=snapshot, env=environment, stdout=log, stderr=subprocess.STDOUT, check=True, timeout=180)
    (snapshot / "inputs.json").write_text(json.dumps({"sourceCommit": BASELINE, "version": "0.3.2", "selection": ["HostedPages", "BffCookie", "private response policy"], "scope": "synthetic Foundation shell HTTP; no consumer modified"}, indent=2) + "\n")
    print((snapshot / "command-3.log").read_text())
    print("Preserved baseline source, commands and HTTP evidence: " + str(snapshot))


if __name__ == "__main__":
    main()
