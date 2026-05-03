#!/usr/bin/env bash
# Cross-platform test entry point. Runs everywhere dotnet runs.
set -euo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

dotnet test "${REPO_ROOT}/src/PACdToolbox.slnx" -c Release --nologo
