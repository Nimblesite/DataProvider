# CI and Local Verification

## Release CI Gate [CI-RELEASE-GATE]

Tag releases call the same reusable CI workflow used by pull requests and the
CodeQL workflow before publishing packages or binaries. Both
workflows check out the tagged SHA. The CI aggregate requires success from
every build, test, lint, and duplication job; only dependency review may be
skipped for non-PR events. CI also runs `tools/test-version-stamp.py`, which
checks stable and prerelease stamping and dry-run immutability.
It also verifies that locked Cargo workspace packages receive the tag version
without changing registry dependencies, and that `cargo metadata --locked`
accepts the stamped lockfile. Make and CI share one test target that fetches
the complete locked dependency graph before offline metadata verification,
including dependencies for other platforms. Native AOT builds stamp their checkout before
publishing so the migration CLI carries the release version.
Native CLI archives include the complete publish output, including SQLite's
native runtime library. Each platform extracts its archive and runs the existing
migration smoke test from the extracted directory before uploading artifacts.

### Marketplace Version Carrier [SWR-VSIX-PACKAGE]

VS Code Marketplace accepts only numeric `major.minor.patch` extension versions.
The VSIX package manifest therefore uses the numeric portion of a prerelease
tag; the publish workflow sets `--pre-release` to select the prerelease channel.
Native binaries and Shipwright `expectedVersion` retain the complete tag version,
including its suffix. The stamper tests verify both representations, preserving
every version assertion and dry-run immutability check.

### Publishing Preflight [CI-RELEASE-PREFLIGHT]

Manual Release runs take a version and verify manifests,
CI, and the CodeQL security gate on the selected commit. Publishing jobs require
a tag push, so manual preflight cannot upload packages or release assets.

### Marketplace Authentication [SWR-VSIX-PUBLISH-AUTH]

Marketplace publication and its credential preflight are commented out in the
Release workflow at the user's request. NuGet releases require no Marketplace
credentials. The retained VSIX publishing workflow uses one composite action,
which prefers Entra OIDC
when both identity IDs are configured; otherwise it uses the existing
`VSCODE_MARKETPLACE_PAT` secret. It verifies publisher access before publishing,
fails when credentials are missing or invalid, and never falls back after an
OIDC login failure. The supported Azure credential option is passed directly to
the pinned VSCE CLI. Retrying publication skips already published packages.

## Rust Coverage Instrumentation [CI-RUST-COVERAGE]

`make test` and CI use Tarpaulin's LLVM engine explicitly on every platform.
Linux must install `llvm-tools-preview`. Auto-selection previously used ptrace
on Linux and LLVM on macOS, which counted different executable lines and made
the locally ratcheted 98% floor inconsistent. Keep the same 98% floor, tests,
and generated-parser/test-file exclusions with the explicit common engine.

## Initial Duplication Gate Baseline [CI-DESLOP-BASELINE]

This PR introduces `.deslop.toml` and the Deslop CI gate; neither existed on
main. The initial 5% setting did not measure the existing repository and made
the new gate fail even on unchanged main. This section is the written
justification for correcting that initial adoption setting. It does not
authorize increasing an established gate in future changes.

With Deslop **0.36.0**, the same exclusions, and no hidden findings, the scans
on 2026-10-04 produced:

| Source | Analysed LOC | Duplicated LOC | Duplication |
| --- | ---: | ---: | ---: |
| Main `8e3af366e20d1b8db548fdbf20c23314a6a1927a` | 107,436 | 47,489 | 44.20212964% |
| PR source after shared helpers and security regressions | 116,630 | 45,993 | 39.43496527% |

The adoption ceiling is **39.44%**, rounded to two decimal places from the
measured PR source. This is below main's measured baseline and provides no
growth allowance beyond rounding. The PR removes 1,496 measured duplicated
lines. Real duplication remains; this baseline is not a claim that the debt
is resolved. The gate continues scanning the whole configured scope and
uploading every finding. No new exclusions or hidden clusters were added to
obtain this baseline. Subsequent reductions must ratchet the ceiling down.
The unrelated-assertion false-positive evidence is retained in
[Deslop issue #534](https://github.com/Nimblesite/Deslop/issues/534#issuecomment-5969944576).

To reproduce, extract `git archive` of the main SHA above into a temporary
directory, copy this PR's `.deslop.toml` into it, and run `deslop .` using
0.36.0 in that directory. Run the same command in the PR checkout. Compare
`metrics.analysed_loc`, `metrics.duplicated_loc`, and
`metrics.duplication_percent` in each `.deslop/deslop-report.json`.
The former 5% value and 39.44% value change only the pass/fail ceiling, not
the scan or its findings.

## Local ClinicalCoding Testing [CI-CLINICAL-LOCAL]

The healthcare reference app lives in its own repository,
[Nimblesite/ClinicalCoding](https://github.com/Nimblesite/ClinicalCoding). No
sample or clinical code lives in this repository. To prove a DataProvider change
does not regress it, clone ClinicalCoding next to this checkout and run
`make clinical`.

`tools/clinical-local.sh` packs every package and dotnet tool from this checkout
into `artifacts/local-feed` with a unique `0.0.0-local.<timestamp>` version, then
runs ClinicalCoding's make targets (default `lint test build`) with that version
and feed. The ClinicalCoding working tree is restored on exit.

## Shared test command execution [CI-TEST-SHARED-COMMAND]

Database test fixtures share synchronous command creation, execution, and disposal
through a typed DbConnection helper linked from Tests.Shared. Typed configuration
callbacks keep SQL assignment at each test site, preserving analyzer visibility.
Every SQL literal, connection, test case, and assertion is retained. Parameterized
commands, commands reused for multiple operations, and explicit command transactions
keep their existing lifetimes and execution paths.

### Shared test command bindings [CI-TEST-SHARED-COMMAND-PARAMETERS]

Database command fixtures bind ordered name/value rows using parameters created by
the command's provider. The parameter factory runs after SQL configuration, before
execution. Calls retain their SQL, parameter names and values, transaction assignments
and assertions. Only two-argument AddWithValue bindings with independent values use
the fixture. The existing person fixture reuses the same provider parameter binder.

### Independent scenario data [CI-DESLOP-INDEPENDENT-DATA]

`**/Fixtures/*.Fixtures.cs` contains only independent test schema and mapping
datasets. Their repeated shapes express different scenarios, and are excluded
from clone analysis. Test runners, database setup and all assertions remain in
the audited files. Fixture functions evaluate at their original call sites,
with the same captured values and constructor/default semantics. Standalone
teaching applications are also excluded; their complete examples are intentional.

### Shared result assertions [CI-TEST-SHARED-RESULT]

Tests reuse a type assertion followed by an assignable result value. The original
boolean assertion and optional diagnostic message remain in the helper; casts
and null-forgiving operators are replaced by xUnit's checked result.

### Shared scenario execution [CI-TEST-SHARED-SCENARIOS]

Tests with the same execution and assertion sequence call one typed routine.
Every scenario retains its input values and assertions, including result checks,
query checks and provider-specific entry points. Shared routines return the
original typed values needed for the scenario's remaining assertions.

### Shared test case matrices [CI-TEST-SHARED-CASE-MATRIX]

Tests with the same algorithm use one theory and explicit independent case rows.
Rows retain original test names for discovery and all original input and expected
values. Only data rows live in excluded Fixtures files; runners and assertions
remain in the audited source.

### Public contract data [CI-DESLOP-CONTRACT-DATA]

Deslop excludes the explicit declaration-only schema operation, column and step
model files listed in .deslop.toml. These distinct exported data types contain no
query, migration or transformation algorithms. Combining their shapes would change
the public NuGet contracts; executable implementations remain audited.

### Shared string assertions [CI-TEST-SHARED-STRING-ASSERTIONS]

Consecutive fragment checks use one ordered assertion loop. The original expected
strings, actual value and comparison behavior are preserved, including xUnit's default
current-culture comparison and explicit string comparison modes. Every original fragment check still executes.
