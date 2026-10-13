# SQL Model Portability

## Dialect Rendering [DP-SQL-MODEL-DIALECTS]

The `SelectStatement` builder represents a database-independent query. Every
select, filter, join, grouping, ordering, and paging request supported by the
builder must render to SQLite, PostgreSQL, and SQL Server SQL. The same
behavioral test cases must exercise all three renderers. If a provider has no
renderer, the test fails; it must not be skipped or marked as an expected
failure. Each rendered statement must retain the requested columns, predicates,
joins, and row constraints, using the target provider's paging syntax.

## Shared SQL fragment assertions [DP-SQL-MODEL-TEST-FRAGMENTS]

Example SQL-model tests share successful-result validation, typed SQL extraction,
and ordered substring checks. Each query, failure message, expected fragment, and
remaining assertion retains its original value and behavior. Each fragment is still
checked separately with xUnit's original substring comparison.

### Shared predicate construction [DP-SQL-MODEL-SHARED-PREDICATE-COMBINATION]

Constant predicates share parameter and lambda creation. AND and OR share
parameter rebinding while retaining their native short-circuit expression nodes,
original left-hand parameter and original operand order.

### Shared LIKE predicate construction [DP-SQL-MODEL-SHARED-LIKE-PREDICATES]

String Contains and StartsWith share column and value extraction, null filtering
and LIKE-condition creation. They retain their distinct wildcard placement and
original value interpolation; unsupported methods still add no condition.
