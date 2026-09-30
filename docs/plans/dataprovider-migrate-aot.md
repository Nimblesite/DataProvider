# Plan: AOT-compile and AOT-test DataProviderMigrate

Spec group: `[MIG-AOT]`. Make the `DataProviderMigrate` CLI publish as a native
AOT executable and prove it works via tests that run the **native binary** (not
in-process `Program.Main`).

## Why this is non-trivial

The CLI's csproj lists only three project references, but the real AOT surface is
the whole transitive graph:

```
DataProviderMigrate
└─ Migration.Core ─┬─ YamlDotNet            (reflection serialization)  ← BLOCKER 1
                   ├─ Nimblesite.Lql.Core ─ Nimblesite.Sql.Model         ← BLOCKER 2
                   ├─ Nimblesite.Lql.Postgres / SQLite / SqlServer
                   └─ (SqlParserCS, Outcome)
   Migration.SQLite ─ Microsoft.Data.Sqlite (native SQLite, AOT-friendly)
   Migration.Postgres ─ Npgsql              (verify trim/AOT cleanliness)
```

## Blockers (empirically confirmed via `dotnet publish -p:PublishAot=true -r osx-arm64`)

### [MIG-AOT-YAML] YamlDotNet runtime reflection — BLOCKER 1
`SchemaYamlSerializer` uses `SerializerBuilder`/`DeserializerBuilder`, which reflect
over `SchemaDefinition` at runtime. AOT-incompatible.
- Fix: migrate to YamlDotNet 16.x **static** API — `[YamlStaticContext]` + source
  generator + `StaticSerializerBuilder`/`StaticDeserializerBuilder`.
- **OUTCOME: static generator REJECTED.** `Vecc.YamlDotNet.Analyzers.StaticGenerator`
  16.2.1 emits a deserializer that does property *assignment* (`obj.X = v`), which
  fails `CS8852` against our immutable `init`-only records — every schema property.
  Immutable records are a hard project rule, so the generated path is unusable.
- **DECISION: hand-rolled, reflection-free YAML.** Verified AOT-clean via a probe:
  YamlDotNet's `RepresentationModel` DOM (read) and low-level `Emitter` + `Events`
  (write) publish with **zero IL warnings** and run natively. The schema format is
  already fully hand-encoded by the four converters, so a purpose-built
  writer/reader preserves it exactly with no new dependency. New files:
  `SchemaYamlWriter.cs` (Emitter), `SchemaYamlReader.cs` (DOM), shared scalar
  encode/parse helpers; `SchemaYamlSerializer` keeps its public API.

### [MIG-AOT-DYNCODE] Expression.Lambda().Compile() — BLOCKER 2
`IL3050` from `Nimblesite.Sql.Model`:
- `SelectStatementVisitor.cs:470` — `Expression.Lambda(expr).Compile().DynamicInvoke()`
- `SelectStatementLinqExtensions.cs:426` — same pattern
These are constant-folding fallbacks on a narrow LINQ-translation path the migrate
CLI does not exercise. `PredicateBuilder`'s `Expression.Lambda<Func<T,bool>>` only
*builds* trees (no `.Compile()`) — AOT-safe, leave alone.
- **DONE.** Replaced both `.Compile().DynamicInvoke()` sites with a new
  `ConstantExpressionEvaluator.TryEvaluate` that walks the expression tree
  (constants, captured fields, static members) with no dynamic code. The 63
  `SqlModelCoverageTests` still pass — behavior preserved.

### [MIG-AOT-JSON] System.Text.Json reflection — BLOCKER (found during publish)
`SchemaSerializer.ToJson/FromJson` used `JsonSerializer.Serialize<T>(T, options)`
(`IL2026` + `IL3050`). The CLI's migrate path never calls it, but it is public API.
- **DONE.** Added a source-generated `SchemaJsonContext` (`[JsonSerializable]`) and
  routed through the typed `JsonTypeInfo` overload; the `PortableType` converter is
  attached to the context options. Round-trip test passes.

### [MIG-AOT-EXPORT] export command uses Assembly.LoadFrom — BLOCKER 3
`Program.ExecuteExport` does `Assembly.LoadFrom` + `GetType` + reflected
property/method invoke. **Fundamentally incompatible** with a self-contained native
binary (cannot load arbitrary external managed DLLs). Decision required:
- Option A (recommended): keep `export` only in the non-AOT (dotnet-tool) build;
  compile it out under an `AOT` MSBuild constant so the native binary ships
  `migrate` only. The `export` tool remains available as the managed `dotnet tool`.
- Option B: drop `export` from the CLI entirely.
Default to **A** unless the user says otherwise.

### [MIG-AOT-NPGSQL] Npgsql trim/AOT cleanliness
Npgsql 9 is largely AOT-friendly but may emit trim warnings for type mapping.
Capture and resolve any `IL2xxx`/`IL3xxx` from Npgsql; add a trim feature switch or
runtime directives only if needed. `Microsoft.Data.Sqlite` (native SQLitePCLRaw) is
AOT-friendly.

## Distribution goal (from the user)

Ship the migration tool BOTH ways from one source tree:
1. **Managed `dotnet tool`** via NuGet — the existing pack path (`PackAsTool`).
   Keeps `export` (reflection is fine in the managed tool).
2. **Native binaries** via **Homebrew tap + Scoop bucket** — per-platform AOT
   builds (`migrate` only). This is the `Shipwright` release model already used in
   this repo (`shipwright.json`, brew/scoop publish workflows). The `[MIG-AOT-CI]`
   native publish feeds those artifacts.

## Build & test strategy

- `DataProviderMigrate.csproj`: AOT analyzers always on (`IsAotCompatible`,
  `EnableTrimAnalyzer`, `EnableAotAnalyzer`) so `make lint` catches reflection
  regressions; `PublishAot`/`InvariantGlobalization`/`AOT` constant turn on only
  under a Native AOT publish, leaving the managed `dotnet tool` pack untouched.
- Tests: existing CLI tests call `Program.Main` in-process — they do NOT test AOT.
  `NativeAotMigrateSmokeTests` drives the PUBLISHED binary as a **subprocess**
  (`ProcessStartInfo`), gated on `DATAPROVIDERMIGRATE_AOT_BIN` (set by the AOT
  publish/CI), asserting exit codes + schema integrity + idempotency + the
  export-unsupported message. `SkippableFact` skips visibly when the binary is
  absent, so `make test` stays fast and CI runs the real native verification.
- `make` target + CI matrix job publishes native per-platform, sets the env var,
  runs the smoke tests, then hands the binaries to brew/scoop publishing.

## TODO

- [x] **[MIG-AOT-DYNCODE]** `ConstantExpressionEvaluator` replaces both
      `.Compile().DynamicInvoke()` sites. 63 SqlModel tests pass; IL3050 gone.
- [x] **[MIG-AOT-JSON]** Source-gen `SchemaJsonContext` + typed `JsonTypeInfo`
      overloads. Round-trip test passes.
- [x] **[MIG-AOT-EXPORT]** `export` gated under `#if !AOT` in `Program.Export.cs`;
      native build prints unsupported + exits non-zero. Managed tool keeps export.
- [ ] **[MIG-AOT-YAML]** Hand-rolled reflection-free writer (Emitter) + reader (DOM)
      replacing the YamlDotNet reflection serializer (static gen rejected — CS8852 on
      init-only records). Keep `SchemaYamlSerializer` public API; preserve format so
      all YAML tests pass. (workflow `aot-yaml-mapper` in flight)
- [ ] **[MIG-AOT-NPGSQL]** Publish again; resolve any Npgsql/SqlParserCS/Outcome/LQL
      (ANTLR) trim or AOT warnings. Clean `dotnet publish -p:PublishAot=true`.
- [x] **[MIG-AOT-CSPROJ]** AOT props + analyzers added; `AOT` constant + Invariant +
      pack-off only under PublishAot. (verify managed pack still works post-YAML)
- [x] **[MIG-AOT-TEST]** `NativeAotMigrateSmokeTests` subprocess fixture +
      `Xunit.SkippableFact`. (runs once Core compiles + binary published)
- [ ] **[MIG-AOT-CI]** `make` target + CI matrix publish native + run smoke test per
      platform; feed binaries to brew tap + scoop bucket (Shipwright). Timeout = fail.
- [ ] **[MIG-AOT-VERIFY]** Final: clean AOT publish (osx-arm64) zero IL warnings,
      native `migrate` smoke test passes end to end; managed `dotnet pack` still works.
