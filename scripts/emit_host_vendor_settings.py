"""Emit bounded, offline metadata for this Host's exact native configuration integration.

Vendor snapshots are metadata inputs only. Defaults/types come from the shared Roslyn
reader; no vendor assembly, settings constructor, service or application is executed.
The Host owns binding/selection semantics; origins retain each native source authority.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import subprocess
import tempfile
import shutil
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
HOST = ROOT / "src/Orbyss.Foundation.Host"
RESOURCES = HOST / "metadata/vendor-settings"
IMAGE_VENDOR = ".orbyss-foundation/settings-sources/vendor/"
MAX_JSON = 2_097_152
MAX_ARCHIVE = 134_217_728
MAX_ASSEMBLY = 67_108_864
EXPECTED_IDS = frozenset(("CShells", "CShells.Abstractions", "CShells.AspNetCore", "CShells.AspNetCore.Abstractions",
                         "Nuplane", "Nuplane.Abstractions", "Nuplane.Loading", "Nuplane.Loading.Abstractions", "Nuplane.Sources.Directory"))


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def contained(root: Path, relative: str) -> Path:
    path = PurePosixPath(relative)
    require(bool(relative) and not path.is_absolute() and all(x not in ("", ".", "..") for x in path.parts)
            and "\\" not in relative and ":" not in relative, "Metadata path must be relative and contained: " + relative)
    resolved = (root / Path(*path.parts)).resolve()
    require(resolved.is_relative_to(root.resolve()), "Metadata path escapes its root.")
    return resolved


def read_json(path: Path, limit: int = MAX_JSON) -> dict:
    require(path.is_file() and 0 < path.stat().st_size <= limit, "Missing or excessive JSON input: " + str(path))
    value = json.loads(path.read_bytes().decode("utf-8-sig"))
    require(isinstance(value, dict), "JSON input must be an object: " + str(path))
    return value


def normalized(path: Path) -> bytes:
    require(path.is_file() and path.stat().st_size <= 1_048_576, "Source input is missing or exceeds 1 MiB.")
    return path.read_bytes().decode("utf-8-sig").replace("\r\n", "\n").replace("\r", "\n").encode("utf-8")


def digest(path: Path, limit: int = MAX_ASSEMBLY) -> str:
    require(path.is_file() and 0 < path.stat().st_size <= limit, "Binary input is missing or exceeds its finite limit: " + str(path))
    result = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(65_536), b""):
            result.update(block)
    return result.hexdigest()


def stream_digest(stream, limit: int) -> str:
    result = hashlib.sha256()
    count = 0
    for block in iter(lambda: stream.read(65_536), b""):
        count += len(block)
        require(count <= limit, "Native archive member exceeds its finite limit.")
        result.update(block)
    return result.hexdigest()


def write_json(path: Path, value: dict) -> None:
    """Admit a finite encoded output before atomically replacing the prior metadata."""
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    created = False
    try:
        with temporary.open("xb") as stream:
            created = True
            count = 0
            for part in json.JSONEncoder(ensure_ascii=True, sort_keys=True, separators=(",", ":")).iterencode(value):
                data = part.encode("utf-8")
                count += len(data)
                require(count < MAX_JSON, "Host settings metadata exceeds 2 MiB.")
                stream.write(data)
            stream.write(b"\n")
        os.replace(temporary, path)
    finally:
        if created and temporary.exists():
            temporary.unlink()


def package_inputs(resources: Path, assets: Path, host_output: Path) -> tuple[list[dict], list[Path]]:
    manifest = read_json(resources / "inputs.json")
    require(set(manifest) == {"schemaVersion", "origins"} and manifest["schemaVersion"] == 1,
            "Unsupported native source inventory.")
    origins = manifest["origins"]
    require(isinstance(origins, list) and len(origins) == 9
            and {x["packageId"] for x in origins} == EXPECTED_IDS, "Native origins must contain exactly nine unique selected packages.")
    restore = read_json(assets, 16_777_216)
    folders = [Path(x).resolve() for x in restore["packageFolders"]]
    libraries = restore["libraries"]
    result, sources = [], []
    image_names = {}
    for file in host_output.glob("*.dll"):
        key = file.name.casefold()
        require(key not in image_names, "Host has duplicate case-varied assembly filenames.")
        image_names[key] = file
    for original in origins:
        item = copy.deepcopy(original)
        require(set(item) == {"packageId", "packageVersion", "archive", "assembly", "source"}, "Unsupported native origin fields.")
        identity, version = item["packageId"], item["packageVersion"]
        require(f"{identity}/{version}" in libraries and libraries[f"{identity}/{version}"]["type"] == "package",
                "Native origin differs from actual restore: " + identity)
        library = libraries[f"{identity}/{version}"]
        name = f"{identity.lower()}.{version.lower()}.nupkg"
        require(item["archive"]["name"] == name and set(item["archive"]) == {"name", "sha256"}, "Native archive identity differs.")
        candidates = [contained(folder, library["path"] + "/" + name) for folder in folders]
        archives = [file for file in candidates if file.is_file()]
        require(len(archives) == 1, "Native archive requires one actual restored input: " + identity)
        archive = archives[0]
        require(digest(archive, MAX_ARCHIVE) == item["archive"]["sha256"], "Native archive hash differs: " + identity)
        with zipfile.ZipFile(archive) as package:
            names = package.namelist()
            require(len(names) <= 4096 and len(names) == len(set(names)), "Native archive entries are duplicated or excessive.")
            specifications = [file for file in names if file.lower().endswith(".nuspec")]
            require(len(specifications) == 1, "Native archive must have one nuspec.")
            member = package.getinfo(specifications[0])
            require(member.file_size <= 262_144, "Native nuspec exceeds 256 KiB.")
            metadata = ET.fromstring(package.read(member))
            def field(local):
                values = [x for x in metadata.iter() if x.tag.split("}")[-1] == local]
                require(len(values) == 1, "Native nuspec identity is ambiguous: " + local)
                return values[0]
            require(field("id").text == identity and field("version").text == version, "Native nuspec ID/version differs.")
            repo = field("repository")
            require(repo.attrib.get("url") == item["source"]["repository"]
                    and repo.attrib.get("commit") == item["source"]["commit"], "Native nuspec source provenance differs.")
            assembly_name = identity + ".dll"
            require(item["assembly"]["name"] == assembly_name and set(item["assembly"]) == {"name", "sha256"},
                    "Native assembly identity differs.")
            binary = "lib/net10.0/" + assembly_name
            require(binary in names and package.getinfo(binary).file_size <= MAX_ASSEMBLY,
                    "Native package lacks its selected net10 assembly.")
            with package.open(binary) as stream:
                require(stream_digest(stream, MAX_ASSEMBLY) == item["assembly"]["sha256"], "Native archive assembly hash differs.")
            selected = image_names.get(assembly_name.casefold())
            require(selected is not None and selected.name == assembly_name
                    and digest(selected) == item["assembly"]["sha256"], "Native assembly differs from actual Host output: " + identity)
        require(set(item["source"]) == {"repository", "commit", "files"}
                and 0 < len(item["source"]["files"]) <= 128, "Native source inventory is incomplete/excessive.")
        image_files = {}
        for relative, expected in sorted(item["source"]["files"].items()):
            require(relative.startswith(identity + "/") and relative.endswith(".txt"), "Source does not belong to its native package.")
            source = contained(resources / "sources", relative)
            require(hashlib.sha256(normalized(source)).hexdigest() == expected, "Native source snapshot differs: " + relative)
            sources.append(source)
            image_files[IMAGE_VENDOR + relative] = expected
        context = ET.fromstring(normalized(contained(resources / "sources", identity + "/Directory.Build.props.txt")))
        values = {element.tag: element.text for element in context.iter()}
        require(values.get("ImplicitUsings") == "enable" and values.get("Nullable") == "enable",
                "Native source inspection requires the exact captured SDK implicit/nullable compilation context.")
        item["source"]["files"] = image_files
        result.append(item)
    require(len(sources) <= 128 and len(set(sources)) == len(sources)
            and sum(x.stat().st_size for x in sources) <= 16_777_216, "Source snapshot set exceeds its finite bound.")
    require({x.resolve() for x in (resources / "sources").rglob("*.txt")} == set(sources),
            "Metadata source snapshots must exactly match the declared inventory.")
    return sorted(result, key=lambda x: x["packageId"]), sources


def sdk_context(dotnet: Path) -> tuple[list[Path], list[str], dict[str, Path]]:
    """Read the selected SDK's native import/targeting-pack declarations, not a table."""
    version = read_json(ROOT / "global.json")["sdk"]["version"]
    directory = dotnet.parent / "sdk" / version
    using_source = directory / "Sdks/Microsoft.NET.Sdk/targets/Microsoft.NET.Sdk.CSharp.props"
    versions_source = directory / "Microsoft.NETCoreSdk.BundledVersions.props"
    using_tree = ET.fromstring(normalized(using_source))
    namespaces = [x.attrib["Include"] for x in using_tree.iter() if x.tag.split("}")[-1] == "Using"]
    require(0 < len(namespaces) <= 32 and len(namespaces) == len(set(namespaces)), "Selected native SDK import context is ambiguous/excessive.")
    versions = ET.fromstring(normalized(versions_source))
    paths = []
    for pack in ("Microsoft.NETCore.App.Ref", "Microsoft.AspNetCore.App.Ref"):
        selected = [x for x in versions.iter() if x.tag.split("}")[-1] == "KnownFrameworkReference"
                    and x.attrib.get("TargetFramework") == "net10.0" and x.attrib.get("TargetingPackName") == pack]
        require(len(selected) == 1, "Selected SDK framework reference identity is ambiguous: " + pack)
        directory = dotnet.parent / "packs" / pack / selected[0].attrib["TargetingPackVersion"] / "ref/net10.0"
        require(directory.is_dir(), "Selected SDK reference pack is unavailable: " + pack)
        paths.extend(sorted(directory.glob("*.dll")))
    return paths, namespaces, {"compiler/global.json": ROOT / "global.json",
        "compiler/Microsoft.NET.Sdk.CSharp.props": using_source,
        "compiler/Microsoft.NETCoreSdk.BundledVersions.props": versions_source}


def inspect_graph(dotnet: Path, task_assembly: Path, sources: list[Path], references: list[Path], types: list[str], namespaces: list[str], work: Path) -> dict:
    require(task_assembly.is_file(), "The shared static Build graph task must be built before Host metadata emission.")
    require(len(types) <= 64 and len(types) == len(set(types)), "Graph type selection is ambiguous or excessive.")
    target = ET.Element("Project")
    ET.SubElement(target, "UsingTask", TaskName="Orbyss.Foundation.Build.InspectSettingsSourceGraph", AssemblyFile=str(task_assembly))
    items = ET.SubElement(target, "ItemGroup")
    for kind, values in (("Source", sources), ("Reference", references), ("SelectedType", types)):
        for value in values:
            ET.SubElement(items, kind, Include=str(value))
    for namespace in namespaces:
        ET.SubElement(items, "NativeUsing", Include=namespace)
    for type_name in ("Nuplane.Feeds.Setup.NuplaneFeedSetupOptions", "Nuplane.Abstractions.FeedDefinition"):
        secret = ET.SubElement(items, "NativeSecret", Include=type_name + ".Credentials")
        ET.SubElement(secret, "TypeName").text = type_name
        ET.SubElement(secret, "Property").text = "Credentials"
    build = ET.SubElement(target, "Target", Name="Inspect")
    ET.SubElement(build, "InspectSettingsSourceGraph", SourceFiles="@(Source)", ReferenceFiles="@(Reference)",
                  TypeNames="@(SelectedType)", GlobalUsings="@(NativeUsing)", NullableContext="enable",
                  SecretProperties="@(NativeSecret)", OutputFile=str(work / "graph.json"))
    targets = work / "Inspect.proj"
    ET.ElementTree(target).write(targets, encoding="utf-8", xml_declaration=True)
    command = [str(dotnet), "msbuild", str(targets), "/t:Inspect", "/v:minimal", "/nr:false"]
    with (work / "graph.log").open("wb") as stream:
        completed = subprocess.run(command, cwd=work, stdout=stream, stderr=subprocess.STDOUT, timeout=90, check=False)
    require(completed.returncode == 0, "Static graph inspection rejected its source inputs; see " + str(work / "graph.log")
            + "\n" + (work / "graph.log").read_bytes()[-12_000:].decode("utf-8", errors="replace"))
    graph = read_json(work / "graph.json")
    require(graph["schemaVersion"] == 1 and {x["typeName"] for x in graph["types"]} == set(types), "Static graph output differs from its exact type selection.")
    require(graph["sourceSha256"] == {str(x): hashlib.sha256(normalized(x)).hexdigest() for x in sources},
            "Static graph output differs from the validated native source inventory.")
    require(graph["compilerContext"] == dict(nullable="enable", globalUsings=namespaces), "Static graph compiler context differs from its source-backed inputs.")
    return graph


def property_contract(entry: dict, types: dict[str, dict], depth: int = 0) -> tuple[str, dict]:
    require(depth <= 16, "Native settings shape exceeds depth16.")
    constraints = {"nullable": entry["nullable"]}
    name = entry["typeName"]
    kind = entry["type"]
    definition = types.get(name)
    if definition and definition["kind"] == "enum":
        kind = "string"
        constraints["enum"] = definition["members"]
        constraints["nativeIntegralValues"] = definition["memberValues"]
        constraints["nativeEnumBinding"] = "ConfigurationBinder accepts enum names and integral spellings; the owning validator determines rejection of undefined values."
    if entry.get("typeFormat"):
        constraints["format"] = "dotnetTimeSpan" if entry["typeFormat"] == "timespan" else "uri"
    constraints["nativeType"] = name
    if definition and definition["kind"] in ("object", "record"):
        constraints["properties"] = {}
        for prop in definition["properties"]:
            child_kind, child = property_contract(prop, types, depth + 1)
            child["type"] = child_kind
            if prop["name"] == "Credentials":
                child["secret"] = True
            elif prop["hasDefault"]:
                child["default"] = prop.get("default")
            constraints["properties"][prop["name"]] = child
        constraints["required"] = [x["name"] for x in definition["properties"] if not x["hasDefault"] and not x["nullable"]]
    return kind, constraints


def assemble(graph: dict, declaration: dict, source_hashes: dict, origins: list[dict], version: str, assembly_hash: str) -> dict:
    types = {x["typeName"]: x for x in graph["types"]}
    require(declaration["schemaVersion"] == 1 and declaration["owner"] == "Orbyss.Foundation.Host", "Host integration declaration identity differs.")
    contracts = []
    for boundary in declaration["boundaries"]:
        settings = []
        for binding in boundary["bindings"]:
            definition = types[binding["typeName"]]
            require(definition["kind"] in ("object", "record"), "Native binding root must have a static property graph.")
            properties = {x["name"]: x for x in definition["properties"]}
            selected = binding.get("properties", list(properties))
            require(set(selected).issubset(properties) and len(selected) == len(set(selected)), "Declared binding properties do not match source.")
            require(binding.get("readerControls") or set(selected) == set(properties),
                    "Typed native binding must cover its complete actual public property graph.")
            require(set(binding.get("semantics", {})).issubset(selected), "Declared semantics refer to an unbound property.")
            for property_name in selected:
                prop = properties[property_name]
                kind, constraints = property_contract(prop, types)
                semantics = binding.get("semantics", {}).get(property_name, {})
                constraints.update(semantics.get("constraints", {}))
                constraints["sourceType"] = binding["typeName"]
                constraints["sourceProperty"] = property_name
                if binding.get("readerControls"):
                    constraints["bindingAuthority"] = {"kind": "native-reader-control", "sourceApi": binding["sourceApi"]}
                item = dict(path=binding["prefix"] + ":" + property_name, type=kind, required=not prop["hasDefault"] and not prop["nullable"],
                            secret=semantics.get("secret", False), constraints=constraints, binding=binding["binding"],
                            precedence=boundary["precedence"], reload="restart", description=semantics.get("description", "Native " + prop["typeName"] + " property " + property_name + "."))
                if prop["hasDefault"] and not item["secret"] and not binding.get("readerControls"):
                    item["default"] = copy.deepcopy(prop.get("default"))
                settings.append(item)
        require(len(settings) <= 256 and len({x["path"].casefold() for x in settings}) == len(settings), "Host binding paths are duplicated/excessive.")
        contracts.append(dict(schemaVersion=2, owner="Orbyss.Foundation.Host", scope=boundary["scope"], complete=True,
                              appliesTo=dict(kind="host", features=[], configuration=boundary["configuration"]),
                              sources=source_hashes, settings=settings, semanticConstraints=boundary["semanticConstraints"]))
    require({x["scope"] for x in contracts} == {"host-cshells-binding", "host-nuplane-binding"}, "Required native Host scopes are incomplete.")
    return dict(schemaVersion=2, packageId="Orbyss.Foundation.Host", packageVersion=version, sourceSha256=source_hashes,
                sourceFiles={key: ".orbyss-foundation/settings-sources/host/" + key + ".txt" for key in source_hashes},
                contracts=contracts, assembly=dict(name="Orbyss.Foundation.Host.dll", sha256=assembly_hash), imports=[], origins=origins)


def emit(args) -> Path:
    resources = args.resources.resolve()
    host = args.host_output.resolve()
    output = args.output.resolve()
    require(output.is_relative_to(host), "Host metadata output must be contained in its actual output directory.")
    origins, sources = package_inputs(resources, args.assets.resolve(), host)
    declaration = read_json(resources / "integration.json")
    framework, namespaces, compiler_inputs = sdk_context(args.dotnet.resolve())
    references = {x.name.casefold(): x for x in host.glob("*.dll")}
    references.update({x.name.casefold(): x for x in framework})
    work_root = args.work.resolve(); work_root.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix="host-vendor-settings-", dir=work_root))
    graph = inspect_graph(args.dotnet.resolve(), args.build_task_assembly.resolve(), [x for x in sources if x.name.endswith(".cs.txt")],
                          list(references.values()), declaration["types"], namespaces, work)
    own_sources = {"Program.cs": HOST / "Program.cs", "metadata/vendor-settings/inputs.json": resources / "inputs.json",
                   "metadata/vendor-settings/integration.json": resources / "integration.json",
                   "producer/emit_host_vendor_settings.py": Path(__file__),
                   "producer/InspectSettingsSourceGraph.cs": ROOT / "src/Orbyss.Foundation.Build/InspectSettingsSourceGraph.cs",
                   "producer/SourceSettingsDefaults.cs": ROOT / "src/Orbyss.Foundation.Build/SourceSettingsDefaults.cs"}
    own_sources.update(compiler_inputs)
    own_bytes = {key: normalized(file) for key, file in own_sources.items()}
    own_hashes = {key: hashlib.sha256(value).hexdigest() for key, value in own_bytes.items()}
    metadata = assemble(graph, declaration, own_hashes, origins, args.version, digest(host / "Orbyss.Foundation.Host.dll"))
    if args.validate_only:
        require(read_json(output) == metadata, "Existing Host metadata is stale or differs from current source/defaults/assembly/origins.")
        for key, data in own_bytes.items():
            require(normalized(contained(host, metadata["sourceFiles"][key])) == data, "Retained Host authority source copy differs: " + key)
        for origin in origins:
            for image_relative, expected in origin["source"]["files"].items():
                require(hashlib.sha256(normalized(contained(host, image_relative))).hexdigest() == expected,
                        "Retained native origin source copy differs: " + image_relative)
        print("Existing offline native Host metadata validated without refresh: " + str(output))
        return output
    # All source inputs are admitted before output. Copies are metadata-only .txt resources.
    for key, data in own_bytes.items():
        destination = contained(host, metadata["sourceFiles"][key]); destination.parent.mkdir(parents=True, exist_ok=True); destination.write_bytes(data)
    for origin in origins:
        for image_relative in origin["source"]["files"]:
            source = contained(resources / "sources", image_relative.removeprefix(IMAGE_VENDOR))
            destination = contained(host, image_relative); destination.parent.mkdir(parents=True, exist_ok=True); destination.write_bytes(normalized(source))
    write_json(output, metadata)
    write_json(work / "result.json", dict(status="passed", output=str(output), metadataSha256=digest(output),
               origins=9, sourceFiles=len(sources), constructorsExecuted=False, applicationInitialized=False, graph=graph))
    print("Offline native Host metadata emitted: " + str(output) + "; evidence: " + str(work / "result.json"))
    return output


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--resources", type=Path, default=RESOURCES)
    parser.add_argument("--assets", type=Path, required=True)
    parser.add_argument("--host-output", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--build-task-assembly", type=Path, required=True)
    parser.add_argument("--dotnet", type=Path, default=Path(shutil.which("dotnet") or "dotnet"))
    parser.add_argument("--work", type=Path, default=ROOT / "artifacts/host-vendor-settings")
    parser.add_argument("--validate-only", action="store_true", help="Reject stale existing metadata/source copies without regenerating or refreshing them.")
    emit(parser.parse_args())


if __name__ == "__main__":
    main()
