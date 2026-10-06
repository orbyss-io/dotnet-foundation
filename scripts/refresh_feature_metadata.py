"""Review/update current publisher source inventories; never touch historical evidence."""
import argparse
import hashlib
import json
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true", help="Update current descriptor source hashes after source review")
    parser.add_argument("packages", nargs="*", help="Exact current project names; omit to check all")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    mismatches = []
    for descriptor in sorted((root / "src").glob("*/feature.json")):
        if args.packages and descriptor.parent.name not in args.packages:
            continue
        data = json.loads(descriptor.read_text(encoding="utf-8-sig"))
        inventory = {}
        for source in sorted(descriptor.parent.rglob("*.cs")):
            relative = source.relative_to(descriptor.parent)
            if any(part in {"bin", "obj"} for part in relative.parts):
                continue
            text = source.read_text(encoding="utf-8-sig").replace("\r\n", "\n")
            inventory[relative.as_posix()] = hashlib.sha256(text.encode("utf-8")).hexdigest()
        if data.get("sourceSha256") != inventory:
            if args.write:
                data["sourceSha256"] = inventory
                descriptor.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
                print(f"Updated current {descriptor.parent.name}: {len(inventory)} source files")
            else:
                mismatches.append(descriptor.relative_to(root).as_posix())
    if mismatches:
        raise SystemExit("Current publisher inventories need review/update: " + ", ".join(mismatches))
    print("Current publisher source inventories match.")


if __name__ == "__main__":
    main()
