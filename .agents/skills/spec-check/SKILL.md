---
name: spec-check
description: Audits spec/plan documents against the codebase to ensure every spec section has implementing code and tests. Use when the user says "check specs", "audit specs", "spec coverage", or "validate specs".
---
<!-- agent-pmo:d75d5c8 -->

# Spec Check

Audit spec and plan documents against the codebase.

## Steps

### Step 1 — Validate spec ID structure

For every markdown file in `docs/specs/`:
1. Find all headings that contain a spec ID (pattern: `[GROUP-TOPIC-DETAIL]`)
2. Validate each ID:
   - MUST be uppercase, hyphen-separated
   - MUST NOT contain sequential numbers (e.g., `[SPEC-001]` is ILLEGAL)
   - First word is the **group** — all sections sharing the same group MUST be adjacent
3. Check for duplicate IDs across all spec files
4. Report any violations

### Step 2 — Find spec documents

Scan `docs/specs/` and `docs/plans/` for all markdown files. For each file:
1. Extract all spec section IDs
2. Build a map: `spec ID → file path + heading`

### Step 3 — Check code references

For each spec ID found in Step 2:
1. Search the entire codebase (C#, Rust, TypeScript, F# files) for references to the ID
2. A reference is any comment containing the spec ID (e.g., `// Implements [AUTH-TOKEN-VERIFY]`)
3. Record which files reference each spec ID

### Step 4 — Check test references

For each spec ID:
1. Search test files for references to the ID
2. A test reference is a comment like `// Tests [AUTH-TOKEN-VERIFY]` in a test file

### Step 5 — Verify code logic matches spec

For spec IDs that DO have code references:
1. Read the spec section
2. Read the implementing code
3. Check that the code actually does what the spec describes
4. Flag any discrepancies

### Step 6 — Report

Output a table:

| Spec ID | Spec File | Code References | Test References | Status |
|---------|-----------|-----------------|-----------------|--------|

Status values:
- **COVERED** — has both code and test references
- **UNTESTED** — has code references but no test references
- **UNIMPLEMENTED** — has no code references at all
- **ORPHANED** — spec ID found in code but not in any spec document

## Rules

- Never modify spec documents — only report findings
- Never modify code — only report findings
- Every spec section MUST have at least one code reference and one test reference
- Orphaned references (code mentioning a spec ID that doesn't exist) are errors
