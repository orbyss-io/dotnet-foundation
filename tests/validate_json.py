from pathlib import Path
import json
import subprocess
import tempfile

def main():
    root = Path(__file__).resolve().parents[1]
    command = ["dotnet", "run", "--project", str(root / "tests/dotnet/Orbyss.Foundation.Json.Probe"),
               "-c", "Release", "--no-build", "--no-restore"]
    subprocess.run(command, cwd=root, check=True, timeout=180)
    generated = subprocess.run(["node", str(root / "tests/json_cross_language_vectors.mjs")],
                               capture_output=True, text=True, encoding="utf-8", check=True)
    vectors = [json.loads(line) for line in generated.stdout.split("\n") if line]
    with tempfile.TemporaryDirectory() as folder:
        source = Path(folder) / "inputs.jsonl"
        source.write_text("\n".join(v["input"] for v in vectors), encoding="utf-8")
        result = subprocess.run(command + ["--", "--vectors", str(source)], cwd=root, check=True,
                                capture_output=True, text=True, encoding="utf-8", timeout=180)
    actual = result.stdout.split("\n")
    if actual and actual[-1] == "": actual.pop()
    assert len(actual) == len(vectors), "Canonical vector output count changed."
    for vector, output in zip(vectors, actual):
        assert output.rstrip("\r") == vector["expected"], f"Canonical mismatch: {vector!r}, got {output!r}"
    print(f"Cross-language canonicalization passed: {len(vectors)} Node reference vectors.")

if __name__ == "__main__":
    main()
