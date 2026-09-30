# Declarative Trigger Guards (GitHub issue 82)

User-declared BEFORE triggers as first-class schema objects: diffed, inspected,
and dropped destructively, exactly like RLS policies. Motivating case:
last-owner protection on `tenant_members` (refuse to demote/delete the only
`owner` of a tenant), which RLS predicates cannot express.

## [MIG-TRIGGER-YAML] YAML shape

Triggers are declared per table under `triggers:`.

```yaml
tables:
  - name: tenant_members
    triggers:
      - name: assert_not_last_owner
        timing: Before            # Before (default) or After
        events: [Update, Delete]  # Insert | Update | Delete
        forEachRow: true          # default true; false is rejected (guards are row-level)
        raiseWhen: |              # LQL guard predicate; row refs via old./new.
          old.role = 'owner' and not exists(
            tenant_members
            |> filter(fn(m) => m.tenant_id = old.tenant_id and m.role = 'owner' and m.id <> old.id)
          )
        errorMessage: cannot remove the last owner of a tenant
```

The trigger raises `errorMessage` and aborts the statement when `raiseWhen`
evaluates true for the affected row. `raiseWhen` + `errorMessage` is the
declarative, platform-independent form of the issue's `bodyLql` proposal.

## [MIG-TRIGGER-MODEL] Model

`TriggerDefinition` record (Name, Timing, Events, ForEachRow, RaiseWhenLql,
ErrorMessage) on `TableDefinition.Triggers`. Enums `TriggerTiming`
(Before/After) and `TriggerEvent` (Insert/Update/Delete). Closed, immutable,
YAML round-trippable with defaults omitted.

## [MIG-TRIGGER-DIFF] Diff semantics

Triggers are diffed by name per table (mirror of RLS policy diff):
missing in current → `CreateTriggerOperation`; present in current but removed
from desired → `DropTriggerOperation` only when `allowDestructive` (the drop
is logged at Warning), otherwise skipped. `DropTriggerOperation` is
destructive and gated by `MigrationOptions.AllowDestructive`.

## [MIG-TRIGGER-GUARD-LQL] Guard predicate LQL

`raiseWhen` is an LQL boolean expression over the affected row and the
database. Row column references use `old.<column>` / `new.<column>`
(case-insensitive prefix). Supports simple comparisons, `and`/`or`
composition, and `exists(...)` / `not exists(...)` pipeline subqueries.
Transpiles identically (same semantics) on every supported platform.

Validation (all fail the migration loudly rather than diverging at runtime):

- `old.` references require every event to be Update/Delete; `new.`
  references require Insert/Update (OLD does not exist on INSERT, NEW does
  not exist on DELETE).
- `forEachRow: false` is rejected -- guards are row-level on every platform
  (SQLite has no statement triggers; Postgres statement triggers see NULL
  OLD/NEW and would silently never fire).
- The token `__TRG_` is reserved for internal sentinels; predicates
  containing it are rejected so user literals can never be rewritten.

On Postgres, transpiled `exists()` pipeline SQL gets an identifier-quoting
pass so mixed-case column names are not case-folded.

## [MIG-TRIGGER-SQLITE] SQLite emission and inspection

One trigger per event named `usr_{event}_{trigger}_{table}`:
`CREATE TRIGGER ... BEFORE {EVENT} ON [table] FOR EACH ROW BEGIN
SELECT RAISE(ABORT, '{errorMessage}') WHERE {predicate}; END`.
Inspector reads `usr_`-prefixed triggers back from `sqlite_master` (grouped by
trigger name, `rls_` triggers excluded) so re-diff is a no-op.

## [MIG-TRIGGER-PG] PostgreSQL emission and inspection

One plpgsql function `{table}_{trigger}_trgfn` raising `errorMessage` when the
predicate holds (`RAISE ... USING MESSAGE` avoids `%` format interpretation;
the dollar-quote tag is chosen to never occur in the body), plus one trigger
`usr_{trigger}` (`BEFORE {events} ... FOR EACH ROW EXECUTE FUNCTION`). Drop
removes trigger and function. Identifiers over PostgreSQL's 63-byte limit are
rejected loudly (silent truncation would desynchronise create/drop/inspect).

Inspector reads only `usr_`-prefixed triggers back from
`information_schema.triggers` grouped by trigger name (prefix stripped) so
re-diff is a no-op and triggers created outside the migration tool are never
dropped by destructive runs. `*_trgfn` guard functions are excluded from
support-function read-back because they are owned by the trigger lifecycle.
