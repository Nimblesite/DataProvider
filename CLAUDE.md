# DataProvider — Agent Instructions

⚠️ CRITICAL: **Reduce token usage.** Check file size before loading. Write less. Delete fluff and dead code. Alert user when context is loaded with pointless files. ⚠️ 

> Read this entire file before writing any code.
> These rules are NON-NEGOTIABLE. Violations will be rejected in review.

⚠️ NEVER KILL ANY VSCODE PROCESS ⚠️

<!-- agent-pmo:d75d5c8 -->

## Project Overview

DataProvider is a comprehensive .NET database access toolkit: source generation for SQL extension methods, the Lambda Query Language (LQL) transpiler, bidirectional offline-first sync, WebAuthn + RBAC auth, and an embeddable reporting platform. The LQL LSP is implemented in Rust with a VS Code extension in TypeScript. Healthcare sample applications live in a separate repo: [Nimblesite/ClinicalCoding](https://github.com/Nimblesite/ClinicalCoding).

**Primary language(s):** C# (.NET 10.0), Rust, TypeScript, F#
**Build command:** `make ci`
**Test command:** `make test`
**Lint command:** `make lint`

The LQL parser is ANTLR generated in Rust. We don't use manually generated parsers. Always upgrade to the latest version of ANTLR to make sure the parser is correct.

## Too Many Cooks (Multi-Agent Coordination)

If the TMC server is available:
1. Register immediately: descriptive name, intent, files you will touch
2. Before editing any file: lock it via TMC
3. Broadcast your plan before starting work
4. Check messages every few minutes
5. Release locks immediately when done
6. Never edit a locked file — wait or find another approach

## Hard Rules — Universal (no exceptions)

- **Parsing SQL with anything other than the ⭐️ OFFICIAL 👨🏼‍⚖️ ⭐️ platform specific parser = ⛔️ILLEGAL** - Use the actual .NET parser specified by the DB maintainer
- **DO NOT use git or Docker commands.** No `git add`, `git commit`, `git push`, or any git/Docker command. CI and GitHub Actions handle these.
- **ZERO DUPLICATION.** Before writing any code, search the codebase for existing implementations. Move code, don't copy it.
- **NEVER THROW** — Return `Result<T,E>`. Wrap failures in try/catch
- **No casting/!** — Pattern match on type only
- **No suppressing warnings** — Illegal. Fix the code, not the linter.
- **No raw SQL inserts/updates** — Use generated extensions
- **Use DataProvider Migrations to spin up DBs** — SQL for creating db schema = ILLEGAL (schema.sql = ILLEGAL). Use the Migration.CLI with YAML. This is the ONLY valid tool to migrate dbs unless the app itself spins up the migrations in code.
- **NO CLASSES** — Records + static methods (FP style)
- **PRIVATE/INTERNAL BY DEFAULT** — Don't expose types/members that users don't need
- **NO INTERFACES** — Use `Action<T>`/`Func<T>`
- **Expressions over assignments** — Pure functions over statements
- **Named parameters** — No ordinal calls
- **Close type hierarchies** — Private constructors:
```csharp
public abstract partial record Result<TSuccess, TFailure> { private Result() { } }
```
- **No singletons** — Inject `Func` into static methods
- **Immutable types!** — Use records. Don't use `List<T>`. Use `ImmutableList` `FrozenSet` or `ImmutableArray`
- **Always use type aliases (using) for result types** — Don't write like this: `new Result<string, SqlError>.Ok`
- **All tables must have a SINGLE primary key**
- **Primary keys MUST be UUIDs**
- **No in-memory dbs** — Real dbs all the way
- **NO REGEX** — Parse SQL with ANTLR .g4 grammars or SqlParserCS library. Never parse JSON, YAML, TOML, code, or any structured format with regex.
- **All public members require XMLDOC** — Except in test projects
- **One type per file** (except small records)
- **No commented-out code** — Delete it
- **No consecutive Console.WriteLine** — Use single string interpolation
- **No placeholders** — If incomplete, leave LOUD compilation error with TODO
- **Never use Fluent Assertions**
- **Keep files under 450 LOC and functions under 20 LOC**
- **100% test coverage is the goal.** Never delete or skip tests. Never remove assertions.
- **Prefer E2E/integration tests.** Unit tests are acceptable only for isolating problems.
- **Routinely format with CSharpier** — `dotnet csharpier .` in root folder
- **Every spec section MUST have a unique, hierarchical, non-numeric ID.** Format: `[GROUP-TOPIC]` or `[GROUP-TOPIC-DETAIL]` (e.g., `[AUTH-TOKEN-VERIFY]`, `[CI-TIMEOUT]`). NEVER use sequential numbers like `[SPEC-001]`. All code, tests, and design docs that implement or relate to a spec section MUST reference its ID in a comment (e.g., `// Implements [AUTH-TOKEN-VERIFY]`).

## Logging Standards

- **Use structured logging libraries.** Never use `Console.WriteLine`, `println!`, or `console.log` for diagnostics.
- **Log at entry/exit of all significant operations.** Use levels: `error`, `warn`, `info`, `debug`, `trace`.
- **Structured fields over string interpolation.** Log `{ "userId": 42, "action": "checkout" }` not `"User 42 performed checkout"`.
- **Copious ILogger** — Especially sync projects. Every service, handler, and non-trivial operation should log.
- **VS Code extensions:** Write detailed logs to a file in the extension's state folder. Basic errors and diagnostics MUST also appear in the extension's VS Code Output Channel.
- **SaaS / server apps:** Log writes to database/file MUST be async or on a background thread — never block the request path.
- **NEVER log personal data.** No PII: names, emails, addresses, phone numbers, IP addresses.
- **NEVER log secrets.** No API keys, tokens, passwords, connection strings. Log `"API key: present"` instead.

### Logging Libraries

| Language | Library |
|----------|---------|
| C# | `Microsoft.Extensions.Logging` + Serilog |
| Rust | `tracing` + `tracing-subscriber` |
| TypeScript | `pino` |

## Hard Rules — Language-Specific

### C# / F#

- No throwing exceptions — return `Result<T,E>` or `Option<T>`
- No `!` null-forgiving operator
- No `as` casts — use pattern matching
- No `dynamic`
- Nullable reference types enabled everywhere
- Records for immutable data (C#)
- F#: prefer discriminated unions, pipe operators, computation expressions
- Avoid classes. Use static methods as pure functions

#### Mandatory Packages (C#)

Always include these in `Directory.Build.props`:
- `Microsoft.CodeAnalysis.NetAnalyzers` — .NET analyzers
- `Outcome` — Result types for Railway Oriented Programming
- `Exhaustion` — Exhaustive pattern matching analyzer

### Rust

- No `unwrap()` — use `?` or explicit `match`
- No `expect()` in production code (tests may use it)
- No `panic!()`, `todo!()`, `unimplemented!()`, `unreachable!()` in production code
- No `unsafe {}` blocks without documented justification reviewed by a human
- No `allow(clippy::...)` attributes without documented justification
- All public items must have doc comments (`///`)
- Use `thiserror` for error types; `anyhow` only in application code (not libraries)

### TypeScript

- No `any` — use `unknown` and narrow explicitly
- No `!` (non-null assertion) — use optional chaining or explicit guards
- No implicit `any` — all function parameters and return types must be annotated
- No `// @ts-ignore` or `// @ts-nocheck`
- No `as Type` casts without a comment explaining why it's safe
- Strict mode always on (`tsconfig.json` must have `"strict": true`)
- No throwing — return `Result<T, E>` using a Result type library or discriminated union

## LQL

- LQL is database platform INDEPENDENT. It MUST work exactly the same on whatever platform it is transpiled to. Failure for this to happen must be logged as a GitHub issue

## LQL 
- LQL is database platform INDEPENDENT. It MUST work exactly the same on whatever platform it is transpiled to. Failure for this to happen must be logged as a GitHub issue

## CSS

- **MINIMAL CSS** — Do not duplicate CSS classes
- **Aggressively merge duplicate CSS** — consistency is key
- **Name classes after component, NOT section** — Sections should not have their own CSS classes

## Testing Rules

- **NEVER remove assertions**
- **FAILING TEST = OK. TEST THAT DOESN'T ENFORCE BEHAVIOR = ILLEGAL**
- **Skipping tests = ILLEGAL** — Failing tests = OK. Aggressively unskip tests
- **No try/catch in tests** that swallows the exception and asserts success
- **Tests must be deterministic.** No sleep(), no relying on timing, no random state
- **Timeout = FAILURE**
- **Bug fix process:** write test that fails because of bug -> verify test fails because of bug -> fix bug -> verify that test passes
- **E2E with zero mocking** — Test at the highest level. Avoid mocks. Only full integration testing
- **100% coverage, Stryker score 70%+**
- **Medical data:** use [FHIR spec](https://build.fhir.org/resourcelist.html)
- **VSCode extension E2E:** interact only via `vscode.commands.executeCommand`. Never call provider methods directly.

## Web

- Minimise CSS classes. We want as few as possible
- Optimise for AI and SEO. Must conform to these:

[Top ways to ensure your content performs well in Google's AI experiences on Search](https://developers.google.com/search/blog/2025/05/succeeding-in-ai-search)

[Search Engine Optimization (SEO) Starter Guide](https://developers.google.com/search/docs/fundamentals/seo-starter-guide)

## Build Commands (exact — cross-platform via GNU Make)

```bash
make build          # compile everything
make test           # run tests with coverage
make lint           # run all linters
make fmt            # format all code
make fmt-check      # check formatting (CI uses this)
make clean          # remove build artifacts
make check          # lint + test (pre-commit)
make ci             # lint + test + build (full CI simulation)
make coverage       # generate and open coverage report
make coverage-check # assert coverage thresholds
make setup          # post-create dev environment setup
```

## Architecture

| Component | Path | Purpose |
|-----------|------|---------|
| DataProvider | `DataProvider/` | Source gen for SQL -> extension methods |
| LQL | `Lql/` | Lambda Query Language -> SQL transpiler |
| LQL LSP | `Lql/lql-lsp-rust/` | Language server (Rust workspace: lql-parser, lql-analyzer, lql-lsp) |
| LQL Extension | `Lql/LqlExtension/` | VS Code extension (TypeScript) |
| Migration | `Migration/` | Schema migration framework (YAML -> SQL DDL) |
| Sync | `Sync/` | Offline-first bidirectional sync |
| Gatekeeper | `Gatekeeper/` | WebAuthn + RBAC auth |
| Samples | `Samples/` | Clinical, Scheduling, ICD10, Dashboard |
| Reporting | `Reporting/` | Embeddable reporting platform (SQL/LQL data sources, JSON config, React renderer) |
| Website | `Website/` | Documentation site (Eleventy + DocFX) |

## Repo Structure

```
DataProvider/
├── .github/workflows/     # CI/CD pipelines
├── .agents/skills/        # Shared agent skills
├── .claude/skills/        # Claude Code skill pointer files
├── DataProvider/           # Core source generator + CLI tools
├── Lql/                   # Lambda Query Language
│   ├── Lql/               # Core transpiler library
│   ├── lql-lsp-rust/      # Language server (Rust)
│   └── LqlExtension/      # VS Code extension (TypeScript)
├── Migration/             # Schema migration framework
├── Sync/                  # Bidirectional sync engine
├── Gatekeeper/            # WebAuthn auth + RBAC
├── Samples/               # Healthcare samples
├── Reporting/             # Embeddable reporting platform
├── Website/               # Documentation site
├── docs/
│   ├── specs/             # Specification documents
│   └── plans/             # Implementation plans
├── CLAUDE.md              # This file (canonical instructions)
├── AGENTS.md              # Pointer → CLAUDE.md
└── Makefile               # Cross-platform build interface
```

## Config

- .NET 10.0, C# latest, nullable, warnings as errors
- Central config in `Directory.Build.props`
- Format: `dotnet csharpier .`

## Agent Skills

- Canonical project skills live in `.agents/skills/` so Codex can load them from the repository scope.
- `.claude/skills/*/SKILL.md` files are thin `@../../../.agents/skills/.../SKILL.md` pointers for Claude Code. Do not duplicate full skill bodies there.
- [Claude Code Skills Overview](https://platform.claude.com/docs/en/agents-and-tools/agent-skills/overview)
- [Codex Skills](https://developers.openai.com/codex/skills)
- [The Complete Guide to Building Skills for Claude (PDF)](https://resources.anthropic.com/hubfs/The-Complete-Guide-to-Building-Skill-for-Claude.pdf)
