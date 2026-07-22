# Documentation Consolidation Final Fix Report

## Result

Closed the final documentation-review gap by deriving old module-relative
`docs/**` forms from `docs/documentation-move-manifest.md`, guarding those forms
across tracked text, and rewriting every current occurrence to its manifest
destination.

## Root cause and RED evidence

The guard searched exact repository-root source paths and resolved relative
Markdown links, but it did not derive paths relative to an imported module
root. Consequently, text such as `docs/releasing.md` was invisible in shell,
C#, SQL, PowerShell, and configuration files.

A tracked `.txt` fixture was added with the old shared-library release path.
Before the guard change,
`tests/verify-documentation-layout-regression.test.sh` exited 1 because the
expected module-relative diagnostic was absent.

## Implementation

- For every missing manifest source, the guard removes its known module-root
  prefix and records the result when it starts with `docs/`.
- `git grep -I` scans arbitrary tracked text for those old forms.
- The move manifest and internal SDD review packages are excluded from the
  scan. `docs/history/**` is also excluded so imported historical evidence can
  accurately retain paths that described the repository at that time.
- The stale-reference total now includes exact old source paths, resolved old
  Markdown links, and module-relative old document paths.
- Rewrote 50 non-Markdown occurrences and 27 current Markdown occurrences to
  manifest destinations. The non-Markdown set includes release automation,
  export policy, portal EF/database/Sonar source comments, SQL, PowerShell, and
  text configuration.
- The shared release test now resolves the centralized guide through
  `$repository_root/../../docs/release/rvt-monitor-common/releasing.md`.
- The user's untracked duplicate C# file was neither modified nor staged.

## GREEN evidence

- `tests/verify-documentation-layout.test.sh`: passed with 122 moves and seven
  retained entry points.
- `tests/verify-documentation-layout-regression.test.sh`: passed and detected
  both an exact old source path and the module-relative path in non-Markdown
  text.
- Explicit manifest-derived scan outside approved historical/internal
  exclusions: zero current stale alias groups.
- `bash -n` passed for the guard, regression, monitor export script, and shared
  release-automation test.
- `pwsh` is not installed in the verification environment; the PowerShell
  change is confined to prose inside its comment-based help block.
- The centralized shared release guide exists at the path computed by the
  release-automation test.
- `git diff --check`: passed.
