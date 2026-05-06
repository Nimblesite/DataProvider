## Lambda Query Language (Lql) Spec (v0.1)

### Overview

Lambda Query Language (Lql) is a functional–pipeline–style DSL that transpiles to procedural SQL or pure SQL for popular RDBMSs.

### Pipeline syntax

Chained operations using `|>`:

```
table |> join(other, on = …) |> filter(…) |> select(…) |> insert(…)
```

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

### Output

* Target SQL dialect chosen at transpilation (`postgres`, `mysql`, `sqlserver`, etc.)
* Defaults to PostgreSQL if not specified.

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

#### Identifier Validation

- **Numeric Start**: Identifiers cannot start with numbers (e.g., `123table` is invalid)
- **Undefined Variables**: Identifiers containing underscores that appear as pipeline bases are treated as undefined variables and result in syntax errors
  - Invalid: `undefined_variable |> select(name)` → "Syntax error: Undefined variable"
  - Valid: `users |> select(name)` → Simple table names without underscores are allowed

#### Error Handling

The parser performs semantic validation during the parsing phase:
- Identifiers starting with digits trigger "Syntax error: Identifier cannot start with a number"
- Undefined variables (identifiers with underscores used as pipeline bases) trigger "Syntax error: Undefined variable"
