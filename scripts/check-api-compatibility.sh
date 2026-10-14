#!/usr/bin/env bash
set -euo pipefail

base_sha="${1:-${BASE_SHA:-}}"
if [[ -z "$base_sha" ]]; then
  echo "Usage: check-api-compatibility.sh <base-commit-sha>" >&2
  exit 2
fi

repo_root="${GITHUB_WORKSPACE:-$(cd "$(dirname "$0")/.." && pwd)}"
temp_root="${RUNNER_TEMP:-${TMPDIR:-/tmp}}"
baseline_dir="$temp_root/backend-safety-api-baseline"
tool_dir="$temp_root/backend-safety-apicompat-tool"

rm -rf "$baseline_dir" "$tool_dir"
mkdir -p "$tool_dir"
git -C "$repo_root" worktree add --detach "$baseline_dir" "$base_sha"
cleanup() {
  git -C "$repo_root" worktree remove --force "$baseline_dir" >/dev/null 2>&1 || true
}
trap cleanup EXIT

dotnet restore "$baseline_dir/BackendSafetyKit.slnx"
dotnet build "$baseline_dir/BackendSafetyKit.slnx" --configuration Release --no-restore

dotnet tool install --tool-path "$tool_dir" Microsoft.DotNet.ApiCompat.Tool --version 10.0.301

compare_api() {
  local assembly="$1"
  local project="$2"
  local baseline="$baseline_dir/$project/bin/Release/net10.0/$assembly.dll"
  local candidate="$repo_root/$project/bin/Release/net10.0/$assembly.dll"

  if [[ ! -f "$baseline" ]]; then
    echo "Missing baseline assembly: $baseline" >&2
    exit 1
  fi
  if [[ ! -f "$candidate" ]]; then
    echo "Missing candidate assembly: $candidate" >&2
    exit 1
  fi

  echo "Checking public API compatibility for $assembly"
  "$tool_dir/apicompat" --left "$baseline" --right "$candidate"
}

compare_api "BackendSafetyKit" "src/BackendSafetyKit"
compare_api "BackendSafetyKit.AspNetCore" "src/BackendSafetyKit.AspNetCore"
compare_api "BackendSafetyKit.OpenTelemetry" "src/BackendSafetyKit.OpenTelemetry"
