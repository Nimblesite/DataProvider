"""Exercise the release CLI against real carriers. [SWR-VERSION-BUILD-STAMPING]"""
from pathlib import Path
import json
import shutil
import subprocess
import sys
import tempfile
import tomllib
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
CARRIERS = (
    "Directory.Build.props", "Lql/lql-lsp-rust/Cargo.toml",
    "Lql/LqlExtension/package.json", "package.json",
    "shipwright.json", "Lql/LqlExtension/shipwright.json",
)


def assert_versions(root: Path, version: str) -> None:
    assert ET.parse(root / CARRIERS[0]).findtext(".//Version") == version
    cargo = tomllib.loads((root / CARRIERS[1]).read_text())
    assert cargo["workspace"]["package"]["version"] == version
    for name in CARRIERS[2:]:
        data = json.loads((root / name).read_text())
        if "product" in data:
            assert data["product"]["version"] == version
            assert all(
                c["expectedVersion"] == version
                for c in data["components"] if "expectedVersion" in c
            )
        else:
            expected = version.partition("-")[0] if name == "Lql/LqlExtension/package.json" else version
            assert data["version"] == expected


def run_stamp(root: Path, version: str, dry_run: bool = False) -> None:
    subprocess.run(
        [sys.executable, str(ROOT / "tools/shipwright-version-stamp.py"),
         "--root", str(root), "--tag", f"v{version}",
         *(["--dry-run"] if dry_run else [])],
        check=True, timeout=10, stdout=subprocess.DEVNULL,
    )


def test_release_carriers() -> None:
    original = {name: (ROOT / name).read_bytes() for name in CARRIERS}
    with tempfile.TemporaryDirectory(prefix="version-stamp-ci-") as directory:
        root = Path(directory)
        for name in CARRIERS:
            target = root / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / name, target)
        run_stamp(root=root, version="1.2.3", dry_run=True)
        assert {name: (root / name).read_bytes() for name in CARRIERS} == original
        package = json.loads((root / "package.json").read_text())
        (root / "package.json").write_text(json.dumps(package | {"version": "0.0.0-dev"}))
        for version in ("1.2.3", "1.2.4-beta.5"):
            run_stamp(root=root, version=version)
            assert_versions(root=root, version=version)
        assert {name: (ROOT / name).read_bytes() for name in CARRIERS} == original


def test_release_preserves_locked_cargo_dependencies() -> None:
    """Release stamping remains buildable with --locked. [SWR-VERSION-BUILD-STAMPING]"""
    cargo_root = Path("Lql/lql-lsp-rust")
    original = tomllib.loads((ROOT / cargo_root / "Cargo.lock").read_text())["package"]
    registry = [package for package in original if "source" in package]
    with tempfile.TemporaryDirectory(prefix="version-lock-ci-") as directory:
        root = Path(directory)
        shutil.copytree(src=ROOT / cargo_root, dst=root / cargo_root, ignore=shutil.ignore_patterns("target"))
        for version in ("1.2.3", "1.2.4-beta.5"):
            run_stamp(root=root, version=version)
            packages = tomllib.loads((root / cargo_root / "Cargo.lock").read_text())["package"]
            assert [package for package in packages if "source" in package] == registry
            assert {package["version"] for package in packages if "source" not in package} == {version}
            subprocess.run(
                args=["cargo", "metadata", "--locked", "--offline", "--format-version", "1"],
                cwd=root / cargo_root, check=True, timeout=30, stdout=subprocess.DEVNULL,
            )


if __name__ == "__main__":
    test_release_carriers()
    test_release_preserves_locked_cargo_dependencies()
