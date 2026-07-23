#!/usr/bin/env bash
#
# share-dev-database.sh - export/restore the dockerised RVT dev database for sharing between dev teams.
#
# Major updates:
# - 2026-07-16 pending Added so dev teams can move a working schema+data snapshot without hand-rolled pg_dump flags.
#
# WHY THIS EXISTS
#
# A plain `pg_dump` of this database is usable but has two sharp edges:
#
#   1. It warns "there are circular foreign-key constraints on this table: continuous_agg". That cycle is a
#      self-referencing FK inside TimescaleDB's OWN catalog (_timescaledb_catalog.continuous_agg, which supports
#      nested continuous aggregates). It is not an RVT table - the application schema has no FK cycles at all.
#      The warning is expected on a full dump and is safe: it only bites on a --data-only restore.
#   2. A Timescale database must be restored through timescaledb_pre_restore()/timescaledb_post_restore(),
#      which is exactly what makes that catalog ordering work. Restoring without them is what actually breaks.
#
# This script does both correctly and records the source versions, because a Timescale dump only restores into a
# matching extension version.
#
# DATA NOTICE
#
# The export is deliberately verbatim: it contains the real contents of the dev database, including personal data
# (user email addresses and phone numbers). Handle the artifact accordingly and only share it with teams already
# authorised to process that data. It is written OUTSIDE the repository on purpose - never commit it, and never
# let it reach the client release export.
#
# USAGE
#
#   ./share-dev-database.sh export  [--container NAME] [--db NAME] [--out DIR] [--label TEXT]
#   ./share-dev-database.sh restore --file FILE [--container NAME] [--db NAME] [--force] [--keep-ownership]
#
#   export   Dumps schema + all data (including the Timescale hypertable chunks) to a compressed custom-format
#            file, writes a .info manifest beside it, and verifies the archive is readable.
#   restore  Recreates the database in the target container using the Timescale pre/post-restore procedure.
#
set -euo pipefail

CONTAINER="rvt-timescaledb"
DB="rvt"
PGUSER="postgres"
OUT_DIR="${HOME}/Documents/rvt-send"
LABEL=""
FILE=""
FORCE=0
KEEP_OWNERSHIP=0

die() { printf 'error: %s\n' "$*" >&2; exit 1; }
note() { printf '  %s\n' "$*"; }

usage() {
    sed -n '2,40p' "$0" | sed 's/^# \{0,1\}//'
    exit "${1:-0}"
}

require_container() {
    docker inspect -f '{{.State.Running}}' "$CONTAINER" 2>/dev/null | grep -q true \
        || die "container '$CONTAINER' is not running (docker ps)."
}

# Reports "<server_version>|<timescaledb_version>" for a database in the container.
versions_of() {
    local db="$1"
    docker exec "$CONTAINER" psql -U "$PGUSER" -d "$db" -tAc \
        "SELECT current_setting('server_version') || '|' || COALESCE((SELECT extversion FROM pg_extension WHERE extname='timescaledb'),'none');" \
        2>/dev/null | tr -d '[:space:]'
}

cmd_export() {
    require_container
    docker exec "$CONTAINER" psql -U "$PGUSER" -d "$DB" -tAc 'SELECT 1' >/dev/null 2>&1 \
        || die "cannot connect to database '$DB' in '$CONTAINER'."

    local vers pg_ver ts_ver stamp base remote
    vers="$(versions_of "$DB")"
    pg_ver="${vers%%|*}"
    ts_ver="${vers##*|}"
    stamp="$(date -u +%Y%m%d-%H%M%S)"
    base="rvt-dev-${LABEL:+${LABEL}-}${stamp}"
    remote="/tmp/${base}.dump"

    mkdir -p "$OUT_DIR"

    echo "Exporting ${DB} from ${CONTAINER}"
    note "PostgreSQL : ${pg_ver}"
    note "TimescaleDB: ${ts_ver}"
    note "Scope      : schema + all data (including Timescale hypertable chunks)"
    echo

    # -Fc: compressed custom format, so the restore can be parallelised and is version-tolerant within a major.
    # The continuous_agg circular-FK warning below is expected; see the header. Errors still surface.
    local errlog; errlog="$(mktemp)"
    if ! docker exec "$CONTAINER" pg_dump -U "$PGUSER" -d "$DB" -Fc -f "$remote" 2>"$errlog"; then
        cat "$errlog" >&2; rm -f "$errlog"; die "pg_dump failed."
    fi
    if grep -q 'circular foreign-key' "$errlog"; then
        note "note: pg_dump reported the expected TimescaleDB continuous_agg circular-FK warning - safe, see header."
    fi
    grep -v -e 'circular foreign-key' -e 'continuous_agg' -e 'You might not be able to restore' \
         -e 'Consider using a full dump' -e '^pg_dump: detail' -e '^pg_dump: hint' "$errlog" >&2 || true
    rm -f "$errlog"

    # Verify the archive inside the container, where the postgres client tools are guaranteed to exist (the host
    # generally has none). A truncated or non-archive file fails here rather than at the far end, days later.
    local toc_entries
    toc_entries="$(docker exec "$CONTAINER" pg_restore -l "$remote" 2>/dev/null | grep -c ';' || true)"
    [ "${toc_entries:-0}" -gt 0 ] || { docker exec "$CONTAINER" rm -f "$remote" || true; die "the produced archive is not readable by pg_restore."; }

    docker cp "${CONTAINER}:${remote}" "${OUT_DIR}/${base}.dump" >/dev/null
    docker exec "$CONTAINER" rm -f "$remote" || true

    local dump="${OUT_DIR}/${base}.dump"
    [ -s "$dump" ] || die "the copied archive is empty: $dump"

    local size sha
    size="$(du -h "$dump" | cut -f1)"
    sha="$(shasum -a 256 "$dump" | cut -d' ' -f1)"

    cat > "${OUT_DIR}/${base}.info" <<EOF
RVT dev database snapshot
=========================
source database : ${DB}
taken (UTC)     : ${stamp}
postgres        : ${pg_ver}
timescaledb     : ${ts_ver}
scope           : schema + all data, including Timescale hypertable chunks
format          : pg_dump custom (-Fc)
size            : ${size}
sha256          : ${sha}

CONTAINS PERSONAL DATA (user email addresses and phone numbers). Handle per your data agreement.

Restore (TimescaleDB requires the pre/post-restore procedure - a plain pg_restore is what breaks):

    ./share-dev-database.sh restore --file ${base}.dump --db ${DB}

or manually:

    psql -c "CREATE DATABASE ${DB};"
    psql -d ${DB} -c "CREATE EXTENSION IF NOT EXISTS timescaledb;"
    psql -d ${DB} -c "SELECT timescaledb_pre_restore();"
    pg_restore -d ${DB} --no-owner --no-privileges ${base}.dump
    psql -d ${DB} -c "SELECT timescaledb_post_restore();"

The target must run TimescaleDB ${ts_ver} (a Timescale dump does not restore into a different extension version).
EOF

    echo "Export complete."
    note "dump : ${dump}  (${size})"
    note "info : ${OUT_DIR}/${base}.info"
    note "sha256: ${sha}"
    echo
    note "This file contains real personal data - share only with teams authorised to process it."
    note "Do not commit it; it lives outside the repository on purpose."
}

cmd_restore() {
    [ -n "$FILE" ] || die "restore needs --file <dump>."
    [ -f "$FILE" ] || die "no such file: $FILE"
    require_container

    local target_vers target_ts
    target_vers="$(versions_of postgres)"
    target_ts="${target_vers##*|}"

    local info="${FILE%.dump}.info" src_ts=""
    if [ -f "$info" ]; then
        src_ts="$(awk -F': *' '/^timescaledb/{print $2; exit}' "$info" | tr -d '[:space:]')"
    fi
    if [ -n "$src_ts" ] && [ "$src_ts" != "$target_ts" ]; then
        die "TimescaleDB mismatch: dump is from ${src_ts}, target container has ${target_ts}. A Timescale dump does not restore across extension versions."
    fi

    if docker exec "$CONTAINER" psql -U "$PGUSER" -d postgres -tAc \
        "SELECT 1 FROM pg_database WHERE datname='${DB}'" | grep -q 1; then
        [ "$FORCE" -eq 1 ] || die "database '${DB}' already exists in '${CONTAINER}'. Pass --force to drop and recreate it."
        echo "Dropping existing database '${DB}'..."
        docker exec "$CONTAINER" psql -U "$PGUSER" -d postgres -c \
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='${DB}' AND pid<>pg_backend_pid();" >/dev/null
        docker exec "$CONTAINER" psql -U "$PGUSER" -d postgres -c "DROP DATABASE ${DB};" >/dev/null
    fi

    echo "Restoring ${FILE} into ${CONTAINER}:${DB}"
    docker exec "$CONTAINER" psql -U "$PGUSER" -d postgres -c "CREATE DATABASE ${DB};" >/dev/null
    docker exec "$CONTAINER" psql -U "$PGUSER" -d "$DB" -c "CREATE EXTENSION IF NOT EXISTS timescaledb;" >/dev/null

    # These two calls are the whole point: they put Timescale into a restoring state so its catalog (including the
    # circular continuous_agg FK) and the chunk metadata load in a consistent order.
    docker exec "$CONTAINER" psql -U "$PGUSER" -d "$DB" -tAc "SELECT timescaledb_pre_restore();" >/dev/null
    note "timescaledb_pre_restore() done"

    local restore_args=(--no-owner --no-privileges)
    [ "$KEEP_OWNERSHIP" -eq 1 ] && restore_args=()

    # Keep --exit-on-error off so pg_restore can report every archive issue, but never convert its aggregate
    # status into success. Capture it explicitly so strict mode cannot skip the diagnostic, then return the exact
    # pg_restore status without running post-restore success verification against a partial database.
    local restore_status=0
    docker exec -i "$CONTAINER" pg_restore -U "$PGUSER" -d "$DB" "${restore_args[@]}" < "$FILE" \
        || restore_status=$?
    if [ "$restore_status" -ne 0 ]; then
        printf 'error: pg_restore failed with status %d; restore is incomplete.\n' "$restore_status" >&2
        return "$restore_status"
    fi

    docker exec "$CONTAINER" psql -U "$PGUSER" -d "$DB" -tAc "SELECT timescaledb_post_restore();" >/dev/null
    note "timescaledb_post_restore() done"

    docker exec "$CONTAINER" psql -U "$PGUSER" -d "$DB" -c "ANALYZE;" >/dev/null
    note "ANALYZE done"

    local verification public_tables hypertables
    verification="$(docker exec "$CONTAINER" psql -U "$PGUSER" -d "$DB" -tA -F '|' -c \
        "SELECT (SELECT count(*) FROM pg_tables WHERE schemaname='public'),
                (SELECT count(*) FROM timescaledb_information.hypertables);")" \
        || die "restore verification query failed."
    IFS='|' read -r public_tables hypertables <<< "$verification"

    [[ "$public_tables" =~ ^[0-9]+$ ]] || die "restore verification returned an invalid public table count: '${public_tables}'."
    [[ "$hypertables" =~ ^[0-9]+$ ]] || die "restore verification returned an invalid hypertable count: '${hypertables}'."
    [ "$public_tables" -gt 0 ] || die "restore verification found no public tables."
    [ "$hypertables" -gt 0 ] || die "restore verification found no TimescaleDB hypertables."

    echo
    echo "Restore complete. Sanity check:"
    note "public tables: ${public_tables}"
    note "hypertables  : ${hypertables}"
}

[ $# -ge 1 ] || usage 1
MODE="$1"; shift
while [ $# -gt 0 ]; do
    case "$1" in
        --container) CONTAINER="$2"; shift 2 ;;
        --db)        DB="$2"; shift 2 ;;
        --user)      PGUSER="$2"; shift 2 ;;
        --out)       OUT_DIR="$2"; shift 2 ;;
        --label)     LABEL="$2"; shift 2 ;;
        --file)      FILE="$2"; shift 2 ;;
        --force)     FORCE=1; shift ;;
        --keep-ownership) KEEP_OWNERSHIP=1; shift ;;
        -h|--help)   usage 0 ;;
        *) die "unknown option: $1" ;;
    esac
done

case "$MODE" in
    export)  cmd_export ;;
    restore) cmd_restore ;;
    -h|--help) usage 0 ;;
    *) die "unknown mode '$MODE' (expected: export | restore)" ;;
esac
