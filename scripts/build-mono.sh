#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

bash scripts/verify-postgresql-only.sh .

solution="${repo_root}/Rvt.Mono.slnx"

dotnet restore "${solution}" --disable-parallel
"${repo_root}/scripts/verify-engineering-standards.sh" --working-tree
dotnet build "${solution}" --no-restore --nologo -m:1
dotnet test "${solution}" --no-build --nologo
