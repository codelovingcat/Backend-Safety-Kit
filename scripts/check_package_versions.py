#!/usr/bin/env python3
"""Validate that all shipping package projects use one valid SemVer version."""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path
from xml.etree import ElementTree


PROJECTS = {
    "BackendSafetyKit": "src/BackendSafetyKit/BackendSafetyKit.csproj",
    "BackendSafetyKit.Redis": "src/BackendSafetyKit.Redis/BackendSafetyKit.Redis.csproj",
    "BackendSafetyKit.AspNetCore": "src/BackendSafetyKit.AspNetCore/BackendSafetyKit.AspNetCore.csproj",
    "BackendSafetyKit.OpenTelemetry": "src/BackendSafetyKit.OpenTelemetry/BackendSafetyKit.OpenTelemetry.csproj",
}
IDENTIFIER = r"(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)"
SEMVER = re.compile(
    rf"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)"
    rf"(?:-({IDENTIFIER}(?:\.{IDENTIFIER})*))?"
    rf"(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$"
)


def child_text(root: ElementTree.Element, name: str) -> str:
    child = next(
        (
            item
            for item in root.iter()
            if item is not root and item.tag.rsplit("}", 1)[-1] == name
        ),
        None,
    )
    return (child.text or "").strip() if child is not None else ""


def validate_versions(root: Path, tag: str | None = None) -> str:
    found: dict[str, str] = {}
    for expected_id, relative_path in PROJECTS.items():
        path = root / relative_path
        if not path.is_file():
            raise ValueError(f"Missing shipping project: {relative_path}")
        project = ElementTree.parse(path).getroot()
        package_id = child_text(project, "PackageId")
        version = child_text(project, "Version")
        if package_id != expected_id:
            raise ValueError(
                f"{relative_path}: expected PackageId {expected_id!r}, got {package_id!r}"
            )
        if not SEMVER.fullmatch(version):
            raise ValueError(f"{relative_path}: {version!r} is not a valid SemVer version")
        found[package_id] = version

    unique_versions = set(found.values())
    if len(unique_versions) != 1:
        details = ", ".join(f"{package_id}={version}" for package_id, version in found.items())
        raise ValueError(f"Shipping package versions must match: {details}")

    version = next(iter(unique_versions))
    if tag:
        tag_version = tag[1:] if tag.startswith("v") else tag
        if not SEMVER.fullmatch(tag_version):
            raise ValueError(f"Release tag {tag!r} is not v-prefixed SemVer")
        if tag_version != version:
            raise ValueError(
                f"Release tag version {tag_version!r} does not match package version {version!r}"
            )
    return version


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--tag", help="Optional release tag, for example v0.1.0 or v1.2.0-rc.1")
    args = parser.parse_args()
    version = validate_versions(args.root, args.tag)
    print(f"Validated {len(PROJECTS)} shipping packages at version {version}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ElementTree.ParseError, ValueError) as exc:
        print(f"Package version policy failed: {exc}", file=sys.stderr)
        raise SystemExit(1)
