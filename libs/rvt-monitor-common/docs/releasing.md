# Releasing the common packages

The three compatibility packages are released together with one immutable exact version:
`Rvt.Monitor.Common`, `Rvt.Monitor.Common.Infrastructure`, and
`Rvt.Monitor.IntegrationTesting`. Published versions are never replaced or deleted as a
normal release or rollback operation.

## Access and release prerequisites

- The repository is currently bootstrapped with `@chris-oldgeorge` as the only visible
  collaborator and CODEOWNER. The account is a repository administrator so bootstrap
  releases remain possible while organization teams are unavailable to this repository.
  Before adding non-administrator contributors, create or grant access to the common,
  monitor, and portal maintainer teams; replace the individual CODEOWNERS entries with
  those teams; then require a non-author CODEOWNER approval without an administrator
  bypass. Until then, all bootstrap releases require a green `build-test-pack` check and
  a recorded independent review in the pull request.
- Protect the stable `v*.*.*` tag pattern so only the release maintainers can create a
  protected tag after the required `common-ci` checks and review have passed.
- Grant GitHub Packages read access explicitly to the `RVT-Group-LTD/rvt-monitors`
  repository and to the approved portal repository. Keep package administration and
  release permissions with the common-package maintainers.
- Release jobs use the workflow-scoped GitHub token. They do not require a stored
  package credential or repository secret.
- Microsoft.Sbom.DotNetTool 4.1.5 requires a native .NET 8 runtime. The workflow installs
  .NET 8 for the SBOM tool and .NET 10 for the build. On a .NET-10-only development
  machine, set `SBOM_DOTNET=/path/to/dotnet8/dotnet` when running
  `scripts/build-release-artifacts.sh`; do not roll the tool forward to .NET 10 because
  component detection can produce an incomplete root-only manifest.
- Confirm the database migration archive owner and the designated RVT deployment
  operator before publishing a version that contains shared-schema changes.

## Publish a release candidate

Open the `release-common` Actions workflow, choose `Run workflow` (`workflow_dispatch`),
select the main/default branch (`main`), and enter a new SemVer prerelease such as
`0.2.0-rc.1`. The workflow rejects dispatches from any other branch and rejects stable
versions on this path. It checks all three packages for the requested version before it
builds or publishes anything. A preflight failure is not bypassed: select a new version.

After the job completes, verify its package-validation and package-only consumer gates.
Retain the workflow artifact containing the flat release asset set: packages, symbols, the
migration archive, `SHA256SUMS`, the SPDX 2.2 manifest, and its tool-generated checksum.
Every entry in `SHA256SUMS` is a basename for a file in that same directory, so a downloaded
artifact or GitHub release can be verified without recreating the repository tree:

```bash
cd release-assets
shasum -a 256 -c SHA256SUMS  # macOS
# or: sha256sum -c SHA256SUMS  # Ubuntu
```

## Publish a stable release

Create an annotated protected tag from the reviewed commit and push it:

```bash
git tag -a v0.2.0 -m "Release common packages 0.2.0"
git push origin v0.2.0
```

Stable tags must be exactly `vMAJOR.MINOR.PATCH`; a prerelease tag is rejected. The tag
starts the same immutable package build and creates a GitHub release whose assets are the
three packages, three symbol packages, migration archive, checksum list, SPDX 2.2 manifest,
and its checksum. The workflow stages all of them into one flat assets directory;
both Actions artifact upload and GitHub release creation consume that exact directory. The
tag, package version, and GitHub release must continue to identify the same source commit.

Release concurrency is keyed by the requested manual version or pushed tag and never
cancels an in-progress run. A duplicate same-version run waits, then repeats the immutable
package preflight and fails if the first run published that version.

## Consumer pins and package access

Consumers use an exact version, never a floating range. For a project-local reference:

```xml
<PackageReference Include="Rvt.Monitor.Common" Version="[0.2.0]" />
```

For centrally managed versions, use the same exact version syntax for every package:

```xml
<PackageVersion Include="Rvt.Monitor.Common" Version="[0.2.0]" />
<PackageVersion Include="Rvt.Monitor.Common.Infrastructure" Version="[0.2.0]" />
<PackageVersion Include="Rvt.Monitor.IntegrationTesting" Version="[0.2.0]" />
```

Grant repository access at the package level rather than sharing a long-lived personal
token. If any restore or release credential is exposed, begin credential revocation
immediately, review the GitHub audit log, rotate the affected credential, and rebuild from
a clean checkout.

## Migration ownership

The versioned `rvt-common-migrations-VERSION.tar.gz` archive belongs to the common-package
release. Only the designated RVT deployment operator may apply it, once, through the
controlled deployment procedure after verifying `SHA256SUMS` and reviewing the scripts.
Consumer containers must not run shared migrations at container startup. Application
replicas may start only after the operator has completed the shared-schema migration and
recorded the applied package version.

## Emergency patch flow

For an emergency fix, branch from the affected stable tag, make the smallest reviewed
change, and publish a new patch version from a new protected tag. Never overwrite the
affected package. After release, forward merge the patch commit into the active development
branch so the correction is not lost from later versions.

## Rollback

Rollback does not republish packages or reverse a tag. Pin all three references to the same
already-published prior version, restore in locked mode, rebuild the consumer image, and
deploy that image through the normal approval path. The designated RVT deployment operator
owns any database rollback or forward-fix decision; do not automatically run down scripts
from a container startup hook. Record the selected package version, image digest, migration
state, and validation evidence in the deployment record.
