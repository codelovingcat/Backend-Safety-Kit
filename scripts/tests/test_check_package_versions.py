from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import check_package_versions as policy


class PackageVersionPolicyTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp_dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp_dir.cleanup)
        self.root = Path(self.temp_dir.name)
        for package_id, relative_path in policy.PROJECTS.items():
            path = self.root / relative_path
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(
                "<Project><PropertyGroup>"
                f"<PackageId>{package_id}</PackageId><Version>0.1.0</Version>"
                "</PropertyGroup></Project>",
                encoding="utf-8",
            )

    def test_accepts_matching_shipping_package_versions(self) -> None:
        self.assertEqual("0.1.0", policy.validate_versions(self.root))

    def test_accepts_matching_release_tag(self) -> None:
        self.assertEqual("0.1.0", policy.validate_versions(self.root, "v0.1.0"))

    def test_accepts_semver_prerelease_tag(self) -> None:
        path = self.root / policy.PROJECTS["BackendSafetyKit"]
        path.write_text(
            path.read_text(encoding="utf-8").replace("0.1.0", "1.2.0-rc.1"),
            encoding="utf-8",
        )
        for package_id, relative_path in list(policy.PROJECTS.items())[1:]:
            project_path = self.root / relative_path
            project_path.write_text(
                project_path.read_text(encoding="utf-8").replace("0.1.0", "1.2.0-rc.1"),
                encoding="utf-8",
            )
        self.assertEqual("1.2.0-rc.1", policy.validate_versions(self.root, "v1.2.0-rc.1"))

    def test_rejects_inconsistent_package_versions(self) -> None:
        path = self.root / policy.PROJECTS["BackendSafetyKit.OpenTelemetry"]
        path.write_text(
            path.read_text(encoding="utf-8").replace("0.1.0", "0.2.0"),
            encoding="utf-8",
        )
        with self.assertRaisesRegex(ValueError, "must match"):
            policy.validate_versions(self.root)

    def test_rejects_invalid_semver(self) -> None:
        path = self.root / policy.PROJECTS["BackendSafetyKit"]
        path.write_text(
            path.read_text(encoding="utf-8").replace("0.1.0", "01.0.0"),
            encoding="utf-8",
        )
        with self.assertRaisesRegex(ValueError, "valid SemVer"):
            policy.validate_versions(self.root)

    def test_rejects_tag_mismatch(self) -> None:
        with self.assertRaisesRegex(ValueError, "does not match package version"):
            policy.validate_versions(self.root, "v0.2.0")

    def test_rejects_missing_project(self) -> None:
        (self.root / policy.PROJECTS["BackendSafetyKit"]).unlink()
        with self.assertRaisesRegex(ValueError, "Missing shipping project"):
            policy.validate_versions(self.root)


if __name__ == "__main__":
    unittest.main()
