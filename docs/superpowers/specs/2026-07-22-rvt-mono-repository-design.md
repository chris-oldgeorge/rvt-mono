# RVT Mono-Repository Design

## Goal

Create a new RVT mono-repository from four existing repositories with one fresh
Git history and one root solution, while preserving each imported system as a
clear, independently buildable module.

## Source Revisions

| Source repository | Default branch | Pinned import revision |
| --- | --- | --- |
| `chris-oldgeorge/rvt-monitors` | `main` | `5935f40614073afa6c4ef954db1308a72a5f8f2b` |
| `chris-oldgeorge/rvtportal-spa-alpha` | `master` | `8355070f094a591297c9f8468057f44a6c876986` |
| `RVT-Group-LTD/rvt-reporting` | `main` | `f00d5b8a320945ed08e248da8641ca0c3f7e3b82` |
| `chris-oldgeorge/rvt-reporting-new` | `main` | `e602e8317e35bd94a1eb4dd017759b91713ea111` |

The import uses each source's checked-out file content at the listed revision.
It does not preserve or merge the source Git histories.

## Repository Layout

```text
apps/
  monitors/                 # rvt-monitors
  portal/                   # rvtportal-spa-alpha
libs/
  rvt-monitor-common/       # RVT-Group-LTD/rvt-reporting package source
services/
  reporting/                # rvt-reporting-new
docs/
  imports/                  # source/revision manifest and integration notes
  superpowers/specs/
  superpowers/plans/
Rvt.Mono.slnx
README.md
.gitignore
project_state.md
```

The source directory contents are preserved under their assigned module. This
avoids collisions between root-level build files, NuGet configuration, database
assets, scripts, and independent solution files while making ownership explicit.
The imported `AGENTS.md` files remain within their module directories and apply
to future changes below those directories.

## Shared Solution

`Rvt.Mono.slnx` will list every C# project from all modules and arrange them in
the following solution folders:

- `Apps/Monitors`
- `Apps/Portal`
- `Libraries/RVT Monitor Common`
- `Services/Reporting`
- corresponding `Tests` folders where the project type permits it

Existing module solutions remain in place as focused developer entry points.
The root solution is the integration entry point. No production project is
renamed, flattened, or combined during the initial import.

## Dependency and Configuration Boundaries

The initial mono-repository preserves each module's existing `Directory.Build.*`,
`Directory.Packages.props`, `NuGet.config`, Docker/Compose files, database
assets, and deployment scripts in its module directory. This deliberately keeps
the existing private-package boundary between monitor consumers and the shared
package source intact. Replacing packages with cross-module project references,
deduplicating reporting implementations, or merging database schemas are
separate compatibility work and are out of scope.

The root will contain only mono-repository ownership metadata, a source import
manifest, the aggregate solution, and documentation. It will not introduce a
root NuGet policy that overrides module-local restore behavior.

## Import Procedure

1. Initialize the new root Git repository and add repository-wide ignore rules.
2. Clone every source at its pinned revision into disposable staging locations.
3. Copy tracked source content into the defined module directories, excluding
   source `.git` directories.
4. Resolve only root-level structural conflicts by keeping the files within
   their module boundaries; do not modify source code during import.
5. Create an import manifest with source URLs, branches, revisions, module
   paths, and excluded Git metadata.
6. Generate `Rvt.Mono.slnx` from the imported projects, retaining module-local
   solutions.
7. Write root developer documentation and `project_state.md`.
8. Run safe structural checks, restore/build the root solution when private
   package credentials and the SDK permit it, and report any unavailable
   dependency separately from source errors.

## Error Handling

- If a source cannot be fetched, stop before copying partial source content and
  identify the inaccessible source and revision.
- If a project path or project identity conflicts in the aggregate solution,
  retain the project files unchanged and resolve the conflict using solution
  folders or solution display names only.
- Private NuGet restore failures are reported without weakening NuGet security,
  changing package versions, or recording credentials.

## Verification

- Confirm the manifest revisions match the checked-out source revisions.
- Confirm all expected projects appear in `Rvt.Mono.slnx`.
- Confirm no imported `.git` directory exists below a module path.
- Run `dotnet sln Rvt.Mono.slnx list` (or the equivalent `.slnx` command) and
  inspect the resulting project list.
- Attempt restore/build using the installed SDK. Run module test suites only
  when their dependencies are available; unavailable private-package access is
  recorded as an environmental limitation, not bypassed.

## Non-Goals

- Preserve original Git history.
- Merge or deduplicate the legacy and newer reporting implementations.
- Convert private-package references into project references.
- Merge databases, migrations, CI pipelines, Docker Compose stacks, or release
  processes.
- Deploy applications, alter external services, or add credentials.
