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
handlers; unknown functions retain the existing fallback behavior. PostgreSQL and
SQL Server share construction of date-function overrides, retaining their native
function names and handlers. PostgreSQL and SQLite share LIMIT/OFFSET syntax
construction with their original date expressions and boolean support.

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

### Shared hover-test requests [LQL-HOVER-TEST-REQUEST]

LSP protocol tests share the hover request and non-null result assertion. Every
document URI, line, character, failure message, and content assertion remains
unchanged, including multi-line document positions.

### Shared LSP protocol scenarios [LQL-PROTOCOL-SHARED-SCENARIOS]

Protocol tests reuse document initialization, completion requests, hover requests,
diagnostics updates and formatting validation. Every original test and assertion
remains active; helpers accept the original documents, positions and messages.

### Shared Rust diagnostic scenarios [LQL-RUST-SHARED-TEST-WORKFLOWS]

Diagnostic tests with identical workflows share their execution and assertions.
Every named test remains, with its original source, search strings and assertions.

### Shared literal copying [LQL-SHARED-LITERAL-COPY]

The CLI, Postgres renderer and migration expression rewriters reuse one internal
string-literal copying routine. It preserves escaped apostrophes, closing-delimiter
positions and unterminated-text handling from the original routines.

### Independent Rust expectations [LQL-RUST-INDEPENDENT-EXPECTATIONS]

Keep expected function-name arrays independent of implementation constants so
language diagnostics are checked against explicit test oracles. Data-only include
files contain the original arrays and are excluded from duplication analysis;
all iteration, analysis and assertions remain in the test runners.

### Shared protocol position requests [LQL-PROTOCOL-SHARED-POSITION-REQUEST]

Protocol tests share the standard text-document URI and cursor-position request
shape. Each caller retains its method, document, position and response checks.

### Shared terminal text [LQL-SHARED-TERMINAL-TEXT]

AST conversion reuses the first available terminal's text through the generated
ANTLR getters. Token priority and later unsupported-expression handling retain
the original order; no additional SQL or LQL parser is introduced.

### [LQL-CLI-SHARED-COMMAND]

Platform subcommands share required input, optional output, validation options, and handler setup. Each supplies its platform label and SQL conversion function.

### [LQL-SHARED-QUOTED-TOKEN]

SQL rewriting shares quoted-token dispatch and preserves single-quote escapes, double-quoted identifiers, and unterminated-token handling.

## [LQL-FSHARP-SHARED-SQL-ASSERTIONS]

Twenty-three named F# runtime scenarios use one transpile and case-insensitive
assertion workflow. The independent data table preserves all 23 queries, original
case labels and 47 expected fragments in their original order.

## [LQL-SHARED-IDENTIFIER-END]

Existing identifier rewrite paths use one letter, digit and underscore scan. Callers keep their start offsets, quoted handling and replacement decisions.

## [LQL-RUST-NAMED-CASE-MATRICES]

Repeated Rust test bodies run once per named input row. Each iteration retains the original assertions and creates fresh local state. Data-only tables contain original case names, inputs and expected values; runners remain analysed.

### Shared ORDER BY column extraction [LQL-ORDER-SHARED-COLUMN-EXTRACTION]

Explicit and implicit ORDER BY directions use the same ANTLR column extraction, preserving qualified identifiers and branch-specific error messages. The fallback checks DESC before ASC and retains suffix trimming and case-insensitive matching.

### Shared WHERE token rendering [LQL-CONTEXT-WHERE-SHARED]

All three dialects share token dispatch for comparisons, logical operators,
parentheses and expression conditions. Each retains its own column formatter;
unknown tokens retain the existing comment output.

### Shared ANTLR error listener [LQL-PARSER-SHARED-ERROR-LISTENER]

Lexer and parser callbacks share error collection and source-position formatting.
Lexer symbols keep zero byte offsets; parser symbols keep their token offsets
with the existing zero fallback. Lexer errors retain precedence during parsing.

### Shared F# seed orders [LQL-FSHARP-SHARED-SEED-ORDERS]

The original seven order rows feed one generated insert callback. Inserts remain
sequential and deferred, with the same values, transaction and first-error return.
The independent input table is excluded with the existing fixture data rule.

### Shared F# database row counts [LQL-FSHARP-SHARED-ROW-COUNT]

Four named integration cases share fresh database creation, query execution and
row-count assertions. Additional customer-name assertions run before the database
connection is disposed, as in the original case.

### Shared F# parse error assertions [LQL-FSHARP-SHARED-PARSE-ERROR]

Invalid and empty runtime queries share the same Result match. Their named test
cases, inputs and branch-specific failure messages remain intact.

### Shared parser exception construction [LQL-PARSER-SHARED-EXCEPTION-CONSTRUCTION]

Unsupported AST branches share exception construction with the existing static
source-position formatter. Every branch retains its error message, context and
throw point; no new failure branch is introduced.

### Shared compile-time F# cases [LQL-FSHARP-SHARED-COMPILE-TIME-CASES]

Sixteen named SQL-fragment cases share one runner, preserving their original
compile-time query types and 24 expected fragments. F# fixture exclusions cover
only the independent query aliases and expectation rows, leaving database setup,
query execution and every assertion analysed.

### Shared clause formatting [LQL-FUNCTION-MAPPING-SHARED-CLAUSE-FORMAT]

LIMIT and OFFSET use one invariant-culture format operation with each dialect's
original syntax template and caller-supplied count.

- **[LQL-FSHARP-SHARED-CLEAR-DATA]**: Clear the F# provider fixture tables through one sequential native SQLite command loop, in Orders/Users/Products/Customer dependency order, disposing each command before the next deletion.
- **[LQL-FILE-FIXTURE-SHARED-SET-ASSERTION]**: Compare each dialect fixture set through one assertion, preserving exact set equality and both missing-file diagnostics.

- **[LQL-POSTGRES-SHARED-BARE-QUOTING]**: Inline qualified identifiers reuse the existing bare-identifier quoting rule for both the prefix and tail, preserving uppercase ASCII detection and SQL literal handling.

- **[LQL-FSHARP-SHARED-EXECUTION-CASES]**: Six named native SQLite execution scenarios share count/nonempty runners. Preserve both original exact row counts, every positive-result check, and each original computed-column or aggregate-column assertion. Each case seeds and disposes its own connection.

- **[LQL-TEST-SHARED-DIALECT-RESULTS]**: Dialect E2E conversion outcomes reuse the existing shared success assertion and value extraction. Execute all three conversions before checking PostgreSQL, SQL Server and SQLite in their original order, retaining dialect failure messages.

- **[LQL-AST-ARG-DEFAULTS]**: AST step factories share first-argument extraction and retain their existing per-step fallback values when no argument is supplied.

- **[LQL-AST-ARG-MAPPING]**: AST step factories share projection of each ANTLR argument in order through the original mapper.

- **[LQL-FSHARP-SHARED-NAMED-CASE]**: F# scenario runners share the original nonempty-label assertion before invoking each scenario body; preserve assertion order and native connection lifetimes.

- **[LQL-PREDICATE-IN-SUBQUERY]**: LQL IN predicates retain nested pipeline-to-subquery rendering and qualified identifier handling.

- **[LQL-PREDICATE-EXISTS-SUBQUERY]**: LQL EXISTS predicates retain nested pipeline-to-subquery rendering and their original parser error behavior.
