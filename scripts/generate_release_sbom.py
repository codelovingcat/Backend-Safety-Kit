#!/usr/bin/env python3
"""Create and verify a CycloneDX SBOM from shipped NuGet packages and restored dependency graphs."""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
import uuid
import zipfile
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import quote
from xml.etree import ElementTree


PACKAGE_PROJECTS = {
    "BackendSafetyKit": "src/BackendSafetyKit/BackendSafetyKit.csproj",
    "BackendSafetyKit.AspNetCore": "src/BackendSafetyKit.AspNetCore/BackendSafetyKit.AspNetCore.csproj",
    "BackendSafetyKit.OpenTelemetry": "src/BackendSafetyKit.OpenTelemetry/BackendSafetyKit.OpenTelemetry.csproj",
    "BackendSafetyKit.Redis": "src/BackendSafetyKit.Redis/BackendSafetyKit.Redis.csproj",
    "BackendSafetyKit.EntityFrameworkCore": "src/BackendSafetyKit.EntityFrameworkCore/BackendSafetyKit.EntityFrameworkCore.csproj",
}


@dataclass(frozen=True)
class ShippedPackage:
    package_id: str
    version: str
    archive: Path
    checksum: str
    direct_dependency_ids: frozenset[str]

    @property
    def bom_ref(self) -> str:
        return package_url(self.package_id, self.version)


@dataclass
class ResolvedGraph:
    components: dict[str, dict[str, object]]
    references_by_id: dict[str, str]
    dependencies: dict[str, set[str]]


def fail(message: str) -> None:
    raise ValueError(message)


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def first_child(element: ElementTree.Element, name: str) -> ElementTree.Element | None:
    return next((child for child in element if local_name(child.tag) == name), None)


def element_text(element: ElementTree.Element, name: str) -> str:
    child = first_child(element, name)
    return (child.text or "").strip() if child is not None else ""


def package_url(package_id: str, version: str) -> str:
    return f"pkg:nuget/{quote(package_id, safe='.-')}@{quote(version, safe='.-')}"


def read_nuspec(archive_path: Path) -> tuple[str, str, frozenset[str]]:
    try:
        with zipfile.ZipFile(archive_path) as archive:
            nuspecs = [
                name for name in archive.namelist()
                if name.lower().endswith(".nuspec")
            ]
            if len(nuspecs) != 1:
                fail(f"{archive_path.name}: expected one .nuspec, found {nuspecs}")
            root = ElementTree.fromstring(archive.read(nuspecs[0]))
    except (OSError, zipfile.BadZipFile, ElementTree.ParseError) as exc:
        raise ValueError(f"{archive_path.name}: cannot read NuGet metadata: {exc}") from exc

    metadata = first_child(root, "metadata")
    if metadata is None:
        fail(f"{archive_path.name}: .nuspec has no metadata element")

    package_id = element_text(metadata, "id")
    version = element_text(metadata, "version")
    if not package_id or not version:
        fail(f"{archive_path.name}: package ID or version is missing")

    dependencies = frozenset(
        item.attrib["id"].strip()
        for item in metadata.iter()
        if local_name(item.tag) == "dependency" and item.attrib.get("id", "").strip()
    )
    return package_id, version, dependencies


def load_shipped_packages(output_dir: Path) -> dict[str, ShippedPackage]:
    discovered: dict[str, ShippedPackage] = {}
    for archive in sorted(output_dir.glob("*.nupkg")):
        package_id, version, dependencies = read_nuspec(archive)
        key = package_id.casefold()
        if key in discovered:
            fail(f"Multiple primary NuGet archives found for package ID {package_id!r}")
        discovered[key] = ShippedPackage(
            package_id=package_id,
            version=version,
            archive=archive,
            checksum=hashlib.sha256(archive.read_bytes()).hexdigest(),
            direct_dependency_ids=dependencies,
        )

    expected = {package_id.casefold() for package_id in PACKAGE_PROJECTS}
    actual = set(discovered)
    missing = sorted(expected - actual)
    extra = sorted(actual - expected)
    if missing or extra:
        fail(f"Unexpected NuGet artifact set: missing={missing}, extra={extra}")

    package_versions = {package.version for package in discovered.values()}
    if len(package_versions) != 1:
        details = ", ".join(
            f"{package.package_id}={package.version}"
            for package in sorted(discovered.values(), key=lambda item: item.package_id)
        )
        fail(f"Shipping packages must use the same version: {details}")

    for expected_id in PACKAGE_PROJECTS:
        actual_package = discovered[expected_id.casefold()]
        if actual_package.package_id != expected_id:
            fail(
                f"Package ID casing mismatch: expected {expected_id!r}, "
                f"got {actual_package.package_id!r}"
            )

    return discovered


def split_library_key(key: str) -> tuple[str, str]:
    try:
        package_id, version = key.rsplit("/", 1)
    except ValueError as exc:
        raise ValueError(f"Unexpected NuGet asset library key: {key!r}") from exc
    if not package_id or not version:
        fail(f"Invalid NuGet asset library key: {key!r}")
    return package_id, version


def is_net10_target(target_name: str) -> bool:
    normalized = target_name.casefold().replace(" ", "")
    return (
        normalized.startswith("net10.0")
        or ".netcoreapp,version=v10.0" in normalized
    )


def load_resolved_graph(
    project_path: Path,
    shipped_refs_by_id: dict[str, str],
) -> ResolvedGraph:
    assets_path = project_path.parent / "obj" / "project.assets.json"
    if not assets_path.is_file():
        fail(
            f"{project_path}: missing {assets_path}. "
            "Run 'dotnet restore BackendSafetyKit.slnx' before generating the SBOM."
        )

    try:
        assets = json.loads(assets_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ValueError(f"{assets_path}: cannot read restored dependency graph: {exc}") from exc

    targets = assets.get("targets")
    libraries = assets.get("libraries")
    if not isinstance(targets, dict) or not isinstance(libraries, dict):
        fail(f"{assets_path}: missing NuGet targets or libraries maps")

    selected_targets = {
        name: content
        for name, content in targets.items()
        if is_net10_target(name) and isinstance(content, dict)
    }
    if not selected_targets:
        fail(f"{assets_path}: no .NET 10 target found in restored dependencies")

    package_rows: dict[str, tuple[str, str, dict[str, object]]] = {}
    for target in selected_targets.values():
        for library_key, target_entry in target.items():
            if not isinstance(target_entry, dict):
                continue
            library = libraries.get(library_key, {})
            if not isinstance(library, dict) or library.get("type") != "package":
                continue
            package_id, version = split_library_key(library_key)
            reference = package_url(package_id, version)
            package_rows[reference] = (
                package_id,
                version,
                target_entry,
            )

    references_by_id: dict[str, str] = {}
    for reference, (package_id, _, _) in package_rows.items():
        key = package_id.casefold()
        previous = references_by_id.get(key)
        if previous is not None and previous != reference:
            fail(
                f"{assets_path}: multiple resolved versions for {package_id!r}: "
                f"{previous} and {reference}"
            )
        references_by_id[key] = reference

    components: dict[str, dict[str, object]] = {}
    for reference, (package_id, version, _) in package_rows.items():
        components[reference] = {
            "type": "library",
            "name": package_id,
            "version": version,
            "bom-ref": reference,
            "purl": reference,
            "scope": "required",
        }

    dependencies: dict[str, set[str]] = {reference: set() for reference in package_rows}
    references_to_id = {
        key: value for key, value in references_by_id.items()
    }
    references_to_id.update(shipped_refs_by_id)

    for reference, (package_id, _, target_entry) in package_rows.items():
        declared = target_entry.get("dependencies", {})
        if not isinstance(declared, dict):
            fail(f"{assets_path}: malformed dependencies for {package_id!r}")
        for dependency_id in declared:
            dependency_key = str(dependency_id).casefold()
            dependency_ref = references_to_id.get(dependency_key)
            if dependency_ref is None:
                fail(
                    f"{assets_path}: resolved package {package_id!r} depends on "
                    f"{dependency_id!r}, which is absent from the .NET 10 package graph"
                )
            dependencies[reference].add(dependency_ref)

    return ResolvedGraph(
        components=components,
        references_by_id=references_by_id,
        dependencies=dependencies,
    )


def validate_direct_edges(
    package: ShippedPackage,
    actual_direct_refs: set[str],
    shipped_refs_by_id: dict[str, str],
    resolved_graph: ResolvedGraph,
) -> None:
    expected_refs: set[str] = set()
    for dependency_id in package.direct_dependency_ids:
        reference = shipped_refs_by_id.get(dependency_id.casefold())
        if reference is None:
            reference = resolved_graph.references_by_id.get(dependency_id.casefold())
        if reference is None:
            fail(
                f"{package.package_id}: .nuspec dependency {dependency_id!r} "
                "is not present in the restored .NET 10 dependency graph"
            )
        expected_refs.add(reference)

    if actual_direct_refs != expected_refs:
        fail(
            f"{package.package_id}: generated direct dependencies do not match its .nuspec; "
            f"expected={sorted(expected_refs)}, actual={sorted(actual_direct_refs)}"
        )


def generate_sbom(packages: dict[str, ShippedPackage], root: Path) -> dict[str, object]:
    shipped_refs_by_id = {
        key: package.bom_ref for key, package in packages.items()
    }
    all_components: dict[str, dict[str, object]] = {}
    dependency_edges: dict[str, set[str]] = {}
    shipped_refs = set(shipped_refs_by_id.values())

    for package in packages.values():
        all_components[package.bom_ref] = {
            "type": "library",
            "name": package.package_id,
            "version": package.version,
            "bom-ref": package.bom_ref,
            "purl": package.bom_ref,
            "hashes": [{"alg": "SHA-256", "content": package.checksum}],
            "properties": [
                {
                    "name": "codelovingcat:distribution",
                    "value": package.archive.name,
                },
                {
                    "name": "codelovingcat:component-role",
                    "value": "shipping-nuget-package",
                },
            ],
        }
        dependency_edges.setdefault(package.bom_ref, set())

    package_to_resolved_graph: dict[str, ResolvedGraph] = {}
    for package_id, relative_path in PACKAGE_PROJECTS.items():
        graph = load_resolved_graph(root / relative_path, shipped_refs_by_id)
        package_to_resolved_graph[package_id.casefold()] = graph

    for package_key, package in packages.items():
        graph = package_to_resolved_graph[package_key]
        direct_refs: set[str] = set()
        for dependency_id in package.direct_dependency_ids:
            dependency_ref = shipped_refs_by_id.get(dependency_id.casefold())
            if dependency_ref is None:
                dependency_ref = graph.references_by_id.get(dependency_id.casefold())
            if dependency_ref is None:
                fail(
                    f"{package.package_id}: cannot resolve direct .nuspec dependency "
                    f"{dependency_id!r} against project.assets.json"
                )
            direct_refs.add(dependency_ref)

        validate_direct_edges(package, direct_refs, shipped_refs_by_id, graph)
        dependency_edges[package.bom_ref].update(direct_refs)

        visited: set[str] = set()
        pending = list(direct_refs)
        while pending:
            current = pending.pop()
            if current in visited or current in shipped_refs:
                continue
            visited.add(current)
            component = graph.components.get(current)
            if component is None:
                fail(
                    f"{package.package_id}: dependency component {current!r} "
                    "is missing from project.assets.json"
                )
            previous = all_components.get(current)
            if previous is not None and previous != component:
                # Prefer the same package identity once it has been resolved by any project.
                if previous.get("name") != component.get("name") or previous.get("version") != component.get("version"):
                    fail(f"Conflicting SBOM component metadata for {current!r}")
            else:
                all_components[current] = component

            children = graph.dependencies.get(current, set())
            dependency_edges.setdefault(current, set()).update(children)
            pending.extend(child for child in children if child not in visited and child not in shipped_refs)

        reachable_count = len(visited)
        print(
            f"Resolved {package.package_id} {package.version}: "
            f"{len(direct_refs)} direct dependencies, "
            f"{reachable_count} external dependency components in its resolved closure."
        )

    component_refs = set(all_components)
    for source, destinations in dependency_edges.items():
        if source not in component_refs:
            fail(f"SBOM dependency source is missing: {source}")
        unknown = sorted(destinations - component_refs)
        if unknown:
            fail(f"SBOM dependency targets are missing for {source}: {unknown}")

    for package in packages.values():
        if package.bom_ref not in component_refs:
            fail(f"SBOM is missing shipped package {package.package_id}")
        actual_direct_refs = dependency_edges.get(package.bom_ref, set())
        validate_direct_edges(
            package,
            actual_direct_refs,
            shipped_refs_by_id,
            package_to_resolved_graph[package.package_id.casefold()],
        )

    release_version = next(iter(packages.values())).version
    sorted_components = sorted(
        all_components.values(),
        key=lambda component: (
            str(component["name"]).casefold(),
            str(component["version"]),
            str(component["bom-ref"]),
        ),
    )
    sorted_dependencies = [
        {
            "ref": reference,
            **({"dependsOn": sorted(destinations)} if destinations else {}),
        }
        for reference, destinations in sorted(dependency_edges.items())
    ]

    return {
        "bomFormat": "CycloneDX",
        "specVersion": "1.6",
        "serialNumber": f"urn:uuid:{uuid.uuid4()}",
        "version": 1,
        "metadata": {
            "timestamp": datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
            "component": {
                "type": "application",
                "name": "Backend Safety Kit NuGet release set",
                "version": release_version,
                "bom-ref": f"urn:uuid:{uuid.uuid4()}",
            },
            "properties": [
                {
                    "name": "codelovingcat:sbom:inventory-source",
                    "value": "NuGet .nuspec metadata and restored .NET 10 project.assets.json dependency graphs",
                },
                {
                    "name": "codelovingcat:sbom:shipped-package-count",
                    "value": str(len(packages)),
                },
            ],
        },
        "components": sorted_components,
        "dependencies": sorted_dependencies,
    }


def validate_sbom(
    sbom: dict[str, object],
    schema_path: Path,
    packages: dict[str, ShippedPackage],
) -> None:
    try:
        import jsonschema
        from jsonschema import Draft7Validator, FormatChecker
    except ImportError as exc:
        raise ValueError(
            "Missing JSON Schema validator. Install scripts/requirements-sbom.txt."
        ) from exc

    try:
        schema = json.loads(schema_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ValueError(f"Cannot load CycloneDX schema at {schema_path}: {exc}") from exc

    Draft7Validator.check_schema(schema)
    validator = Draft7Validator(schema, format_checker=FormatChecker())
    errors = sorted(
        validator.iter_errors(sbom),
        key=lambda error: (list(map(str, error.absolute_path)), error.message),
    )
    if errors:
        details = "\n".join(
            f"- {'.'.join(map(str, error.absolute_path)) or '<root>'}: {error.message}"
            for error in errors[:20]
        )
        fail(f"Generated SBOM does not satisfy the CycloneDX 1.6 JSON Schema:\n{details}")

    components = sbom.get("components", [])
    if not isinstance(components, list):
        fail("SBOM components must be an array")
    component_refs = {
        component.get("bom-ref"): component
        for component in components
        if isinstance(component, dict) and isinstance(component.get("bom-ref"), str)
    }
    if len(component_refs) != len(components):
        fail("SBOM components have missing or duplicate bom-ref values")

    for package in packages.values():
        component = component_refs.get(package.bom_ref)
        if component is None:
            fail(f"SBOM does not contain shipping package {package.package_id}")
        hashes = component.get("hashes", [])
        if not any(
            item.get("alg") == "SHA-256" and item.get("content") == package.checksum
            for item in hashes
            if isinstance(item, dict)
        ):
            fail(f"SBOM does not contain the correct SHA-256 hash for {package.archive.name}")

    dependencies = sbom.get("dependencies", [])
    if not isinstance(dependencies, list):
        fail("SBOM dependencies must be an array")
    dependency_refs = {
        item.get("ref"): set(item.get("dependsOn", []))
        for item in dependencies
        if isinstance(item, dict) and isinstance(item.get("ref"), str)
    }
    if len(dependency_refs) != len(dependencies):
        fail("SBOM has missing or duplicate dependency refs")

    for source, destinations in dependency_refs.items():
        if source not in component_refs:
            fail(f"SBOM dependency source does not identify a component: {source}")
        missing_targets = sorted(destinations - set(component_refs))
        if missing_targets:
            fail(f"SBOM dependency {source} references absent components: {missing_targets}")

    for package in packages.values():
        actual_direct_refs = dependency_refs.get(package.bom_ref, set())
        expected_direct_ids = package.direct_dependency_ids
        actual_direct_ids = {
            str(component_refs[reference].get("name"))
            for reference in actual_direct_refs
        }
        if {item.casefold() for item in actual_direct_ids} != {
            item.casefold() for item in expected_direct_ids
        }:
            fail(
                f"SBOM direct dependencies for {package.package_id} do not match its .nuspec: "
                f"expected={sorted(expected_direct_ids)}, actual={sorted(actual_direct_ids)}"
            )

    print(
        "Validated CycloneDX 1.6 schema, all five shipping package identities, "
        "package SHA-256 hashes, and dependency graph references."
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("artifacts", nargs="?", type=Path, default=Path("./artifacts"))
    parser.add_argument(
        "--schema",
        required=True,
        type=Path,
        help="Path to the official CycloneDX 1.6 JSON schema.",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("./artifacts/backend-safety-kit.sbom.json"),
    )
    args = parser.parse_args()
    output_dir = args.artifacts.resolve()
    schema_path = args.schema.resolve()
    output_path = args.output.resolve()

    if not output_dir.is_dir():
        fail(f"NuGet artifact directory does not exist: {output_dir}")

    packages = load_shipped_packages(output_dir)
    root = Path(__file__).resolve().parents[1]
    sbom = generate_sbom(packages, root)
    validate_sbom(sbom, schema_path, packages)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(
        json.dumps(sbom, indent=2, sort_keys=False) + "\n",
        encoding="utf-8",
    )
    print(f"Wrote {output_path} with {len(sbom['components'])} unique components.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError, json.JSONDecodeError, zipfile.BadZipFile) as exc:
        print(f"SBOM generation/validation failed: {exc}", file=sys.stderr)
        raise SystemExit(1)
