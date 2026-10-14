from __future__ import annotations

import io
import sys
import tempfile
import unittest
import zipfile
from contextlib import redirect_stdout
from pathlib import Path
from xml.etree import ElementTree

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import validate_nuget_packages as validator


PACKAGE_VERSION = "0.1.0"
REPOSITORY_URL = "https://github.com/codelovingcat/Backend-Safety-Kit"
REPOSITORY_COMMIT = "a" * 40


class NuGetPackageValidatorTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp_dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp_dir.cleanup)
        self.output = Path(self.temp_dir.name)

    def _write_nuspec(
        self,
        package_id: str,
        version: str,
        dependencies: set[str],
        repository_commit: str = REPOSITORY_COMMIT,
    ) -> bytes:
        package = ElementTree.Element("package")
        metadata = ElementTree.SubElement(package, "metadata")
        for name, value in (
            ("id", package_id),
            ("version", version),
            ("authors", "Backend Safety Kit"),
            ("description", "Test fixture package."),
            ("projectUrl", REPOSITORY_URL),
            ("tags", "backend;security;tests"),
            ("readme", "README.md"),
        ):
            ElementTree.SubElement(metadata, name).text = value

        ElementTree.SubElement(metadata, "license", {"type": "expression"}).text = "MIT"
        ElementTree.SubElement(
            metadata,
            "repository",
            {"type": "git", "url": REPOSITORY_URL, "commit": repository_commit},
        )

        if dependencies:
            dependency_group = ElementTree.SubElement(
                ElementTree.SubElement(metadata, "dependencies"),
                "group",
                {"targetFramework": "net10.0"},
            )
            for dependency_id in sorted(dependencies):
                ElementTree.SubElement(
                    dependency_group,
                    "dependency",
                    {"id": dependency_id, "version": "1.0.0"},
                )

        return ElementTree.tostring(package, encoding="utf-8", xml_declaration=True)

    def _write_package(
        self,
        package_id: str,
        dependencies: set[str],
        *,
        include_xml: bool = True,
        invalid_xml: bool = False,
        repository_commit: str = REPOSITORY_COMMIT,
        include_symbols: bool = True,
        symbol_version: str | None = None,
    ) -> None:
        package_path = self.output / f"{package_id}.{PACKAGE_VERSION}.nupkg"
        with zipfile.ZipFile(package_path, "w", zipfile.ZIP_DEFLATED) as archive:
            archive.writestr(
                f"{package_id}.nuspec",
                self._write_nuspec(
                    package_id, PACKAGE_VERSION, dependencies, repository_commit
                ),
            )
            archive.writestr("README.md", b"# Test package\n")
            archive.writestr("LICENSE", b"MIT License\n")
            archive.writestr(f"lib/net10.0/{package_id}.dll", b"assembly placeholder")
            if include_xml:
                xml_content = b"<doc><members><member name='T:Fixture'/></members></doc>"
                archive.writestr(
                    f"lib/net10.0/{package_id}.xml",
                    b"<doc>" if invalid_xml else xml_content,
                )

        if include_symbols:
            symbols_path = self.output / f"{package_id}.{PACKAGE_VERSION}.snupkg"
            with zipfile.ZipFile(symbols_path, "w", zipfile.ZIP_DEFLATED) as archive:
                archive.writestr(
                    f"{package_id}.nuspec",
                    self._write_nuspec(
                        package_id,
                        symbol_version or PACKAGE_VERSION,
                        dependencies,
                        repository_commit,
                    ),
                )
                archive.writestr(f"lib/net10.0/{package_id}.pdb", b"portable pdb placeholder")

    def _populate(self, overrides: dict[str, dict[str, object]] | None = None) -> None:
        overrides = overrides or {}
        for package_id, expected_dependencies in validator.EXPECTED.items():
            arguments = {"dependencies": expected_dependencies}
            arguments.update(overrides.get(package_id, {}))
            self._write_package(package_id, **arguments)

    def _run_validator(self) -> None:
        original_argv = sys.argv
        try:
            sys.argv = ["validate_nuget_packages.py", str(self.output)]
            with redirect_stdout(io.StringIO()):
                validator.main()
        finally:
            sys.argv = original_argv

    def test_accepts_all_expected_packages_and_matching_symbol_archives(self) -> None:
        self._populate()
        self._run_validator()

    def test_rejects_missing_package(self) -> None:
        for package_id, dependencies in validator.EXPECTED.items():
            if package_id != "BackendSafetyKit.OpenTelemetry":
                self._write_package(package_id, dependencies)

        with self.assertRaisesRegex(ValueError, "missing IDs"):
            self._run_validator()

    def test_rejects_missing_xml_documentation(self) -> None:
        self._populate({"BackendSafetyKit": {"include_xml": False}})
        with self.assertRaisesRegex(ValueError, "missing required package files"):
            self._run_validator()

    def test_rejects_malformed_xml_documentation(self) -> None:
        self._populate({"BackendSafetyKit": {"invalid_xml": True}})
        with self.assertRaises(ElementTree.ParseError):
            self._run_validator()

    def test_rejects_unexpected_runtime_dependency(self) -> None:
        self._populate({"BackendSafetyKit": {"dependencies": {"Unexpected.Dependency"}}})
        with self.assertRaisesRegex(ValueError, "dependency boundary changed"):
            self._run_validator()

    def test_rejects_missing_symbol_archive(self) -> None:
        self._populate({"BackendSafetyKit.AspNetCore": {"include_symbols": False}})
        with self.assertRaisesRegex(ValueError, "Missing symbol package"):
            self._run_validator()

    def test_rejects_symbol_archive_with_different_version(self) -> None:
        self._populate({"BackendSafetyKit": {"symbol_version": "9.9.9"}})
        with self.assertRaisesRegex(ValueError, "does not match"):
            self._run_validator()

    def test_rejects_missing_repository_commit(self) -> None:
        self._populate({"BackendSafetyKit": {"repository_commit": ""}})
        with self.assertRaisesRegex(ValueError, "40-character repository commit"):
            self._run_validator()


if __name__ == "__main__":
    unittest.main()
