from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import check_dotnet_vulnerabilities as scanner


class DotNetVulnerabilityReportTests(unittest.TestCase):
    def test_clean_report_has_no_findings(self) -> None:
        report = {
            "projects": [
                {
                    "frameworks": [
                        {
                            "topLevelPackages": [{"id": "Example.Core", "vulnerabilities": []}],
                            "transitivePackages": [{"id": "Example.Dependency", "vulnerabilities": []}],
                        }
                    ]
                }
            ]
        }
        self.assertEqual([], scanner.find_vulnerabilities(report))

    def test_detects_direct_package_vulnerability(self) -> None:
        report = {
            "id": "Example.Core",
            "vulnerabilities": [
                {"severity": "High", "advisoryurl": "https://example.test/advisory/1"}
            ],
        }
        findings = scanner.find_vulnerabilities(report)
        self.assertEqual(1, len(findings))
        self.assertIn("Example.Core", findings[0])
        self.assertIn("High", findings[0])
        self.assertIn("https://example.test/advisory/1", findings[0])

    def test_detects_nested_transitive_vulnerability(self) -> None:
        report = {
            "projects": [
                {
                    "frameworks": [
                        {
                            "transitivePackages": [
                                {
                                    "id": "Example.Transitive",
                                    "vulnerabilities": [
                                        {"severity": "Moderate", "url": "https://example.test/2"}
                                    ],
                                }
                            ]
                        }
                    ]
                }
            ]
        }
        findings = scanner.find_vulnerabilities(report)
        self.assertEqual(1, len(findings))
        self.assertIn("Example.Transitive", findings[0])

    def test_ignores_non_vulnerability_fields(self) -> None:
        self.assertEqual(
            [],
            scanner.find_vulnerabilities(
                {"packages": [{"id": "Safe.Package", "version": "1.2.3"}]}
            ),
        )

    def test_invalid_json_report_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            report_path = Path(temp_dir) / "report.json"
            report_path.write_text("{broken", encoding="utf-8")
            previous_argv = sys.argv
            try:
                sys.argv = ["check_dotnet_vulnerabilities.py", str(report_path)]
                self.assertEqual(2, scanner.main())
            finally:
                sys.argv = previous_argv


if __name__ == "__main__":
    unittest.main()
