"""Dependency-update workflow: discover upstreams, upgrade active pins, and scan images.

Normal Spec Kit commands do not call this tool. Historical profiles are immutable.
This standard-library tool is independently maintained in each owning repository.
"""
from __future__ import annotations

import argparse
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tarfile
import tempfile
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8", newline="\n")


def safe_path(root, relative):
    path = (root / relative).resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError("Maintenance path escapes repository")
    return path


def inventory(root, policy):
    files = set()
    for pattern in policy["inputs"]:
        matches = [p for p in root.glob(pattern) if p.is_file()]
        if not matches:
            raise ValueError("Maintenance input pattern has no files: " + pattern)
        files.update(matches)
    profile_registry = policy.get('profileRegistry')
    if profile_registry:
        registry = safe_path(root, profile_registry)
        index = read_json(registry / 'index.json')
        entry = index['profiles'][index['default']]
        files.update([registry / entry['path'], registry / entry['evidence']['path']])
    pins = {}

    def add(kind, name, version, path, channel="stable"):
        if not isinstance(version, str) or not version:
            raise ValueError("Missing exact dependency pin: " + name)
        key = kind + ":" + name + "@" + version
        item = pins.setdefault(key, {"id": key, "kind": kind, "name": name,
                                    "current": version, "channel": channel, "paths": []})
        relative = path.relative_to(root).as_posix()
        if relative not in item["paths"]:
            item["paths"].append(relative)

    for path in sorted(files):
        if path.relative_to(root).as_posix() in policy.get('immutableInputs',[]):
            continue
        text = path.read_text(encoding="utf-8")
        if path.name == "global.json":
            add("dotnet-sdk", "dotnet-sdk", json.loads(text)["sdk"]["version"], path)
        elif path.name == "package.json":
            value = json.loads(text)
            for section in ("dependencies", "devDependencies"):
                for name, version in value.get(section, {}).items():
                    add("npm", name, version, path)
            for name, version in value.get("engines", {}).items():
                if name in {"node", "npm"}:
                    add(name, name, version, path)
        elif path.name in {".nvmrc", ".npm-version", ".oasdiff-version", ".python-version"}:
            kind = {".nvmrc": "node", ".npm-version": "npm", ".oasdiff-version": "github", ".python-version":"python"}[path.name]
            add(kind, "oasdiff/oasdiff" if kind == "github" else kind, text.strip().removeprefix("v"), path)
        elif path.suffix in {".props", ".csproj"}:
            for element in ET.fromstring(text).iter():
                if element.tag in {"PackageVersion", "PackageReference"} and element.get("Version"):
                    name, version = element.get("Include"), element.get("Version")
                    if "$" not in version:
                        add("nuget", name, version, path, "preview" if "-" in version else "stable")
        elif path.name == "dotnet-tools.json":
            for name, tool in json.loads(text)["tools"].items():
                add("nuget", name, tool["version"], path)
        elif path.name == "json-schema-requirements.txt":
            for name, version in re.findall(r"^([\w.-]+)==([^\s]+)$", text, re.M):
                add("pypi", name, version, path)
        for image in re.findall(r"([a-zA-Z0-9][a-zA-Z0-9./:_-]*@sha256:[a-f0-9]{64})", text):
            add("image", image.split("@")[0], image.split("@")[1], path)
        for action, sha in re.findall(r"uses:\s*([\w.-]+/[\w.-]+)(?:/[^@\s]+)?@([a-f0-9]{40})", text):
            add("github-action", action, sha, path)
        for version in re.findall(r"specify-cli==([\d.]+)", text):
            add("pypi", "specify-cli", version, path)
        for version in re.findall(r'node-version:\s*[\"\']?(\d+(?:\.\d+){0,2})', text):
            add('node','node',version,path)
        for version in re.findall(r'npm install --global npm@(\d+(?:\.\d+)+)', text):
            add('npm','npm',version,path)
        for version in re.findall(r'python-version:\s*[\"\']?(\d+(?:\.\d+){0,2})', text):
            add('python','python',version,path)
        for version in re.findall(r"trivy-version:\s*[\"']?v?([\d.]+)", text):
            add("github", "aquasecurity/trivy", version, path)
        for version in re.findall(r"uses:\s*aquasecurity/setup-trivy@[^\n]+\s*with:\s*version:\s*v?([\d.]+)", text):
            add("github", "aquasecurity/trivy", version, path)
    if profile_registry:
        profile_path = registry / entry['path']
        for family, details in read_json(profile_path)['families'].items():
            add('nuget', policy['families'][family], details['releaseVersion'], profile_path)
            for name, version in details.get('toolVersions', {}).items():
                add('nuget', name, version, profile_path)
    for extra in policy.get("additionalPins", []):
        path = safe_path(root, extra["path"])
        if path not in files:
            raise ValueError("Additional pin must be a hashed input")
        value = read_json(path)
        for key in extra["pointer"].strip("/").split("/"):
            value = value[key]
        if extra["kind"] == "profile":
            for family, details in value.items():
                name = policy["families"][family]
                add("nuget", name, details["releaseVersion"], path)
                for name, version in details.get("toolVersions", {}).items():
                    add("nuget", name, version, path)
        else:
            add(extra["kind"], extra["name"], value, path)
    return {"inputs": {p.relative_to(root).as_posix(): digest(p) for p in sorted(files)},
            "pins": sorted(pins.values(), key=lambda p: p["id"])}


def fetch(url):
    headers = {"User-Agent": "dependency-maintenance", "Accept": "application/json"}
    # Never send a GitHub token to another host or through redirects.
    if urllib.parse.urlparse(url).hostname == "api.github.com" and os.environ.get("GH_TOKEN"):
        headers["Authorization"] = "Bearer " + os.environ["GH_TOKEN"]
    request = urllib.request.Request(url, headers=headers)
    class NoRedirect(urllib.request.HTTPRedirectHandler):
        def redirect_request(self, *args, **kwargs):
            raise ValueError("Metadata redirects require a separately reviewed canonical source")
    with urllib.request.build_opener(NoRedirect).open(request, timeout=30) as response:
        if urllib.parse.urlparse(response.url).hostname != urllib.parse.urlparse(url).hostname:
            raise ValueError("Unexpected metadata redirect")
        data=response.read(32*1024*1024+1)
        if len(data)>32*1024*1024: raise ValueError('Publisher metadata exceeds bounded input size')
        return json.loads(data)


def version_key(value):
    numbers = re.match(r"^v?(\d+(?:\.\d+)*)", value)
    if not numbers:
        return ()
    return tuple(int(n) for n in numbers.group(1).split("."))


def nuget_version_key(value):
    main,_,suffix=value.partition('-')
    prerelease=tuple((1,int(part)) if part.isdigit() else (0,part.casefold())
                    for part in re.split(r'[.-]',suffix))
    return (version_key(main),not bool(suffix),prerelease)


def observe(pin, overrides):
    kind, name = pin["kind"], pin["name"]
    url = overrides.get(name)
    if kind == "image":
        base=name.rsplit(':',1)[0] if ':' in name else name
        selected=overrides.get('image:'+base,{})
        image_name=name
        release=None
        if selected.get('github'):
            release=observe({'kind':'github','name':selected['github'],'channel':'stable'}, {})['latest']
            image_name=base+':'+release
        elif selected.get('tag'): image_name=base+':'+selected['tag']
        output = subprocess.check_output(["docker", "buildx", "imagetools", "inspect", image_name], text=True, timeout=90)
        match = re.search(r"^Digest:\s*(sha256:[a-f0-9]{64})$", output, re.M)
        if not match:
            raise ValueError("Registry returned no manifest digest")
        result={"latest": match.group(1), "source": "registry:" + image_name,
                'imageName':base if selected.get('omitTag') else image_name}
        if release: result['imageVersion']=release
        if selected.get('versionEnvironment'):
            immutable=base+'@'+match.group(1)
            subprocess.run(['docker','pull',immutable],check=True,capture_output=True,text=True,timeout=300)
            configuration=json.loads(subprocess.check_output(['docker','image','inspect','--format','{{json .Config.Env}}',immutable],text=True,timeout=30))
            versions=[item.split('=',1)[1] for item in configuration if item.startswith(selected['versionEnvironment']+'=')]
            if len(versions)!=1: raise ValueError('Native image version is missing or ambiguous')
            result['imageVersion']=versions[0].split('-',1)[0]
        return result
    if kind == "nuget":
        url = url or "https://api.nuget.org/v3-flatcontainer/" + name.lower() + "/index.json"
        versions = fetch(url)["versions"]
        versions = [v for v in versions if pin["channel"] == "preview" or "-" not in v]
        latest = max(versions, key=nuget_version_key)
    elif kind == 'python':
        url = 'https://raw.githubusercontent.com/actions/python-versions/main/versions-manifest.json'
        versions = [v['version'] for v in fetch(url) if v.get('stable') and re.fullmatch(r'\d+\.\d+\.\d+',v['version'])]
        latest = max(versions,key=version_key)
    elif kind == 'node':
        url = 'https://nodejs.org/dist/index.json'
        versions = [v for v in fetch(url) if re.fullmatch(r'v\d+\.\d+\.\d+', v['version'])]
        latest = max(versions, key=lambda v: version_key(v['version']))['version'].removeprefix('v')
    elif kind == "npm":
        url = url or "https://registry.npmjs.org/" + urllib.parse.quote(name, safe="")
        latest = fetch(url)["dist-tags"]["latest"]
    elif kind == "pypi":
        url = url or "https://pypi.org/pypi/" + name + "/json"
        latest = fetch(url)["info"]["version"]
    elif kind == "dotnet-sdk":
        # Report both the supported selected major and the newest upstream channel.
        url = "https://builds.dotnet.microsoft.com/dotnet/release-metadata/" + pin["current"].split(".")[0] + ".0/releases.json"
        selected = fetch(url)
        index_url = "https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json"
        channels = fetch(index_url)["releases-index"]
        supported = [c for c in channels if c.get("support-phase") in {"active", "maintenance"}]
        newest = max(supported, key=lambda c: version_key(c["channel-version"]))
        return {"latest": newest["latest-sdk"], "source": url,
                "newestSupportedSdk": newest["latest-sdk"], "channelSource": index_url}
    else:
        url = url or "https://api.github.com/repos/" + name + "/releases/latest"
        release = fetch(url)
        latest = release["tag_name"] if kind == "github-action" else release["tag_name"].removeprefix("v")
        if kind == "github-action":
            reference = fetch("https://api.github.com/repos/" + name + "/git/ref/tags/" + urllib.parse.quote(latest, safe=""))["object"]
            while reference["type"] == "tag":
                reference = fetch(reference["url"])["object"]
            if reference["type"] != "commit":
                raise ValueError("Action tag does not resolve to a commit")
            return {"latest": reference["sha"], "release": latest, "source": url}
    return {"latest": latest, "source": url}



def collect(root, policy, observer=observe):
    value = inventory(root, policy)
    def query(pin):
        try:
            return {**pin, **observer(pin, policy.get("metadataOverrides", {}))}
        except Exception as error:
            return {**pin, "latest": None, "source": "unavailable", "error": type(error).__name__}
    with ThreadPoolExecutor(max_workers=6) as executor:
        value["pins"] = list(executor.map(query, value["pins"]))
    return {"schemaVersion": 1, "observedAt": datetime.now(timezone.utc).isoformat(), **value}


def upgrade(root, policy, observations):
    """Upgrade active source pins; historical profiles are regenerated by the profile updater."""
    before = {path: safe_path(root, path).read_bytes() for path in observations["inputs"]}
    if any(hashlib.sha256(content).hexdigest()!=observations['inputs'][path] for path,content in before.items()):
        raise ValueError('Maintenance inputs changed during upstream discovery; collect again')
    changes = []
    for pin in observations["pins"]:
        latest = pin.get("latest")
        if not latest:
            raise ValueError("Publisher lookup failed: " + pin["id"] + "; update has not started")
    try:
        for pin in observations["pins"]:
            latest, current, kind, name = pin["latest"], pin["current"], pin["kind"], pin["name"]
            if latest == current:
                continue
            for relative in pin["paths"]:
                if "/dependency-profiles/" in relative or relative in policy.get('immutableInputs',[]):
                    continue  # Never rewrite an immutable accepted profile.
                path = safe_path(root, relative)
                text = path.read_text(encoding="utf-8")
                changed = text
                if path.name == "global.json" and kind == "dotnet-sdk":
                    value = json.loads(text); value["sdk"]["version"] = latest
                    changed = json.dumps(value, indent=2) + "\n"
                elif path.name == "package.json":
                    value = json.loads(text)
                    for section in ("dependencies", "devDependencies", "engines"):
                        if value.get(section, {}).get(name) == current:
                            value[section][name] = latest
                    changed = json.dumps(value, indent=2) + "\n"
                elif path.name == "dotnet-tools.json" and kind == "nuget":
                    value = json.loads(text)
                    for identity, tool in value["tools"].items():
                        if identity.casefold() == name.casefold():
                            tool["version"] = latest
                    changed = json.dumps(value, indent=2) + "\n"
                elif kind == "nuget" and path.suffix in {".props", ".csproj"}:
                    pattern = r'(<Package(?:Version|Reference)\b[^>]*Include="' + re.escape(name) + r'"[^>]*Version=")' + re.escape(current) + r'(")'
                    changed = re.sub(pattern, lambda match: match[1] + latest + match[2], text)
                elif kind == "pypi":
                    changed = text.replace(name + "==" + current, name + "==" + latest)
                elif kind == "github-action":
                    changed = re.sub(r'(uses:\s*' + re.escape(name) + r'(?:/[^@\s]+)?@)' + current,
                                     lambda match: match[1] + latest, text)
                    if pin.get("release"):
                        changed = re.sub(r'(' + re.escape(latest) + r'\s+#\s*)v?[^\s]+',
                                         lambda match: match[1] + pin["release"], changed)
                elif kind == "image":
                    selected_name=pin.get('imageName',name)
                    changed = text.replace(name + "@" + current, selected_name + "@" + latest)
                    if path.name=='persistence-runtimes.json' and pin.get('imageVersion'):
                        value=json.loads(changed); value['reviewed']=datetime.now(timezone.utc).date().isoformat()
                        for runtime in value['profiles'].values():
                            if runtime.get('image')==selected_name+'@'+latest:
                                old=runtime['version']; runtime['version']=pin['imageVersion']
                                runtime['sources']=[url.replace(current,latest).replace('/release/'+old+'/', '/release/'+runtime['version']+'/') for url in runtime['sources']]
                                runtime['evidence']='Immutable official image configuration observed: '+runtime['version']+'. Native bootstrap/database validation remains required.'
                        changed=json.dumps(value,indent=2)+'\n'
                elif path.name in {".nvmrc", ".npm-version", ".oasdiff-version", ".python-version"}:
                    changed = latest + "\n"
                elif kind == 'node':
                    changed = re.sub(r'(node-version:\s*[\"\']?)'+re.escape(current)+r'(?=[\"\'\s]|$)',lambda match:match[1]+latest,text)
                elif kind == 'npm' and name == 'npm':
                    changed = text.replace('npm install --global npm@'+current,'npm install --global npm@'+latest)
                elif kind == 'python':
                    changed = re.sub(r'(python-version:\s*[\"\']?)'+re.escape(current)+r'(?=[\"\'\s]|$)',lambda match:match[1]+latest,text)
                elif kind == "github" and name == "aquasecurity/trivy":
                    changed = re.sub(r'(version:\s*)v?' + re.escape(current), lambda match: match[1] + "v" + latest, text)
                elif kind == "github" and name == "structurizr/structurizr":
                    value = json.loads(text)
                    selected = value["selected"]
                    for field in ("version", "docker_image", "java_war", "java_war_url"):
                        selected[field] = selected[field].replace(current, latest)
                    image = observe({"kind":"image", "name":selected["docker_image"]}, {})
                    selected["docker_digest"] = image["latest"]
                    value["retrieved"] = datetime.now(timezone.utc).date().isoformat()
                    for source in value.get('sources',[]):
                        source['url']=source['url'].replace('/tag/v'+current,'/tag/v'+latest)
                    value["tested"] = {"status":"pending-update-validation"}
                    changed = json.dumps(value, indent=2) + "\n"
                if changed != text:
                    path.write_text(changed, encoding="utf-8", newline="\n")
                    changes.append({"path":relative, "dependency":pin["id"], "to":latest})
        # An SDK tag and its immutable digest must move together.
        sdk = next((p["latest"] for p in observations["pins"] if p["kind"] == "dotnet-sdk"), None)
        if sdk:
            for relative in before:
                path = safe_path(root, relative)
                if path.name.startswith("Dockerfile"):
                    text = path.read_text(encoding="utf-8")
                    for previous in set(re.findall(r'mcr\.microsoft\.com/dotnet/sdk:[^@\s]+@sha256:[a-f0-9]{64}', text)):
                        image = "mcr.microsoft.com/dotnet/sdk:" + sdk
                        resolved = observe({"kind":"image", "name":image}, {})["latest"]
                        text = text.replace(previous, image + "@" + resolved)
                    path.write_text(text, encoding="utf-8", newline="\n")
        return changes
    except Exception:
        for relative, content in before.items():
            safe_path(root, relative).write_bytes(content)
        raise


def default_host(root, policy):
    registry = safe_path(root, policy["profileRegistry"])
    index = read_json(registry / "index.json")
    entry = index["profiles"][index["default"]]
    proof = registry / entry["evidence"]["path"]
    if digest(proof) != entry["evidence"]["sha256"]:
        raise ValueError("Selected qualification evidence changed")
    image = read_json(proof)["hostRuntime"]["inputs"]["hostImage"]
    if not re.fullmatch(r'[^\s]+@sha256:[0-9a-f]{64}', image):
        raise ValueError("Host scan requires the qualified immutable image")
    return image


def scan(root, image):
    """Both platforms must pass; findings and scanner failures remain reviewable."""
    output = root / "artifacts/dependency-security"
    output.mkdir(parents=True, exist_ok=True)
    results = []
    with tempfile.TemporaryDirectory(prefix='oci-scan-',dir=output) as temporary:
        layout=Path(temporary)
        archived=Path(image).is_file()
        if archived:
            with tarfile.open(image) as archive:
                archive.extractall(layout,filter='data')
            index=read_json(layout/'index.json')
            def leaves(value):
                found=[]
                for descriptor in value['manifests']:
                    blob=layout/'blobs'/descriptor['digest'].replace(':','/')
                    if digest(blob)!=descriptor['digest'].split(':')[1]:
                        raise ValueError('OCI manifest digest mismatch')
                    document=read_json(blob)
                    found.extend(leaves(document) if 'manifests' in document else [descriptor])
                return found
            manifests=leaves(index)
        for platform in ("linux/amd64", "linux/arm64"):
            path = output / (platform.replace("/", "-") + ".json")
            if archived:
                system,architecture=platform.split('/')
                selected=[item for item in manifests if item.get('platform',{}).get('os')==system
                          and item.get('platform',{}).get('architecture')==architecture]
                if len(selected)!=1: raise ValueError('OCI archive must contain exactly one image for '+platform)
                write_json(layout/'index.json',{'schemaVersion':2,'manifests':selected})
            # OCI input uses ':' for a tag; a Windows drive letter is not a tag.
            target = ["--input", layout.relative_to(root).as_posix()] if archived else [image]
            result = subprocess.run(["trivy", "image", "--platform", platform, "--scanners", "vuln",
                "--format", "json", "--output", str(path), "--exit-code", "1", "--severity", "HIGH,CRITICAL",
                "--timeout", "15m", *target], cwd=root, timeout=960, check=False)
            results.append({"image":image, "platform":platform, "exitCode":result.returncode,
                            "manifestDigest":selected[0]['digest'] if archived else None,
                            "report":path.relative_to(root).as_posix(), "sha256":digest(path) if path.exists() else None})
    write_json(output / "result.json", {"observedAt":datetime.now(timezone.utc).isoformat(), "scans":results})
    if any(r["exitCode"] != 0 or r["sha256"] is None for r in results):
        raise ValueError("Image security gate failed; inspect retained findings/tool errors")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["inventory", "collect", "upgrade", "scan", "host-image"])
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--report", default="artifacts/dependency-update.json")
    parser.add_argument("--image")
    args = parser.parse_args()
    root = args.root.resolve()
    policy = read_json(root / "maintenance-policy.json")
    if args.command == "host-image":
        print(default_host(root, policy))
    elif args.command == "scan":
        if not args.image:
            parser.error("scan requires --image")
        scan(root, args.image)
    elif args.command == "inventory":
        print(json.dumps(inventory(root, policy), indent=2))
    else:
        path = safe_path(root, args.report)
        if path.exists():
            raise ValueError("Preserve prior observations; choose a fresh --report")
        observations = collect(root, policy)
        write_json(path, observations)
        if args.command == "upgrade":
            changes = upgrade(root, policy, observations)
            write_json(path.with_suffix(".changes.json"), {"changes":changes})
            print("Upgraded active pins: " + str(len(changes)))
        else:
            print("Publisher observations: " + str(path))


if __name__ == "__main__":
    main()
