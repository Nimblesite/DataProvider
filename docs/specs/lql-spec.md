## Lambda Query Language (Lql) Spec (v0.1)

### Overview

Lambda Query Language (Lql) is a functional–pipeline–style DSL that transpiles to procedural SQL or pure SQL for popular RDBMSs.

### Pipeline syntax [LQL-PIPELINE-COMPOSITION]

Chained operations using `|>`:

```
table |> join(other, on = …) |> filter(…) |> select(…) |> insert(…)
```

Each appended operation must retain the earlier operations' meaning when
transpiled for PostgreSQL, SQLite, or SQL Server. Adding a join and filter must
keep the selected source; adding ordering and a limit must keep that join and
filter while applying the requested ordering and row limit.

### Constructs

| Feature           | Description                     |        |          |
| ----------------- | ------------------------------- | ------ | -------- |
| `let name = expr` | Bind an expression to a name    |        |          |
| Identifiers       | Tables, columns, or bound names |        |          |
| Function calls    | `join(table, on=cond)`          |        |          |
| Pipelines         | \`table                         | > func | > func\` |
| Arguments         | Positional or named (`on=…`)    |        |          |
| Literals          | `'string'`, `123`               |        |          |

### IN List Predicates [LQL-PREDICATE-IN-LIST]

Filter predicates may use `expr in (literal, literal, ...)` to express
membership in a non-empty literal list. Pipeline filter bodies, including
`exists(table |> filter(fn(row) => ...))`, must emit SQL `IN (...)` for the
target platform. This supports compact role-membership predicates such as
`m.role in ('owner', 'admin')` without expanding to `OR` chains.

### Common Table Expressions [LQL-CTE]

`with name as (pipeline), other as (pipeline) main-pipeline` names pipelines that
the main pipeline (and later CTEs) can use as tables. It transpiles to
`WITH name AS (...), other AS (...) SELECT ...` on every dialect.

```
with high_value_customers as (
    orders |> group_by(orders.user_id) |> having(fn(g) => sum(orders.total) > 10000) |> select(orders.user_id)
)
users |> join(high_value_customers, on = users.id = high_value_customers.user_id) |> select(users.id)
```

### Derived Tables [LQL-DERIVED-TABLE]

`join((pipeline), on = ...)` joins the result of a parenthesized pipeline. The
derived table is aliased after its base table, and outer references to that base
table (`orders.order_count`) resolve to the derived table.

### Subquery Layout [LQL-SUBQUERY-LAYOUT]

Statements with CTEs or derived tables are rendered identically on SQLite,
PostgreSQL and SQL Server, one clause per line:

- Queries that join sources alias each source by the initials of its
  underscore-separated name (`users` → `u`, `high_value_customers` → `hvc`, with a
  numeric suffix on collision) and qualify columns with those aliases.
- A standalone single-table body (a CTE or derived table) drops its table
  qualifiers; EXISTS/IN bodies keep them, because they may correlate with the
  outer query.
- Nested bodies are indented four spaces inside `(` … `)`.
- Several `filter` steps are ANDed, each parenthesized so an `OR` inside one
  filter cannot bind across the `AND`.
- Only paging differs by dialect: `LIMIT`/`OFFSET` on SQLite and PostgreSQL,
  `TOP n` or `OFFSET … ROWS FETCH NEXT … ROWS ONLY` on SQL Server. LIMIT/OFFSET
  inside EXISTS/IN bodies are rejected rather than dropped.
- SQLite otherwise renders statements on one line; a WHERE holding a multi-line
  subquery switches it to one clause per line.

Futures:

join(table2, on = …)      
filter(fn(row) => …)      
select(cols…)             
union (this contains a LIST of select statements)        
insert(target_table)      
group_by(cols…)           
order_by(cols…, dir)      
limit(n)   

### Supported Functions

| Function             | Purpose                        |
| -------------------- | ------------------------------ |
| `join(table, on=…)`  | SQL `JOIN`                     |
| `filter(fn(row)=>…)` | SQL `WHERE`                    |
| `map(fn(row)=>…)`    | SQL loop or `SELECT` transform |
| `select(cols…)`      | SQL `SELECT`                   |
| `insert(target)`     | SQL `INSERT INTO … SELECT …`   |
| `union(other)`       | SQL `UNION`                    |
| `range(a,b)`         | generates a range              |

### Output [LQL-OUTPUT-DIALECTS]

* Target SQL dialect chosen at transpilation (`postgres`, `mysql`, `sqlserver`, etc.)
* Defaults to PostgreSQL if not specified.
* Equivalent select, filter, join, grouping, ordering, and paging requests retain their meaning on PostgreSQL, SQLite, and SQL Server. A target dialect that cannot render a requested statement must return an error rather than silently dropping an operation.

### Shared Rendering [LQL-RENDER-SHARED]

The three dialect extension APIs share internal statement validation, subquery
dispatch, identifier fallback, and conversion of rendering exceptions to SQL
errors. Dialect delegates retain pipeline rendering, paging, and PostgreSQL's
bare-identifier formatting. Existing null-argument behavior and public method
signatures remain unchanged. SQL-model rendering uses the same error boundary.

### Identifier Casing

All identifiers (table names, column names) are **case-insensitive** and transpile to **lowercase** in generated SQL. This is a fundamental design rule that ensures LQL works identically across all database platforms.

- LQL source may use any casing: `fhir_Patient.GivenName`, `fhir_patient.givenname`, `FHIR_PATIENT.GIVENNAME` are all equivalent
- Transpiled SQL always emits unquoted lowercase identifiers
- DDL generators (Migration tool) always create lowercase identifiers
- **Never quote identifiers** in generated SQL — quoting preserves case and breaks cross-platform compatibility
- Column names in YAML schemas may use PascalCase for readability; DDL generators lowercase them automatically

This guarantees portability:
- PostgreSQL: unquoted identifiers fold to lowercase
- SQLite: identifiers are case-insensitive
- SQL Server: identifiers are case-insensitive (default collation)

### Validation Rules

#### Identifier Validation [LQL-IDENTIFIER-VALIDATION]

- **Numeric Start**: Identifiers cannot start with numbers (e.g., `123table` is invalid)
- **Underscored Table Names**: An identifier such as `tenant_members` is a valid pipeline base. The parser cannot distinguish an unknown variable from a table name without schema metadata; any undefined-variable check requires a later semantic pass with table context.

#### Error Handling

The parser performs semantic validation during the parsing phase:
- Identifiers starting with digits trigger "Syntax error: Identifier cannot start with a number"
- Undefined variables (identifiers with underscores used as pipeline bases) trigger "Syntax error: Undefined variable"

### Shared portable function mappings [LQL-FUNCTION-MAPPING-SHARED]

All dialect providers reuse mappings for count, sum, average, minimum, maximum,
coalesce, uppercase and lowercase. PostgreSQL and SQL Server additionally share
standard substring, EXISTS and window-function mappings. Providers retain their
exact supported-function sets and their existing date, length and SQLite substring
handlers; unknown functions retain the existing fallback behavior.

### Shared error-test execution [LQL-ERROR-TEST-EXECUTION]

Named parser-error tests share parsing and error-type/message assertions. Every
input and expected message remains unchanged, including the original ordinal
comparison. Syntax cases retain non-null source positions; malformed syntax,
invalid characters, and formatted errors retain their line/column bounds checks.
The identifier-validation theory still checks both accepted table names against
all three SQL dialects.

### Shared completion-test labels [LQL-COMPLETION-TEST-LABELS]

Completion tests collect labels through one helper while retaining every context, scope, keyword assertion, ordering check, and duplicate-label check. The helper keeps labels in a vector so repeated labels remain observable.

### Shared context ordering [LQL-CONTEXT-ORDERING-SHARED]

PostgreSQL, SQLite, and SQL Server contexts append ordering items through one internal Core helper. Public method signatures and XML documentation remain intact. Columns and directions retain their original order and values, and existing dialect integration tests cover each public path.

### Shared analyzer column fixture [LQL-TEST-COLUMN-FIXTURE]

Rust completion, hover and schema tests reuse one test-only column constructor, preserving all names, SQL types, nullability and primary-key flags at their original call sites.
