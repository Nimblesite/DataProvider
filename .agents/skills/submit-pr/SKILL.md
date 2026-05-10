---
name: submit-pr
description: Creates a pull request with a well-structured description after verifying CI passes. Use when the user asks to submit, create, or open a pull request.
disable-model-invocation: true
---
<!-- agent-pmo:74cf183 -->

# Submit PR

Create a pull request for the current branch with a well-structured description.

## Steps

### Step 1 — Run `make ci` (BLOCKING — DO NOT SKIP)

You MUST invoke the Bash tool with the literal command `make ci` in THIS session before calling `gh pr create`. No exceptions.

**Skip allowed ONLY if** there is a Bash tool call in the visible transcript of THIS session whose `command` was exactly `make ci` (or `make check` — which is `lint + test`, a strict subset that does not satisfy `make ci`'s `lint + test + build`) and which exited 0 AFTER the most recent code change. If you are unsure, run it. The cost of re-running is low; the cost of a broken PR is high.

**Skip NOT allowed when:**
- You ran `dotnet test`, `make test`, `make lint`, partial test filters, or any subset — these are NOT `make ci`. Run `make ci` anyway.
- You ran `make ci` earlier but have edited code or skill files since — re-run.
- The user asked you to skip — refuse and ask them to invoke `make ci` themselves if they want to bypass. Tell them which step they're bypassing.
- You "reasoned" the change is small/safe/test-only — irrelevant. Run it.

If `make ci` fails: STOP. Do not create the PR. Fix the failures or report them to the user and wait for direction.

### Step 2 — Generate the diff against main

Run `git diff main...HEAD > /tmp/pr-diff.txt` to capture the full diff between the current branch and the head of main. This is the ONLY source of truth for what the PR contains. **Warning:** the diff can be very large. If the diff file exceeds context limits, process it in chunks (e.g., read sections with `head`/`tail` or split by file) rather than trying to load it all at once.

### Step 3 — Derive the PR title and description SOLELY from the diff

Read the diff output and summarize what changed. Ignore commit messages, branch names, and any other metadata — only the actual code/content diff matters.

### Step 4 — Write the PR body

Use the template in `.github/PULL_REQUEST_TEMPLATE.md`. Fill in (based on the diff analysis from step 3):
- TLDR: one sentence
- What Was Added: new files, features, deps
- What Was Changed/Deleted: modified behaviour
- How Tests Prove It Works: specific test names or output
- Spec/Doc Changes: if any
- Breaking Changes: yes/no + description

### Step 5 — Create the PR

Use `gh pr create` with the filled template.

## Rules

- **Never call `gh pr create` without first verifying `make ci` passed in this session.** Step 1 is the gate, not a suggestion.
- PR description must be specific and tight — no vague placeholders.
- Link to the relevant GitHub issue if one exists.

## Success criteria

- `make ci` was invoked in this session and exited 0 after the most recent code change
- PR created with `gh pr create`
- PR URL returned to user
