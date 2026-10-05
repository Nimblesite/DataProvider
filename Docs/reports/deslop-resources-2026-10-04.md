# Deslop resource evidence — 2026-10-04

## Findings [DEDUP-RESOURCE-EVIDENCE]

Deslop **0.36.0** completed cached and cold scans of DataProvider without an
out-of-memory exception, watchdog termination, or host crash. Observed peak RSS
was **819.11 MiB**. This reproducer does not establish a memory leak.

CPU bursts are reproducible: the initial cached scan reached **821.62%** sampled
CPU despite `RAYON_NUM_THREADS=2`. Later throttled scans still had short bursts
up to **1468.33%**, followed by pauses that bounded cumulative CPU consumption.
100% CPU means one logical core; this peak approximates all fourteen cores.
The sampled percentage is approximate, not precise utilization above host capacity.
`RAYON_NUM_THREADS` is not an effective Deslop worker limit.

## Environment and binary [DEDUP-RESOURCE-ENVIRONMENT]

- DataProvider baseline: `62b3062eb50b16e34f3d5094d477f8ba766f8673`.
- macOS arm64; 36 GiB physical RAM; 14 logical CPUs.
- Before scanning: 67% system-wide free-memory reading; 1505.88 MiB swap already
  in use. Existing swap is not evidence that Deslop caused swapping.
- Latest public GitHub release checked during this run:
  [v0.36.0](https://github.com/Nimblesite/Deslop/releases/tag/v0.36.0).
- Downloaded archive SHA-256: `eb0eca1cb827cfb533a1fd4dc0ca20f2ec3dc9b1d6979c70c3e420c61788f2d7`.
- Executable SHA-256: `a32d051af8d223a5ab87b8a1c8fffd8ec269fa98ba5ec817b3bf40820afae13d`.
- The archive matched the release's published checksum.
- Existing `.deslop.toml` scope: generated ANTLR outputs and Reporting excluded;
  no additional exclusions, hidden findings, or altered detection thresholds.
- Baseline: 507 cache hits, zero misses; 116,630 analysed lines; 45,993 duplicated
  lines; **39.43496527%**. Cold and cached findings are compared before refactoring.

## Measurements [DEDUP-RESOURCE-MEASUREMENTS]

The external watchdog samples process-tree RSS and cumulative CPU time with
`ps` approximately every 250 ms. CPU peaks are interval estimates, not a whole-run
average; sampling can miss brief allocations and exited processes. Memory is
RSS, not virtual allocation. The raw observations and guard settings are in
[evidence.json](deslop-resources-2026-10-04/evidence.json).

| Run | Wall seconds | Peak RSS MiB | Peak CPU % | Average CPU % | Throttle pause seconds | Exit |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| deslop-baseline | 12.82 | 751.92 | 821.62 | 162.88 | 0.00 | 0 |
| deslop-cpu-throttled | 14.21 | 738.59 | 1125.62 | 146.30 | 1.99 | 0 |
| deslop-cold-baseline | 16.10 | 819.11 | 1119.94 | 151.41 | 4.70 | 0 |
| deslop-after-client | 14.17 | 753.42 | 1207.10 | 143.89 | 2.00 | 0 |
| deslop-round-measure | 13.48 | 779.38 | 1401.28 | 146.47 | 2.37 | 0 |
| deslop-after-function-coverage | 13.49 | 735.75 | 1180.69 | 141.18 | 2.13 | 0 |
| deslop-after-mapping-fixtures | 13.86 | 752.06 | 750.53 | 143.34 | 0.61 | 0 |

The initial run used a sustained CPU budget guard. Subsequent runs added
feedback throttling: pause only the launched Deslop PID when accumulated CPU
seconds exceed twice elapsed wall seconds plus a 0.25-second allowance, then
resume once the budget catches up. This bounds average consumption but permits
short parallel bursts. No VS Code or unrelated process is stopped or signalled.
Every scan has a 120-second wall timeout and 2048-MiB process-tree RSS cutoff.
All child commands run at niceness 10. Monitoring prevents a runaway scan from
continuing; it cannot guarantee that no allocation spike occurs between samples.

Baseline `make test` also passed with all coverage gates: 264.35 seconds,
1638.31 MiB peak process-tree RSS. .NET was restricted to two processors,
1536-MiB GC heap, one MSBuild worker, and disabled build servers. Cargo used two
build jobs. Testcontainers ran in a separate Colima VM limited to two CPUs and
6 GiB; VM allocation is separate from the measured command process tree.

## Released worker selection [DEDUP-RESOURCE-WORKERS]

The source at the **v0.36.0 tag**, independently checked from the local Deslop
checkout, chooses shard workers from `std::thread::available_parallelism()`:

- [shard.rs worker_count](https://github.com/Nimblesite/Deslop/blob/v0.36.0/crates/deslop-core/src/shard.rs#L53)
  caps workers by machine parallelism and work size.
- [corpus.rs fingerprint_corpus](https://github.com/Nimblesite/Deslop/blob/v0.36.0/crates/deslop-core/src/pipeline/corpus.rs#L102)
  passes machine parallelism to corpus processing.

Neither path reads the Rayon setting. This explains why that environment hint
fails to cap these worker pools. This report is evidence for a supported
cross-pipeline worker limit; changing DataProvider's Rayon environment alone
would not fix it. Deslop's engine source was not changed in this task.

## Reproduction [DEDUP-RESOURCE-REPRODUCTION]

1. Check out the baseline commit above with its committed `.deslop.toml`.
2. Use the verified macOS arm64 binary from the v0.36.0 release.
3. Run a bounded, monitored scan with `RAYON_NUM_THREADS=2`:

   ```sh
   nice -n 10 deslop . --notext --nohtml --no-color
   ```

4. Repeat with `--no-incremental` for the cold path. Keep the same config.
5. Record process-tree RSS and cumulative CPU time at 250-ms intervals. Abort
   above 2048 MiB RSS or 120 seconds. Monitor before running; do not run this
   reproduction without the guards.
6. Compare `metrics` and cluster membership in the JSON outputs. The CLI
   fallback workflow is documented in [Deslop's agent guide](https://deslop.live/docs/for-ai/).

The release writes an empty `schema_doc` string in this report despite the guide
recommending that field. Consumers must inspect the actual JSON keys and use the
published reference; an empty schema document is not an empty duplicate result.

## Duplication work [DEDUP-ROUND-RESULTS]

The reduction target is **10%**. Refactor only confirmed equivalent implementations
with test coverage, preserve public APIs and assertions, and rescan after each
change. Independent SQL catalog verification and distinct assertion predicates
must not be collapsed merely because the detector reports similar shapes.
Measured checkpoints (same scope and detector):

| Checkpoint | Analysed LOC | Duplicated LOC | Duplication |
| --- | ---: | ---: | ---: |
| Baseline | 116630 | 45993 | 39.434965% |
| Shared dialect rendering | 116578 | 45795 | 39.282712% |
| SQLite lifetime and cleanup regression | 116463 | 45733 | 39.268265% |
| PostgreSQL migration runner | 116303 | 45434 | 39.065200% |
| SQLite client operation delegation | 116347 | 45219 | 38.865635% |
| Shared expression tests and file-size split | 116334 | 45222 | 38.872557% |
| Forty additional migration runner calls | 116273 | 45252 | 38.918752% |
| Shared function mappings and F# execution cases | 116275 | 45187 | 38.862180% |
| Shared mapping test factories | 116244 | 45103 | 38.800282% |
| Shared LQL error assertions | 116124 | 44950 | 38.708622% |
| Shared migration diff assertions | 116071 | 44813 | 38.608266% |
| HTTP mapping integration fixture | 115982 | 44694 | 38.535290% |
| SQLite scalar execution | 115958 | 44700 | 38.548440% |
| Shared user transformation fixture | 115894 | 44611 | 38.492933% |
| Identical completion label collection (mass 240) | 115892 | 44594 | 38.478929% |
| Identical person parameters, compact calls (mass 225) | 115900 | 44575 | 38.459879% |
| Identical default HTTP mapping source setup (mass 192) | 115886 | 44568 | 38.458485% |
| Shared covered context ordering (mass 190) | 115889 | 44569 | 38.458352% |
| Shared users schema fixture (mass 140) | 115823 | 44484 | 38.406879% |
| Reuse 42 typed assertion results | 115797 | 44422 | 38.361961% |
| Shared YAML roundtrip | 115785 | 44397 | 38.344345% |
| Shared table checks | 115785 | 44386 | 38.334845% |
| Shared subscription roundtrip | 115774 | 44326 | 38.286662% |
| Shared sync log reader | 115704 | 44208 | 38.207841% |
| Reuse twelve SQLite seed pipelines | 115578 | 44067 | 38.127498% |

**The 10% target has not been reached.** The latest retained validated scan is **38.080198%**,
a 1.354767 percentage-point reduction from baseline. The user-transformation
fixture, migration-diff, HTTP mapping and SQLite scalar batches
passed all tests and all twelve precise coverage floors.
The repository gate is currently **38.92%**, down from 39.44%; it will be ratcheted
again after final verification. No scope exclusions or hidden findings changed.
This is a measured checkpoint, not a claim that remaining duplication is acceptable
or entirely false positive.

Changes consolidate SQL rendering across three dialects, SQLite client operations,
SQLite test lifetimes, 31 PostgreSQL migration calls followed by 40 additional
provider-specific calls, and parse/evaluate/assert execution for 38 expression
cases. Every original test case and assertion remains enforced. The expression
fixtures are split by expression versus mapping behavior to meet the 450-line
file limit; splitting files is not counted as deduplication work.

The SQLite cleanup refactor exposed an existing lifecycle defect: the helper
unlinked its database before disposing the open connection. A real-file regression
failed before the fix and passed after it. Disposal now precedes cleanup. Full
`make test` passed after rendering, lifetime, cleanup, and PostgreSQL runner changes;
the nine new client database-error cases passed before client refactoring.
The full suite after client refactoring passed in 204.649 seconds at 1825.67 MiB
peak RSS. Every measured .NET coverage percentage stayed at or above baseline;
SQLite sync increased from 40.08% to 40.47%.

The first full client-validation run failed because Playwright's required
`chromium_headless_shell-1155` executable was absent from the shared cache.
Restoring that exact browser with browser garbage collection disabled fixed the
environment; the subsequent full run passed. No product code was changed for
this failure.

Shared SQL function maps now own the common aggregate, string, and window
mappings used by the dialect providers; dialect-specific mappings and public APIs
are retained. The first full test run passed but exact F# coverage fell from
47.60% to 47.54%, which failed the stricter baseline comparison. Two real SQLite
execution cases exercise the provider API from F# and raised coverage to **49.00%**.
The subsequent full suite passed: **234.030 seconds, 1629.91 MiB**, 2105 .NET tests,
with every .NET coverage floor preserved.

Mapping fixture work consolidates seven helper copies and 94 setup calls or
constructors across three test fixtures. All 38 cases and 144 assertion sites
remain. Scenario-specific exclusions, primary keys, direction, disabled mappings,
and multi-target settings are preserved. The full suite passed in **201.847 seconds, 1820.70 MiB**; Sync.Core coverage rose
from 65.11% to **65.97%**, with every baseline floor preserved.

The first mapping-fixture validation failed because the shared Playwright cache
again lacked `chromium_headless_shell-1155`. The batch was saved and reverted.
The exact browser was installed into a task-specific cache with garbage collection
disabled; a real headless launch passed, then the unchanged batch was reapplied
and the full suite passed. Subsequent guarded commands inherit the private cache
path, so external installations cannot evict this task's browser. No product fix
or test weakening was used to resolve the missing executable.

LQL error tests now share parsing and error assertions. A Roslyn call-graph audit
confirmed identical literal inputs and assertion kinds across all 14 test methods
(13 facts plus one two-row theory), accounting for helper calls. All original
message comparisons and position bounds remain enforced. The file fell from 317
to 197 analysed lines and from 177 to 24 duplicated lines. The full suite passed
in **222.435 seconds, 1832.25 MiB** with unchanged coverage floors.

The next batch shares 25 migration-diff success checks through the existing NAP
upgrade test helper, moved into a common utility. Eight destructive cases moved
to a partial fixture to keep both files below 450 lines. No case was removed;
full-suite validation passed with all twelve coverage floors preserved.

## Upstream findings [DEDUP-RESOURCE-UPSTREAM]

- CPU bursts and missing CLI-wide worker control are filed as
  [Deslop #578](https://github.com/Nimblesite/Deslop/issues/578), including release
  hashes, command, host limits, measurements, and the tagged worker-selection source.
- The unrelated assertion fragments in
  [Deslop #534](https://github.com/Nimblesite/Deslop/issues/534#issuecomment-5969944576)
  still reproduce. Cluster `f5dce1bf917e31b4` joins SELECT/JOIN SQL assertions to
  generated INSERT/UPDATE assertions. Explicit pair comparison reports structural
  similarity 1.0 and fused score 1.0 despite rename consistency 0.0, literal fraction
  0.0, agreement 0.5, and different text. The raw paired snippets and comparison
  output are retained in the evidence file. This finding does not establish that
  all remaining duplication is false positive.

- Already-delegated named expression tests are filed separately as
  [Deslop #579](https://github.com/Nimblesite/Deslop/issues/579). Cluster
  `ac3551bddc6c5db6` joins ASCII uppercasing, Unicode identity, and invalid-date
  fallback cases whose only bodies call the same shared assertion helper.
  The finding includes three exact source spans, report metadata, and the helper.
- Distinct multiline catalog SQL counted as duplicate code is filed as
  [Deslop #581](https://github.com/Nimblesite/Deslop/issues/581). Column introspection
  and index introspection scored structural/token/fused **1.0**, despite different
  SQL operations and `literal_fraction: 0.0`. Cluster `39a23feda30750d4` has 32
  occurrences. The exact source pair, byte spans, comparison and resource result
  are saved in the evidence file. Shared parameter binding does not make these
  SQL query bodies interchangeable.
- Concurrent release-tool work on the shared branch changed analysed source
  after the client checkpoint. Subsequent whole-repository totals include those
  files; they must not be attributed solely to the dedup refactors.

## Other processes and failed commands [DEDUP-RESOURCE-LIMITATIONS]

A read-only snapshot of the installed `nimblesite.deslop-live-0.0.0-dev` editor
LSP showed 704.19 MiB RSS and 0% CPU. This is a development build and a single
instantaneous observation, not a released-CLI benchmark or idle-time guarantee.
No editor process was stopped.

A read-only Pi review reached its 180-second timeout (about 135 MiB RSS) and was
terminated by its own watchdog. It produced no completed review and is not counted
as an approval. Early client-contract test commands failed to compile because the
new tests needed a migration project reference and had an unused using after the
fixture split; both were corrected before production refactoring. A formatter
invocation used obsolete syntax and exited 1; rerunning with the installed
`csharpier format` command passed. None of these failures was an OOM or host crash.

## Validation checkpoints and coordination [DEDUP-ROUND-VALIDATION]

- Earlier runner checkpoint `make test`: exit 0, **186.818 seconds**, **1798.28 MiB** peak process-tree
  RSS, 466.07% sampled peak CPU. All **2103 .NET tests** passed (10 additional cases
  compared with baseline), plus Rust and 70 extension tests. All 12 .NET coverage
  reports meet or exceed baseline; Rust remains 98.54%, extension 54.1%.
- `make lint`: exit 0, **31.369 seconds**, **1341.75 MiB** peak RSS. Includes .NET
  analyzers, CSharpier, Rust formatting/Clippy, and ESLint. No dead code was removed
  without demonstrated absence of callers.
- Resource cutoffs remained enabled. No Deslop scan, build, or test hit its memory
  cutoff; no OOM or host crash was observed. This does not guarantee absence of
  transient peaks between samples or safety for every workload.
- Source changes remain on the single `refactor/deslop-ten-percent` branch.
  Concurrent release PR #116 merged separately with green CI. Its workflow/stamping
  changes are excluded from the description of this refactor.
- Colima remains at two CPUs, 6 GiB, Rosetta enabled for the concurrent release
  and continuing dedup tasks. Cleanup must be coordinated after both finish,
  restoring the original **stopped, 4 GiB, Rosetta disabled** state. The release
  agent has agreed to leave it running for the next dedup test cycles.

The remaining percentage requires further investigation and refactoring. Confirmed
precision problems have been logged upstream with reproducible evidence; neither
raising the gate nor hiding tests is used to manufacture a 10% result.

## Further verified refactors [DEDUP-ROUND-SCALARS]

The migration-diff batch passed in 182.64 seconds with 1710.53 MiB peak RSS;
the HTTP mapping fixture passed in 176.52 seconds with 1707.77 MiB peak RSS.
The SQLite scalar batch passed in 180.05 seconds with 1817.22 MiB peak RSS.
All runs passed the full suite (2105 .NET tests, Rust tests and 70 extension tests)
and preserved all twelve measured coverage floors. No watchdog cutoff occurred.

Five SQLite scalar readers now use the existing command executor, moved from
MappingRepository. They retain their SQL, strict Int64-or-zero behavior, exception
type and exact error prefixes. Conversion-based and string-parsing readers stay
distinct. The five affected files lost 24 analysed LOC and 13 duplicated LOC;
the repository-wide count nevertheless rose by six duplicated lines because
cluster membership depends on the entire corpus. Already-shared public forwarders
remain counted by the detector. These observations are not evidence that every
remaining finding is false.

The largest approximate CPU sample, 1468.33%, occurred in the 13.29-second
migration-diff scan (734.83 MiB RSS). Its sampling interval and rounded process
counters can yield estimates above the host's fourteen-core capacity. It confirms
a brief near-full-machine burst, not precise utilization above available capacity.

## Identical clusters by impact [DEDUP-IDENTICAL-PRIORITY]

The CLI JSON contained 119 identical clusters. Work now prioritizes these in descending mass. Cluster `26b8c3ed8490f7b5` (mass 240) repeated completion execution and label collection seven times. One helper collects labels, preserving repeated labels and all 20 tests and 52 assertions. The full suite passed in 209.683 seconds with peak process-tree RSS 1738.34 MiB and all twelve coverage floors preserved. The rescan no longer contains that cluster; duplication is 44594 / 115892 = **38.478929%**. The scan used 725 MiB and 13.475 seconds, with sampled CPU peaking at 1151.36% despite the CPU feedback guard.

The next highest identical cluster is `b03545de11cfdb52` (mass 225): six copies of id/name/email command binding and execution. Its shared helper retains each original SQL statement and connection. The full suite passed in 190.245 seconds with peak RSS 1697.89 MiB and all twelve coverage floors held. The cluster is absent from the rescan (38.476800%). Compact helper calls passed another full suite in 189.815 seconds, peak RSS 1782.34 MiB, with all floors preserved. The compact-call scan is **38.459879%**, with the original cluster still absent. An exact reconstruction audit preserves all SQL and 182 assertions across the three fixtures.

Priority review also found identical Eleventy JavaScript configuration clusters (mass 200 and 164). These untyped website files have no measured test coverage, so the code-dedup safety gate excludes them from edits. The mass-190 LQL context cluster consists chiefly of required public API forwarding methods and XML documentation; the public contracts must remain present. The substantive ordering loop remains a candidate for sharing internally. These decisions do not alter scan scope, findings, or the duplication ceiling.

The first parameter-helper attempt failed compilation because an unqualified `Tests.Shared` reference resolved inside the SQLite test namespace. That batch was reverted, qualified correctly, and rerun successfully. This was an implementation error, not a Deslop failure or resource termination. The failed and successful watchdog results are retained in evidence.json.

At a read-only host memory checkpoint during the bounded test runs, macOS reported **55% free memory** and **1409.88 MiB swap used**, below the initial 1505.88 MiB swap usage. These observations do not establish causal attribution. The timeout/RSS guards remained active.

Cluster `d88177f6d9e4f0b0` (mass 192) is absent after consolidating seven default mapping source setups. Exact source reconstruction confirms all 14 named cases and 69 assertions in the two partial fixtures remain unchanged. Full validation passed in 189.055 seconds with peak RSS 1684.44 MiB and all twelve coverage floors preserved. The rescan took 13.545 seconds, peak RSS 767.70 MiB, sampled peak CPU 1216.42%.

Within cluster `c3cb8af7187b60f8` (mass 190), three fully covered public ordering loops now delegate to one internal Core implementation. Exact reconstruction preserves the public signatures and all remaining source, including XML documentation. The uncovered private SQLite ordering path stays unchanged. Full validation passed in 227.351 seconds, peak RSS 1807.61 MiB, and all twelve baseline floors held. The original cluster is absent. Whole-corpus duplicated LOC increased by one while analysed LOC increased by three; the genuine shared loop therefore makes only a tiny percentage change, to **38.458352%**. This is recorded without treating the cluster mass as removed LOC.

The mass-180 mapping payload extraction passed all tests but moved the corpus from 44569 to 44581 duplicated LOC (38.468043%), despite removing its original cluster. It was reverted to the exact previously verified source. No extra abstraction or assertion from that attempt remains. Its scan, validation, and source audit remain in evidence.json for traceability.

Nine identical users-schema constructions now extend the existing UUID-primary-key fixture. The mass-140 cluster is absent, and the scan fell to **38.406879%** (44484 / 115823). Exact reconstruction preserved all 19 facts and 45 assertions in both partial files. The full suite passed in 193.370 seconds, peak RSS 1726.38 MiB, with all twelve baseline coverage floors preserved.

Forty-two existing typed xUnit assertions now supply their checked value directly, removing the following cast. A Roslyn token audit proves all **498 assertions**, including their arguments, and every test method/attribute remain unchanged across eleven files. The full suite passed in **235.270 seconds**, peak RSS **1684.08 MiB**, and all twelve coverage floors held. The rescan is **38.361961%** (44422 / 115797), 62 fewer duplicated LOC than the preceding retained checkpoint.

The mass-129 required-email column preset passed all tests and preserved 48 facts and 226 assertions, but the corpus rose from 44422 to 44432 duplicated LOC (38.368940%). It was reverted to the exact previous source. The scan and validation evidence are retained; the preset is not part of the changes.

Four identical YAML roundtrip/table selections (mass 108) now share one helper. All named tests and assertions are unchanged by the Roslyn token audit. Full validation passed in 193.730 seconds, peak RSS 1686.06 MiB, with all twelve coverage floors held. The cluster is absent; the retained scan is **38.344345%** (44397 / 115785), 25 fewer duplicated LOC. Scan: 13.242 seconds, 757.48 MiB peak RSS, 1124.98% sampled peak CPU. The 10% target remains unmet.

The mass-104 table-existence cluster is absent after sharing its four checks across three migration scenarios. Exact reconstruction preserves all twelve assertions. Full validation: 222.776 seconds, 1622.58 MiB peak RSS, all twelve coverage floors held. Retained scan: **38.334845%** (44386 / 115785), 13.044 seconds, 700.83 MiB RSS, 1132.88% sampled peak CPU.

The mass-96 subscription roundtrip cluster is absent. All three cases reconstruct exactly with their original inputs and success/single-row assertions; the shared helper also checks the typed result. Full validation: 208.280 seconds, 1686.47 MiB peak RSS, all twelve coverage floors held. Retained scan: **38.286662%** (44326 / 115774), 60 fewer duplicated LOC; 13.085 seconds, 729.02 MiB RSS, 1123.66% sampled peak CPU.

Reusing the canonical test sync-log reader removed two query bodies and the duplicated operation parsers (mass 94). Roslyn audits prove all 70 assertions and named cases remain unchanged, all three reader bodies match, and coordinator error handling is preserved. Full validation: 178.439 seconds, 1655.59 MiB peak RSS, all twelve coverage floors held. Retained scan: **38.207841%** (44208 / 115704), 118 fewer duplicated LOC; 12.804 seconds, 741.31 MiB RSS, 1153.81% sampled peak CPU.

Twelve matching SQLite inspect/diff/apply pipelines reuse the existing seed helper; two cases retain their intermediate operations for later assertions. All 143 assertions and named tests remain unchanged by the Roslyn audit. Full validation: 209.219 seconds, 1783.05 MiB peak RSS, all twelve coverage floors held. Retained scan: **38.127498%** (44067 / 115578), 141 fewer duplicated LOC and 126 fewer source lines; 12.777 seconds, 728.31 MiB RSS, 1123.26% sampled peak CPU.

The mass-84 Rust column fixture cluster is absent after moving its constructor to a private test-only module. All other non-import source remains unchanged: 51 tests and 107 assertions. Full validation: 204.565 seconds, 1806.16 MiB peak RSS, all twelve coverage floors held. Retained scan: **38.101048%** (44033 / 115569), 34 fewer duplicated LOC; 12.571 seconds, 729.23 MiB RSS, 1265.79% sampled peak CPU.

The mass-77 obsolete-column fixture cluster is absent after extending the existing users builder. All 45 assertions and named cases in both partial fixtures are unchanged. Full validation: 181.119 seconds, 1713.88 MiB peak RSS, all twelve coverage floors held. Retained scan: **38.080198%** (44007 / 115564), 26 fewer duplicated LOC; 12.607 seconds, 750.52 MiB RSS, 1321.18% sampled peak CPU.

The first shared bulk-loop attempt passed the full suite and retained all 24 captured generated outputs byte for byte. However, the original DataProvider coverage floor check failed: 90.48% versus 90.68%. It was immediately reverted, including its helper and spec changes. Its 38.004899% scan is rejected and is not the current retained result. Full test: 204.144 seconds, 1788.77 MiB RSS; scan: 12.505 seconds, 737.45 MiB RSS. No resource cutoff or host crash occurred. Missing default-config source-validation coverage is investigated before retrying.
