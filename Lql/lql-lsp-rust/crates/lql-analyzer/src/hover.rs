use crate::schema::SchemaCache;

/// Hover information for a word in the document.
#[derive(Debug, Clone)]
pub struct HoverInfo {
    pub title: String,
    pub detail: String,
    pub signature: Option<String>,
}

/// Get hover/IntelliPrompt information for a word at the cursor.
/// Falls back gracefully with no schema.
pub fn get_hover(word: &str) -> Option<HoverInfo> {
    let lower = word.to_ascii_lowercase();
    HOVER_DATABASE
        .iter()
        .find(|(name, _, _, _)| *name == lower.as_str())
        .map(|(_, title, detail, sig)| HoverInfo {
            title: title.to_string(),
            detail: detail.to_string(),
            signature: sig.map(|s| s.to_string()),
        })
}

/// Get hover info with schema awareness.
/// If `qualified` is "Table.Column", shows column type from the database.
/// If `word` is a table name, shows table schema (columns and types).
pub fn get_hover_with_schema(
    word: &str,
    qualified: Option<(&str, &str)>,
    schema: Option<&SchemaCache>,
) -> Option<HoverInfo> {
    // Try qualified column hover first: "Table.Column"
    if let (Some((table_name, col_name)), Some(schema)) = (qualified, schema) {
        if let Some(table) = schema.get_table(table_name) {
            if let Some(col) = table.get_column(col_name) {
                return Some(HoverInfo {
                    title: format!("{}.{} — {}", table.name, col.name, col.type_description()),
                    detail: format!(
                        "Column `{}` on table `{}`\n\nType: `{}`\nNullable: {}\nPrimary Key: {}",
                        col.name,
                        table.name,
                        col.sql_type,
                        if col.is_nullable { "yes" } else { "no" },
                        if col.is_primary_key { "yes" } else { "no" },
                    ),
                    signature: Some(format!(
                        "{}.{} : {}",
                        table.name,
                        col.name,
                        col.type_description()
                    )),
                });
            }
            // Table exists but column doesn't — show available columns
            let available: Vec<&str> = table.columns.iter().map(|c| c.name.as_str()).collect();
            return Some(HoverInfo {
                title: format!("{}.{} — Column not found", table.name, col_name),
                detail: format!(
                    "Column `{}` does not exist on table `{}`.\n\nAvailable columns: {}",
                    col_name,
                    table.name,
                    available.join(", ")
                ),
                signature: None,
            });
        }
    }

    // Try table name hover
    if let Some(schema) = schema {
        if let Some(table) = schema.get_table(word) {
            let col_lines: Vec<String> = table
                .columns
                .iter()
                .map(|c| format!("  {} : {}", c.name, c.type_description()))
                .collect();
            return Some(HoverInfo {
                title: format!("{} — Table ({} columns)", table.name, table.columns.len()),
                detail: format!(
                    "Database table `{}`\nSchema: `{}`\n\nColumns:\n{}",
                    table.name,
                    table.schema,
                    col_lines.join("\n")
                ),
                signature: Some(format!(
                    "{} ({})",
                    table.name,
                    table
                        .columns
                        .iter()
                        .map(|c| c.name.as_str())
                        .collect::<Vec<_>>()
                        .join(", ")
                )),
            });
        }
    }

    // Fall back to keyword hover
    get_hover(word)
}

/// Static database of hover information for all LQL constructs.
const HOVER_DATABASE: &[(&str, &str, &str, Option<&str>)] = &[
    // Pipeline operations
    (
        "select",
        "select — Project Columns",
        "Projects specified columns from the input data. Supports aliases, expressions, and aggregate functions.",
        Some("table |> select(col1, col2 as alias, expr as name)"),
    ),
    (
        "filter",
        "filter — Filter Rows",
        "Filters rows based on a lambda predicate. The lambda receives a row parameter for accessing columns.",
        Some("table |> filter(fn(row) => row.table.column > value)"),
    ),
    (
        "join",
        "join — Inner Join",
        "Performs an inner join between the current pipeline and another table on a condition.",
        Some("table1 |> join(table2, on = table1.id = table2.fk_id)"),
    ),
    (
        "left_join",
        "left_join — Left Outer Join",
        "Performs a left outer join, keeping all rows from the left table.",
        Some("table1 |> left_join(table2, on = table1.id = table2.fk_id)"),
    ),
    (
        "group_by",
        "group_by — Group Rows",
        "Groups rows by specified columns. Use with aggregate functions in select.",
        Some("table |> group_by(col1, col2) |> select(col1, count(*) as cnt)"),
    ),
    (
        "order_by",
        "order_by — Order Results",
        "Orders result rows by specified columns with optional asc/desc direction.",
        Some("table |> order_by(column asc, column2 desc)"),
    ),
    (
        "having",
        "having — Filter Groups",
        "Filters groups after group_by based on aggregate conditions.",
        Some("table |> group_by(col) |> having(fn(g) => count(*) > 5)"),
    ),
    (
        "limit",
        "limit — Limit Results",
        "Restricts the number of rows returned.",
        Some("table |> limit(10)"),
    ),
    (
        "offset",
        "offset — Skip Rows",
        "Skips the first N rows. Typically used with limit for pagination.",
        Some("table |> offset(20) |> limit(10)"),
    ),
    (
        "union",
        "union — Combine Queries",
        "Combines the results of two queries, removing duplicates.",
        Some("query1 |> union(query2)"),
    ),
    (
        "insert",
        "insert — Insert Into Table",
        "Inserts pipeline results into a target table.",
        Some("pipeline |> insert(target_table)"),
    ),
    // Aggregate functions
    (
        "count",
        "count — Count Rows",
        "Counts the number of rows or non-null values in a column.",
        Some("count(*) or count(column)"),
    ),
    (
        "sum",
        "sum — Sum Values",
        "Calculates the sum of numeric values in a column.",
        Some("sum(column)"),
    ),
    (
        "avg",
        "avg — Average",
        "Calculates the average of numeric values in a column.",
        Some("avg(column)"),
    ),
    (
        "max",
        "max — Maximum",
        "Finds the maximum value in a column.",
        Some("max(column)"),
    ),
    (
        "min",
        "min — Minimum",
        "Finds the minimum value in a column.",
        Some("min(column)"),
    ),
    // String functions
    (
        "concat",
        "concat — Concatenate Strings",
        "Concatenates two or more strings together.",
        Some("concat(str1, str2, ...)"),
    ),
    (
        "substring",
        "substring — Extract Substring",
        "Extracts a portion of a string starting at a position for a given length.",
        Some("substring(string, start, length)"),
    ),
    (
        "length",
        "length — String Length",
        "Returns the number of characters in a string.",
        Some("length(string)"),
    ),
    (
        "trim",
        "trim — Trim Whitespace",
        "Removes leading and trailing whitespace from a string.",
        Some("trim(string)"),
    ),
    (
        "upper",
        "upper — Uppercase",
        "Converts a string to uppercase.",
        Some("upper(string)"),
    ),
    (
        "lower",
        "lower — Lowercase",
        "Converts a string to lowercase.",
        Some("lower(string)"),
    ),
    (
        "round",
        "round — Round Number",
        "Rounds a number to the specified precision.",
        Some("round(number, precision)"),
    ),
    (
        "abs",
        "abs — Absolute Value",
        "Returns the absolute value of a number.",
        Some("abs(number)"),
    ),
    (
        "coalesce",
        "coalesce — First Non-Null",
        "Returns the first non-null value from its arguments.",
        Some("coalesce(val1, val2, ...)"),
    ),
    // Window functions
    (
        "row_number",
        "row_number — Row Number",
        "Assigns a sequential number to each row within a partition.",
        Some("row_number() over (partition by col order by col2)"),
    ),
    (
        "rank",
        "rank — Rank",
        "Assigns a rank to each row within a partition, with gaps for ties.",
        Some("rank() over (partition by col order by col2)"),
    ),
    (
        "dense_rank",
        "dense_rank — Dense Rank",
        "Assigns a rank to each row within a partition, without gaps for ties.",
        Some("dense_rank() over (partition by col order by col2)"),
    ),
    // Keywords
    (
        "let",
        "let — Variable Binding",
        "Binds a pipeline expression to a name for reuse later in the program.",
        Some("let name = pipeline_expression"),
    ),
    (
        "fn",
        "fn — Lambda Expression",
        "Defines an anonymous function used in filter and having clauses.",
        Some("fn(param1, param2) => expression"),
    ),
    (
        "case",
        "case — Conditional Expression",
        "Evaluates conditions and returns the matching result.",
        Some("case when condition then result else default end"),
    ),
    (
        "exists",
        "exists — Existence Check",
        "Returns true if the subquery returns any rows.",
        Some("exists(subquery_pipeline)"),
    ),
    (
        "distinct",
        "distinct — Remove Duplicates",
        "Eliminates duplicate values in aggregate functions.",
        Some("count(distinct column)"),
    ),
];

#[cfg(test)]
mod tests {
    use super::*;
    use crate::schema::{SchemaCache, TableInfo};
    use crate::test_support::make_col;

    fn sample_schema() -> SchemaCache {
        SchemaCache::from_tables(vec![TableInfo {
            name: "users".to_string(),
            schema: "public".to_string(),
            columns: vec![
                make_col("id", "uuid", false, true),
                make_col("name", "text", false, false),
                make_col("email", "text", true, false),
            ],
        }])
    }

    // ── get_hover (keyword only) ──

    #[test]
    fn test_hover_select() {
        let info = get_hover("select").unwrap();
        assert!(info.title.contains("select"));
        assert!(info.signature.is_some());
    }

    #[test]
    fn test_hover_case_insensitive() {
        let info = get_hover("SELECT").unwrap();
        assert!(info.title.contains("select"));
    }

    #[test]
    fn test_hover_filter() {
        let info = get_hover("filter").unwrap();
        assert!(info.title.contains("filter"));
        assert!(info.detail.contains("lambda"));
    }

    #[test]
    fn test_hover_unknown_word() {
        assert!(get_hover("zzz_not_a_keyword").is_none());
    }

    #[test]
    fn test_hover_all_entries_have_title() {
        let keywords = [
            "select",
            "filter",
            "join",
            "left_join",
            "group_by",
            "order_by",
            "having",
            "limit",
            "offset",
            "union",
            "insert",
            "count",
            "sum",
            "avg",
            "max",
            "min",
            "concat",
            "substring",
            "length",
            "trim",
            "upper",
            "lower",
            "round",
            "abs",
            "coalesce",
            "row_number",
            "rank",
            "dense_rank",
            "let",
            "fn",
            "case",
            "exists",
            "distinct",
        ];
        for kw in &keywords {
            let info = get_hover(kw);
            assert!(info.is_some(), "No hover for '{kw}'");
        }
    }

    // ── get_hover_with_schema ──

    #[test]
    fn test_hover_qualified_column() {
        let schema = sample_schema();
        let info = get_hover_with_schema("id", Some(("users", "id")), Some(&schema)).unwrap();
        assert!(info.title.contains("users.id"));
        assert!(info.detail.contains("uuid"));
        assert!(info.signature.is_some());
    }

    #[test]
    fn test_hover_qualified_column_not_found() {
        let schema = sample_schema();
        let info =
            get_hover_with_schema("nonexistent", Some(("users", "nonexistent")), Some(&schema))
                .unwrap();
        assert!(info.title.contains("not found"));
        assert!(info.detail.contains("Available columns"));
    }

    #[test]
    fn test_hover_table_name() {
        let schema = sample_schema();
        let info = get_hover_with_schema("users", None, Some(&schema)).unwrap();
        assert!(info.title.contains("users"));
        assert!(info.title.contains("Table"));
        assert!(info.detail.contains("id"));
        assert!(info.signature.is_some());
    }

    #[test]
    fn test_hover_fallback_to_keyword() {
        let schema = sample_schema();
        let info = get_hover_with_schema("select", None, Some(&schema)).unwrap();
        assert!(info.title.contains("select"));
    }

    #[test]
    fn test_hover_no_schema_keyword() {
        let info = get_hover_with_schema("filter", None, None).unwrap();
        assert!(info.title.contains("filter"));
    }

    #[test]
    fn test_hover_no_schema_unknown() {
        assert!(get_hover_with_schema("zzz", None, None).is_none());
    }

    #[test]
    fn test_hover_qualified_table_not_in_schema() {
        let schema = sample_schema();
        // Table "orders" doesn't exist in schema, should fall back
        let info = get_hover_with_schema("id", Some(("orders", "id")), Some(&schema));
        // Falls back to keyword lookup for "id" which is not a keyword
        assert!(info.is_none());
    }

    #[test]
    fn test_hover_column_shows_nullable_info() {
        let schema = sample_schema();
        let info = get_hover_with_schema("email", Some(("users", "email")), Some(&schema)).unwrap();
        assert!(info.detail.contains("Nullable: yes"));
    }

    #[test]
    fn test_hover_column_shows_pk_info() {
        let schema = sample_schema();
        let info = get_hover_with_schema("id", Some(("users", "id")), Some(&schema)).unwrap();
        assert!(info.detail.contains("Primary Key: yes"));
    }
}
