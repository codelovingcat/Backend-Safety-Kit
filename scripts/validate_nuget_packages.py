#!/usr/bin/env python3
"""Validate the NuGet artifacts produced by dotnet pack using only Python's stdlib."""
from __future__ import annotations

import sys
import zipfile
from pathlib import Path
from xml.etree import ElementTree


EXPECTED = {
    "BackendSafetyKit": set(),
    "BackendSafetyKit.AspNetCore": {"BackendSafetyKit"},
    "BackendSafetyKit.OpenTelemetry": {
        "BackendSafetyKit",
        "OpenTelemetry.Api.ProviderBuilderExtensions",
    },
    "BackendSafetyKit.EntityFrameworkCore": {"BackendSafetyKit", "Microsoft.EntityFrameworkCore"},
}


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def child(element: ElementTree.Element, name: str) -> ElementTree.Element | None:
    return next((item for item in element if local_name(item.tag) == name), None)


def text_of(element: ElementTree.Element, name: str) -> str:
    item = child(element, name)
    return (item.text or "").strip() if item is not None else ""


def fail(message: str) -> None:
    raise ValueError(message)


def nuspec_metadata(archive: zipfile.ZipFile) -> tuple[str, str, ElementTree.Element]:
    nuspecs = [name for name in archive.namelist() if name.lower().endswith(".nuspec")]
    if len(nuspecs) != 1:
        fail(f"Expected exactly one .nuspec, found {nuspecs}")
    root = ElementTree.fromstring(archive.read(nuspecs[0]))
    metadata = child(root, "metadata")
    if metadata is None:
        fail(f"{nuspecs[0]} has no metadata element")
    package_id = text_of(metadata, "id")
    version = text_of(metadata, "version")
    if not package_id or not version:
        fail(f"{nuspecs[0]} is missing package ID or version")
    return package_id, version, metadata


def dependency_ids(metadata: ElementTree.Element) -> set[str]:
    result: set[str] = set()
    dependencies = child(metadata, "dependencies")
    if dependencies is not None:
        for item in dependencies.iter():
            if local_name(item.tag) == "dependency":
                dep_id = item.attrib.get("id", "").strip()
                if dep_id:
                    result.add(dep_id)
    return result


def validate_main_package(path: Path) -> tuple[str, str]:
    with zipfile.ZipFile(path) as archive:
        names = set(archive.namelist())
        package_id, version, metadata = nuspec_metadata(archive)
        if package_id not in EXPECTED:
            fail(f"{path.name} contains unexpected package ID {package_id!r}")
        required = {
            "README.md",
            f"lib/net10.0/{package_id}.dll",
            f"lib/net10.0/{package_id}.xml",
        }
        missing = sorted(required - names)
        if missing:
            fail(f"{path.name} is missing required package files: {missing}")

        for field in ("authors", "description", "projectUrl", "tags", "readme"):
            if not text_of(metadata, field):
                fail(f"{path.name} is missing NuGet metadata field <{field}>")

        license_node = child(metadata, "license")
        if (
            license_node is None
            or license_node.attrib.get("type") != "expression"
            or (license_node.text or "").strip() != "MIT"
        ):
            fail(f"{path.name} must declare the MIT license expression")

        repository = child(metadata, "repository")
        if repository is None or repository.attrib.get("type") != "git":
            fail(f"{path.name} is missing its Git repository metadata")
        repository_url = repository.attrib.get("url", "").strip()
        commit = repository.attrib.get("commit", "").strip()
        if repository_url != "https://github.com/codelovingcat/Backend-Safety-Kit":
            fail(f"{path.name} has unexpected repository URL {repository_url!r}")
        if len(commit) != 40 or any(ch not in "0123456789abcdefABCDEF" for ch in commit):
            fail(f"{path.name} must record the 40-character repository commit in its nuspec")

        dependencies = dependency_ids(metadata)
        expected_dependencies = EXPECTED[package_id]
        if dependencies != expected_dependencies:
            fail(
                f"{path.name} dependency boundary changed: expected "
                f"{sorted(expected_dependencies)}, got {sorted(dependencies)}"
            )

        xml_name = f"lib/net10.0/{package_id}.xml"
        xml_root = ElementTree.fromstring(archive.read(xml_name))
        members = [
            item for item in xml_root.iter()
            if local_name(item.tag) == "member" and item.attrib.get("name")
        ]
        if not members:
            fail(f"{path.name} contains an empty XML documentation file")

        print(f"Validated {path.name}: metadata, license, README, XML docs and dependencies")
        return package_id, version


def validate_symbol_package(path: Path, package_id: str, version: str) -> None:
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        symbol_id, symbol_version, _ = nuspec_metadata(archive)
        if symbol_id != package_id or symbol_version != version:
            fail(
                f"{path.name} identity {symbol_id} {symbol_version} does not match "
                f"{package_id} {version}"
            )
        expected_pdb = f"{package_id}.pdb".lower()
        if not any(Path(name).name.lower() == expected_pdb for name in names):
            fail(f"{path.name} does not contain {package_id}.pdb")
        print(f"Validated {path.name}: matching identity and portable PDB")


def main() -> int:
    output = Path(sys.argv[1] if len(sys.argv) > 1 else "./artifacts").resolve()
    if not output.is_dir():
        print(f"Package output directory does not exist: {output}", file=sys.stderr)
        return 2

    packages = sorted(output.glob("*.nupkg"))
    by_id: dict[str, tuple[Path, str]] = {}
    for package in packages:
        package_id, version = validate_main_package(package)
        if package_id in by_id:
            fail(f"Multiple primary packages found for {package_id}")
        by_id[package_id] = (package, version)

    missing_ids = sorted(set(EXPECTED) - set(by_id))
    extra_ids = sorted(set(by_id) - set(EXPECTED))
    if missing_ids or extra_ids:
        fail(f"Unexpected pack output; missing IDs={missing_ids}, extra IDs={extra_ids}")

    for package_id, (package, version) in by_id.items():
        symbol_path = output / f"{package.stem}.snupkg"
        if not symbol_path.is_file():
            fail(f"Missing symbol package: {symbol_path.name}")
        validate_symbol_package(symbol_path, package_id, version)

    print(f"Successfully validated {len(EXPECTED)} NuGet packages and symbol packages.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, zipfile.BadZipFile, ElementTree.ParseError, ValueError) as exc:
        print(f"NuGet package validation failed: {exc}", file=sys.stderr)
        raise SystemExit(1)
