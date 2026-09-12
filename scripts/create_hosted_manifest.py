"""Build an explicit public deployment manifest from trusted Vite output and constrained branding."""
import argparse
import hashlib
import json
from pathlib import Path

MIME = {".js": "text/javascript; charset=utf-8", ".css": "text/css; charset=utf-8",
        ".png": "image/png", ".woff2": "font/woff2"}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", required=True, type=Path)
    parser.add_argument("--vite", required=True)
    parser.add_argument("--branding", required=True)
    parser.add_argument("--entry", required=True)
    parser.add_argument("--revision", required=True)
    parser.add_argument("--form-release", required=True)
    parser.add_argument("--retain", help="Previous admitted manifest inside the same deployment root.")
    parser.add_argument("--output", default="manifest.json")
    args = parser.parse_args()
    root = args.root.resolve(strict=True)

    def path(relative):
        raw = Path(relative)
        if raw.is_absolute() or any(part in (".", "..") for part in raw.parts):
            raise ValueError("Use literal relative deployment paths.")
        candidate = root / raw
        if not candidate.resolve().is_relative_to(root):
            raise ValueError("Path escapes deployment root.")
        for part in (candidate, *candidate.parents):
            if part.is_symlink() or (hasattr(part, "is_junction") and part.is_junction()):
                raise ValueError("Linked deployment paths are forbidden.")
            if part == root: break
        return candidate

    vite = json.loads(path(args.vite).read_text(encoding="utf-8"))
    brand = json.loads(path(args.branding).read_text(encoding="utf-8"))
    if set(brand) - {"defaultLocale", "locales", "theme", "logo"}:
        raise ValueError("Branding contains fields outside the public projection.")
    if args.entry not in vite or not vite[args.entry].get("isEntry"):
        raise ValueError("Select an emitted Vite entry.")
    runtime = set()
    for chunk in vite.values():
        runtime.add(chunk["file"])
        runtime.update(chunk.get("css", []))
        runtime.update(chunk.get("assets", []))
        for imported in chunk.get("imports", []) + chunk.get("dynamicImports", []):
            if imported not in vite: raise ValueError("Missing Vite import.")
    assets = []
    for index, file in enumerate(sorted(runtime)):
        asset_path = path(file)
        assets.append(dict(id=f"runtime-{index}", file=file, kind="runtime", visibility="public",
                           contentType=MIME[asset_path.suffix], sha256=hashlib.sha256(asset_path.read_bytes()).hexdigest()))
    if brand.get("logo"):
        logo = path(brand["logo"])
        if logo.suffix != ".png": raise ValueError("Branding supports admitted RGB/RGBA PNG only.")
        if brand["logo"] in runtime: raise ValueError("Keep authored branding separate from runtime dependencies.")
        assets.append(dict(id="logo", file=brand["logo"], kind="branding", visibility="public",
                           contentType="image/png", sha256=hashlib.sha256(logo.read_bytes()).hexdigest()))
    revision = dict(id=args.revision, formReleaseId=args.form_release, defaultLocale=brand["defaultLocale"],
                    locales=brand["locales"], theme=brand.get("theme", "blue"), logoAssetId="logo" if brand.get("logo") else None,
                    entry=args.entry, vite=vite, assets=assets)
    retained = json.loads(path(args.retain).read_text(encoding="utf-8"))["revisions"] if args.retain else []
    if any(item["id"] == args.revision for item in retained):
        raise ValueError("Publish a new revision identity; do not overwrite retained revisions.")
    data = json.dumps(dict(version="1", currentRevision=args.revision, revisions=retained + [revision]),
                      ensure_ascii=True, separators=(",", ":")).encode("utf-8")
    output = path(args.output)
    output.write_bytes(data)
    print(json.dumps({"CurrentRevision": args.revision, "Manifest": args.output,
                      "ManifestSha256": hashlib.sha256(data).hexdigest()}, indent=2))

if __name__ == "__main__":
    main()
