#!/usr/bin/env python3
"""Vendored Shipwright manifest validator. [SWR-GATE-CI] [SWR-VERSION-MANIFEST]

Structural check of shipwright.json: required keys, canonical platform ids, valid
component kinds and sources. Exits non-zero with a precise message on any violation.
Stands in for `@nimblesite/shipwright-validate-manifest` where npm is not wired.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

CANONICAL_PLATFORMS = frozenset(
    {"darwin-arm64", "darwin-x64", "linux-x64", "linux-arm64", "win32-x64", "win32-arm64", "all"}
)
VALID_KINDS = frozenset({"lsp", "mcp", "cli", "sidecar", "helper", "config"})
VALID_SOURCES = frozenset({"user-setting", "env", "bundled", "github-release"})
COMPONENT_KEYS = ("id", "kind", "language", "binaryName", "expectedVersion", "platforms", "sources", "required")


def validate(path: Path) -> list[str]:
    errors: list[str] = []
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        return [f"{path}: not readable/parseable JSON: {exc}"]

    if data.get("manifestVersion") != 1:
        errors.append(f"{path}: manifestVersion must be 1")
    product = data.get("product")
    if not isinstance(product, dict) or not all(k in product for k in ("id", "displayName", "version")):
        errors.append(f"{path}: product must have id, displayName, version")

    components = data.get("components")
    if not isinstance(components, list) or not components:
        return errors + [f"{path}: components must be a non-empty array"]

    for index, component in enumerate(components):
        where = f"{path}: components[{index}]"
        if not isinstance(component, dict):
            errors.append(f"{where}: must be an object")
            continue
        for key in COMPONENT_KEYS:
            if key not in component:
                errors.append(f"{where}: missing required key '{key}'")
        for platform in component.get("platforms", []) or []:
            if platform not in CANONICAL_PLATFORMS:
                errors.append(f"{where}: non-canonical platform '{platform}'")
        if component.get("kind") not in VALID_KINDS:
            errors.append(f"{where}: invalid kind '{component.get('kind')}'")
        for source in component.get("sources", []) or []:
            if source not in VALID_SOURCES:
                errors.append(f"{where}: invalid source '{source}'")
    return errors


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: validate-manifest.py <shipwright.json>", file=sys.stderr)
        return 2
    errors = validate(Path(sys.argv[1]))
    if errors:
        for error in errors:
            print(f"::error::{error}", file=sys.stderr)
        return 1
    print(f"{sys.argv[1]}: valid Shipwright manifest")
    return 0


if __name__ == "__main__":
    sys.exit(main())
