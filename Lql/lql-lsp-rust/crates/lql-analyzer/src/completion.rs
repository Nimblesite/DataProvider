use crate::{schema::SchemaCache, scope::ScopeMap};

/// A completion item returned by the analyzer.
#[derive(Debug, Clone)]
pub struct CompletionItem {
    pub label: String,
    pub kind: CompletionKind,
    pub detail: String,
    pub documentation: String,
    pub insert_text: Option<String>,
    /// Sort priority (lower = shown first). Column=0, Pipeline=1, Function=2, Keyword=3, Table=4,
    /// Variable=5
    pub sort_priority: u8,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CompletionKind {
    Keyword,
    Function,
    Snippet,
    Variable,
    Table,
    Column,
}

/// Context about where the cursor is in the document.
#[derive(Debug, Clone)]
pub struct CompletionContext {
    /// Text of the line up to the cursor.
    pub line_prefix: String,
    /// Whether we're inside a function argument list.
    pub in_arg_list: bool,
    /// Whether we're after a pipe operator.
    pub after_pipe: bool,
    /// Whether we're inside a lambda body.
    pub in_lambda: bool,
    /// Current word prefix being typed.
    pub word_prefix: String,
    /// The table qualifier if user typed "table." (the part before the dot).
    pub table_qualifier: Option<String>,
}

/// Provide context-aware completions based on cursor position and scope.
/// Falls back gracefully if schema is None (no DB connection).
pub fn get_completions(
    ctx: &CompletionContext,
    scope: &ScopeMap,
    schema: Option<&SchemaCache>,
) -> Vec<CompletionItem> {
    let mut items = Vec::new();
    let prefix = ctx.word_prefix.to_ascii_lowercase();

    // If user typed "table." — suggest columns for that table from schema
    if let Some(ref qualifier) = ctx.table_qualifier {
        if let Some(schema) = schema {
            let columns = schema.get_columns(qualifier);
            for col in columns {
                let col_lower = col.name.to_lowercase();
                if col_lower.starts_with(&prefix) {
                    items.push(CompletionItem {
                        label: col.name.clone(),
                        kind: CompletionKind::Column,
                        detail: col.type_description(),
                        documentation: format!(
                            "Column `{}.{}` — {}",
                            qualifier,
                            col.name,
                            col.type_description()
                        ),
                        insert_text: None,
                        sort_priority: 0,
                    });
                }
            }
        }
        // If we have a qualifier, column completions are primary
        if !items.is_empty() {
            return items;
        }
    }

    // After pipe operator: suggest pipeline operations
    if ctx.after_pipe {
        items.extend(pipeline_completions(&prefix));
    }

    // Inside lambda: suggest comparison operators and logical keywords
    if ctx.in_lambda {
        items.extend(lambda_completions(&prefix));
    }

    // Always available: keywords, built-in functions, scope bindings
    items.extend(keyword_completions(&prefix));
    items.extend(aggregate_completions(&prefix));
    items.extend(string_function_completions(&prefix));

    // Schema-based table completions (real tables from database)
    if let Some(schema) = schema {
        for table_name in schema.table_names() {
            if table_name.to_lowercase().starts_with(&prefix) {
                let table = schema.get_table(table_name).unwrap();
                let col_names: Vec<&str> = table.columns.iter().map(|c| c.name.as_str()).collect();
                let col_list = if col_names.len() <= 6 {
                    col_names.join(", ")
                } else {
                    format!(
                        "{}, ... ({} total)",
                        col_names[..5].join(", "),
                        col_names.len()
                    )
                };
                items.push(CompletionItem {
                    label: table_name.to_string(),
                    kind: CompletionKind::Table,
                    detail: format!("table ({} columns)", table.columns.len()),
                    documentation: format!("Table: {table_name}\nColumns: {col_list}"),
                    insert_text: None,
                    sort_priority: 4,
                });
            }
        }
    }

    // Scope-based completions
    for name in scope.binding_names() {
        if name.to_ascii_lowercase().starts_with(&prefix) {
            push_named_item(
                &mut items,
                name.to_string(),
                CompletionKind::Variable,
                "let binding",
                format!("Variable bound with `let {name} = ...`"),
                5,
            );
        }
    }

    for name in scope.table_names() {
        if name.to_ascii_lowercase().starts_with(&prefix) {
            // Skip if already provided by schema
            if schema.is_some_and(|s| s.get_table(name).is_some()) {
                continue;
            }
            push_named_item(
                &mut items,
                name.to_string(),
                CompletionKind::Table,
                "table",
                format!("Table: {name}"),
                4,
            );
        }
    }

    // Deduplicate by label (keep first occurrence — higher priority items are added first)
    let mut seen = std::collections::HashSet::new();
    items.retain(|item| seen.insert(item.label.clone()));

    // Sort: by priority (lower first), then alphabetically
    items.sort_by(|a, b| {
        a.sort_priority
            .cmp(&b.sort_priority)
            .then(a.label.cmp(&b.label))
    });

    items
}

fn pipeline_completions(prefix: &str) -> Vec<CompletionItem> {
    let ops = vec![
        (
            "select",
            "Project columns",
            "Projects specified columns from the input data",
            "select($0)",
        ),
        (
            "filter",
            "Filter rows",
            "Filters rows based on a lambda predicate",
            "filter(fn(row) => $0)",
        ),
        (
            "join",
            "Join tables",
            "Joins with another table on a condition",
            "join($1, on = $0)",
        ),
        (
            "left_join",
            "Left join tables",
            "Left joins with another table",
            "left_join($1, on = $0)",
        ),
        (
            "group_by",
            "Group rows",
            "Groups rows by specified columns",
            "group_by($0)",
        ),
        (
            "order_by",
            "Order results",
            "Orders rows by specified columns",
            "order_by($0)",
        ),
        (
            "having",
            "Filter groups",
            "Filters groups based on aggregate conditions",
            "having(fn(group) => $0)",
        ),
        (
            "limit",
            "Limit results",
            "Limits the number of returned rows",
            "limit($0)",
        ),
        (
            "offset",
            "Skip rows",
            "Skips the first n rows",
            "offset($0)",
        ),
        (
            "union",
            "Union queries",
            "Combines results of two queries",
            "union($0)",
        ),
        (
            "insert",
            "Insert into table",
            "Inserts results into a target table",
            "insert($0)",
        ),
    ];

    ops.into_iter()
        .filter(|(name, _, _, _)| name.starts_with(prefix))
        .map(|(name, detail, doc, snippet)| CompletionItem {
            label: name.into(),
            kind: CompletionKind::Function,
            detail: detail.into(),
            documentation: doc.into(),
            insert_text: Some(snippet.into()),
            sort_priority: 1,
        })
        .collect()
}

fn build_completions(
    entries: Vec<(&str, &str, &str)>,
    prefix: &str,
    kind: CompletionKind,
    with_snippet: bool,
    sort_priority: u8,
) -> Vec<CompletionItem> {
    entries
        .into_iter()
        .filter(|(name, _, _)| name.starts_with(prefix))
        .map(|(name, detail, doc)| CompletionItem {
            label: name.into(),
            kind,
            detail: detail.into(),
            documentation: doc.into(),
            insert_text: with_snippet.then(|| format!("{name}($0)")),
            sort_priority,
        })
        .collect()
}

/// Pushes a non-snippet completion item with the given priority.
fn push_named_item(
    items: &mut Vec<CompletionItem>,
    name: String,
    kind: CompletionKind,
    detail: &str,
    documentation: String,
    sort_priority: u8,
) {
    items.push(CompletionItem {
        label: name,
        kind,
        detail: detail.into(),
        documentation,
        insert_text: None,
        sort_priority,
    });
}

fn aggregate_completions(prefix: &str) -> Vec<CompletionItem> {
    let fns = vec![
        ("count", "Count rows", "count(*) or count(column)"),
        ("sum", "Sum values", "sum(column)"),
        ("avg", "Average values", "avg(column)"),
        ("max", "Maximum value", "max(column)"),
        ("min", "Minimum value", "min(column)"),
        ("row_number", "Row number", "row_number() over (...)"),
        ("rank", "Rank", "rank() over (...)"),
        ("dense_rank", "Dense rank", "dense_rank() over (...)"),
    ];

    build_completions(fns, prefix, CompletionKind::Function, true, 2)
}

fn string_function_completions(prefix: &str) -> Vec<CompletionItem> {
    let fns = vec![
        ("concat", "Concatenate strings", "concat(str1, str2, ...)"),
        (
            "substring",
            "Extract substring",
            "substring(string, start, length)",
        ),
        ("length", "String length", "length(string)"),
        ("trim", "Trim whitespace", "trim(string)"),
        ("upper", "Uppercase", "upper(string)"),
        ("lower", "Lowercase", "lower(string)"),
        ("round", "Round number", "round(number, precision)"),
        ("abs", "Absolute value", "abs(number)"),
        ("coalesce", "First non-null", "coalesce(val1, val2, ...)"),
    ];

    build_completions(fns, prefix, CompletionKind::Function, true, 2)
}

fn keyword_completions(prefix: &str) -> Vec<CompletionItem> {
    let kws = vec![
        (
            "let",
            "Variable binding",
            "Binds a pipeline result to a name",
        ),
        ("fn", "Lambda function", "fn(params) => expression"),
        ("case", "Case expression", "case when ... then ... end"),
        ("as", "Column alias", "expression as alias_name"),
        ("asc", "Ascending order", "Sort ascending"),
        ("desc", "Descending order", "Sort descending"),
        ("distinct", "Distinct modifier", "Remove duplicates"),
    ];

    build_completions(kws, prefix, CompletionKind::Keyword, false, 3)
}

fn lambda_completions(prefix: &str) -> Vec<CompletionItem> {
    let ops = vec![
        ("and", "Logical AND", "Combines conditions with AND"),
        ("or", "Logical OR", "Combines conditions with OR"),
        ("not", "Logical NOT", "Negates a condition"),
        ("is", "IS check", "IS NULL / IS NOT NULL"),
        ("in", "IN check", "Check membership in a set"),
        ("like", "LIKE pattern", "Pattern matching with wildcards"),
        ("exists", "EXISTS subquery", "Check subquery has results"),
    ];

    build_completions(ops, prefix, CompletionKind::Keyword, false, 3)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{
        schema::{ColumnInfo, SchemaCache, TableInfo},
        test_support::make_col,
    };

    fn empty_ctx() -> CompletionContext {
        CompletionContext {
            line_prefix: String::new(),
            in_arg_list: false,
            after_pipe: false,
            in_lambda: false,
            word_prefix: String::new(),
            table_qualifier: None,
        }
    }

    // Implements [LQL-COMPLETION-TEST-LABELS].
    fn completion_labels(items: Vec<CompletionItem>) -> Vec<String> {
        items.into_iter().map(|item| item.label).collect()
    }

    fn sample_schema() -> SchemaCache {
        independent_expanded_schema_data_1()
    }

    // Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
    fn labels_ctx(ctx: &CompletionContext) -> Vec<String> {
        completion_labels(get_completions(ctx, &ScopeMap::new(), None))
    }

    // ── basic completions (no prefix) ──

    #[test]
    fn test_empty_context_returns_keywords_and_functions() {
        let labels = labels_ctx(&empty_ctx());
        assert!(!labels.is_empty());
        crate::test_support::assert_labels(&labels, &["let", "count", "concat"], true);
    }

    #[test]
    fn test_after_pipe_includes_pipeline_ops() {
        let mut ctx = empty_ctx();
        ctx.after_pipe = true;
        crate::test_support::assert_labels(
            &labels_ctx(&ctx),
            &[
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
            ],
            true,
        );
    }

    #[test]
    fn test_in_lambda_includes_logical_ops() {
        let mut ctx = empty_ctx();
        ctx.in_lambda = true;
        crate::test_support::assert_labels(
            &labels_ctx(&ctx),
            &["and", "or", "not", "is", "in", "like", "exists"],
            true,
        );
    }

    #[test]
    fn test_not_in_lambda_excludes_logical_ops() {
        crate::test_support::assert_labels(&labels_ctx(&empty_ctx()), &["and", "or"], false);
    }

    // ── prefix filtering ──

    #[test]
    fn test_prefix_filters_items() {
        let mut ctx = empty_ctx();
        ctx.word_prefix = "sel".to_string();
        ctx.after_pipe = true;
        let items = get_completions(&ctx, &ScopeMap::new(), None);
        for item in &items {
            assert!(
                item.label.to_lowercase().starts_with("sel") || item.label.starts_with("sel"),
                "Item '{}' doesn't match prefix 'sel'",
                item.label
            );
        }
    }

    #[test]
    fn test_prefix_no_match_returns_empty_or_fewer() {
        let mut ctx = empty_ctx();
        ctx.word_prefix = "zzz_no_match".to_string();
        let items = get_completions(&ctx, &ScopeMap::new(), None);
        assert!(items.is_empty());
    }

    // ── schema-based completions ──

    #[test]
    fn test_table_qualifier_returns_columns() {
        let mut ctx = empty_ctx();
        ctx.table_qualifier = Some("users".to_string());
        let items = get_completions(&ctx, &ScopeMap::new(), Some(&sample_schema()));
        let labels = completion_labels(items);
        crate::test_support::assert_labels(&labels, &["id", "name", "email"], true);
        // Should ONLY return columns when qualifier matches
        crate::test_support::assert_labels(&labels, &["select"], false);
    }

    #[test]
    fn test_table_qualifier_with_prefix() {
        let mut ctx = empty_ctx();
        ctx.table_qualifier = Some("users".to_string());
        ctx.word_prefix = "na".to_string();
        let items = get_completions(&ctx, &ScopeMap::new(), Some(&sample_schema()));
        assert_eq!(items.len(), 1);
        assert_eq!(items[0].label, "name");
        assert_eq!(items[0].kind, CompletionKind::Column);
    }

    #[test]
    fn test_table_qualifier_unknown_table() {
        let mut ctx = empty_ctx();
        ctx.table_qualifier = Some("nonexistent".to_string());
        let items = get_completions(&ctx, &ScopeMap::new(), Some(&sample_schema()));
        // No columns, falls through to other completions
        assert!(!items.is_empty());
        assert!(crate::test_support::completion_items_of_kind(&items, CompletionKind::Column)
            .is_empty());
    }

    #[test]
    fn test_schema_table_completions() {
        let items = get_completions(&empty_ctx(), &ScopeMap::new(), Some(&sample_schema()));
        let table_items =
            crate::test_support::completion_items_of_kind(&items, CompletionKind::Table);
        assert!(table_items.len() >= 2);
        let labels: Vec<&str> = table_items.iter().map(|i| i.label.as_str()).collect();
        crate::test_support::assert_labels(&labels, &["users", "orders"], true);
    }

    #[test]
    fn test_schema_table_completion_shows_column_count() {
        let items = get_completions(&empty_ctx(), &ScopeMap::new(), Some(&sample_schema()));
        let users_item = crate::test_support::completion_by_label(&items, "users");
        assert!(users_item.detail.contains("3 columns"));
    }

    // ── scope-based completions ──

    #[test]
    fn test_scope_binding_completion() {
        let ctx = empty_ctx();
        let mut scope = ScopeMap::new();
        scope.add_binding("my_query".to_string(), 0, 0);
        let labels = completion_labels(get_completions(&ctx, &scope, None));
        assert!(labels.contains(&"my_query".to_string()));
    }

    #[test]
    fn test_scope_table_completion() {
        let ctx = empty_ctx();
        let mut scope = ScopeMap::new();
        scope.add_table("my_table".to_string());
        let labels = completion_labels(get_completions(&ctx, &scope, None));
        assert!(labels.contains(&"my_table".to_string()));
    }

    #[test]
    fn test_scope_table_skipped_if_in_schema() {
        let ctx = empty_ctx();
        let schema = sample_schema();
        let mut scope = ScopeMap::new();
        scope.add_table("users".to_string());
        let items = get_completions(&ctx, &scope, Some(&schema));
        let user_items: Vec<_> = items.iter().filter(|i| i.label == "users").collect();
        // Only one "users" entry (from schema), not duplicated by scope
        assert_eq!(user_items.len(), 1);
    }

    // ── sort order ──

    #[test]
    fn test_completions_sorted_by_priority() {
        let mut ctx = empty_ctx();
        ctx.after_pipe = true;
        let items = get_completions(&ctx, &ScopeMap::new(), None);
        crate::test_support::assert_completions_sorted(&items);
    }

    // ── deduplication ──

    #[test]
    fn test_deduplication() {
        let mut ctx = empty_ctx();
        ctx.after_pipe = true;
        let labels = labels_ctx(&ctx);
        let unique: std::collections::HashSet<&String> = labels.iter().collect();
        assert_eq!(labels.len(), unique.len(), "Duplicate completions found");
    }

    // ── pipeline completions have snippets ──

    #[test]
    fn test_pipeline_completions_have_insert_text() {
        let mut ctx = empty_ctx();
        ctx.after_pipe = true;
        let items = get_completions(&ctx, &ScopeMap::new(), None);
        let select_item = crate::test_support::completion_by_label(&items, "select");
        assert!(select_item.insert_text.is_some());
        assert!(select_item.insert_text.as_ref().unwrap().contains("$0"));
    }

    // ── aggregate completions ──

    #[test]
    fn test_aggregate_completions() {
        let mut ctx = empty_ctx();
        ctx.word_prefix = "co".to_string();
        crate::test_support::assert_labels(&labels_ctx(&ctx), &["count", "coalesce", "concat"], true);
    }

    // ── many columns ──

    #[test]
    fn test_table_with_many_columns_truncated_docs() {
        let cols: Vec<ColumnInfo> = (0..10)
            .map(|i| make_col(&format!("col_{i}"), "text", true, false))
            .collect();
        let schema = SchemaCache::from_tables(vec![TableInfo {
            name: "big_table".to_string(),
            schema: "public".to_string(),
            columns: cols,
        }]);
        let items = get_completions(&empty_ctx(), &ScopeMap::new(), Some(&schema));
        let table_item = crate::test_support::completion_by_label(&items, "big_table");
        assert!(table_item.documentation.contains("10 total"));
    }

    // ── column completion kind ──

    #[test]
    fn test_column_completion_kind_and_priority() {
        let mut ctx = empty_ctx();
        ctx.table_qualifier = Some("users".to_string());
        let items = get_completions(&ctx, &ScopeMap::new(), Some(&sample_schema()));
        for item in &items {
            assert_eq!(item.kind, CompletionKind::Column);
            assert_eq!(item.sort_priority, 0);
        }
    }

    include!("completion_expanded_schema_data_0.fixtures.rs");
}
