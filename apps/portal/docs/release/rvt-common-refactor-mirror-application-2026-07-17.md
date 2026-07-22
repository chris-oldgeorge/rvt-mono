# RVT common refactor mirror application

## Source and target

- Target repository: `chris-oldgeorge/rvtportal-spa-alpha`.
- Target baseline: `7c0564b1e688366899c1cba80434028f31d559fd` on `master`.
- Source repository: `RVT-Group-LTD/RVT-Cloud`.
- Source implementation head: `9a0b3c046a461516c8a0f27a8781b0c01ff0d2e2`.
- Source compatible snapshot: `f2582bcf7d2b17ae69a40cc32e47b6d2e1685eea`.
- Source manifest: `docs/release/rvt-common-refactor-mirror-change-manifest.md`
  in the source repository.

The repositories do not share the source commit history, so this is a
compatibility port rather than a literal cherry-pick.

## Applied source orders

| Source order | Class | Mirror treatment |
| ---: | --- | --- |
| 10 | required | Added repository-layout and zero-package boundary tests. |
| 20 | required | Applied the warning-free generated-regex and scalar theory-data fixes. Preserved the mirror's newer database constraint/index registry instead of replacing it with the older source snapshot. |
| 30 | required | Added repeatable backend/frontend verification and ignored generated artifacts. |
| 40 | recommended | Added the pinned, unprivileged frontend image and smoke gate. |
| 50 | recommended | Replaced secret-dependent Windows CI with mandatory Linux verification and optional analysis. |
| 70 | recommended | Restricted the entire Sonar job to trusted pushes on `master`, pinned scanner `11.2.1`, and retained exact container smoke assertions. |

Source documentation orders `00`, `01`, `51`, `60`, and `71` describe
the organizational source-branch workflow and are not copied verbatim. This
record is the mirror-specific documentation replacement.

## Intentional adaptations

- Workflow triggers and the trusted Sonar ref use `master`, the mirror's
  default branch, instead of `rvt-portal-refactor`.
- The current mirror database registry is retained because it contains
  post-baseline schema metadata.
- Existing portal-owned EF contexts, migrations, providers, and public-only
  restore path remain unchanged.
- No private NuGet feed, package credential, package permission, SQL execution,
  database migration, provider call, or deployment is part of this port.

## Verification contract

The binding gates are:

```bash
bash scripts/verify-backend.sh
bash scripts/verify-frontend.sh
bash scripts/verify-frontend-container.sh rvtportal-spa-alpha:mirror-refactor
git diff --check
```

Pull requests must run the mandatory `verify` job without access to
`SONAR_TOKEN`. The `analyze` job is admitted only for a trusted push to
`refs/heads/master` and skips its analysis steps when the token is absent.
