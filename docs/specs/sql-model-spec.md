# SQL Model Portability

## Dialect Rendering [DP-SQL-MODEL-DIALECTS]

The `SelectStatement` builder represents a database-independent query. Every
select, filter, join, grouping, ordering, and paging request supported by the
builder must render to SQLite, PostgreSQL, and SQL Server SQL. The same
behavioral test cases must exercise all three renderers. If a provider has no
renderer, the test fails; it must not be skipped or marked as an expected
failure. Each rendered statement must retain the requested columns, predicates,
joins, and row constraints, using the target provider's paging syntax.
