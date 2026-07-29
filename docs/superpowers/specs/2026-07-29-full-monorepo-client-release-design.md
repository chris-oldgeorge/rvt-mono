# Full Monorepo Client Release Design

**Date:** 2026-07-29
**Target repository:** `https://github.com/RVT-Group-LTD/rvt-monitors.git`
**Target branch:** `release-candidate`

## Problem

The existing monitor client-release scripts predate the monorepo. They assume
that monitor files live at the repository root, resolve the exclusion policy
from the wrong location, and copy file contents from the current working tree.
That combination can omit required monorepo content and can accidentally export
uncommitted local edits.

The client release now needs to provide a reviewable snapshot of the entire
monorepo while withholding internal development material, local state,
generated output, and saved secrets.

## Goals

- Publish a reproducible snapshot of one exact committed monorepo revision.
- Include the complete product, test, build, deployment, and operational source
  needed for client review.
- Preserve the monorepo layout so project references, Docker build contexts, and
  repository automation remain valid.
- Exclude internal development/session material and release mechanics.
- Fail closed when a saved credential, private key, or blocked path is detected.
- Make the source revision and exported file set independently auditable.
- Protect the remote branch from overwriting an unexpected concurrent update.

## Non-goals

- Rewriting or flattening the monorepo layout.
- Publishing uncommitted working-tree changes.
- Reproducing internal Git history in the client repository.
- Supplying runtime credentials or client environment configuration.

## Export Scope

The export starts from an exact Git commit and includes every tracked path by
default. This includes:

- `.github/`
- `apps/`
- `libs/`
- `eng/`
- `scripts/`
- `tests/`
- client-facing and operational content under `docs/`
- root build, editor, Docker, SDK, solution, and repository files

The exporter reads blobs from the selected Git revision. It must not use
`cp` against the working tree. Ignored files, untracked files, and unstaged
edits are therefore outside the export boundary.

## Exclusion Policy

A single repository-relative policy file defines the paths withheld from the
client snapshot. The initial policy excludes:

- every `AGENTS.md`
- `project_state.md` and archived project-state/session checkpoints
- `.agents/`, `.codex/`, and `.codegraph/`
- `docs/superpowers/`
- internal plans, specifications, evidence, reviews, and historical work logs
  under `docs/history/` and `docs/reviews/`
- client-release design, runbook, exclusion policy, export scripts, publisher,
  and their contract tests
- build and test output such as `bin/`, `obj/`, `TestResults/`, coverage,
  Sonar working data, package caches, and `node_modules/`
- local environment files and secret-bearing file types

The policy is intentionally deny-list based because the approved release is the
entire monorepo. Product source, tests, CI definitions, deployment files,
database scripts, architecture documentation, and operational runbooks remain
unless a specific path is classified as internal-only.

## Export Metadata

Each package contains:

- `RELEASE_SOURCE.json`, with the full source commit, source commit timestamp,
  export format version, and target branch
- `RELEASE_MANIFEST.txt`, a sorted list of all other exported files

The manifest is generated only after exclusions are applied. Neither metadata
file contains local paths, usernames, machine information, or credentials.

## Secret Boundary

The exporter performs two complementary checks on the final payload:

1. A filename check rejects private-key formats, local settings, `.env` files,
   credential stores, and other explicitly blocked secret-bearing paths.
2. A content scan rejects high-confidence credential material, including PEM
   private-key blocks, common cloud/service token formats, credentials embedded
   in connection strings or URLs, and assignments of non-placeholder values to
   recognized secret names.

Documentation that names environment variables and sample configuration that
uses empty values or obvious placeholders is allowed. Findings are reported by
path and rule, without echoing the suspected secret value.

## Exporter Interface

`scripts/export-client-release.sh` becomes the repository-level entry point:

```text
scripts/export-client-release.sh \
  --source-ref <commit-or-ref> \
  --export-dir <absolute-temporary-directory>
```

The source ref defaults to `origin/main` when available and otherwise `HEAD`.
The script resolves it once to a full commit SHA, refuses unsafe output paths,
recreates only the explicit export directory, applies the exclusion policy,
generates metadata, and runs all boundary checks.

## Publisher Interface

`scripts/publish-client-release.sh`:

- invokes the repository-level exporter with the chosen source ref
- resolves the current remote `release-candidate` SHA
- creates a fresh orphan release commit in a temporary clone
- shows a file-count and diff summary before publication
- pushes with an explicit `--force-with-lease=<branch>:<observed-sha>`
- clones or fetches the published branch and reruns the release-boundary checks

A remote update after the observed SHA causes the push to fail. The script does
not silently replace an unexpected client-side change.

## Verification

Before publication, validation runs from the exported snapshot:

- release script contract tests
- manifest and metadata consistency checks
- blocked-path and secret-content scans
- .NET restore, build, and test for `Rvt.Mono.slnx`
- frontend dependency installation, lint, tests, and production build using the
  repository-pinned package manager workflow
- Docker Compose configuration validation for repository compositions
- project-reference and Docker-context integrity checks

Tests that require external services run only when their documented disposable
