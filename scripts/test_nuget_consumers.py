#!/usr/bin/env python3
"""Restore and compile clean consumer apps using the actual packed NuGet artifacts."""
from __future__ import annotations

import json
import shutil
import subprocess
import sys
import tempfile
import zipfile
from html import escape
from pathlib import Path
from xml.etree import ElementTree


EXPECTED_PACKAGES = {
    "BackendSafetyKit",
    "BackendSafetyKit.AspNetCore",
    "BackendSafetyKit.OpenTelemetry",
    "BackendSafetyKit.Redis",
    "BackendSafetyKit.EntityFrameworkCore",
}
NUGET_ORG = "https://api.nuget.org/v3/index.json"

# Each consumer references exactly one shipping package and uses a public API.
CONSUMERS = {
    "BackendSafetyKit": {
        "project_sdk": "Microsoft.NET.Sdk.Web",
        "program": """using BackendSafetyKit;

Console.WriteLine(typeof(ISensitiveDataMasker).FullName);
""",
    },
    "BackendSafetyKit.AspNetCore": {
        "project_sdk": "Microsoft.NET.Sdk.Web",
        "program": """using BackendSafetyKit;
using BackendSafetyKit.AspNetCore.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddBackendSafety();
var app = builder.Build();
app.UseBackendSafety();
app.MapGet("/", () => Results.Ok("consumer-smoke"));
app.Run();
""",
    },
    "BackendSafetyKit.OpenTelemetry": {
        "project_sdk": "Microsoft.NET.Sdk.Web",
        "program": """using BackendSafetyKit.OpenTelemetry.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddBackendSafetyOpenTelemetry();
var app = builder.Build();
app.MapGet("/", () => Results.Ok("consumer-smoke"));
app.Run();
""",
    },
    "BackendSafetyKit.Redis": {
        "project_sdk": "Microsoft.NET.Sdk.Web",
        "program": """using BackendSafetyKit.Redis.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddBackendSafetyRedisDiagnostics(options =>
{
    options.StreamKey = "consumer-smoke";
    options.QueueCapacity = 8;
    options.MaxStreamLength = 32;
});
var app = builder.Build();
app.MapGet("/", () => Results.Ok("consumer-smoke"));
app.Run();
""",
    },
    "BackendSafetyKit.EntityFrameworkCore": {
        "project_sdk": "Microsoft.NET.Sdk.Web",
        "program": """using BackendSafetyKit.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

var options = new DbContextOptionsBuilder()
    .AddBackendSafetyEntityFrameworkCore();
Console.WriteLine(options.Options.GetType().FullName);
""",
    },
}

OPTIONAL_BOUNDARIES = {
    "BackendSafetyKit": {
        "BackendSafetyKit.Redis",
        "BackendSafetyKit.EntityFrameworkCore",
        "StackExchange.Redis",
        "Microsoft.EntityFrameworkCore",
    },
    "BackendSafetyKit.AspNetCore": {
        "BackendSafetyKit.Redis",
        "BackendSafetyKit.EntityFrameworkCore",
        "StackExchange.Redis",
        "Microsoft.EntityFrameworkCore",
    },
    "BackendSafetyKit.OpenTelemetry": {
        "BackendSafetyKit.Redis",
        "BackendSafetyKit.EntityFrameworkCore",
        "StackExchange.Redis",
        "Microsoft.EntityFrameworkCore",
    },
    "BackendSafetyKit.Redis": {
        "BackendSafetyKit.EntityFrameworkCore",
        "Microsoft.EntityFrameworkCore",
    },
    "BackendSafetyKit.EntityFrameworkCore": {
        "BackendSafetyKit.Redis",
        "StackExchange.Redis",
    },
}


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def package_identity(path: Path) -> tuple[str, str]:
    try:
        with zipfile.ZipFile(path) as archive:
            nuspecs = [name for name in archive.namelist() if name.lower().endswith(".nuspec")]
            if len(nuspecs) != 1:
                raise ValueError(f"expected one .nuspec, found {nuspecs}")
            root = ElementTree.fromstring(archive.read(nuspecs[0]))
    except (OSError, zipfile.BadZipFile, ElementTree.ParseError) as exc:
        raise ValueError(f"cannot read package metadata in {path.name}: {exc}") from exc

    metadata = next(
        (item for item in root if local_name(item.tag) == "metadata"),
        None,
    )
    if metadata is None:
        raise ValueError(f"{path.name} has no nuspec metadata")
    values = {
        local_name(item.tag): (item.text or "").strip()
        for item in metadata
    }
    package_id = values.get("id", "")
    version = values.get("version", "")
    if not package_id or not version:
        raise ValueError(f"{path.name} is missing package ID or version")
    return package_id, version


def run(command: list[str], *, cwd: Path, label: str) -> None:
    completed = subprocess.run(
        command,
        cwd=cwd,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        check=False,
    )
    if completed.returncode != 0:
        print(f"\nFAILED: {label}\nCommand: {' '.join(command)}", file=sys.stderr)
        print(completed.stdout, file=sys.stderr)
        raise RuntimeError(f"{label} exited with code {completed.returncode}")


def consumer_project(package_id: str, version: str, sdk: str) -> str:
    return f"""<Project Sdk="{sdk}">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="{package_id}" Version="{version}" />
  </ItemGroup>
</Project>
"""


def verify_boundaries(package_id: str, project_dir: Path) -> None:
    assets_path = project_dir / "obj" / "project.assets.json"
    if not assets_path.is_file():
        raise RuntimeError(f"{package_id}: restore did not produce {assets_path}")
    assets = json.loads(assets_path.read_text(encoding="utf-8"))
    libraries = assets.get("libraries", {})
    resolved = {
        key.split("/", 1)[0].casefold()
        for key, item in libraries.items()
        if item.get("type") == "package"
    }
    expected = package_id.casefold()
    if expected not in resolved:
        raise RuntimeError(
            f"{package_id}: its package does not appear in project.assets.json"
        )
    forbidden = {value.casefold() for value in OPTIONAL_BOUNDARIES[package_id]}
    leaked = sorted(forbidden & resolved)
    if leaked:
        raise RuntimeError(
            f"{package_id}: optional dependency boundary violated; "
            f"unexpected resolved packages: {leaked}"
        )


def main() -> int:
    output = Path(sys.argv[1] if len(sys.argv) > 1 else "./artifacts").resolve()
    if not output.is_dir():
        print(f"Package output directory does not exist: {output}", file=sys.stderr)
        return 2

    discovered: dict[str, tuple[Path, str]] = {}
    for path in sorted(output.glob("*.nupkg")):
        package_id, version = package_identity(path)
        if package_id in discovered:
            raise ValueError(f"Multiple packages found for {package_id}")
        discovered[package_id] = (path, version)

    actual = set(discovered)
    if actual != EXPECTED_PACKAGES:
        raise ValueError(
            f"Unexpected package set: missing={sorted(EXPECTED_PACKAGES - actual)}, "
            f"extra={sorted(actual - EXPECTED_PACKAGES)}"
        )
    versions = {version for _, version in discovered.values()}
    if len(versions) != 1:
        raise ValueError(f"Shipping packages do not share one version: {sorted(versions)}")

    with tempfile.TemporaryDirectory(prefix="backend-safety-consumer-smoke-") as temp_name:
        workspace = Path(temp_name)
        feed = workspace / "feed"
        feed.mkdir()
        for package_path, _ in discovered.values():
            shutil.copy2(package_path, feed / package_path.name)

        config_path = workspace / "NuGet.Config"
        config_path.write_text(
            f"""<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-artifacts" value="{escape(str(feed), quote=True)}" />
    <add key="nuget.org" value="{NUGET_ORG}" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local-artifacts">
      <package pattern="BackendSafetyKit*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
""",
            encoding="utf-8",
        )

        for package_id, details in CONSUMERS.items():
            _, version = discovered[package_id]
            project_dir = workspace / package_id.replace(".", "-")
            project_dir.mkdir()
            project_file = project_dir / "Consumer.csproj"
            project_file.write_text(
                consumer_project(package_id, version, details["project_sdk"]),
                encoding="utf-8",
            )
            (project_dir / "Program.cs").write_text(details["program"], encoding="utf-8")

            print(f"[{package_id}] restore {version}")
            run(
                [
                    "dotnet", "restore", str(project_file),
                    "--configfile", str(config_path),
                    "--verbosity", "minimal",
                ],
                cwd=project_dir,
                label=f"{package_id} consumer restore",
            )
            verify_boundaries(package_id, project_dir)

            print(f"[{package_id}] build")
            run(
                [
                    "dotnet", "build", str(project_file),
                    "--configuration", "Release",
                    "--no-restore",
                    "--verbosity", "minimal",
                ],
                cwd=project_dir,
                label=f"{package_id} consumer build",
            )
            print(f"Passed consumer smoke test: {package_id} {version}")

    print(
        f"Successfully restored and compiled {len(CONSUMERS)} clean consumers "
        f"(version {next(iter(versions))}) from local artifacts."
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, RuntimeError, KeyError, json.JSONDecodeError) as exc:
        print(f"NuGet consumer smoke tests failed: {exc}", file=sys.stderr)
        raise SystemExit(1)
