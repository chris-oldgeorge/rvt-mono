# RVT Common Monitor Source-Removal Verification

## Scope and source identity

- Verification date: 2026-07-17.
- Monitor repository: `RVT-Group-LTD/rvt-monitors`.
- Verified branch: `codex/rvt-common-private-nuget-migration`.
- Gate baseline commit: `2274bc73d8f1b6ef0609262b0b18cb997635df57`.
- Source-removal commit: `51d680311c63327e2580723860f37097c5d8ea25`.
- Package-boundary fix commit: `6c409f8`.
- Active-document and policy commit: `06c6aa6d26f284beefe30ab4517dcecd35f19e84`.
- Final-review hardening commit: `2274bc73d8f1b6ef0609262b0b18cb997635df57`.
- Solution test-discovery correction commit: `b5639963e7dc89b191f39b5ed588d8f7e93f5921`.
- Test-discovery guard hardening commit: `1af6012`.
- Authoritative Common repository: `RVT-Group-LTD/rvt-reporting`.
- Published RC source commit: `f00d5b8a320945ed08e248da8641ca0c3f7e3b82`.
- Exact immutable package version: `0.2.0-rc.1`.

The active checkout contains no `rvt-monitor-common/` directory, local Common project, Common `ProjectReference`, or `UseLocalRvtCommon` source switch. The source-removal commit deleted the retained six-project rollback tree and removed its entries from all active solutions. Historical designs, plans, evidence, and Git history remain intact.

## Package resolution and consumer boundary

The three central `PackageVersion` bindings use exact singleton ranges. An authenticated `dotnet restore --force-evaluate` regenerated all 14 consumer locks, and a subsequent clean authenticated locked restore used a fresh isolated NuGet cache and passed for all 14 projects. Under the installed NuGet 10 client, the 18 direct requested ranges are canonically serialized as `[0.2.0-rc.1, 0.2.0-rc.1]`: both bounds are closed and equal, so no later version can resolve. This force-evaluate output supersedes the review finding's shorthand `[0.2.0-rc.1]` representation. The architecture guard accepts either exact singleton serialization and rejects open, unequal, missing, or drifting bounds.

Twelve lock files changed because they contain direct or centrally pinned RVT dependencies; the Reporting Core and PDF locks were regenerated but remained byte-identical because they do not consume an RVT package. The three package hashes are unchanged from the accepted RC evidence:

| Package | Resolved version | NuGet lock content hash |
| --- | --- | --- |
| `Rvt.Monitor.Common` | `0.2.0-rc.1` | `+JAnUTKwFD07+rTwl1eHePz3HVltf5FNvHMN/bJc0uZYxfVBox1taYwF1uANaVYKG8agZjnhzDmgEDoj/vQTTw==` |
| `Rvt.Monitor.Common.Infrastructure` | `0.2.0-rc.1` | `PscR8XS7pIXoiGUlCJWmyYPhkWZa/jwH1Xv6489Wbv4uy6R5V4deguTJihmZRnoy5HFJU+RUVuv0ftCUAFUPPQ==` |
| `Rvt.Monitor.IntegrationTesting` | `0.2.0-rc.1` | `0p35dT+xJv8tbfsPlglwbhtxqM2H89Vp/wBJzF+XKjueE/2tGIMZlPWDW0kvVZ/S4dlG4wPr4OjbP1rAeBCanA==` |

The lock inventory found Common in 12 lock files, Infrastructure in 10, and IntegrationTesting in five, with one version/hash pair for each package. The architecture boundary suite passed the approved direct-consumer matrix, rejected unrelated and case-mismatched RVT references, enforced central exact versions and exact direct-lock singleton semantics, and confirmed that IntegrationTesting occurs only in the five test projects with `PrivateAssets="all"`. Every active consumer project whose filename ends in `Tests` must contain exactly one `IsTestProject` declaration: it must equal `true` case-insensitively, be a direct child of an unconditional top-level `PropertyGroup`, and have no own `Condition`. Any conditional, Target-scoped, duplicate, or false declaration fails. Negative fixtures prove the guards detect an open central binding, an open direct lock range, renamed local Common projects through filename/`AssemblyName`/`PackageId`/`RootNamespace`, a renamed-path `ProjectReference`, a conditional test property group, a Target-scoped test property, and a true-then-false override.

## Solution, formatter, build, and test results

The solution inventory passed with 14 projects in `rvt-monitors.sln` and two projects, the app and its tests, in each of the AirQ, MyATM, Omnidots, and Svantek vendor solutions. No active solution lists a retired Common project.

Independent branch-finishing verification found that the .NET 10 solution test target silently skipped AirQ, Svantek, and Reporting under the exact CI command `dotnet test rvt-monitors.sln --no-build --nologo`: evaluated `IsTestProject` was empty in those three project files, while it was `true` in MyATM and Omnidots. The new architecture guard failed first with exactly those three paths. Commit `b5639963e7dc89b191f39b5ed588d8f7e93f5921` adds the explicit property to the three affected projects; the focused guard then passed. The exact CI root command now launches and reports all five suites.

Reviewer follow-up found that the first guard accepted any descendant `IsTestProject=true`, including declarations that MSBuild could condition away or override. Commit `1af6012` hardens the structural invariant above. Its three data-driven negative cases first failed because the guard accepted all three malformed fixtures, then passed after the exact-declaration validator replaced the descendant predicate.

| Check | Result |
| --- | --- |
| Locked restore with isolated cache | Passed; 14 projects restored |
| Formatter verification | Passed; no changes |
| Serial solution build (`-m:1`) | Passed in 3.48 seconds; 0 warnings, 0 errors |
| PostgreSQL-backed exact root solution tests | Passed; 929 passed, 0 failed, 0 skipped |

The final build deliberately used `-m:1`: the default-parallel build had previously stalled after AirQ in this shared desktop environment, while the identical serial build completed cleanly. This is an execution-environment detail, not a source change.

Exact suite counts were:

| Suite | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| AirQ | 118 | 0 | 0 |
| MyATM | 213 | 0 | 0 |
| Omnidots | 399 | 0 | 0 |
| Svantek | 124 | 0 | 0 |
| Reporting | 75 | 0 | 0 |
| **Total** | **929** | **0** | **0** |

The preceding discovery-fix gate recorded 926 across the five suites. The reviewer regression is one data-driven MyATM architecture method with three executed rows, producing the reconciled total of 929; no production behavior test count was lost. The decisive run used a disposable PostgreSQL 17 fixture and the unchanged root command without serializing test execution.

## Package, Compose, container, and runtime gates

`scripts/verify-private-package-builds.sh` passed its source-tree, source-switch, solution, Dockerfile secret-mount, credential-pattern, and Compose-secret checks. It now enumerates every active local project, asks MSBuild for evaluated project identities, and resolves evaluated `ProjectReference` filenames, so a renamed directory cannot hide a retired Common project. An isolated negative fixture with inherited `AssemblyName=Rvt.Monitor.Common` was rejected. `docker compose config --quiet` passed. `docker compose build` rebuilt all five package-only images.

`scripts/report-rvt-package-inventory.sh` read the synchronized expected version from `Directory.Packages.props`, inspected each runtime `.deps.json`, required Common and Infrastructure both to equal `0.2.0-rc.1`, and confirmed that IntegrationTesting is absent. A negative invocation with expected version `9.9.9` was rejected:

| Image | Local image ID | Common | Infrastructure | IntegrationTesting in runtime |
| --- | --- | --- | --- | --- |
| `rvt/airqmonitor:local` | `sha256:cbaf22459cb3e57dc7d381973c62b51fd0cc1a10c34a8b3110eac477cb970ddd` | `0.2.0-rc.1` | `0.2.0-rc.1` | Absent |
| `rvt/myatmmonitor:local` | `sha256:ed4fc75e7790b3ecbd3feaa22d0c42670e9a16ff1935a27d198c36d2fb91af85` | `0.2.0-rc.1` | `0.2.0-rc.1` | Absent |
| `rvt/omnidotsmonitor:local` | `sha256:365b15aa0ab94347c155f914baf87997348a876b16ce970affccca82067cb04e` | `0.2.0-rc.1` | `0.2.0-rc.1` | Absent |
| `rvt/svantekmonitor:local` | `sha256:abd057d72d235232ef9f3d76830f4c68d669380f697d420688a8e55579503a6f` | `0.2.0-rc.1` | `0.2.0-rc.1` | Absent |
| `rvt/reportingmonitor:local` | `sha256:7d9048bb18fe7b46bd34f1c0a42134a45d62eedf844b1eb9ab4d3bdb35bdb19e` | `0.2.0-rc.1` | `0.2.0-rc.1` | Absent |

A read-only image configuration/history scan found no persisted NuGet credential or token pattern. These identifiers are local content-addressed image IDs; this gate did not publish registry digests.

## CI, active documents, and curated export

- The package-consumer CI workflow has a credential-free `contents: read` policy job for every pull request and a credentialed full gate only for trusted pushes or same-repository pull requests. Both checkouts set `persist-credentials: false`; official checkout v6 is pinned to `df4cb1c069e1874edd31b4311f1884172cec0e10` and setup-dotnet v5 to `26b0ec14cb23fa6904739307f278c14f94c95bf1`, as resolved from the official repositories. `packages: read` exists only on the trusted job, and the NuGet credential environment exists only on its restore and Docker build steps.
- CI's existing exact root test command now exercises AirQ, MyATM, Omnidots, Svantek, and Reporting because all five test project files explicitly evaluate `IsTestProject=true`; the exact structural architecture guard prevents conditional, nested, duplicate, false, or otherwise override-prone declarations from silently reintroducing solution-level omission.
- MyATM migration guidance no longer depends on an expiring workflow artifact. It clones `RVT-Group-LTD/rvt-reporting`, fetches exact authoritative commit `f00d5b8a320945ed08e248da8641ca0c3f7e3b82`, archives only the two confirmed migration paths, and verifies their locally confirmed SHA-256 values before the separately designated migration authority applies either script. The local authoritative clone remained unmodified.
- The active-document boundary test passed for the seven designated operational documents: none names the retired local source path. The root README names `RVT-Group-LTD/rvt-reporting` as the private package owner and exact source authority.
- The curated release export passed and copied 593 tracked files plus its generated manifest. It included `NuGet.config` and `Directory.Packages.props`, excluded the Common source tree and internal evidence/state material, and its blocked-file scan found no development settings, environment files, or private key/certificate files.
- `git diff --check` passed before evidence was written.

## Authorized deviation, rollback, and operational limits

The original cross-repository migration sequence required accepted portal evidence and stable `0.2.0` before deleting monitor-owned source. The approved 2026-07-17 source-removal design intentionally deviates from that stable-first sequence: the user authorized deleting the inactive rollback tree while consumers remain pinned to the already validated immutable `0.2.0-rc.1` train. Stable promotion and portal validation remain separate future work; this gate does not represent a stable release.

The recorded pre-conversion source-reference rollback commit remains `924ed20deee37fba17452612ae40eae8e0fe6168`. Before merge, rollback may revert the source-removal and package-conversion commits. Normal forward development must not restore a permanent local-source switch.

## Credential handling, safety, and cleanup

The authenticated package credential existed only in one fixed temporary gate shell process. That shell obtained it from the keyring-backed `gh` session without printing it, immediately unset the intermediate token variable, exposed the NuGet credential to only the restore and Compose-build child processes, and unset it immediately after the five image builds. A trap also unset it on every exit. It was not written to the repository, NuGet configuration, cache configuration, export, image environment, or image history.

The PostgreSQL 17 fixture was local and disposable. No credential, connection string, environment dump, live provider call, operational migration, deployment, or production database operation occurred. Tests used only the disposable fixture; no live application/provider smoke call was made.

Cleanup removed the disposable database container, isolated package cache, and curated export. Post-cleanup checks confirmed all three were absent.
