# PROBLEM: Codegen is shipped as out-of-process CLI tools instead of as library APIs

> **Status: resolved.** The resolution is specified in
> [`codegen-cli-tool.md`](./codegen-cli-tool.md).

## What this document is

This document describes a **problem** with how DataProvider currently exposes its
code-generation pipeline to consumers. It does **not** propose a solution. The goal
is to write down, in one place, exactly what is wrong and why it hurts users.

## TL;DR

DataProvider's core code-generation work — parsing LQL, transpiling LQL to
Postgres/SQLite SQL, introspecting a schema, and emitting typed C# data-access
code — is implemented inside ordinary C# libraries (`Lql`, `Lql.Postgres`,
`DataProvider`, `DataProvider.Postgres`, etc.). Those libraries are published as
NuGet packages and consumers reference them directly.

But the **only supported way to actually invoke** that code generation from a
consumer build is to shell out to a separate `dotnet` global/local tool
(`dataprovider-postgres`, `lql-postgres`, `lql-sqlite`, `dataprovider-sqlite`,
`migration-cli`) via `<Exec>` tasks in MSBuild.

The CLI tools are ~50–100 line wrappers that do nothing the libraries can't
already do in-process. Yet consumer projects must:

1. Add a `.config/dotnet-tools.json` manifest entry for each tool.
2. Run `dotnet tool restore` before every build.
3. Pin a specific tool version separately from the library version.
4. Pay the cost of forking a new `dotnet` process per file per build.
5. Materialize generated `*.g.cs` and `*.generated.sql` files to disk.
6. Hope the tool is on `PATH` and that its package id matches what's actually on
   nuget.org.

This architecture is the wrong shape and it is actively breaking downstream
projects today.

---

## How the pipeline currently works (consumer side)

A consumer project that uses DataProvider has to put something like this in its
`.csproj`:

```xml
<Target Name="GenerateDataProvider"
        BeforeTargets="BeforeCompile;CoreCompile"
        Inputs="DataProvider.json;@(LqlFiles)"
        Outputs="$(MSBuildProjectDirectory)/Generated/.timestamp">

  <RemoveDir Directories="$(MSBuildProjectDirectory)/Generated" />
  <MakeDir   Directories="$(MSBuildProjectDirectory)/Generated" />

  <ItemGroup>
    <LqlFiles Include="$(MSBuildProjectDirectory)/**/*.lql" />
  </ItemGroup>

  <!-- Step 1: shell out to the LQL CLI, once per file -->
  <Exec Command='dotnet lql-postgres
                   --input  "%(LqlFiles.Identity)"
                   --output "%(LqlFiles.RelativeDir)%(LqlFiles.Filename).generated.sql"' />

  <!-- Step 2: shell out to the DataProvider CLI to emit the *.g.cs files -->
  <Exec Command='dotnet dataprovider-postgres
                   --project-dir "$(MSBuildProjectDirectory)"
                   --config      "$(MSBuildProjectDirectory)/DataProvider.json"
                   --out         "$(MSBuildProjectDirectory)/Generated"' />

  <Touch Files="$(MSBuildProjectDirectory)/Generated/.timestamp" AlwaysCreate="true" />

  <ItemGroup>
    <Compile Include="$(MSBuildProjectDirectory)/Generated/**/*.g.cs" />
  </ItemGroup>
</Target>
```

The same project has these in its `PackageReference` list:

```xml
<PackageReference Include="Nimblesite.DataProvider.Core" Version="0.4.0-beta" />
<PackageReference Include="Nimblesite.Lql.Postgres"      Version="0.4.0-beta" />
```

So the **runtime libraries that contain all the actual transpilation and
generation logic are already in the project's compile graph**. The build then
ignores them and shells out to a separate exe to do the same work.

---

## What the CLI tools actually contain

I read every CLI's source. They are uniformly thin.

### `lql-postgres` / `lql-sqlite` (Lql/LqlCli.SQLite/Program.cs)

```csharp
var lql      = await File.ReadAllTextAsync(input);
var stmt     = LqlStatementConverter.ToStatement(lql);    // library call
var sqlResult = stmt.ToSQLite();                          // library call
                                                          // (or .ToPostgreSql())
File.WriteAllText(output, sqlResult.Value);
```

That is the entire interesting payload. Everything else in `Program.cs` is
`System.CommandLine` argument parsing, console pretty-printing, and exit-code
mapping. The transpilation is one method call on a public extension that lives
in `Lql.Postgres` / `Lql.SQLite`.

### `dataprovider-postgres` (DataProvider/DataProvider.Postgres.Cli)

Same shape. It loads `DataProvider.json`, opens an `NpgsqlConnection`, asks
`DataProvider.Core` to introspect the schema, asks `DataProvider.Core` to
generate strings of C# source, and writes those strings to `.g.cs` files. Every
non-trivial line of work happens inside `DataProvider.Core` — a library that the
consumer already references.

### `migration-cli` / `DataProviderMigrate`

Reads a YAML schema, calls `Migration.Postgres` (or `Migration.SQLite`) to apply
DDL. Same story: a `Main` method around a library call.

---

## Why this is a problem

### 1. The CLI is the only supported integration point, but the library is what's documented

The `README.md` for `Nimblesite.Lql.Postgres` and `Nimblesite.DataProvider.Core`
describes them as libraries. Consumers `PackageReference` them and expect to be
able to call them. There is no documented way to invoke code generation from
those libraries directly — every example, every sample, every downstream project
uses `<Exec>` against the CLI. The library APIs that the CLI calls are public
but undocumented as a build-time integration point.

Consumers end up with two artifacts in their project that do the same job, and
no guidance about which one to use when.

### 2. The CLI version and the library version drift

`Nimblesite.DataProvider.Core` is at `0.4.0-beta` on nuget.org.
`Nimblesite.DataProvider.Postgres.Cli` is at `0.2.7-beta` on nuget.org.
`Nimblesite.Lql.Cli.Postgres` is **not on nuget.org at all** — it has never been
published. The consumer project's plan doc references a fictional
`nimblesite.lql.cli.postgres@0.1.8-beta` that does not exist anywhere.

These version skews are invisible at `dotnet restore` time because the tool
manifest and the package references are restored independently. The build only
fails later when MSBuild tries to `<Exec>` a `dotnet` command that isn't there:

```
Could not execute because the specified command or file was not found.
* You intended to execute a .NET program, but dotnet-lql-postgres does not exist.
```

There is no compile-time check that the CLI's command shape, flag names, or
output format match the library version the consumer is linking against. There
is no compile-time check that the CLI even exists.

### 3. Every build forks N processes for work that should be in-process

A consumer with 8 `.lql` files does 8 `dotnet lql-postgres` invocations plus 1
`dotnet dataprovider-postgres` invocation. That is 9 `dotnet` cold-starts on
every build. Each one re-jits `System.CommandLine`, re-loads `Npgsql`, re-loads
`SqlParserCS`, re-parses `DataProvider.json`. The libraries doing the actual
work are *already loaded* in the C# compiler's process — they're being compiled
into the consumer's assembly five seconds later.

This shows up as multi-second build overhead per project on cold builds and
broken incrementality on warm builds (the `<RemoveDir>` at the top of the target
deletes the `.timestamp` immediately, so MSBuild's `Inputs`/`Outputs` check
always sees outputs as missing and re-runs every time).

### 4. Generated files have to live on disk, which forces gitignore gymnastics

Because the CLI emits files, the consumer has to choose: commit them to git
(stale, churn-prone, merge-conflict magnet) or gitignore them (every fresh
clone needs the CLI installed and Postgres running before `dotnet build` works
at all). Both options are bad. The [ClinicalCoding repository](https://github.com/Nimblesite/ClinicalCoding) documents the consequences of this architectural choice.

### 5. The build needs a live database to compile

Because `dataprovider-postgres` introspects a real Postgres connection at
build time, `dotnet build` for any consumer project is actually:

```
docker compose up -d postgres
&& wait for healthy
&& dotnet DataProviderMigrate (4 schemas)
&& dotnet build
```

A C# project that requires a running database container to compile is a
significant departure from how every other .NET library works, and it is the
direct consequence of code generation living in a separate process whose only
input source is "open a socket". The schema is *also* declared in YAML files
that ship with the project, so the database is being used as a redundant type
oracle for information that's already available statically.

### 6. The `<Exec>` task swallows or mangles errors

Consumers historically set `IgnoreExitCode="true"` on the codegen `<Exec>` to
stop transient failures from breaking the build. That hides real bugs. Removing
`IgnoreExitCode` makes the build correctly fail loud, but then any unrelated
flake — a port collision on 5432, a slow Postgres health check, a Docker
restart — turns into a red CI build whose error message is

```
The command "dotnet dataprovider-postgres ..." exited with code 1.
```

with no structured information about *what* failed inside the tool. The library
version of the same call returns a `Result<T, SqlError>` with file/line/column
info. The CLI flattens that into a `Console.WriteLine` and an integer exit code.

### 7. Tool discovery is fragile across environments

`dotnet tool restore` reads `.config/dotnet-tools.json`. That file is the only
way the consumer pins which CLI version to use. The package id in the manifest
does not have to match the package id on nuget.org (and in DataProvider's case,
the csproj `PackageId` is `DataProvider.Postgres.Cli` while the published id is
`Nimblesite.DataProvider.Postgres.Cli` — the rename happens somewhere in the
publish pipeline and is not documented). A consumer who copies the manifest
from one project to another has no way to know whether the tool exists, whether
the command name is right, or whether a newer compatible version is on
nuget.org.

When it goes wrong, the failure mode is the one ClinicalCoding is hitting
right now:

```
==> Linting...
dotnet build --configuration Release
Could not execute because the specified command or file was not found.
* You intended to execute a .NET program, but dotnet-lql-postgres does not exist.

error MSB3073: The command "dotnet lql-postgres ..." exited with code 1.
```

The package was never on nuget.org, the manifest entry pointed at a ghost, and
the build fails 30 seconds in with no remediation path other than "go publish a
package upstream and tag a release."

### 8. Source generators are the platform answer to this problem and DataProvider isn't using them

.NET has had Roslyn source generators since C# 9 (Nov 2020). Every comparable
modern .NET data tooling — EF Core compiled models, Refit, Mapperly, Dapper.AOT,
System.Text.Json source-gen, StronglyTypedId, Riok.Mapperly — runs its code
generation **inside the C# compiler process**, reads its inputs from
`AdditionalFiles`, and emits `.g.cs` content the compiler ingests directly,
in-memory, with no external tool, no `<Exec>`, no on-disk `Generated/` folder,
and no second package for users to install.

DataProvider's CLI architecture is the pre-source-generator way of doing this.
It works, but every modern .NET library has moved off it because the failure
modes documented above are well-understood industry-wide.

---

## Symptom log: where this is currently biting

- **ClinicalCoding** CI is red because `dotnet-lql-postgres` is not on
  nuget.org and never was. The plan that introduced the dependency on it
  ([ClinicalCoding repository](https://github.com/Nimblesite/ClinicalCoding))
  cited a version (`0.1.8-beta`) of a package that does not exist.
- **ClinicalCoding** has had at least four commits in a row trying to fix
  the dotnet-tools manifest (`fix tools list`, `fix`, `DataProvider version`,
  `move version to build build props`) without resolving the underlying
  "the tool isn't real" problem.
- The `Generated/` folder churn between machines was severe enough to motivate
  a 300-line plan to stop committing generated files. That plan in turn
  required spinning Postgres up during every `dotnet build`, which in turn
  required CI to start a database before linting, which in turn made `make
  lint` depend on `make db-migrate`, which in turn means a developer can't
  even run `csharpier check` without Docker running. Every step compounds.
- The `dataprovider-postgres` package's only published version is `0.2.7-beta`
  while every other package in the family is at `0.4.0-beta`. There is no way
  for a consumer to know whether `0.2.7-beta` is compatible with the
  `0.4.0-beta` libraries, and no compile-time check enforces it.

---

## SOLUTION (added by DataProviderSamples + DataProvider, in progress)

### Hard constraints (non-negotiable, set by repo owner)

1. **No CLI tools for codegen.** No `dotnet tool restore`, no `<Exec>`, no
   global/local tool manifests. Delete `lql-postgres`, `dataprovider-postgres`,
   `lql-sqlite`, `dataprovider-sqlite` as build-time integration points.
2. **Simple consumer setup.** A consumer should add ONE `PackageReference` and
   a connection string. Nothing else. No tools manifest. No `<Target>` blocks.
   No `Generated/` folder. No post-processing `sed` step.
3. **Roslyn source generators are the preferred mechanism.** Run inside the C#
   compiler process. Read inputs from `AdditionalFiles` and
   `AnalyzerConfigOptions`. Emit `.g.cs` content directly into the compilation.
4. **DB connection at codegen time is MANDATORY and non-negotiable.** The
   generator MUST open a live Npgsql/SQLite connection and introspect the real
   schema. Static YAML/JSON schema parsing is NOT a substitute. If the DB is
   unreachable, the build MUST fail loudly. This is a deliberate design choice
   — the database is the single source of truth for types.

### Proposed shape

Ship an incremental Roslyn source generator inside
`Nimblesite.DataProvider.Postgres` (and a sibling in
`Nimblesite.DataProvider.SQLite`). The generator:

1. Receives `.lql` files via `AdditionalFiles` (consumer declares
   `<AdditionalFiles Include="**/*.lql" />` once in their csproj — or the
   DataProvider package's `.props` does it automatically).
2. Receives `DataProvider.json` via `AdditionalFiles`.
3. Receives the connection string via `AnalyzerConfigOptionsProvider` reading
   an MSBuild property like `<DataProviderConnectionString>` (which itself
   should resolve from an env var so the string never lives in the repo).
4. Opens an `NpgsqlConnection` at generation time, introspects the schema using
   the existing `DataProvider.Core` introspection logic.
5. Calls `LqlStatementConverter.ToStatement(...)` and `.ToPostgreSql()` on each
   `.lql` input — same library calls the CLI currently makes.
6. Emits typed records, query methods, and any other current `*.g.cs` output
   straight into the compilation via `SourceProductionContext.AddSource(...)`.
   Nothing on disk. Nothing in `obj/`. Nothing in `Generated/`.
7. Uses the incremental generator pipeline so the DB is only re-hit when one
   of (lql file content, json content, connection string) actually changes.
   Cached on `(hash(inputs))`.

### Consumer-side after the change

Consumer csproj shrinks to:

```xml
<ItemGroup>
  <PackageReference Include="Nimblesite.DataProvider.Postgres" Version="0.4.0-beta" />
</ItemGroup>

<PropertyGroup>
  <DataProviderConnectionString>$(DATAPROVIDER_CONN)</DataProviderConnectionString>
</PropertyGroup>

<ItemGroup>
  <AdditionalFiles Include="**/*.lql" />
  <AdditionalFiles Include="DataProvider.json" />
</ItemGroup>
```

That's it. No `.config/dotnet-tools.json`. No `<Target Name="GenerateDataProvider">`.
No `RemoveDir`/`MakeDir`/`Touch`/`<Compile Include="Generated/**/*.g.cs">`. No
`sed` step. No `Generated/` directory under source control or under gitignore.

### Locked decisions (both agents agreed)

- **Generator host project:** new `DataProvider.Postgres.SourceGenerator`
  project shipped INSIDE the existing `Nimblesite.DataProvider.Postgres` NuGet
  package as an analyzer asset (`analyzers/dotnet/cs/`). Consumers do not get
  a second package. One `PackageReference` replaces tool + lib + `<Target>`.
- **Generator type:** `IIncrementalGenerator`. Inputs cached on
  `(hash(*.lql), hash(DataProvider.json), hash(connection string))`. DB is
  re-introspected only when one of those changes.
- **Inputs:** `.lql` files and `DataProvider.json` flow in via
  `AdditionalFiles`. The DataProvider package's auto-imported `.props`
  declares `<AdditionalFiles Include="**/*.lql" />` and
  `<AdditionalFiles Include="DataProvider.json" />` so consumers do not
  hand-author it.
- **Output:** `SourceProductionContext.AddSource(...)` ONLY. Nothing on disk.
  No `Generated/` folder. No `obj/` artifacts. No post-processing `sed`.
- **DB connection at codegen is mandatory and non-negotiable.** Generator
  opens a real `NpgsqlConnection` and introspects the live schema. If the DB
  is unreachable the build MUST fail loud via
  `SourceProductionContext.ReportDiagnostic` with: target host, failed query,
  underlying Npgsql error message. Not a flat exit code.
- **Npgsql version inside the analyzer:** pinned to **Npgsql 8.x** as a
  PRIVATE dependency of the source generator analyzer dll. This is invisible
  to the consumer's runtime — consumers can still reference Npgsql 9 (or any
  other version) for their own runtime data access. NOT ILRepack/ILMerge —
  too risky and brittle.
- **ClinicalCoding build environment:** Postgres is already brought up
  before `dotnet build` in `make ci` (`docker compose up -d postgres`
  → `db-migrate` → `dotnet build`). Same flow stays. Build will fail loud
  if Postgres is down. This is the intended contract.
- **CLI projects fate:** `LqlCli.SQLite`, `DataProvider.Postgres.Cli`,
  `LqlCli.Postgres` (which never existed), and the `migration-cli` are
  REMOVED from the consumer's build path entirely. They may continue to
  exist in the upstream repo as standalone debugging utilities, but they
  are no longer the integration point and are no longer documented as such.

### Still open (waiting on DataProvider agent)

- **netstandard2.0 multi-targeting status** of `DataProvider.Core`,
  `Lql.Postgres`, `Lql`, and `Selecta`. The analyzer host requires
  `netstandard2.0`. If any of these are net8/net10-only, they need to be
  multi-targeted before the source generator can reference them.
- **Connection string discovery path.** Proposed: env var `DATAPROVIDER_CONN`
  → `<DataProviderConnectionString>` MSBuild property →
  `AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.DataProviderConnectionString", out var conn)`.
  Awaiting confirmation on the property name and that the env var indirection
  is acceptable (so the connection string never lives inside committed XML).

### What this section is

This section IS a design proposal. The constraints above are fixed by the repo
owner. The shape is the working agreement between `DataProviderSamples` (the
consumer/sample repo agent) and `DataProvider` (the upstream library agent).
Both agents must converge on a single concrete plan before either starts
implementing.
