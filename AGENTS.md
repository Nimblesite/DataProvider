# DataProvider — Agent instructions

## Before starting

Read this entire file before writing code. These rules are mandatory.

- **Reduce token usage:** Check file sizes before loading. Write less, delete fluff and dead code, and alert the user when pointless files consume context.
- **Never kill a VS Code process.**

<!-- agent-pmo:795a9c2 -->

## Project overview

DataProvider is a .NET database access toolkit covering source generation for SQL extension methods, the Lambda Query Language (LQL) transpiler, offline-first bidirectional sync, WebAuthn + RBAC authentication, and an embeddable reporting platform. The LQL language server is implemented in Rust with a VS Code extension in TypeScript. Healthcare sample applications live in a separate repository: [Nimblesite/ClinicalCoding](https://github.com/Nimblesite/ClinicalCoding).

Languages: C#, Rust, TypeScript, F#. Full CI validation: `make ci`; tests: `make test`; lint: `make lint`.

## Agent workflow

- Act autonomously. Resolve ambiguity with the most reasonable default, record assumptions, and complete the task without asking questions or pausing for confirmation, clarification, or approval. Deliver finished work with a short assumptions summary.
- Auto-memory is off. Persistent rules require a reviewed PR to this file. Keep `"autoMemoryEnabled": false` in committed `.claude/settings.local.json`.
- Use Too Many Cooks (TMC) for file locks and messages across agent boundaries.
- Codex subagents are forbidden. Delegate parallel code work only to Pi instances and monitor them closely. Start each in this checkout, verify its branch before edits, and assign nonoverlapping files. Never create a branch or worktree for a Pi agent.
- Do not use Git without explicit user authorization; do not use Docker commands. Leave Git operations to the user and CI unless authorized under [Git discipline](#git-discipline).
- Search for existing implementations before writing code. Move code instead of copying it; run Deslop before and after changes as specified under [Duplication](#duplication).

## Git discipline

Git requires explicit user authorization for the task; otherwise, leave operations to the user and CI.

- Never push directly to `main`; always PR → green CI → merge.
- Own opened PRs until all required checks pass. Enable auto-merge where allowed (`gh pr merge --auto --squash`); monitor, fetch failure logs, fix, push, and repeat. Never hand back red or running checks.
- Never add agent attribution or `Co-Authored-By` trailers; this is non-overridable.
- Use TMC to coordinate exactly one shared branch. Check first and reuse existing feature branches; merge multiple feature branches immediately before other work.
- No worktrees; all agents share this checkout. Follow [Pi delegation rules](#agent-workflow), including branch verification and nonoverlapping files; never create agent branches/worktrees.

## Duplication

Implements `[CI-DESLOP]`. Read [docs](https://deslop.live/docs/for-ai/); report [false positives](https://github.com/Nimblesite/Deslop/issues). Every code change requires:

- Before authoring code, fixtures, or test setup: `find-similar`.
  - `signals.fused ≥ 0.85` or `identical` / `nearly_identical`: reuse; never duplicate.
  - `0.6 ≤ fused < 0.85`: inspect canonical code; strongly prefer reuse/extension.
  - `fused < 0.6` or empty: proceed.
- After changes: `rescan` → `top-offenders` (severity-ranked); `cluster-by-id` for merge candidates' full members/signals. Inspect with `report-for-file` / `report-for-range`; read `schema-doc` once per session.
- Never evade the gate through budget increases, `hidden` code, or trivial variations.

`.deslop.toml`: `max_duplication_percent`; CI: `deslop .`, exit 3 when exceeded. Lower the threshold with duplication reductions in the same PR; increases require written justification.

## Database and model rules

- Parse SQL only with the official platform-specific .NET parser specified by the database maintainer.
- Never parse structured formats with regex, including SQL, JSON, YAML, TOML, or code. Use ANTLR `.g4` grammars or SqlParserCS for SQL, subject to the official-parser requirement above.
- Generate the Rust LQL parser with ANTLR; never write a parser manually. Always upgrade ANTLR to its latest version.
- LQL must behave identically across all target database platforms. Log any platform-independence violation as a GitHub issue.
- **Generate every data model with [typeDiagram](https://typediagram.dev/docs/).** Define domain types, DTOs, entities, enums, and algebraic data types (ADTs) in the model, then generate language code from it. The committed model is the source of truth; `make build` regenerates language types. Never hand-craft models or edit generated files. If typeDiagram cannot express a case, file an upstream issue and reference it at a minimal temporary shim. Implements `[MODEL-TYPEDIAGRAM]`.
- Use generated extensions for inserts and updates; never use raw SQL inserts or updates.
- Create and migrate database schemas through DataProvider Migration.CLI with YAML, or through the application's migration code. Schema SQL, including `schema.sql`, is forbidden.
- Every table must have exactly one primary key, and it must be a UUID.
- Use real databases; in-memory databases are forbidden.

## Coding rules

- Never throw. Return `Result<T, E>`; wrap failures in `try`/`catch`.
- Use type pattern matching instead of casts or null-forgiving operators. See the language-specific rules for prohibited syntax and documented exceptions.
- Never suppress warnings. Fix the code instead of disabling the linter.
- Use records and static methods; no classes, interfaces, or singletons. Replace interfaces with `Action<T>` / `Func<T>`; inject `Func` into static methods.
- Keep types and members private or internal by default. Expose only the types and members that users need to consume the toolkit.
- Prefer expressions over assignments and pure functions over statements.
- Use named parameters; no ordinal calls.
- Close type hierarchies with private constructors:

  ```csharp
  public abstract partial record Result<TSuccess, TFailure> { private Result() { } }
  ```

- Use immutable records for data. Choose immutable collections such as `ImmutableList`, `FrozenSet`, or `ImmutableArray` instead of mutable `List<T>`.
- Always declare result type aliases with `using`; do not write `new Result<string, SqlError>.Ok`.
- Document all public members with XML documentation, except in test projects. Follow language-specific documentation rules below.
- Keep one type per file, except small records. Keep files under 450 LOC and functions under 20 LOC.
- Delete commented-out code. No placeholders: incomplete work must produce a loud compilation error with a TODO.
- Never use consecutive `Console.WriteLine` calls; combine output into one interpolated string. Diagnostic output must use structured logging.
- Routinely format C# with CSharpier: `dotnet csharpier .` from the root.

### Spec traceability

Every spec section must have a unique, hierarchical, non-numeric ID: `[GROUP-TOPIC]` or `[GROUP-TOPIC-DETAIL]`, such as `[AUTH-TOKEN-VERIFY]` or `[CI-TIMEOUT]`. Never use sequential IDs such as `[SPEC-001]`. All related code, tests, and design documents must reference the section ID in a comment, such as `// Implements [AUTH-TOKEN-VERIFY]`.

## Language-specific rules

### C# and F#

- Return `Result<T, E>` or `Option<T>` for failures; never throw exceptions.
- No `!` null-forgiving operator, `as` casts, or `dynamic`; use pattern matching.
- Enable nullable reference types everywhere. Use immutable records in C# and pure static methods instead of classes.
- In F#, prefer discriminated unions, pipe operators, and computation expressions.
- Use the latest .NET and C#, with nullable enabled and warnings as errors. Keep central configuration in `Directory.Build.props`.

### Rust

- No `unwrap()`; use `?` or explicit `match`.
- No `expect()` in production code; tests may use it.
- No `panic!()`, `todo!()`, `unimplemented!()`, or `unreachable!()` in production code.
- No `unsafe {}` without documented justification reviewed by a human.
- No `allow(clippy::...)` without documented justification.
- Document every public item with `///` comments.
- Use `thiserror` for error types; use `anyhow` only in applications, never libraries.

### TypeScript

- No `any`; use `unknown` and narrow explicitly.
- No non-null assertion (`!`); use optional chaining or explicit guards.
- Annotate all function parameters and return types; no implicit `any`.
- No `// @ts-ignore` or `// @ts-nocheck`.
- No `as Type` casts without a comment explaining why they are safe.
- Keep `"strict": true` in `tsconfig.json`.
- Never throw; return `Result<T, E>` through a Result library or discriminated union.

## Testing

- Target 100% coverage and a Stryker score of at least 70%.
- Never delete or skip tests, or remove assertions. Restore skipped tests aggressively.
- Failing tests are acceptable; tests that do not enforce behavior are forbidden.
- Never swallow exceptions in `try`/`catch` and assert success.
- Tests must be deterministic: no `sleep()`, timing dependencies, or random state. A timeout is a failure.
- For bugs: write a test that fails because of the bug, verify that failure, fix the bug, then verify the test passes.
- Prefer E2E/integration tests at the highest level: exercise the full integration with zero mocking. Unit tests are acceptable only when needed to isolate problems.
- Never use Fluent Assertions.
- VS Code extension E2E tests must interact only through `vscode.commands.executeCommand`; never call provider methods directly.

## Logging standards

- Structured fields; no interpolated diagnostics, `Console.WriteLine`, `println!`, or `console.log`.
- Log significant operation/service/handler entry/exit, especially sync; levels: `error`, `warn`, `info`, `debug`, `trace`.
- VS Code: detailed file logs in extension state folders; basic errors/diagnostics also in Output Channel.
- Servers: async/background database/file log writes; never block requests.
- Never log PII (names, emails, addresses, phones, IPs) or secrets (keys, tokens, passwords, connection strings); record presence only.
- Libraries: C# — `Microsoft.Extensions.Logging` + Serilog; Rust — `tracing` + `tracing-subscriber`; TypeScript — `pino`.

## Web and CSS

- Minimize CSS classes and aggressively merge duplicates. Name classes after components, never sections.
- Optimize website content for AI search performance and search engine optimization (SEO). All web work must conform to Google's [AI search guidance](https://developers.google.com/search/blog/2025/05/succeeding-in-ai-search) and [SEO Starter Guide](https://developers.google.com/search/docs/fundamentals/seo-starter-guide).

## Build commands

Use these exact GNU Make targets for cross-platform execution. The seven standard portfolio targets below are the supported build interface. `make test` is the only test entry point.

```bash
make build          # compile everything
make test           # fail-fast tests + coverage + threshold
make lint           # all linters/analyzers, no formatting
make fmt            # format in-place; CHECK=1 verifies without edits (CI)
make clean          # remove build artifacts
make ci             # lint + test + build (full CI simulation)
make setup          # post-create dev environment setup
```

Repository-specific targets:

```bash
make check          # lint + test (pre-commit shortcut)
make coverage       # generate and open HTML coverage report
make vsix           # build LSP + package VS Code extension
```

## Repository map

| Component | Path | Purpose |
|-----------|------|---------|
| DataProvider | `DataProvider/` | SQL extension source generator + CLI tools |
| LQL | `Lql/Lql/` | Lambda Query Language → SQL transpiler |
| LQL LSP | `Lql/lql-lsp-rust/` | Rust workspace: lql-parser, lql-analyzer, lql-lsp |
| LQL extension | `Lql/LqlExtension/` | TypeScript VS Code extension |
| Migration | `Migration/` | YAML schema migrations → SQL DDL |
| Sync | `Sync/` | Offline-first bidirectional sync |
| Gatekeeper | `Gatekeeper/` | WebAuthn + RBAC authentication |
| Reporting | `Reporting/` | SQL/LQL sources, JSON config, React renderer |
| Website | `Website/` | Eleventy + DocFX documentation |
| CI | `.github/workflows/` | CI/CD pipelines |
| Specs and plans | `docs/specs/`, `docs/plans/` | Specifications and implementation plans |
| Instructions | `AGENTS.md`, `CLAUDE.md` | Canonical instructions; Claude pointer to `AGENTS.md` |
| Build | `Makefile` | Cross-platform build interface |

## Agent skills

- Canonical project skills live in `.agents/skills/` so Codex can load shared instructions from repository scope. Keep full skill bodies in this location.
- `.claude/skills/*/SKILL.md` files contain thin `@../../../.agents/skills/.../SKILL.md` pointers for Claude Code. Never duplicate skill bodies.
- References: [Claude Code skills](https://platform.claude.com/docs/en/agents-and-tools/agent-skills/overview), [Codex skills](https://developers.openai.com/codex/skills), [skills guide (PDF)](https://resources.anthropic.com/hubfs/The-Complete-Guide-to-Building-Skill-for-Claude.pdf).
