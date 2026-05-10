---
name: spec-check
description: Audit spec/plan documents against the codebase. Ensures every spec section has implementing code, tests, and matching logic. Use when the user says "check specs", "spec audit", or "verify specs".
argument-hint: "[optional spec ID or filename filter]"
---
<!-- agent-pmo:74cf183 -->

# spec-check

> **Portable skill.** This skill adapts to the current repository. The agent MUST inspect the repo structure and use judgment to apply these instructions appropriately.

Audit spec/plan documents against the codebase. Ensures every spec section has implementing code, tests, and that the code logic matches the spec.

## Arguments

- `$ARGUMENTS` — optional spec name or ID to check (e.g., `LQL-PIPE` or `migration-spec`). If empty, check ALL specs. Spec IDs are descriptive slugs, NEVER numbered (see Step 1).

## Instructions

Follow these steps exactly. Be strict and pedantic. Stop on the first failure.

---

### Step 1: Validate spec ID structure

Before checking code/test references, verify that the specs themselves are well-formed.

1. Find all spec documents (see locations in Step 2).
2. Extract every section ID using the regex `\[([A-Z][A-Z0-9]*(-[A-Z0-9]+)+)\]`.
3. **Flag invalid IDs:**
   - Numbered IDs (`[SPEC-001]`, `[REQ-003]`, `[CI-004]`) — must be renamed to descriptive hierarchical slugs.
   - Single-word IDs (`[TIMEOUT]`) — must have a group prefix.
   - IDs with trailing numbers (`[FEAT-AUTH-01]`) — the number is meaningless, remove it.
4. **Check group clustering:** The first word of each ID is its group. All sections in the same group MUST appear together (adjacent) in the document. If they're scattered, flag it.
5. **Check for missing IDs:** Any heading that defines a requirement or behavior should have an ID. Flag headings in spec files that look like they define behavior but lack an ID.

If any ID violations are found, report them all and **STOP**:
```
SPEC ID VIOLATIONS:

- docs/specs/migration-spec.md line 12: [SPEC-001] → rename to descriptive ID (e.g., [MIG-DDL-DIFF])
- docs/specs/sync-spec.md line 30: [SYNC-PUSH] and [SYNC-PULL] are not adjacent (scattered group)
- docs/specs/lql-spec.md line 5: "## Pipeline operators" has no spec ID

Fix spec IDs first, then re-run spec-check.
```

If all IDs are valid, proceed to Step 2.

---

### Step 2: Find all spec/plan documents

Search for markdown files that contain spec sections with IDs. Look in these locations:

- `docs/specs/*.md`
- `docs/plans/*.md`
- `docs/**/*.md`

Use Glob to find candidate files, then use Grep to confirm they contain spec IDs.

**Spec ID patterns** — IDs appear in square brackets, typically at the start of a heading or section line. Match this regex pattern:

```
\[([A-Z][A-Z0-9]*(-[A-Z0-9]+)+)\]
```

Spec IDs are **hierarchical descriptive slugs, NEVER numbered.** The format is `[GROUP-TOPIC]` or `[GROUP-TOPIC-DETAIL]`. The first word is the **group** — all sections sharing the same group MUST appear together in the spec's table of contents. IDs are uppercase, hyphen-separated, unique across the repo, and MUST NOT contain sequential numbers.

The hierarchy depth varies by repo: two words for simple repos (`[AUTH-LOGIN]`), three for most (`[AUTH-TOKEN-VERIFY]`), four for complex domains (`[AUTH-OAUTH-REFRESH-FLOW]`). The hierarchy mirrors the spec document's heading structure.

Examples of valid spec IDs (note how groups cluster):
- `[LQL-PIPE]`, `[LQL-FILTER]`, `[LQL-JOIN]` — all in the LQL group
- `[SYNC-PUSH]`, `[SYNC-PULL]`, `[SYNC-CONFLICT]` — all in the SYNC group
- `[MIG-DDL]`, `[MIG-RLS]`, `[MIG-VECTOR]` — all in the MIG group
- `[CI-TIMEOUT]`, `[CI-LINT]`, `[CI-RELEASE]` — all in the CI group

Examples of INVALID spec IDs:
- `[SPEC-001]` — numbered, meaningless
- `[FEAT-AUTH-01]` — trailing number
- `[REQ-003]` — sequential index, no group hierarchy
- `[CI-004]` — numbered, tells the reader nothing
- `[TIMEOUT]` — no group prefix, ungrouped

For each file, extract every spec ID and its associated section title (the heading text after the ID) and the full section content (everything until the next heading of equal or higher level).

---

### Step 3: Filter specs

- If `$ARGUMENTS` is non-empty, filter the discovered specs:
  - If it matches a spec ID exactly (e.g., `LQL-PIPE`), check only that spec.
  - If it matches a partial name (e.g., `migration`), check all specs in files whose path contains that string.
- If `$ARGUMENTS` is empty, process ALL discovered specs.

If filtering produces zero specs, report an error:
```
ERROR: No specs found matching "$ARGUMENTS". Discovered spec files: [list them]
```

---

### Step 4: Check each spec section

For EACH spec section that has an ID, perform checks A, B, and C below. **Stop on the first failure.**

#### Check A: Code references the spec ID

Search the entire codebase for the spec ID string, **excluding** these directories:
- `docs/`
- `node_modules/`
- `.git/`
- `target/` (Rust build artifacts)
- `bin/`, `obj/` (.NET build artifacts)
- `*.md` files (markdown is docs, not code)
- `Lql/lql-lsp-rust/crates/lql-parser/src/generated/` (ANTLR-generated)

Use Grep with the literal spec ID (e.g., `[LQL-PIPE]`) to find references in code files.

Code files should contain comments referencing the spec ID. The search must catch **all** comment styles across languages used in this repo:

**C-style `//` comments** (C#, F#, Rust, TypeScript, JavaScript):
- `// Implements [LQL-PIPE]`
- `// [LQL-PIPE]`
- `// Tests [LQL-PIPE]` (also counts as a code reference)
- `/// Implements [LQL-PIPE]` (XML / rustdoc / TSDoc doc comments)

**Hash `#` comments** (YAML, TOML, shell):
- `# Implements [LQL-PIPE]`
- `# [LQL-PIPE]`
- `# Tests [LQL-PIPE]`

**HTML/XML comments** (HTML, CSS, SVG, XML, `.csproj`, `.fsproj`, `.runsettings`):
- `<!-- Implements [LQL-PIPE] -->`
- `<!-- [LQL-PIPE] -->`

**ML-style comments** (F#):
- `(* Implements [LQL-PIPE] *)`

**The key rule:** any comment in any language containing the exact spec ID string (e.g., `[LQL-PIPE]`) counts as a valid code reference. The Grep search uses the literal spec ID string, so it naturally matches all comment styles. Do NOT restrict the search to specific comment prefixes — just search for the spec ID string itself.

**If NO code files reference the spec ID:**

```
SPEC VIOLATION: [LQL-PIPE] "Section Title" has no implementing code.

Every spec section must have at least one code file that references it via a comment
containing the spec ID (e.g., `// Implements [LQL-PIPE]`).

ACTION REQUIRED: Add a comment referencing [LQL-PIPE] in the file(s) that implement
this spec section, then re-run spec-check.
```

**STOP HERE. Do not continue to other checks.**

#### Check B: Tests reference the spec ID

Search test files for the spec ID. Test projects/files in this repo:
- `**/*.Tests/**/*.cs`, `**/*.Tests/**/*.fs`
- `Lql/lql-lsp-rust/crates/*/tests/**/*.rs`, `Lql/lql-lsp-rust/crates/*/src/**/*.rs` (`#[test]` attribs)
- `Lql/LqlExtension/src/test/**/*.ts`
- `Tests.Shared/**/*.cs`

Use Grep to search these locations for the literal spec ID string.

Tests should contain the spec ID in comments, test names, or attributes:

**C# (xUnit):**
- `// Tests [LQL-PIPE]`
- `[Fact] // Tests [LQL-PIPE]`
- `[Theory] // Tests [LQL-PIPE]`

**F# (xUnit / Expecto):**
- `// Tests [LQL-PIPE]`
- `[<Fact>] // Tests [LQL-PIPE]`

**Rust:**
- `// Tests [LQL-PIPE]`
- `#[test] // Tests [LQL-PIPE]`

**TypeScript (Mocha — VS Code test runner):**
- `// Tests [LQL-PIPE]`
- `describe('[LQL-PIPE] description', () => { ... })`
- `it('[LQL-PIPE] verifies behavior', () => { ... })`

**The key rule:** same as Check A — search for the literal spec ID string in test files. Any occurrence of the exact spec ID in a test file counts. Do NOT restrict to specific patterns — just search for the spec ID string itself.

**If NO test files reference the spec ID:**

```
SPEC VIOLATION: [LQL-PIPE] "Section Title" has no tests.

Every spec section must have corresponding tests that reference the spec ID.

ACTION REQUIRED: Add tests for [LQL-PIPE] with a comment or test name containing
the spec ID, then re-run spec-check.
```

**STOP HERE. Do not continue to other checks.**

#### Check C: Code logic matches the spec

This is the most critical check. You must:

1. **Read the spec section content carefully.** Understand exactly what behavior, logic, ordering, conditions, and constraints the spec describes.

2. **Read the implementing code.** Use the references found in Check A to locate the implementing files. Read the relevant functions/sections.

3. **Compare spec vs. code.** Be SENSITIVE and PEDANTIC. Check for:
   - **Ordering violations** — If the spec says A happens before B, the code must do A before B.
   - **Missing conditions** — If the spec says "only when X", the code must have that condition.
   - **Extra behavior** — If the code does something the spec doesn't mention, flag it only if it contradicts the spec.
   - **Wrong logic** — If the spec says "greater than" but code uses "greater than or equal", that's a violation.
   - **Missing steps** — If the spec describes 5 steps but code only implements 3, that's a violation.
   - **Wrong defaults** — If the spec says "default to X" but code defaults to Y, that's a violation.
   - **LQL platform-independence violations** — Per CLAUDE.md, LQL behavior MUST be identical across all transpiled SQL dialects. If a spec section is implemented in `Postgres` but missing from `SQLite` or `SqlServer`, that is a violation; the spec-check must report it and recommend filing a GitHub issue per CLAUDE.md's LQL rule.

4. **If the code deviates from the spec**, report a detailed error:

```
SPEC VIOLATION: [LQL-PIPE] Code does not match spec.

SPEC SAYS:
> "Pipe operator must left-associate; `a |> b |> c` parses as `(a |> b) |> c`"
> (from docs/specs/lql-spec.md, line 42)

CODE DOES:
> Right-associative parse rule (Lql/lql-lsp-rust/crates/lql-parser/src/grammar.g4:120)

DEVIATION: Grammar uses right-associativity instead of left-associativity.

ACTION REQUIRED: Update grammar in Lql/lql-lsp-rust/crates/lql-parser/src/grammar.g4
to left-associate the pipe operator, regenerate the ANTLR parser, and re-run spec-check.
```

**STOP HERE. Do not continue to other specs.**

5. **If the code matches the spec**, this check passes. Move to the next spec.

---

### Step 5: Report results

#### On failure (any check fails):

Output ONLY the first violation found. Use the exact error format shown above. Do not summarize other specs. Do not offer to fix the code. Just report the violation.

End with:
```
spec-check FAILED. Fix the violation above and re-run.
```

#### On success (all specs pass):

Output a summary table:

```
spec-check PASSED. All specs verified.

| Spec ID        | Title                    | Code References | Test References | Logic Match |
|----------------|--------------------------|-----------------|-----------------|-------------|
| [LQL-PIPE]     | Pipe operator            | Lql/lql-lsp-rust/...grammar.g4 | Lql/Nimblesite.Lql.Tests/...PipeTests.cs | PASS     |
| [SYNC-PUSH]    | Push delta protocol      | Sync/Nimblesite.Sync.Core/Push.cs | Sync/Nimblesite.Sync.Tests/PushTests.cs | PASS     |
| ...            | ...                      | ...             | ...             | ...         |

Checked N spec sections across M files. All have implementing code, tests, and matching logic.
```

---

## Search strategy summary

1. **Validate spec IDs:** Check all IDs are hierarchical, descriptive, grouped, and non-numbered
2. **Find spec files:** Glob for `docs/specs/**/*.md`, `docs/plans/**/*.md`, `docs/**/*.md`
3. **Extract spec IDs:** Grep for `\[[A-Z][A-Z0-9]*(-[A-Z0-9]+)+\]` in those files
4. **Find code refs:** Grep for the literal spec ID in all files, excluding `docs/`, `node_modules/`, `.git/`, `target/`, `bin/`, `obj/`, `*.md`, ANTLR-generated dirs
5. **Find test refs:** Grep for the literal spec ID in test directories and test file patterns
6. **Read and compare:** Read the spec section content and the implementing code, compare logic

## Key principles

- **Fail fast.** Stop on the first violation. One fix at a time.
- **Be pedantic.** If the spec says it, the code must do it. No "close enough".
- **Quote everything.** Always quote the spec text and the code in error messages so the developer sees exactly what's wrong.
- **Be actionable.** Every error must tell the developer what file to change and what to do.
- **Exclude docs from code search.** Markdown files are documentation, not implementation. Only search actual code files for spec references.
- **Exclude generated code.** ANTLR-generated parser sources under `Lql/lql-lsp-rust/crates/lql-parser/src/generated/` are not authored code; never expect spec IDs there.
- **No numbered IDs.** Spec IDs are hierarchical descriptive slugs (`[LQL-PIPE]`), NEVER sequential numbers (`[SPEC-001]`). The first word is the group — sections sharing a group must be adjacent in the TOC. If you encounter numbered or ungrouped IDs, flag them as a violation.
