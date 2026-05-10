# SPEC: DataProvider Codegen Tool

> **RIGID. NORMATIVE.** Resolves [PROBLEM-cli-vs-library.md](./PROBLEM-cli-vs-library.md). Database-agnostic. Consumer-agnostic.

## ⚠️ NAMING IS NON-NEGOTIABLE ⚠️

The tool, the package, the executable, and every internal lib scoped under the tool are **`DataProvider`** — bare, no `Nimblesite.` prefix, no `.Tool` / `.Cli` suffix. External libs the tool consumes (`Nimblesite.Lql.*`, `Nimblesite.Sql.Model`, `Outcome`) keep their existing names.

Renaming any `DataProvider.*` artifact to add a prefix or suffix is grounds for the renamer being purged from the data center. — repo owner.

## Overview

`DataProvider` is a console exe shipped **inside** the `DataProvider` NuGet package and invoked by an auto-imported `build/DataProvider.targets` file in the same package. By default it runs in **`schema-doc` mode**: it loads a YAML schema doc from the project, applies convention-driven CRUD emission, transpiles `.lql` → platform SQL, and emits typed data-access code through pluggable templates (default C#, optional any language) into the consumer's `obj/` directory for compilation. In opt-in **`live-db` mode** (`--mode live-db`) it instead opens a real driver connection and introspects the live schema. With `--watch` the same exe stays resident as a hand-rolled LSP, watches `*.schema.yaml` across the solution, and regenerates only the outputs of the project whose schema changed.

## CONSTRAINT

| ID | Constraint |
|---|---|
| CON-NOTOOLMANIFEST | Zero `.config/dotnet-tools.json`, zero `dotnet tool restore` in any consumer. The tool ships inside the package and runs from the auto-imported `.targets`. |
| CON-SIMPLE | One `PackageReference` + one MSBuild property per consumer csproj. No custom `<Target>`, no `<Compile Include="Generated/**">`, no `RemoveDir`/`MakeDir`/`Touch`, no `sed`. |
| CON-MODE | Two modes: `schema-doc` (default) and `live-db`. Mode resolved from `--mode {schema-doc\|live-db}`. `schema-doc` mode is specified in `## SCHEMA-DOC` below. `live-db` mode is what the rest of this spec describes — no behavioral change. |
| CON-NOCONFIG | `DataProvider.json` is **not required**. In `schema-doc` mode the YAML doc + conventions provide everything needed. The config file remains as overrides only. |
| CON-DBLIVE | **Live-db mode only.** Live driver connection + live schema introspection. Static YAML/JSON schema parsing forbidden in this mode. Unreachable DB → loud diagnostic + build fail. |
| CON-NOFALLBACK | **Live-db mode only.** Zero fallbacks. Zero degraded modes. No offline cache. Build passes against a live DB or fails loud. |
| CON-MIG-FIRST | **Live-db mode only.** `dataprovider-migrate` runs against the live DB **before** every codegen invocation. Every live-db flow (CI, local, IDE) runs migrations first. The migration tool stays. |
| CON-UNIVERSAL | One single mechanism for **every** consumer of **every** platform. No consumer-specific names, paths, or domain models. |
| CON-PLATFORM-AGNOSTIC | One single mechanism for **every** database platform. Identical contract. |
| CON-PROCESS-ISOLATION | Codegen runs in its own process, separate from `dotnet build` / MSBuild / IDE. Process isolation is the only way to load any TFM driver + any native dep on any host. |
| CON-PARSER-ONLY | Every platform parses input SQL with a **real parser** — its vendored ANTLR `.g4` grammar. Hand-rolled string scanning, regex against SQL tokens, char-by-char SQL walking, and any pseudo-lexer are ⛔️ ILLEGAL per `CLAUDE.md`. Postgres uses `antlr/grammars-v4/sql/postgresql` (derived from upstream `postgres/postgres/src/backend/parser/gram.y`). SQLite uses `antlr/grammars-v4/sql/sqlite`. |
| CON-SHARED-CORE | Per-platform libs are thin shells. Anything not strictly platform-specific lives in `DataProvider.Core`. The only platform-specific code allowed is: (a) vendored ANTLR `.g4` + generated parser sources, (b) `{Platform}AntlrParser` shell, (c) `{Platform}DatabaseEffects` shell (connection factory + SQL→C# type mapping), (d) `{Platform}CodeGenerator` shell that constructs the platform's `CodeGenerationConfig` for `Core.SqlAntlrCodeGenerator`. ≤ 5 author-written files per library, ≤ 200 LOC each excluding generated parser sources. **Duplication between platform libs is grounds for review rejection.** |

## PLATFORM

| Token | Database | Driver | Status |
|---|---|---|---|
| `Postgres` | PostgreSQL | `Npgsql` | day one |
| `SQLite` | SQLite | `Microsoft.Data.Sqlite` (`e_sqlite3` per RID) | day one |
| `SqlServer` | SQL Server | `Microsoft.Data.SqlClient` (`sni` per RID) | day one |

A platform qualifies iff its primary .NET driver loads inside the tool's net9.0 process. Adding a platform = one `{Platform}AntlrParser` + one `{Platform}DatabaseEffects` + one `{Platform}CodeGenerator` shell + one driver in the tool's deps + a CI run of [TEST-NATIVE]. **No consumer csproj changes.**

Platform selection is **always** explicit via `--platform` (passed by the auto-imported `.targets` file from `<DataProviderPlatform>`). No connection-string sniffing.

## PARSER

Per [CON-PARSER-ONLY], every platform parses with a vendored ANTLR grammar.

| Platform | Grammar source |
|---|---|
| `Postgres` | [`antlr/grammars-v4/sql/postgresql`](https://github.com/antlr/grammars-v4/tree/master/sql/postgresql), derived from upstream [`gram.y`](https://github.com/postgres/postgres/blob/master/src/backend/parser/gram.y) |
| `SQLite` | [`antlr/grammars-v4/sql/sqlite`](https://github.com/antlr/grammars-v4/tree/master/sql/sqlite) (already vendored) |
| `SqlServer` | TBD when introduced |

### PARSER-LAYOUT

```
DataProvider/Nimblesite.DataProvider.{Platform}/Parsing/
├── {Platform}Lexer.g4              # vendored grammar
├── {Platform}Parser.g4             # vendored grammar
├── {Platform}{Lexer,Parser}Base.cs # vendored C# helpers (if grammar uses them), namespaced
├── {Platform}{Lexer,Parser,ParserListener,ParserBaseListener,ParserVisitor,ParserBaseVisitor}.cs  # antlr4-generated, checked in
└── {Platform}AntlrParser.cs        # thin facade we author, ≤ 100 LOC
```

The parameter extractor and query-type listener live in `DataProvider.Core` and operate on any `IParseTree`. They are not duplicated per platform.

### PARSER-REGEN

To bump a grammar:

1. `curl` latest `.g4` + `CSharp/*.cs` from `antlr/grammars-v4/sql/{platform}` into `Parsing/`.
2. Wrap helper base files in `namespace Nimblesite.DataProvider.{Platform}.Parsing;`.
3. `java -jar antlr-4.13.1-complete.jar -Dlanguage=CSharp -visitor -listener -o Parsing -package Nimblesite.DataProvider.{Platform}.Parsing Parsing/{Platform}{Lexer,Parser}.g4`
4. Commit the regenerated `.cs` next to the updated `.g4`.
5. Run the platform's parser test suite.

`antlr4` is **not** a build dependency — only a maintenance dependency for regen.

## TOOL

| ID | Spec |
|---|---|
| TOOL-NAME | `DataProvider` |
| TOOL-PROJECT | `DataProvider/DataProvider/`. `<OutputType>Exe</OutputType>`, `<TargetFramework>net9.0</TargetFramework>`, `<AssemblyName>DataProvider</AssemblyName>`. **No** `PackAsTool=true`. |
| TOOL-ARGS | Required: `--out`, `--platform`. `--mode {schema-doc\|live-db}` defaults to `schema-doc`. `--connection` required iff `--mode live-db`. `--config` optional in both modes (overrides only — see [CON-NOCONFIG]). `--watch` runs the tool as a long-lived LSP per `## SCHEMA-DOC`. `--templates` points at a custom template directory per `## TEMPLATES`. Optional: `--namespace` (default `<RootNamespace>.DataProvider.Generated`), `--accessibility` (default `public`), `--verbosity`. |
| TOOL-DEPS | References `DataProvider.Core`, every `DataProvider.{Platform}`, every `Nimblesite.Lql.{Platform}`, `Nimblesite.Sql.Model`, `Outcome`, plus every database driver. **All TFMs are net9.0**. No netstandard2.0. |
| TOOL-PROCESS | Own process per invocation. Process isolation guarantees: any native driver dep loads cleanly; tool crashes do not corrupt MSBuild build-server state; tool memory fully reclaimed at exit; tool dll never locks files in IDE edit-rebuild cycles. |
| TOOL-EXIT | `0` on success, `1` on any error. Errors → **stderr** in MSBuild error format `path/to/file(line,col): error DPSGxxx: message` so `<Exec>` parses them into structured IDE errors. Diagnostics → stdout. |
| TOOL-PIPELINE | Per invocation: (1) resolve `--mode`. (2a) **schema-doc**: locate `*.schema.yaml` in the project (recursive; one schema doc per project per [DOC-ONE-PER-PROJECT]) → `DPSG010`; load via `SchemaSerializer.FromYaml` → `DPSG011`; skip [TOOL-DBOPEN] entirely. (2b) **live-db**: validate `--connection` → `DPSG001`; open driver per [TOOL-DBOPEN]; introspect schema. (3) parse `--config` JSON if present → `DPSG006` (optional per [CON-NOCONFIG]); apply convention resolver per [CONV-CRUD]. (4) for each `*.lql`, transpile `LqlStatementConverter.ToStatement(content).To{Platform}Sql()`. (5) render templates per `## TEMPLATES`. (6) write generated files to `--out`. (7) if `--watch`, run the LSP loop per `## SCHEMA-DOC` instead of exiting. |
| TOOL-DBOPEN | Synchronous open. Append `Pooling=false;Connect Timeout=5;Command Timeout=10` if absent. On any driver-level connection exception: catch once, emit `DPSG002`, exit `1`. Connection-string sanitised through the platform's connection-string builder before any diagnostic — host:port:database equivalent only, never password / auth. |
| TOOL-EMIT | File names + extensions are **template-driven** per `## TEMPLATES`. Default C# manifest: per-table `DataProvider.{schema}.{table}.g.cs`; per-LQL `DataProvider.lql.{slug}.g.cs`; per-SQL `DataProvider.sql.{slug}.g.cs`; aggregate `DataProvider.Extensions.g.cs`. Custom template manifests may emit any extension (`.ts`, `.kt`, `.swift`, etc.). Slug rules: replace `[/\\.]` → `_`, lowercase, non-ASCII → punycode. Collisions → `DPSG007`. Every generated C# file carries `[GeneratedCodeAttribute("DataProvider", "{version}")]` with **LF line endings**. |
| TOOL-NAMESPACE | Default `<RootNamespace>.DataProvider.Generated`. Override via `<DataProviderGeneratedNamespace>` → `--namespace`. Default accessibility `public`. Override via `<DataProviderGeneratedAccessibility>` → `--accessibility`. |
| TOOL-ADHOC | Same exe is invocable directly: `dotnet path/to/DataProvider.dll --connection "..." --config DataProvider.json --out /tmp/dp --verbosity diagnostic --platform Postgres`. Same binary, same args. No separate debug tool. |

### Diagnostic IDs

| ID | Severity | Meaning |
|---|---|---|
| DPSG001 | Error | `--connection` missing/empty |
| DPSG002 | Error | Driver `Connection.Open` failed (sanitised target + driver error) |
| DPSG003 | Error | Schema introspection query failed |
| DPSG004 | Error | `.lql` parse error (path, line, col, message) |
| DPSG005 | Error | LQL → platform SQL transpile error |
| DPSG006 | Error | `DataProvider.json` parse / schema validation error |
| DPSG007 | Error | Generated source emit collision |
| DPSG010 | Error | Schema-doc mode: no `*.schema.yaml` found in project tree |
| DPSG011 | Error | Schema-doc mode: YAML parse / `SchemaDefinition` validation failed |
| DPSG012 | Error | Schema-doc mode: more than one `*.schema.yaml` in a single project (per [DOC-ONE-PER-PROJECT]) |
| DPSG013 | Warning | Convention resolver skipped CRUD for a table (no PK / composite PK / unmapped column type) |
| DPSG014 | Error | Template render failure (path, line, message) |
| DPSG015 | Error | Template manifest invalid |

## SCHEMA-DOC

| ID | Spec |
|---|---|
| DOC-DEFAULT | `--mode schema-doc` is the default. Tool never opens a DB in this mode. |
| DOC-FORMAT | Schema doc YAML = the existing `SchemaDefinition` shape used by Migration. Loader = `Nimblesite.DataProvider.Migration.Core.SchemaYamlSerializer.FromYaml`. **No new format.** Anything missing is added to `SchemaDefinition`, not forked. |
| DOC-DISCOVER | Recursive glob `**/*.schema.yaml` rooted at the project directory. Symlinks not followed. |
| DOC-ONE-PER-PROJECT | Exactly one `*.schema.yaml` per project. Zero → `DPSG010`. Two or more → `DPSG012`. |
| DOC-COLINFER | SELECT-list column types resolve via base-column lookup against the loaded `SchemaDefinition`. Expression columns require `AS alias::PortableType` annotation in the SQL or a `<query>.columns.yaml` sidecar. Unresolvable → `DPSG013`. |
| WATCH-MODE | `--watch` runs the same exe as a long-lived process. Hand-rolled JSON-RPC over stdio with LSP `Content-Length` framing. **No `OmniSharp.Extensions.LanguageServer`, no `StreamJsonRpc`.** |
| WATCH-FS | Recursive `FileSystemWatcher` rooted at the solution dir for `*.schema.yaml`. 250 ms trailing-edge debounce. |
| WATCH-MULTI | One schema doc per project, but the watcher tracks **every** project in the solution and which `obj/Generated/` tree belongs to which project. State map: `ImmutableDictionary<projectPath, (schemaPath, ImmutableHashSet<outputFile>)>`. Persisted at `<solution>/.dataprovider/watch-state.json`. |
| WATCH-INCREMENTAL | Per-table FNV-1a hash. Only flipped tables regenerate. One-hop FK closure pulls in dependents. Sibling projects' `.g.cs` keep mtime. |
| WATCH-RESILIENCE | YAML parse failure emits a `dataProvider/regenError` notification + `warn` log. Loop never throws and never exits on a single-file failure. |
| WATCH-WIRE | LSP wire models (notifications, project list, schema state, regen events) defined in `.td` files under `DataProvider/Wire/` using **typeDiagram** ([typediagram.dev](https://typediagram.dev/docs/language-reference.html)). Same `.td` source generates C# records for the server and TypeScript types for the future VS Code client. **No hand-written wire DTOs on either side.** |
| WATCH-MSGS | v1 messages: `initialize`, `initialized`, `shutdown`, `workspace/didChangeWatchedFiles`, `dataProvider/projects` (list known projects + their schema state), `dataProvider/regenerated` (per completed regen), `dataProvider/regenError`. |

## CONVENTIONS

| ID | Spec |
|---|---|
| CONV-NO-CONFIG | `DataProvider.json` is **optional** per [CON-NOCONFIG]. With zero config, the convention resolver decides what to emit. |
| CONV-NAME | Model name = `TableDefinition.Name` PascalCased. Column names emit verbatim (preserve casing). Naming helpers (`pascal`/`camel`/`snake`/`kebab`/`screaming-snake`) live in shared `Core/CodeGeneration/NamingConvention.cs` — no per-platform duplication. |
| CONV-CRUD | `Insert` emits iff every column maps to a known `PortableType`. `Update` emits iff `Insert` preconditions hold and at least one non-PK column exists. `Delete` emits iff a single-column PK exists. `BulkInsert`/`BulkUpsert` emit iff `Insert`/`Upsert` preconditions hold and PK is server-generated or supplied. |
| CONV-PK-VIOLATION | No PK or composite PK: model still emits, CRUD methods skipped, `DPSG013` warning logged. |
| CONV-OVERRIDE | `DataProvider.json`'s `tables[].generateInsert/Update/Delete` and `excludeColumns` are pure overrides on top of the convention result. Never required. |

## TEMPLATES

| ID | Spec |
|---|---|
| TPL-ENGINE | **Scriban** (single MIT-licensed NuGet, sandboxable, AOT-friendly, language-agnostic output). No Razor, no T4, no Handlebars. Internal seam is `Func<TemplateName, ImmutableDictionary<string,object>, string>` per CLAUDE.md "no interfaces". |
| TPL-DISC | Resolution order: `--templates <dir>` arg → `templates/` next to the schema doc → embedded defaults shipped in the tool. |
| TPL-MANIFEST | Each generator ships `generator.yaml`: `{ id, version, language, templates: [{ input, foreach: schema\|table, output }] }`. `output` is itself a Scriban expression. |
| TPL-CONTEXT | Frozen template context exposes: `schema`, `table`, `columns`, `meta.tool_version`, `meta.target_language`. Built-in helper modules: `naming` (case conversions), `types` (`to_typescript`/`to_kotlin`/`to_swift`/`to_dart`/`to_go`). |
| TPL-SANDBOX | `Scriban.TemplateContext.MemberFilter` denies access to `System.IO`, `System.Reflection`, `System.Diagnostics.Process`, `System.Environment`. Templates are pure data → text. |
| TPL-OUT | Default route `obj/DataProvider/<generator-id>/<rendered-path>`. Output extension comes from the manifest, not hard-coded. C# generation routes to `obj/DataProvider/csharp/`. |
| TPL-DETERMINISM | `meta.generated_at_utc` is **off by default** so byte-identical inputs produce byte-identical outputs. Opt in via `--templates-include-timestamp`. |
| TPL-TESTING | `Nimblesite.DataProvider.Generators.Testing` package shipped for plugin authors: golden-file harness, fake `SchemaDefinition` builder, snapshot diffing. |

## PKG

| ID | Spec |
|---|---|
| PKG-NAME | `DataProvider`. **One** package for the entire codegen surface. |
| PKG-LAYOUT | `lib/net9.0/` = runtime extension dlls. `build/DataProvider.targets` (auto-imported). `build/tool/net9.0/` = `DataProvider.dll` + `runtimeconfig.json` + every driver dll + every transitive managed dep + native runtime assets under `runtimes/{rid}/native/`. |
| PKG-DRIVERS | Every supported platform driver vendored under `build/tool/net9.0/`: `Npgsql`, `Microsoft.Data.Sqlite`, `Microsoft.Data.SqlClient`, every transitive managed dep, every native asset under `runtimes/{win-x64,linux-x64,linux-arm64,osx-x64,osx-arm64}/native/` (`e_sqlite3.{dll,so,dylib}`, `sni.dll` / `libMicrosoft.Data.SqlClient.SNI.{so,dylib}`). |
| PKG-VERSION | Package version, runtime extension dlls, and tool exe are all the same version by construction. Drift is structurally impossible. |

### TARGETS-FULL

```xml
<Project>
  <ItemGroup>
    <AdditionalFiles Include="$(MSBuildProjectDirectory)/**/*.lql" />
    <AdditionalFiles Include="$(MSBuildProjectDirectory)/**/*.schema.yaml" />
    <AdditionalFiles Include="$(MSBuildProjectDirectory)/DataProvider.json"
                     Condition="Exists('$(MSBuildProjectDirectory)/DataProvider.json')" />
  </ItemGroup>
  <Target Name="DataProviderCodegen"
          BeforeTargets="CoreCompile"
          Inputs="@(AdditionalFiles);$(MSBuildThisFileDirectory)tool/net9.0/DataProvider.dll"
          Outputs="$(IntermediateOutputPath)DataProvider/.timestamp">
    <Error Condition="'$(DataProviderMode)' == 'live-db' AND '$(DataProviderConnectionString)' == ''"
           Code="DPSG001" Text="DataProviderConnectionString MSBuild property is required when DataProviderMode=live-db." />
    <MakeDir Directories="$(IntermediateOutputPath)DataProvider" />
    <Exec Command="dotnet &quot;$(MSBuildThisFileDirectory)tool/net9.0/DataProvider.dll&quot; --mode &quot;$(DataProviderMode)&quot; --connection &quot;$(DataProviderConnectionString)&quot; --config &quot;$(MSBuildProjectDirectory)/DataProvider.json&quot; --out &quot;$(IntermediateOutputPath)DataProvider&quot; --platform &quot;$(DataProviderPlatform)&quot; --namespace &quot;$(DataProviderGeneratedNamespace)&quot; --accessibility &quot;$(DataProviderGeneratedAccessibility)&quot; --templates &quot;$(DataProviderTemplates)&quot;"
          IgnoreExitCode="false" ConsoleToMSBuild="true"
          CustomErrorRegularExpression="^.*\([0-9]+,[0-9]+\):\s*error\s+DPSG[0-9]+:.*$"
          CustomWarningRegularExpression="^.*\([0-9]+,[0-9]+\):\s*warning\s+DPSG[0-9]+:.*$" />
    <Touch Files="$(IntermediateOutputPath)DataProvider/.timestamp" AlwaysCreate="true" />
    <ItemGroup>
      <Compile Include="$(IntermediateOutputPath)DataProvider/**/*.g.cs" />
      <FileWrites Include="$(IntermediateOutputPath)DataProvider/**/*.g.cs" />
      <FileWrites Include="$(IntermediateOutputPath)DataProvider/.timestamp" />
    </ItemGroup>
  </Target>
</Project>
```

Auto-imported by NuGet from `build/DataProvider.targets`. Consumer never sees this file.

## CONSUMER

| ID | Spec |
|---|---|
| CONSUMER-CSPROJ | One `<PackageReference Include="DataProvider">`. One `<DataProviderPlatform>` (required, no auto-detection). `<DataProviderMode>` defaults to `schema-doc`; set to `live-db` to opt in. `<DataProviderConnectionString>$(DATAPROVIDER_CONN)</DataProviderConnectionString>` required iff mode=`live-db`. Optional: `<DataProviderGeneratedNamespace>`, `<DataProviderGeneratedAccessibility>`, `<DataProviderTemplates>`. Nothing else. |
| CONSUMER-CONNSTR | Live-db mode only. Resolves through MSBuild from `$DATAPROVIDER_CONN`. Never committed. CI sets it in env. Local sets it via shell rc / `direnv`. IDE sets it via system env + IDE restart. |
| CONSUMER-SCHEMA | Schema-doc mode (default). Consumer ships exactly one `*.schema.yaml` somewhere under the project (recursive discovery per [DOC-ONE-PER-PROJECT]). Format is the canonical `DatabaseSchema` YAML loaded by `SchemaSerializer.FromYaml`. Zero connection strings, zero env vars, zero migrations required to compile. |
| CONSUMER-DELETE | Migrating consumers delete: every `.config/dotnet-tools.json` legacy entry, every `<Target Name="GenerateDataProvider">`, every `Generated/` folder + `.gitignore` line, every `dotnet tool restore` step, every `<Exec>` calling a legacy DataProvider CLI, every `sed` codegen post-step. `DataProvider.json` is no longer required per [CON-NOCONFIG]; delete it unless explicit overrides are still needed. `dataprovider-migrate` stays for live-db mode per [CON-MIG-FIRST]. |
| CONSUMER-DETERMINISM | Schema-doc mode: deterministic on `(SHA256(schema.yaml bytes), SHA256(*.lql), SHA256(*.sql), SHA256(template files))`. Live-db mode: deterministic across dev/CI iff both run the same migrations against the same schema first per [CON-MIG-FIRST]. |

## DEPS

| ID | Spec |
|---|---|
| DEPS-EXTRACT | Per platform, move every non-`Main` static method out of the legacy `DataProvider/DataProvider.{Platform}.Cli/Program.cs` into the new `DataProvider/DataProvider.{Platform}/` library (net9.0, one type per file, ≤450 LOC, ≤20 LOC per function). No `Console.WriteLine` (use `ILogger<T>`), no `Environment.Exit` (return `Result<T,CodegenError>`), no `File.WriteAllText` (return generated source as a string; the tool writes files). The legacy `Main` shrinks to a thin shim. Existing CLI tests must stay green. |
| DEPS-NS20 | Not needed. Tool is net9.0, runtime libs are net9.0, no analyzer host, no MSBuild Task host. |
| DEPS-SQLPARSER | `SqlParserCS` is dead weight in `Directory.Build.props`. Zero source imports across the repo. Delete the `<PackageReference>`. Zero compile/runtime impact. |
| DEPS-ANTLR | Every platform library references `Antlr4.Runtime.Standard 4.13.1`. Generated parser sources are checked in next to the `.g4` so downstream builds need only the runtime. **No build-time Java dependency anywhere.** |

## TEST

| ID | Spec |
|---|---|
| TEST-UNIT | `DataProvider.Tool.Tests` (net9.0) drives the tool E2E in **both modes**. Schema-doc tests run with no DB, asserting literal generated source from a fixture `*.schema.yaml`. Live-db tests run against a real platform testcontainer (no in-memory DBs). For a given schema both modes must produce byte-identical output. |
| TEST-DIAG | One test per `DPSG001`–`DPSG015`. Each arranges the failure mode and asserts the canonical stderr line + exit code. |
| TEST-MSBUILD | A test project consumes the packed `DataProvider.{version}.nupkg` from a local feed, builds in **both modes** (schema-doc default + live-db opt-in), asserts `.g.cs` lands in `obj/DataProvider/`, asserts MSBuild surfaces `DPSGxxx` errors structurally in the IDE Error List. |
| TEST-NATIVE | Live-db only. Matrix runs on `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`. Each combo loads its driver's native asset from `runtimes/` and runs an introspection against a testcontainer. Schema-doc mode bypasses native drivers entirely. |
| TEST-PARSER | Per platform, `Nimblesite.DataProvider.{Platform}.Tests` parses real fixture SQL and asserts on the resulting syntax tree shape (rule contexts, terminal nodes, parameter list, projection list). Mandatory for every platform under [CON-PARSER-ONLY]. |
| TEST-WATCH | Boot the LSP against a temp solution with two projects, edit one column in one project's `*.schema.yaml`, assert only that project's `.g.cs` rewrites; sibling project keeps mtime. Snapshot test on the typeDiagram-generated TS types confirms the wire surface. |
| TEST-TEMPLATES | Sample TypeScript template under `Samples/` emits `*.d.ts` for the `example` schema; `tsc --noEmit` passes. Sandbox test asserts a malicious template that touches `System.IO` is rejected with `DPSG014`. |

## DX

| Scenario | Behavior |
|---|---|
| Fresh clone (schema-doc, default) | `git clone` → `dotnet build`. No DB, no env vars, no config file required. |
| Fresh clone (live-db) | `git clone` → set `DATAPROVIDER_CONN` → start DB → `dataprovider-migrate` → `dotnet build`. |
| DB unreachable | Live-db only: `DPSG002` (sanitised target + driver error). Schema-doc mode never opens a DB. |
| Schema doc missing / invalid | Schema-doc only: `DPSG010` / `DPSG011`. |
| `dotnet restore` | Tool does **not** run during restore. |
| `dotnet pack` / `dotnet test` | Both invoke compile, therefore invoke the tool. Schema-doc mode needs only the `.schema.yaml` on disk; live-db needs a reachable DB. |
| `--watch` | Tool stays resident, watches every `*.schema.yaml` in the solution, regenerates only the affected project's outputs per `## SCHEMA-DOC`. |

## RISK

| ID | Spec |
|---|---|
| RISK-NATIVE-RID | Live-db only: new RID = add native asset to package + republish. Mitigated by [TEST-NATIVE] running the full RID matrix in CI. Schema-doc mode loads no native driver. |
| RISK-DETERMINISM | Live-db: dev/CI may diverge if schemas diverge. Mitigated by [CON-MIG-FIRST]. Schema-doc: deterministic by file hash per [CONSUMER-DETERMINISM]. |
| RISK-FORK-COST | One `dotnet DataProvider.dll` fork per build. Cold start ≈ 100 ms. < 2 % of a typical build. `--watch` eliminates fork cost entirely for IDE edit cycles. |
