# PLAN: Codegen v2 — Schema-Doc Default, Conventions, LSP/Watch, Templates

> Implements the v2 sections of [`../specs/codegen-cli-tool.md`](../specs/codegen-cli-tool.md): `## SCHEMA-DOC`, `## CONVENTIONS`, `## TEMPLATES`, plus the mode-aware edits to `## CONSTRAINT`, `## TOOL`, `## CONSUMER`, `## DX`, `## TEST`.

## Goal

Ship one tool change with four user-visible features:

1. `--mode schema-doc` is the default. No DB, no env vars, no migrations needed to compile.
2. `DataProvider.json` becomes optional. Convention resolver decides what to emit.
3. `--watch` runs the tool as a hand-rolled LSP, watching every `*.schema.yaml` across the solution and regenerating only the affected project's outputs.
4. Output is template-driven (Scriban). Default templates emit C#; users can drop a `templates/` folder to emit any language.

Live-db mode behavior is **unchanged**.

## Files to modify / create

| Path | Change |
|---|---|
| [`DataProvider/Nimblesite.DataProvider.Core/DataProviderConfig.cs`](../../DataProvider/Nimblesite.DataProvider.Core/DataProviderConfig.cs) | Add `Mode { SchemaDoc, LiveDb }` enum (default `SchemaDoc`). |
| [`DataProvider/DataProvider/Program.cs`](../../DataProvider/DataProvider/Program.cs) | Parse `--mode` first; dispatch to schema-doc pipeline or live-db pipeline. Add `--watch`, `--templates` flags. |
| [`DataProvider/DataProvider/PostgresCli.cs`](../../DataProvider/DataProvider/PostgresCli.cs), `SqliteCli.cs` | Untouched on the live-db path. Connection-string check stays where it is. |
| `DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/SchemaDocPipeline.cs` | NEW. Loads `*.schema.yaml` via `SchemaYamlSerializer.FromYaml`, walks tables, calls existing `SqlAntlrCodeGenerator`. |
| `DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/SchemaDocColumnResolver.cs` | NEW. Replaces `IDatabaseEffects.GetColumnMetadataFromSqlAsync` for schema-doc mode. SELECT-list resolution via base-column lookup against `SchemaDefinition`. ANTLR `IParseTree` only — **no regex** per CLAUDE.md. |
| `DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/NamingConvention.cs` | NEW. Lifts `ToPascalCase`/`ToCamelCase` from [`PostgresCli.cs:2200`](../../DataProvider/DataProvider/PostgresCli.cs) into Core. Adds `snake`/`kebab`/`screaming-snake`. Eliminates per-platform duplication. |
| `DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/ConventionResolver.cs` | NEW. Given `DatabaseSchema` returns per-table CRUD flags per [CONV-CRUD]. `TableConfig` overrides applied on top. |
| `DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/Templating/ScribanRenderer.cs` | NEW. Wraps Scriban; exposes `Func<TemplateName, ImmutableDictionary<string,object>, string>` seam (no interfaces per CLAUDE.md). Sandbox via `TemplateContext.MemberFilter` denying `System.IO`/`System.Reflection`/`System.Diagnostics.Process`/`System.Environment`. |
| `DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/Templating/Templates/Csharp/*.scriban` | NEW. Embedded resources: `Model.scriban`, `Insert.scriban`, `Update.scriban`, `Delete.scriban`, `Select.scriban`, `Extensions.scriban`. Replaces the string-builder bodies in `ModelGenerator.cs` + `DataAccessGenerator.cs`. |
| `DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/Templating/manifest.yaml` (embedded) | NEW. Default C# manifest: `{ id: csharp, language: csharp, templates: [...] }`. |
| [`DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/ModelGenerator.cs`](../../DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/) | Refactor: body becomes "render `Model.scriban`". Logic identical, mechanism switches. |
| [`DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/DataAccessGenerator.cs`](../../DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/) | Same: bodies → templates. |
| `DataProvider/DataProvider.Lsp/` (new project) | Hand-rolled JSON-RPC over stdio. LSP `Content-Length` framing. v1 messages per [WATCH-MSGS]. References Core, no `OmniSharp.Extensions.LanguageServer`, no `StreamJsonRpc`. |
| `DataProvider/Wire/*.td` | NEW. typeDiagram source for every wire DTO. Build step generates C# records under `DataProvider/DataProvider.Lsp/Wire/Generated/` and TypeScript types under `Lql/LqlExtension/src/dataprovider-wire/` (TS output is committed; the future VS Code DataProvider client consumes it). |
| `DataProvider/DataProvider/build/DataProvider.targets` | Add `<DataProviderMode>` default `schema-doc`; `<Error>` on missing connection scoped to `DataProviderMode == 'live-db'`; pass `--mode`, `--templates` to the exe; include `*.schema.yaml` in `AdditionalFiles`. |
| [`Directory.Build.props`](../../Directory.Build.props) | Add `Scriban` PackageReference scoped to `DataProvider.Core`. |
| `DataProvider/DataProvider.Tool.Tests/` | New tests: `SchemaDocModeTests` (no DB), `WatchTests` (LSP boot + edit + assert scoped regen), `TemplatesTests` (sample TS template + sandbox rejection), `ParityTests` (schema-doc vs live-db byte-identical for same schema). |
| [`docs/specs/codegen-cli-tool.md`](../specs/codegen-cli-tool.md) | Already edited to v2 (this plan implements those edits). |

## Reused, not reinvented

- [`SchemaYamlSerializer.FromYaml`](../../Migration/Nimblesite.DataProvider.Migration.Core/SchemaYamlSerializer.cs:16-31) — already loads the canonical YAML.
- [`SchemaDefinition`](../../Migration/Nimblesite.DataProvider.Migration.Core/SchemaDefinition.cs) — schema-doc mode adopts this format verbatim. **No new format.**
- [`SqlAntlrCodeGenerator`](../../DataProvider/Nimblesite.DataProvider.Core/CodeGeneration/) — same generator runs in both modes; only the column resolver differs.
- [`PostgresCli.cs:55-68`](../../DataProvider/DataProvider/PostgresCli.cs) `--offline` proof-of-concept — promoted to default.

## Verification

- `make ci` passes.
- `TEST-UNIT` parity test: same schema → schema-doc and live-db modes emit byte-identical `.g.cs`.
- `TEST-DIAG` covers `DPSG001–DPSG015`.
- `TEST-WATCH` boots the LSP against a two-project temp solution, edits one schema doc, asserts only that project's outputs rewrite.
- `TEST-TEMPLATES` ships a TypeScript template, runs `tsc --noEmit`, and asserts a `System.IO`-touching template is rejected with `DPSG014`.
- `dotnet build` on a fresh consumer with **only** a `*.schema.yaml` and a `<PackageReference>` succeeds — no env vars, no DB, no `DataProvider.json`.

## Out of scope

- VS Code DataProvider client UI (the LSP server is shipped; the VSIX consumer side is a follow-up).
- SqlServer schema-doc support beyond the existing day-one platform tier.
- Generator marketplace / distribution mechanism.
