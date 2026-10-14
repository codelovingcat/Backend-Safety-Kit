#!/usr/bin/env python3
"""Fail when the .NET SDK's machine-readable NuGet vulnerability report has findings."""
from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any


def find_vulnerabilities(document: Any) -> list[str]:
    findings: list[str] = []

    def visit(value: Any, location: str = "report") -> None:
        if isinstance(value, dict):
            vulnerability_key = next(
                (key for key in value if key.lower() == "vulnerabilities"),
                None,
            )
            if vulnerability_key is not None:
                entries = value[vulnerability_key]
                if isinstance(entries, list) and entries:
                    package = (
                        value.get("id")
                        or value.get("name")
                        or value.get("packageId")
                        or location
                    )
                    for entry in entries:
                        if isinstance(entry, dict):
                            severity = entry.get("severity") or entry.get("Severity") or "unknown severity"
                            url = (
                                entry.get("advisoryurl")
                                or entry.get("advisoryUrl")
                                or entry.get("url")
                                or entry.get("Url")
                                or "no advisory URL supplied"
                            )
                            findings.append(f"{package}: {severity} — {url}")
                        else:
                            findings.append(f"{package}: {entry}")

            for key, child in value.items():
                if vulnerability_key is not None and key == vulnerability_key:
                    continue
                visit(child, f"{location}.{key}")
        elif isinstance(value, list):
            for index, child in enumerate(value):
                visit(child, f"{location}[{index}]")

    visit(document)
    return findings


def main() -> int:
    report_path = Path(sys.argv[1] if len(sys.argv) > 1 else "./dependency-report.json")
    try:
        with report_path.open("r", encoding="utf-8") as stream:
            report = json.load(stream)
    except (OSError, json.JSONDecodeError) as exc:
        print(f"Could not read a valid NuGet vulnerability report: {exc}", file=sys.stderr)
        return 2

    findings = find_vulnerabilities(report)
    if findings:
        print(f"Found {len(findings)} vulnerable package reference(s):", file=sys.stderr)
        for finding in findings:
            print(f" - {finding}", file=sys.stderr)
        return 1

    print("NuGet vulnerability report contains no known vulnerable package references.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
