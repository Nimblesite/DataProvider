---
name: ci-prep
description: Prepares the current branch for CI by running the exact same steps locally and fixing issues. If CI is already failing, fetches the GH Actions logs first to diagnose. Use before pushing, when CI is red, or when the user says "fix ci".
argument-hint: "[--failing] [optional job name to focus on]"
---

# CI Prep

Prepare the current state for CI. If CI is already failing, fetch and analyze the logs first.

## Arguments

- `--failing` — Indicates a GitHub Actions run is already failing. When present, you MUST execute **Step 1** before doing anything else.
- Any other argument is treated as a job name to focus on (but all failures are still reported).

If `--failing` is NOT passed, skip directly to **Step 2**.

## Step 1 �� Fetch failed CI logs (only when `--failing`)

You MUST do this before any other work.

```bash
BRANCH=$(git branch --show-current)
PR_JSON=$(gh pr list --head "$BRANCH" --state open --json number,title,url --limit 1)
```

If the JSON array is empty, **stop immediately**:
> No open PR found for branch `$BRANCH`. Create a PR first.

Otherwise fetch the logs:

```bash
PR_NUMBER=$(echo "$PR_JSON" | jq -r '.[0].number')
gh pr checks "$PR_NUMBER"
RUN_ID=$(gh run list --branch "$BRANCH" --limit 1 --json databaseId --jq '.[0].databaseId')
gh run view "$RUN_ID"
gh run view "$RUN_ID" --log-failed
```

Read **every line** of `--log-failed` output. For each failure note the exact file, line, and error message. If a job name argument was provided, prioritize that job but still report all failures.

## Step 2 — Analyze the CI workflow

1. Read `.github/workflows/ci.yml` completely. Parse every job and every step.
2. Extract the ordered list of commands the CI actually runs.
3. Note environment variables, matrix strategies, conditional steps, and service containers.

**Do NOT assume the steps are `make lint`, `make test`, `make build`.** Extract what the CI *actually does*.

## Step 3 — Run each CI step locally, in order

Work through failures in this priority order:

1. **Formatting** — run auto-formatters first to clear noise
2. **Compilation errors** — must compile before lint/test
3. **Lint violations** — fix the code pattern
4. **Runtime / test failures** — fix source code to satisfy the test

For each command extracted from the CI workflow:

1. Run the command exactly as CI would run it.
2. If the step fails, **stop and fix the issues** before continuing to the next step.
3. After fixing, re-run the same step to confirm it passes.
4. Move to the next step only after the current one succeeds.

### Hard constraints

- **NEVER modify test files** — fix the source code, not the tests
- **NEVER add suppressions** (`#pragma warning disable`, `#[allow(...)]`, `// eslint-disable`)
- **NEVER delete or ignore failing tests**
- **NEVER remove assertions**

If stuck on the same failure after 5 attempts, ask the user for help.

## Step 4 — Loop

- Go back to the first step and repeat until all steps pass locally. If `--failing`, you should see the exact same errors in your terminal that CI shows in the logs. Fix those errors until they are resolved.

## Step 5 — Commit/Push (only when `--failing`)

Once all CI steps pass locally:

1. Commit, but DO NOT MARK THE COMMIT WITH YOU AS AN AUTHOR!!! Only the user authors the commit!
2. Push
3. Monitor until completion or failure
4. Upon failure, go back to Step 1

## Rules

- *You are not allowed to commi/push until all tests pass*. Do not waste GitHub action minutes! The local CI must prove that everything is working.
- **Always read the CI workflow first.** Never assume what commands CI runs.
- Do not push if any step fails (unless `--failing` and all steps now pass)
- Fix issues found in each step before moving to the next
- Never skip steps or suppress errors
- If the CI workflow has multiple jobs, run all of them (respecting dependency order)
- Skip steps that are CI-infrastructure-only (checkout, setup actions, cache steps, artifact uploads) — focus on the actual build/test/lint commands

## Success criteria

- Every command that CI runs has been executed locally and passed
- All fixes are applied to the working tree
- The CI passes successfully (if you are correcting an existing failure)
