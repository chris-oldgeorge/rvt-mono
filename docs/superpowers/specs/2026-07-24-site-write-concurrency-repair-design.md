# Site Write Concurrency Repair Design

## Goal

Make site archive and notification-setting writes safe under concurrent relational
requests without weakening the application boundary or changing the public HTTP
contract.

The durable invariants are:

- one `site_archived` row and one canonical archive blob per site;
- `site.archived = true` whenever canonical archive metadata exists;
- one complete `notification_setting` row per `site_user`;
- a request must never delete the blob named by committed archive metadata;
- failed or interrupted loser cleanup must be discoverable by a later archive
  retry; and
- `RVT.SchemaDeploy` must remain non-destructive and safe to rerun.

## Failure Modes Being Repaired

The original archive flow reads `site.archived`, exports a uniquely named blob,
and only then inserts metadata. Two stale readers can therefore export two
different blobs. A unique database claim prevents duplicate metadata, but a
best-effort delete after commit is not durable: a process crash or storage
failure leaves the losing blob untracked, and later requests skip cleanup after
they observe `site.archived = true`.

The transaction outcome can also be unknown when a connection drops during
commit. Treating every transaction exception as a rollback and deleting the
export can leave committed metadata pointing at a deleted blob.

Finally, deterministic duplicate-row cleanup is necessary once on an existing
database, but putting that cleanup in a rerunnable post-load script contradicts
`RVT.SchemaDeploy`'s contract that it never drops table data.

## Approaches Considered

### 1. Deterministic per-site blob key (approved)

New exports use one stable key, `<site-id>/site-archive.zip`, while temporary
workspaces remain unique per request. Concurrent exports may overwrite the same
candidate, but cannot create multiple new blob names. Relational metadata is
still the canonical claim.

This makes recovery derivable from `siteId` plus the canonical metadata URL, so
no new cleanup table or background worker is required.

### 2. Durable archive-attempt/outbox table

Register every unique export before upload, transition losers to cleanup
pending, and process them with leases or a background worker. This preserves
immutable per-attempt blobs and gives the strongest audit trail, but introduces
a new state machine, migration, worker lifecycle, stale-lease policy, and
operational monitoring. That is disproportionate when the public domain permits
only one archive per site.

### 3. Reserve metadata before export

Claim a pending archive row before touching storage, then finalize it after
upload. This prevents duplicate exporters but changes the meaning of archive
metadata and needs recovery for a process that dies while the row is pending.
It is more invasive than the deterministic-key design and creates a new
partially archived state.

## Approved Archive Flow

The archive read model includes the canonical archive URL alongside the
`Archived` flag.

For an active site:

1. Export to the stable per-site blob key. Each request still gets a unique local
   workspace.
2. In the existing unit-of-work transaction, atomically insert canonical
   metadata with provider-native conflict handling and set `site.archived`.
3. If this request owns the claim, return the committed detail.
4. If another request owns the same stable URL, return success without deleting
   anything.
5. If legacy metadata owns a different URL, delete only the derived stable
   candidate. A cleanup failure returns the existing external-service failure so
   the caller can retry.

For a site already marked archived, the request does not export again. It asks
the archive adapter to reconcile the derived stable candidate against the
canonical metadata URL. When the URLs differ, the adapter idempotently deletes
the stable candidate; when they match, it must not delete. This makes a cleanup
that failed, or was interrupted after a losing claim, discoverable after
`site.archived` becomes true.

The stable target is scoped to the configured archive account and container.
Existing metadata URLs and legacy uniquely named blobs remain valid and are not
renamed. A new stable candidate is deleted only when committed metadata names a
different canonical blob.

## Unknown Transaction Outcomes

After a metadata transaction exception, the service performs a non-cancelable
durable archive-state read:

- canonical URL equals the exported stable URL: the commit succeeded, so the
  blob is retained and the request completes from durable state;
- canonical URL names a different blob: this request lost, so only the stable
  candidate is eligible for idempotent cleanup;
- no canonical metadata, or verification itself fails: the service does not
  delete the candidate and rethrows the persistence failure.

Leaving one deterministic candidate is safer than deleting through an unknown
commit outcome. A later request with an active site overwrites/reuses that key
and retries the claim. A later request with canonical legacy metadata derives
and cleans the same key.

This is operation-specific state verification consistent with EF Core's
documented guidance for connection loss during commit.

## Notification Settings

The database model and migration add a unique index on
`notification_setting.site_user_id`. PostgreSQL and SQLite use
`INSERT ... ON CONFLICT ... DO UPDATE`; SQL Server uses a locked update followed
by a conditional insert. Each statement runs inside the existing unit of work,
so concurrent first writes converge to one complete row.

The InMemory provider retains its tracked compatibility path.

PostgreSQL/Npgsql is the canonical checked-in EF migration and model-snapshot
provider. SQL Server runtime atomic DML remains supported and structurally
tested, but this repair does not claim SQL Server migration-deployment closure.
A provider-specific SQL Server migration chain/snapshot and live integration
gate are separate work.

## Existing-Database Deployment

The canonical PostgreSQL EF migration is the only deployment path allowed to
delete duplicate relational rows. It locks the relevant tables, retains
notification settings by smallest UUID, retains the newest archive by
`create_date` then UUID, reconciles `site.archived`, and creates the two unique
indexes. Its down path only relaxes uniqueness. SQLite exercises migration
semantics in a focused test; SQL Server migration deployment is not certified by
this change.

The rerunnable PostgreSQL post-load script performs no `DELETE`. Under table
locks it first detects duplicate owner groups and raises an actionable error
directing the operator to apply the EF migration (or resolve the duplicates)
before rerunning. Only clean data reaches index creation/repair. Replacing a
non-unique index is allowed; dropping table data is not.

Historical blob URLs discarded by the one-time relational migration cannot be
deleted safely by SQL because database deployment has no storage credentials.
They remain an explicit operator/lifecycle audit item.

## Verification

RED coverage must prove:

- an unknown transaction outcome that committed the same URL never invokes
  cleanup and returns durable success;
- a losing cleanup failure is retried after `site.archived = true` without
  another export;
- two workspaces for one site have distinct local roots but the same blob key;
- concurrent archive requests leave one metadata row and one active blob key;
- concurrent notification first writes leave one complete readable row;
- migration cleanup is deterministic and precedes unique indexes;
- post-load SQL contains no data deletion, rejects duplicates with actionable
  guidance, and repairs indexes only after the guard; and
- PostgreSQL provider concurrency is exercised when
  `RVT_TEST_POSTGRES_CONNECTION` is configured, otherwise reported as skipped.

Run the full application and SPA tests, solution build, EF pending-model guard,
canonical PostgreSQL migration-script generation, `git diff --check`, and the
PostgreSQL-gated test when its environment is available. SQL Server runtime DML
remains under structural tests, without a migration-deployment claim.
