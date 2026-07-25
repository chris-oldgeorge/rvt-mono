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

sql_server="Sql""Server"
sql_server_upper="SQL ""Server"
sql_server_lower="sql""server"
sql_server_hyphen="SQL-""Server"
sql_server_underscore="SQL_""Server"
sql_client="Sql""Client"
sql_connection="Sql""Connection"
sql_bulk_copy="Sql""BulkCopy"
ef_provider_package="Microsoft.EntityFrameworkCore.${sql_server}"
data_client_package="Microsoft.Data.${sql_client}"
use_provider="Use${sql_server}"

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
  lockfile-package \
  src/packages.lock.json \
  "{ \"dependencies\": { \"${data_client_package}\": \"6.0.0\" } }"
assert_rejected \
  csharp-api \
  src/Legacy.cs \
  "options.${use_provider}(connectionString);"
assert_rejected \
  csharp-api-connection \
  src/LegacyConnection.cs \
  "var connection = new ${sql_connection}(connectionString);"
assert_rejected \
  csharp-api-bulk-copy \
  src/LegacyBulkCopy.cs \
  "using var bulkCopy = new ${sql_bulk_copy}(connectionString);"
assert_rejected \
  provider-configuration \
  src/appsettings.json \
  "{ \"Database\": { \"Provider\": \"${sql_server}\" } }"
assert_rejected \
  prose \
  docs/legacy.md \
  "Use ${sql_server_upper} for production deployments."
assert_rejected \
  prose-hyphen \
  docs/legacy-hyphen.md \
  "Use ${sql_server_hyphen} for production deployments."
assert_rejected \
  prose-underscore \
  docs/legacy-underscore.md \
  "Use ${sql_server_underscore} for production deployments."
assert_rejected \
  retired-path \
  "database/${sql_server_lower}/legacy.sql" \
  'select 1;'
assert_rejected \
  retired-path-space \
  "database/${sql_server_upper}/legacy.sql" \
  'select 1;'

printf 'PostgreSQL-only guard fixtures verified.\n'
