#!/usr/bin/env python3
"""Shipwright build-time version stamper. [SWR-VERSION-BUILD-STAMPING]

Rewrites every version carrier in the working tree from a release tag, using
structured parsers only (no regex on structured formats). Source stays at the
0.0.0-dev placeholder; the tag-triggered release stamps the runner working tree
and NEVER commits the result.

Carriers stamped:
  - Directory.Build.props                         <Version> (XML, ElementTree)
  - Lql/lql-lsp-rust/Cargo.toml                   [workspace.package].version (TOML, table-aware walk)
  - Lql/lql-lsp-rust/Cargo.lock                   workspace package versions only (TOML)
  - Lql/LqlExtension/package.json                 numeric version for Marketplace (JSON)
  - package.json                                  version (JSON, only if present)
  - shipwright.json                               product.version + every expectedVersion (JSON)
  - Lql/LqlExtension/shipwright.json              product.version + every expectedVersion (JSON)

Usage:
  shipwright-version-stamp.py --tag v1.2.3 --root . --dry-run   # list carriers, change nothing
  shipwright-version-stamp.py --tag v1.2.3 --root .             # stamp the runner working tree
"""

from __future__ import annotations

import argparse
import json
import sys
import tomllib
import xml.etree.ElementTree as ET
from pathlib import Path


def parse_version(tag: str) -> str:
    """Strip a single leading 'v' from a release tag. [SWR-VERSION-MATCHING]"""
    return tag[1:] if tag.startswith("v") else tag


def stamp_json(path: Path, version: str, dry_run: bool) -> list[str]:
    data = json.loads(path.read_text(encoding="utf-8"))
    changed: list[str] = []
    if isinstance(data.get("version"), str):
        data["version"] = version
        changed.append(f"{path}: version -> {version}")
    product = data.get("product")
    if isinstance(product, dict) and isinstance(product.get("version"), str):
        product["version"] = version
        changed.append(f"{path}: product.version -> {version}")
    for component in data.get("components", []) or []:
        if isinstance(component, dict) and isinstance(component.get("expectedVersion"), str):
            component["expectedVersion"] = version
            changed.append(f"{path}: components[{component.get('id')}].expectedVersion -> {version}")
    if changed and not dry_run:
        path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    return changed


def stamp_msbuild(path: Path, version: str, dry_run: bool) -> list[str]:
    tree = ET.parse(path)
    changed: list[str] = []
    for version_el in tree.getroot().iter("Version"):
        version_el.text = version
        changed.append(f"{path}: <Version> -> {version}")
    if changed and not dry_run:
        tree.write(path, encoding="utf-8", xml_declaration=False)
    return changed


def stamp_toml_versions(path: Path, version: str, dry_run: bool, table: str, targets: set[int]) -> list[str]:
    """Preserve TOML text outside selected version keys. [SWR-VERSION-BUILD-STAMPING]"""
    lines = path.read_text(encoding="utf-8").splitlines(keepends=True)
    active_table: str | None = None
    table_index = -1
    changed: list[str] = []
    for index, line in enumerate(lines):
        stripped = line.strip()
        if stripped.startswith("[") and stripped.endswith("]"):
            active_table = stripped
            table_index += int(active_table == table)
        if active_table == table and table_index in targets and stripped.partition("=")[0].strip() == "version":
            eol = "\r\n" if line.endswith("\r\n") else "\n"
            lines[index] = f'version = "{version}"{eol}'
            changed.append(f"{path}: {table}[{table_index}].version -> {version}")
    if changed and not dry_run:
        path.write_text("".join(lines), encoding="utf-8")
    return changed


def stamp_cargo_lock(path: Path, version: str, dry_run: bool) -> list[str]:
    """Update workspace entries without resolving dependencies. [SWR-VERSION-BUILD-STAMPING]"""
    lock = path.parent / "Cargo.lock"
    if not lock.exists():
        return []
    workspace = tomllib.loads(path.read_text(encoding="utf-8"))["workspace"]
    packages = (
        tomllib.loads((path.parent / member / "Cargo.toml").read_text(encoding="utf-8"))["package"]
        for member in workspace["members"]
    )
    names = {package["name"] for package in packages if package.get("version") == {"workspace": True}}
    entries = tomllib.loads(lock.read_text(encoding="utf-8"))["package"]
    targets = {index for index, package in enumerate(entries) if package["name"] in names and "source" not in package}
    return stamp_toml_versions(path=lock, version=version, dry_run=dry_run, table="[[package]]", targets=targets)


def stamp_cargo_workspace(path: Path, version: str, dry_run: bool) -> list[str]:
    """Stamp the manifest and its locked workspace entries. [SWR-VERSION-BUILD-STAMPING]"""
    parsed = tomllib.loads(path.read_text(encoding="utf-8"))
    if "version" not in parsed.get("workspace", {}).get("package", {}):
        return []
    changed = stamp_toml_versions(path=path, version=version, dry_run=dry_run, table="[workspace.package]", targets={0})
    return changed + stamp_cargo_lock(path=path, version=version, dry_run=dry_run)


def main() -> int:
    ap = argparse.ArgumentParser(description="Shipwright version stamper")
    ap.add_argument("--tag", required=True, help="Release tag, e.g. v1.2.3")
    ap.add_argument("--root", default=".", help="Repo root to stamp")
    ap.add_argument("--dry-run", action="store_true", help="List carriers; change nothing")
    args = ap.parse_args()

    version = parse_version(args.tag)
    root = Path(args.root).resolve()

    carriers: list[tuple[Path, str]] = [
        (root / "Directory.Build.props", "msbuild"),
        (root / "Lql" / "lql-lsp-rust" / "Cargo.toml", "cargo"),
        (root / "Lql" / "LqlExtension" / "package.json", "json"),
        (root / "package.json", "json"),
        (root / "shipwright.json", "json"),
        (root / "Lql" / "LqlExtension" / "shipwright.json", "json"),
    ]

    handlers = {"msbuild": stamp_msbuild, "cargo": stamp_cargo_workspace, "json": stamp_json}
    all_changes: list[str] = []
    for path, kind in carriers:
        if not path.exists():
            continue
        # Marketplace represents prereleases through --pre-release, not a suffix.
        # Implements [SWR-VSIX-PACKAGE]; binary expectedVersion retains the full tag.
        carrier_version = version.partition("-")[0] if path == root / "Lql/LqlExtension/package.json" else version
        all_changes.extend(handlers[kind](path=path, version=carrier_version, dry_run=args.dry_run))

    mode = "DRY-RUN (no files changed)" if args.dry_run else "STAMPED"
    print(f"shipwright-version-stamp {mode}: tag={args.tag} version={version} root={root}")
    for change in all_changes:
        print(f"  {change}")
    if not all_changes:
        print("  ERROR: no version carriers found — refusing to release silently.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
