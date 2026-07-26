#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
guard="${repo_root}/scripts/verify-postgresql-only.sh"
temp_dir="$(mktemp -d)"

cleanup() {
  local status=$?
  rm -rf "${temp_dir}"
  exit "${status}"
}
trap cleanup EXIT

retired_engine="Sql""Server"
retired_engine_upper="SQL ""Server"
retired_engine_lower="sql""server"
retired_engine_hyphen="SQL-""Server"
retired_engine_underscore="SQL_""Server"
retired_engine_double_space="SQL  ""Server"
retired_client="Sql""Client"
retired_connection="Sql""Connection"
retired_bulk_copy="Sql""BulkCopy"
ef_provider_package="Microsoft.EntityFrameworkCore.${retired_engine}"
data_client_package="Microsoft.Data.${retired_client}"
use_provider="Use${retired_engine}"

create_fixture() {
  local fixture="$1"

  mkdir -p "${fixture}/src" "${fixture}/docs"
  cat > "${fixture}/src/App.cs" <<'EOF'
using Npgsql;

var connection = new NpgsqlConnection("Host=localhost;Database=rvt");
EOF
  cat > "${fixture}/src/App.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
  </ItemGroup>
</Project>
EOF
  printf 'PostgreSQL is the supported relational database.\n' > "${fixture}/docs/database.md"

  git init -q "${fixture}"
  git -C "${fixture}" add -A
}

assert_rejected() {
  local name="$1"
  local relative_path="$2"
  local content="$3"
  local expected_rule="${4:-}"
  local fixture="${temp_dir}/${name}"
  local output

  create_fixture "${fixture}"
  mkdir -p "$(dirname "${fixture}/${relative_path}")"
  printf '%s\n' "${content}" > "${fixture}/${relative_path}"
  git -C "${fixture}" add -A

  if output="$("${guard}" "${fixture}" 2>&1)"; then
    printf 'FAIL: %s mutation must be rejected.\n' "${name}" >&2
    exit 1
  fi

  if [[ "${output}" != *"${relative_path}"* || "${output}" != *"rule:"* ]]; then
    printf 'FAIL: %s mutation must report its path and matched rule, got:\n%s\n' \
      "${name}" "${output}" >&2
    exit 1
  fi

  if [[ -n "${expected_rule}" && "${output}" != *"[rule: ${expected_rule}]"* ]]; then
    printf 'FAIL: %s mutation must report rule %s, got:\n%s\n' \
      "${name}" "${expected_rule}" "${output}" >&2
    exit 1
  fi
}

positive_fixture="${temp_dir}/positive"
create_fixture "${positive_fixture}"
set +e
"${guard}" "${positive_fixture}"
guard_status=$?
set -e
if (( guard_status != 0 )); then
  exit "${guard_status}"
fi

assert_rejected \
  project-package \
  src/Legacy.csproj \
  "<PackageReference Include=\"${ef_provider_package}\" Version=\"10.0.0\" />"
assert_rejected \
  transitive-lock-entry \
  src/packages.lock.json \
  "{ \"net10.0\": { \"${data_client_package}\": { \"type\": \"Transitive\", \"resolved\": \"6.0.0\" } } }"
assert_rejected \
  csharp-api \
  src/Legacy.cs \
  "options.${use_provider}(connectionString);"
assert_rejected \
  csharp-api-connection \
  src/LegacyConnection.cs \
  "var connection = new ${retired_connection}(connectionString);"
assert_rejected \
  csharp-api-bulk-copy \
  src/LegacyBulkCopy.cs \
  "using var bulkCopy = new ${retired_bulk_copy}(connectionString);"
assert_rejected \
  provider-configuration \
  src/appsettings.json \
  "{ \"Database\": { \"Provider\": \"${retired_engine}\" } }"
assert_rejected \
  provider-conditional-migration \
  apps/portal/RVT.DataAccess/Migrations/LegacyMigration.cs \
  "if (ActiveProvider.Contains(\"Npgsql\")) { }" \
  "provider-conditional EF migration"
assert_rejected \
  prose \
  docs/legacy.md \
  "Use ${retired_engine_upper} for production deployments."
assert_rejected \
  prose-hyphen \
  docs/legacy-hyphen.md \
  "Use ${retired_engine_hyphen} for production deployments."
assert_rejected \
  prose-underscore \
  docs/legacy-underscore.md \
  "Use ${retired_engine_underscore} for production deployments."
assert_rejected \
  prose-double-space \
  docs/legacy-double-space.md \
  "Use ${retired_engine_double_space} for production deployments."
assert_rejected \
  retired-engine-directory \
  "apps/portal/database/${retired_engine_lower}/legacy.sql" \
  'select 1;' \
  "retired database/${retired_engine_lower} path"
assert_rejected \
  retired-engine-script \
  "database/legacy.schema.${retired_engine_lower}.sql" \
  'select 1;' \
  "retired .${retired_engine_lower}.sql script path"

printf 'PostgreSQL-only guard fixtures verified.\n'
