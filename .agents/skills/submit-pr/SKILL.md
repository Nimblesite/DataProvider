---
name: submit-pr
description: Creates a pull request with a well-structured description after verifying CI passes. Use when the user asks to submit, create, or open a pull request.
disable-model-invocation: true
---

# Submit PR

Create a pull request for the current branch with a well-structured description.

## Steps

1. Run `make ci` — must pass completely before creating PR
2. **Generate the diff against main.** Run `git diff main...HEAD > /tmp/pr-diff.txt` to capture the full diff between the current branch and the head of main. This is the ONLY source of truth for what the PR contains. **Warning:** the diff can be very large. If the diff file exceeds context limits, process it in chunks (e.g., read sections with `head`/`tail` or split by file) rather than trying to load it all at once.
3. **Derive the PR title and description SOLELY from the diff.** Read the diff output and summarize what changed. Ignore commit messages, branch names, and any other metadata — only the actual code/content diff matters.
4. Write PR body using the template in `.github/pull_request_template.md`
5. Fill in (based on the diff analysis from step 3):
   - TLDR: one sentence
   - What Was Added: new files, features, deps
   - What Was Changed/Deleted: modified behaviour
   - How Tests Prove It Works: specific test names or output
   - Spec/Doc Changes: if any
   - Breaking Changes: yes/no + description
6. Use `gh pr create` with the filled template

## Rules

- Never create a PR if `make ci` fails
- PR description must be specific and tight — no vague placeholders
- Link to the relevant GitHub issue if one exists

## Success criteria

- `make ci` passed
- PR created with `gh pr create`
- PR URL returned to user
