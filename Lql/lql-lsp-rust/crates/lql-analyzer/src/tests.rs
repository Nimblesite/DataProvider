#[cfg(test)]
mod diagnostics_tests {
    use crate::diagnostics::{analyze, Diagnostic, DiagnosticSeverity};
    use crate::scope::ScopeMap;

    fn empty_scope() -> ScopeMap {
        ScopeMap::new()
    }

    fn diags_matching<'a>(diags: &'a [Diagnostic], needle: &str) -> Vec<&'a Diagnostic> {
        diags
            .iter()
            .filter(|d| d.message.contains(needle))
            .collect()
    }

    // ── analyze: empty / comments ──────────────────────────────────────
    #[test]
    fn empty_source_produces_no_diagnostics() {
        let diags = analyze("", &empty_scope());
        assert!(diags.is_empty());
    }

    #[test]
    fn comment_only_produces_no_diagnostics() {
        let diags = analyze("-- this is a comment", &empty_scope());
        assert!(diags.is_empty());
    }

    #[test]
    fn blank_lines_ignored() {
        let diags = analyze("\n\n\n", &empty_scope());
        assert!(diags.is_empty());
    }

    // ── pipe spacing ───────────────────────────────────────────────────
    #[test]
    fn correct_pipe_spacing_no_warning() {
        let diags = analyze("users |> select(users.id)", &empty_scope());
        let pipe_diags = diags_matching(&diags, "Pipeline");
        assert!(pipe_diags.is_empty());
    }

    #[test]
    fn missing_space_before_pipe() {
        let diags = analyze("users|> select(users.id)", &empty_scope());
        let pipe_diags = diags_matching(&diags, "preceded by a space");
        assert_eq!(pipe_diags.len(), 1);
        assert_eq!(pipe_diags[0].severity, DiagnosticSeverity::Warning);
        assert_eq!(pipe_diags[0].line, 0);
        assert_eq!(pipe_diags[0].col, 5);
    }

    #[test]
    fn missing_space_after_pipe() {
        let diags = analyze("users |>select(users.id)", &empty_scope());
        let pipe_diags = diags_matching(&diags, "followed by a space");
        assert_eq!(pipe_diags.len(), 1);
        assert_eq!(pipe_diags[0].severity, DiagnosticSeverity::Warning);
    }

    #[test]
    fn missing_space_both_sides() {
        let diags = analyze("users|>select(users.id)", &empty_scope());
        let pipe_diags = diags_matching(&diags, "Pipeline");
        assert_eq!(pipe_diags.len(), 2);
    }

    #[test]
    fn tab_before_pipe_is_ok() {
        let diags = analyze("users\t|> select(users.id)", &empty_scope());
        let pipe_diags = diags_matching(&diags, "preceded");
        assert!(pipe_diags.is_empty());
    }

    #[test]
    fn tab_after_pipe_is_ok() {
        let diags = analyze("users |>\tselect(users.id)", &empty_scope());
        let pipe_diags = diags_matching(&diags, "followed");
        assert!(pipe_diags.is_empty());
    }

    #[test]
    fn pipe_at_end_of_line_no_after_warning() {
        let diags = analyze("users |>", &empty_scope());
        let after = diags_matching(&diags, "followed");
        assert!(after.is_empty());
    }

    #[test]
    fn pipe_at_start_of_line_no_before_warning() {
        let diags = analyze("|> select(users.id)", &empty_scope());
        let before = diags_matching(&diags, "preceded");
        assert!(before.is_empty());
    }

    #[test]
    fn multiple_pipes_on_one_line() {
        let diags = analyze(
            "a |> filter(fn(r) => r.a.x > 1) |> select(a.id)",
            &empty_scope(),
        );
        let pipe_diags = diags_matching(&diags, "Pipeline");
        assert!(pipe_diags.is_empty());
    }

    #[test]
    fn multiline_pipe_spacing() {
        let source = "users\n|> filter(fn(r) => r.users.age > 18)\n|> select(users.name)";
        let diags = analyze(source, &empty_scope());
        let pipe_diags = diags_matching(&diags, "Pipeline");
        assert!(pipe_diags.is_empty());
    }

    // ── unknown functions ──────────────────────────────────────────────
    #[test]
    fn known_function_no_diagnostic() {
        let diags = analyze("users |> select(users.id)", &empty_scope());
        let unknown = diags_matching(&diags, "Unknown");
        assert!(unknown.is_empty());
    }

    #[test]
    fn unknown_function_reported() {
        let diags = analyze("users |> foobar(users.id)", &empty_scope());
        let unknown = diags_matching(&diags, "Unknown");
        assert_eq!(unknown.len(), 1);
        assert!(unknown[0].message.contains("foobar"));
        assert_eq!(unknown[0].severity, DiagnosticSeverity::Info);
    }

    #[test]
    fn function_in_scope_not_reported() {
        let mut scope = ScopeMap::new();
        scope.add_binding("my_func".to_string(), 0, 0);
        let diags = analyze("x |> my_func(x.id)", &scope);
        let unknown = diags_matching(&diags, "Unknown");
        assert!(unknown.is_empty());
    }

    #[test]
    fn keyword_not_reported_as_unknown() {
        // "fn" after pipe should not be flagged
        let diags = analyze("users |> filter(fn(r) => r.users.id > 0)", &empty_scope());
        let unknown = diags_matching(&diags, "Unknown function: `fn`");
        assert!(unknown.is_empty());
    }

    #[test]
    fn all_known_functions_recognized() {
        let known = [
            "select",
            "filter",
            "join",
            "left_join",
            "right_join",
            "cross_join",
            "group_by",
            "order_by",
            "having",
            "limit",
            "offset",
            "union",
            "union_all",
            "insert",
            "update",
            "delete",
            "count",
            "sum",
            "avg",
            "min",
            "max",
            "first",
            "last",
            "concat",
            "substring",
            "length",
            "trim",
            "upper",
            "lower",
            "replace",
            "round",
            "floor",
            "ceil",
            "abs",
            "sqrt",
            "power",
            "mod",
            "now",
            "today",
            "year",
            "month",
            "day",
            "hour",
            "minute",
            "second",
            "coalesce",
            "nullif",
            "isnull",
            "isnotnull",
            "row_number",
            "rank",
            "dense_rank",
            "ntile",
            "lag",
            "lead",
            "extract",
            "date_trunc",
            "current_date",
        ];
        for func in known {
            let source = format!("x |> {func}(x.id)");
            let diags = analyze(&source, &empty_scope());
            let unknown = diags_matching(&diags, "Unknown");
            assert!(
                unknown.is_empty(),
                "Function `{func}` should be recognized but got: {:?}",
                unknown
            );
        }
    }

    #[test]
    fn case_insensitive_function_check() {
        let diags = analyze("x |> SELECT(x.id)", &empty_scope());
        let unknown = diags_matching(&diags, "Unknown");
        assert!(unknown.is_empty());
    }

    #[test]
    fn string_literal_not_checked_for_functions() {
        // 'blah(' inside a string shouldn't trigger unknown function
        let diags = analyze(
            "users |> filter(fn(r) => r.users.name = 'blah(')",
            &empty_scope(),
        );
        let unknown = diags_matching(&diags, "Unknown function: `blah`");
        assert!(unknown.is_empty());
    }

    #[test]
    fn escaped_quote_in_string() {
        let diags = analyze(
            r"users |> filter(fn(r) => r.users.name = 'it\'s(test')",
            &empty_scope(),
        );
        let unknown = diags_matching(&diags, "Unknown function: `s`");
        assert!(unknown.is_empty());
    }

    #[test]
    fn identifier_not_followed_by_paren_not_reported() {
        let diags = analyze("users |> select(users.foobar)", &empty_scope());
        let unknown = diags_matching(&diags, "Unknown");
        assert!(unknown.is_empty());
    }

    #[test]
    fn keyword_identifiers_not_flagged() {
        let keywords = [
            "let",
            "fn",
            "as",
            "asc",
            "desc",
            "and",
            "or",
            "not",
            "distinct",
            "exists",
            "null",
            "is",
            "in",
            "case",
            "when",
            "then",
            "else",
            "end",
            "with",
            "over",
            "partition",
            "order",
            "by",
            "on",
            "like",
            "from",
            "interval",
        ];
        for kw in keywords {
            let source = format!("x |> filter(fn(r) => {kw}(r.x.y))");
            let diags = analyze(&source, &empty_scope());
            let unknown = diags_matching(&diags, &format!("Unknown function: `{kw}`"));
            assert!(unknown.is_empty(), "Keyword `{kw}` flagged as unknown");
        }
    }

    #[test]
    fn function_with_whitespace_before_paren() {
        let diags = analyze("x |> foobar  (x.id)", &empty_scope());
        let unknown = diags_matching(&diags, "foobar");
        assert_eq!(unknown.len(), 1);
    }

    #[test]
    fn underscore_identifier_as_function() {
        let diags = analyze("x |> _my_func(x.id)", &empty_scope());
        let unknown = diags_matching(&diags, "_my_func");
        assert_eq!(unknown.len(), 1);
    }

    // NOTE: Bracket checking is handled by ANTLR parse errors (reported separately
    // via parse_lql().errors), not by the semantic analyzer. Tests for bracket
    // matching are in lql-parser's error recovery tests.

    // ── comprehensive scenario ─────────────────────────────────────────
    #[test]
    fn full_pipeline_no_spurious_diagnostics() {
        let source = r#"let active_users =
    users
    |> filter(fn(row) => row.users.active = true)
    |> join(orders, on = users.id = orders.user_id)
    |> group_by(users.id)
    |> select(users.id, count(*) as order_count)
    |> having(fn(g) => count(*) > 5)
    |> order_by(order_count desc)
    |> limit(100)"#;
        let diags = analyze(source, &empty_scope());
        let errors: Vec<_> = diags
            .iter()
            .filter(|d| d.severity == DiagnosticSeverity::Error)
            .collect();
        assert!(errors.is_empty(), "Unexpected errors: {:?}", errors);
    }

    #[test]
    fn diagnostics_report_correct_line_numbers() {
        let source = "users |> select(users.id)\norders|>filter(fn(r) => r.orders.x > 0)";
        let diags = analyze(source, &empty_scope());
        let pipe_diags = diags_matching(&diags, "preceded");
        assert_eq!(pipe_diags.len(), 1);
        assert_eq!(pipe_diags[0].line, 1);
    }
}

#[cfg(test)]
mod completion_tests {
    use crate::completion::{get_completions, CompletionContext, CompletionKind};
    use crate::schema::{ColumnInfo, SchemaCache, TableInfo};
    use crate::scope::ScopeMap;

    fn ctx(prefix: &str, after_pipe: bool, in_lambda: bool) -> CompletionContext {
        CompletionContext {
            line_prefix: prefix.to_string(),
            in_arg_list: false,
            after_pipe,
            in_lambda,
            word_prefix: prefix.to_string(),
            table_qualifier: None,
        }
    }

    fn test_schema() -> SchemaCache {
        SchemaCache::from_tables(vec![
            TableInfo {
                name: "users".to_string(),
                schema: "public".to_string(),
                columns: vec![
                    ColumnInfo {
                        name: "id".to_string(),
                        sql_type: "uuid".to_string(),
                        is_nullable: false,
                        is_primary_key: true,
                    },
                    ColumnInfo {
                        name: "name".to_string(),
                        sql_type: "text".to_string(),
                        is_nullable: false,
                        is_primary_key: false,
                    },
                    ColumnInfo {
                        name: "email".to_string(),
                        sql_type: "text".to_string(),
                        is_nullable: true,
                        is_primary_key: false,
                    },
                ],
            },
            TableInfo {
                name: "orders".to_string(),
                schema: "public".to_string(),
                columns: (0..8)
                    .map(|i| ColumnInfo {
                        name: format!("col{i}"),
                        sql_type: "integer".to_string(),
                        is_nullable: false,
                        is_primary_key: i == 0,
                    })
                    .collect(),
            },
        ])
    }

    // ── after pipe completions ─────────────────────────────────────────
    #[test]
    fn after_pipe_returns_pipeline_ops() {
        let items = get_completions(&ctx("", true, false), &ScopeMap::new(), None);
        let labels: Vec<&str> = items.iter().map(|i| i.label.as_str()).collect();
        assert!(labels.contains(&"select"));
        assert!(labels.contains(&"filter"));
        assert!(labels.contains(&"join"));
        assert!(labels.contains(&"group_by"));
        assert!(labels.contains(&"order_by"));
        assert!(labels.contains(&"limit"));
        assert!(labels.contains(&"offset"));
        assert!(labels.contains(&"having"));
        assert!(labels.contains(&"union"));
        assert!(labels.contains(&"insert"));
        assert!(labels.contains(&"left_join"));
    }

    #[test]
    fn after_pipe_with_prefix_filters() {
        let items = get_completions(&ctx("se", true, false), &ScopeMap::new(), None);
        let labels: Vec<&str> = items.iter().map(|i| i.label.as_str()).collect();
        assert!(labels.contains(&"select"));
        assert!(!labels.contains(&"filter"));
    }

    #[test]
    fn pipeline_ops_have_snippets() {
        let items = get_completions(&ctx("", true, false), &ScopeMap::new(), None);
        let select = items.iter().find(|i| i.label == "select").unwrap();
        assert!(select.insert_text.is_some());
        assert_eq!(select.kind, CompletionKind::Function);
        assert_eq!(select.sort_priority, 1);
    }

    // ── lambda completions ─────────────────────────────────────────────
    #[test]
    fn in_lambda_returns_logical_ops() {
        let items = get_completions(&ctx("", false, true), &ScopeMap::new(), None);
        let labels: Vec<&str> = items.iter().map(|i| i.label.as_str()).collect();
        assert!(labels.contains(&"and"));
        assert!(labels.contains(&"or"));
        assert!(labels.contains(&"not"));
        assert!(labels.contains(&"is"));
        assert!(labels.contains(&"in"));
        assert!(labels.contains(&"like"));
        assert!(labels.contains(&"exists"));
    }

    #[test]
    fn lambda_completions_filtered_by_prefix() {
        let items = get_completions(&ctx("an", false, true), &ScopeMap::new(), None);
        let labels: Vec<&str> = items.iter().map(|i| i.label.as_str()).collect();
        assert!(labels.contains(&"and"));
        assert!(!labels.contains(&"or"));
    }

    // ── keyword completions ────────────────────────────────────────────
    #[test]
    fn keyword_completions_returned() {
        let items = get_completions(&ctx("", false, false), &ScopeMap::new(), None);
        let labels: Vec<&str> = items.iter().map(|i| i.label.as_str()).collect();
        assert!(labels.contains(&"let"));
        assert!(labels.contains(&"fn"));
        assert!(labels.contains(&"case"));
        assert!(labels.contains(&"as"));
        assert!(labels.contains(&"asc"));
        assert!(labels.contains(&"desc"));
        assert!(labels.contains(&"distinct"));
    }

    #[test]
    fn keyword_completions_are_keyword_kind() {
        let items = get_completions(&ctx("le", false, false), &ScopeMap::new(), None);
        let let_item = items.iter().find(|i| i.label == "let").unwrap();
        assert_eq!(let_item.kind, CompletionKind::Keyword);
        assert!(let_item.insert_text.is_none());
        assert_eq!(let_item.sort_priority, 3);
    }

    // ── aggregate completions ──────────────────────────────────────────
    #[test]
    fn aggregate_completions_returned() {
        let items = get_completions(&ctx("", false, false), &ScopeMap::new(), None);
        let labels: Vec<&str> = items.iter().map(|i| i.label.as_str()).collect();
        assert!(labels.contains(&"count"));
        assert!(labels.contains(&"sum"));
        assert!(labels.contains(&"avg"));
        assert!(labels.contains(&"max"));
        assert!(labels.contains(&"min"));
        assert!(labels.contains(&"row_number"));
        assert!(labels.contains(&"rank"));
        assert!(labels.contains(&"dense_rank"));
    }

    #[test]
    fn aggregate_completions_have_snippets() {
        let items = get_completions(&ctx("cou", false, false), &ScopeMap::new(), None);
        let count = items.iter().find(|i| i.label == "count").unwrap();
        assert_eq!(count.insert_text.as_deref(), Some("count($0)"));
        assert_eq!(count.sort_priority, 2);
    }

    // ── string function completions ────────────────────────────────────
    #[test]
    fn string_functions_returned() {
        let items = get_completions(&ctx("", false, false), &ScopeMap::new(), None);
        let labels: Vec<&str> = items.iter().map(|i| i.label.as_str()).collect();
        assert!(labels.contains(&"concat"));
        assert!(labels.contains(&"substring"));
        assert!(labels.contains(&"length"));
        assert!(labels.contains(&"trim"));
        assert!(labels.contains(&"upper"));
        assert!(labels.contains(&"lower"));
        assert!(labels.contains(&"round"));
        assert!(labels.contains(&"abs"));
        assert!(labels.contains(&"coalesce"));
    }

    // ── schema-based completions ───────────────────────────────────────
    #[test]
    fn table_completions_from_schema() {
        let schema = test_schema();
        let items = get_completions(&ctx("us", false, false), &ScopeMap::new(), Some(&schema));
        let table_items: Vec<_> = items
            .iter()
            .filter(|i| i.kind == CompletionKind::Table)
            .collect();
        assert_eq!(table_items.len(), 1);
        assert_eq!(table_items[0].label, "users");
        assert!(table_items[0].detail.contains("3 columns"));
    }

    #[test]
    fn table_with_many_columns_truncates_doc() {
        let schema = test_schema();
        let items = get_completions(&ctx("or", false, false), &ScopeMap::new(), Some(&schema));
        let orders = items.iter().find(|i| i.label == "orders").unwrap();
        assert!(orders.documentation.contains("8 total"));
    }

    #[test]
    fn column_completions_with_table_qualifier() {
        let schema = test_schema();
        let mut c = ctx("", false, false);
        c.table_qualifier = Some("users".to_string());
        c.word_prefix = "".to_string();
        let items = get_completions(&c, &ScopeMap::new(), Some(&schema));
        let labels: Vec<&str> = items.iter().map(|i| i.label.as_str()).collect();
        assert!(labels.contains(&"id"));
        assert!(labels.contains(&"name"));
        assert!(labels.contains(&"email"));
        assert_eq!(items.len(), 3);
        // All should be Column kind
        for item in &items {
            assert_eq!(item.kind, CompletionKind::Column);
            assert_eq!(item.sort_priority, 0);
        }
    }

    #[test]
    fn column_completions_filtered_by_prefix() {
        let schema = test_schema();
        let mut c = ctx("", false, false);
        c.table_qualifier = Some("users".to_string());
        c.word_prefix = "n".to_string();
        let items = get_completions(&c, &ScopeMap::new(), Some(&schema));
        assert_eq!(items.len(), 1);
        assert_eq!(items[0].label, "name");
    }

    #[test]
    fn column_completions_for_unknown_table_empty() {
        let schema = test_schema();
        let mut c = ctx("", false, false);
        c.table_qualifier = Some("nonexistent".to_string());
        let items = get_completions(&c, &ScopeMap::new(), Some(&schema));
        // Falls through to other completions since column list is empty
        assert!(!items.is_empty());
    }

    #[test]
    fn no_schema_returns_no_table_completions() {
        let items = get_completions(&ctx("us", false, false), &ScopeMap::new(), None);
        let table_items: Vec<_> = items
            .iter()
            .filter(|i| i.kind == CompletionKind::Table)
            .collect();
        assert!(table_items.is_empty());
    }

    // ── scope-based completions ────────────────────────────────────────
    #[test]
    fn scope_bindings_in_completions() {
        let mut scope = ScopeMap::new();
        scope.add_binding("my_var".to_string(), 0, 0);
        let items = get_completions(&ctx("my", false, false), &scope, None);
        let var_items: Vec<_> = items
            .iter()
            .filter(|i| i.kind == CompletionKind::Variable)
            .collect();
        assert_eq!(var_items.len(), 1);
        assert_eq!(var_items[0].label, "my_var");
        assert_eq!(var_items[0].sort_priority, 5);
    }

    #[test]
    fn scope_tables_in_completions() {
        let mut scope = ScopeMap::new();
        scope.add_table("products".to_string());
        let items = get_completions(&ctx("pro", false, false), &scope, None);
        let table_items: Vec<_> = items
            .iter()
            .filter(|i| i.kind == CompletionKind::Table)
            .collect();
        assert_eq!(table_items.len(), 1);
        assert_eq!(table_items[0].label, "products");
    }

    #[test]
    fn scope_table_skipped_when_in_schema() {
        let schema = test_schema();
        let mut scope = ScopeMap::new();
        scope.add_table("users".to_string());
        let items = get_completions(&ctx("us", false, false), &scope, Some(&schema));
        let table_items: Vec<_> = items
            .iter()
            .filter(|i| i.kind == CompletionKind::Table && i.label == "users")
            .collect();
        // Should only appear once (from schema, not scope)
        assert_eq!(table_items.len(), 1);
    }

    // ── deduplication and sorting ──────────────────────────────────────
    #[test]
    fn completions_are_deduplicated() {
        // Both pipeline_completions and keyword_completions may return items
        // Ensure no duplicates
        let items = get_completions(&ctx("", true, true), &ScopeMap::new(), None);
        let mut labels: Vec<&str> = items.iter().map(|i| i.label.as_str()).collect();
        let original_len = labels.len();
        labels.sort();
        labels.dedup();
        assert_eq!(labels.len(), original_len);
    }

    #[test]
    fn completions_sorted_by_priority_then_alpha() {
        let items = get_completions(&ctx("", true, false), &ScopeMap::new(), None);
        for window in items.windows(2) {
            assert!(
                window[0].sort_priority <= window[1].sort_priority
                    || (window[0].sort_priority == window[1].sort_priority
                        && window[0].label <= window[1].label),
                "Items not sorted: {} ({}) before {} ({})",
                window[0].label,
                window[0].sort_priority,
                window[1].label,
                window[1].sort_priority
            );
        }
    }
}

#[cfg(test)]
mod hover_tests {
    use crate::hover::{get_hover, get_hover_with_schema};
    use crate::schema::{ColumnInfo, SchemaCache, TableInfo};

    fn test_schema() -> SchemaCache {
        SchemaCache::from_tables(vec![TableInfo {
            name: "users".to_string(),
            schema: "public".to_string(),
            columns: vec![
                ColumnInfo {
                    name: "id".to_string(),
                    sql_type: "uuid".to_string(),
                    is_nullable: false,
                    is_primary_key: true,
                },
                ColumnInfo {
                    name: "name".to_string(),
                    sql_type: "text".to_string(),
                    is_nullable: true,
                    is_primary_key: false,
                },
            ],
        }])
    }

    // ── get_hover ──────────────────────────────────────────────────────
    #[test]
    fn hover_known_keyword() {
        let info = get_hover("select").unwrap();
        assert!(info.title.contains("select"));
        assert!(info.signature.is_some());
    }

    #[test]
    fn hover_case_insensitive() {
        let info = get_hover("SELECT").unwrap();
        assert!(info.title.contains("select"));
    }

    #[test]
    fn hover_unknown_word_returns_none() {
        assert!(get_hover("foobar").is_none());
    }

    #[test]
    fn hover_all_pipeline_ops() {
        for word in &[
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
        ] {
            assert!(
                get_hover(word).is_some(),
                "No hover for pipeline op: {word}"
            );
        }
    }

    #[test]
    fn hover_all_aggregates() {
        for word in &["count", "sum", "avg", "max", "min"] {
            let info = get_hover(word).unwrap();
            assert!(info.signature.is_some(), "No signature for: {word}");
        }
    }

    #[test]
    fn hover_all_string_functions() {
        for word in &[
            "concat",
            "substring",
            "length",
            "trim",
            "upper",
            "lower",
            "round",
            "abs",
            "coalesce",
        ] {
            assert!(get_hover(word).is_some(), "No hover for: {word}");
        }
    }

    #[test]
    fn hover_window_functions() {
        for word in &["row_number", "rank", "dense_rank"] {
            assert!(get_hover(word).is_some(), "No hover for: {word}");
        }
    }

    #[test]
    fn hover_keywords() {
        for word in &["let", "fn", "case", "exists", "distinct"] {
            assert!(get_hover(word).is_some(), "No hover for: {word}");
        }
    }

    // ── get_hover_with_schema ──────────────────────────────────────────
    #[test]
    fn hover_table_name_with_schema() {
        let schema = test_schema();
        let info = get_hover_with_schema("users", None, Some(&schema)).unwrap();
        assert!(info.title.contains("users"));
        assert!(info.title.contains("Table"));
        assert!(info.detail.contains("id"));
        assert!(info.detail.contains("name"));
        assert!(info.signature.is_some());
    }

    #[test]
    fn hover_qualified_column() {
        let schema = test_schema();
        let info = get_hover_with_schema("id", Some(("users", "id")), Some(&schema)).unwrap();
        assert!(info.title.contains("users.id"));
        assert!(info.title.contains("uuid"));
        assert!(info.detail.contains("Primary Key: yes"));
        assert!(info.detail.contains("Nullable: no"));
        assert!(info.signature.is_some());
    }

    #[test]
    fn hover_qualified_nullable_column() {
        let schema = test_schema();
        let info = get_hover_with_schema("name", Some(("users", "name")), Some(&schema)).unwrap();
        assert!(info.detail.contains("Nullable: yes"));
        assert!(info.detail.contains("Primary Key: no"));
    }

    #[test]
    fn hover_qualified_unknown_column() {
        let schema = test_schema();
        let info =
            get_hover_with_schema("missing", Some(("users", "missing")), Some(&schema)).unwrap();
        assert!(info.title.contains("Column not found"));
        assert!(info.detail.contains("id"));
        assert!(info.detail.contains("name"));
    }

    #[test]
    fn hover_fallback_to_keyword_with_schema() {
        let schema = test_schema();
        let info = get_hover_with_schema("filter", None, Some(&schema)).unwrap();
        assert!(info.title.contains("filter"));
    }

    #[test]
    fn hover_unknown_with_schema_returns_none() {
        let schema = test_schema();
        assert!(get_hover_with_schema("xyzzy", None, Some(&schema)).is_none());
    }

    #[test]
    fn hover_no_schema_falls_back_to_keyword() {
        let info = get_hover_with_schema("select", None, None).unwrap();
        assert!(info.title.contains("select"));
    }

    #[test]
    fn hover_no_schema_unknown_returns_none() {
        assert!(get_hover_with_schema("xyzzy", None, None).is_none());
    }

    #[test]
    fn hover_qualified_unknown_table() {
        let schema = test_schema();
        // Unknown table with qualified lookup — falls through to keyword lookup
        let info = get_hover_with_schema("select", Some(("nonexistent", "col")), Some(&schema));
        assert!(info.is_some()); // falls back to keyword "select"
    }
}

#[cfg(test)]
mod schema_tests {
    use crate::schema::{ColumnInfo, SchemaCache, TableInfo};

    fn test_schema() -> SchemaCache {
        SchemaCache::from_tables(vec![
            TableInfo {
                name: "Users".to_string(),
                schema: "public".to_string(),
                columns: vec![
                    ColumnInfo {
                        name: "id".to_string(),
                        sql_type: "uuid".to_string(),
                        is_nullable: false,
                        is_primary_key: true,
                    },
                    ColumnInfo {
                        name: "Name".to_string(),
                        sql_type: "text".to_string(),
                        is_nullable: true,
                        is_primary_key: false,
                    },
                ],
            },
            TableInfo {
                name: "Orders".to_string(),
                schema: "sales".to_string(),
                columns: vec![ColumnInfo {
                    name: "total".to_string(),
                    sql_type: "numeric".to_string(),
                    is_nullable: false,
                    is_primary_key: false,
                }],
            },
        ])
    }

    #[test]
    fn default_cache_is_empty() {
        let cache = SchemaCache::default();
        assert!(cache.is_empty());
        assert_eq!(cache.table_count(), 0);
        assert!(cache.table_names().is_empty());
        assert!(cache.age().is_none());
    }

    #[test]
    fn from_tables_populates_cache() {
        let cache = test_schema();
        assert!(!cache.is_empty());
        assert_eq!(cache.table_count(), 2);
    }

    #[test]
    fn table_names_returns_original_case() {
        let cache = test_schema();
        let names = cache.table_names();
        assert!(names.contains(&"Users"));
        assert!(names.contains(&"Orders"));
    }

    #[test]
    fn get_table_case_insensitive() {
        let cache = test_schema();
        assert!(cache.get_table("users").is_some());
        assert!(cache.get_table("USERS").is_some());
        assert!(cache.get_table("Users").is_some());
        assert!(cache.get_table("nonexistent").is_none());
    }

    #[test]
    fn get_columns_returns_all() {
        let cache = test_schema();
        let cols = cache.get_columns("users");
        assert_eq!(cols.len(), 2);
    }

    #[test]
    fn get_columns_unknown_table_empty() {
        let cache = test_schema();
        let cols = cache.get_columns("nonexistent");
        assert!(cols.is_empty());
    }

    #[test]
    fn age_populated_after_creation() {
        let cache = test_schema();
        assert!(cache.age().is_some());
    }

    #[test]
    fn is_stale_true_for_default() {
        let cache = SchemaCache::default();
        assert!(cache.is_stale(std::time::Duration::from_secs(60)));
    }

    #[test]
    fn is_stale_false_for_fresh() {
        let cache = test_schema();
        assert!(!cache.is_stale(std::time::Duration::from_secs(60)));
    }

    // ── TableInfo ──────────────────────────────────────────────────────
    #[test]
    fn get_column_case_insensitive() {
        let cache = test_schema();
        let table = cache.get_table("users").unwrap();
        assert!(table.get_column("Name").is_some());
        assert!(table.get_column("name").is_some());
        assert!(table.get_column("NAME").is_some());
        assert!(table.get_column("nonexistent").is_none());
    }

    #[test]
    fn primary_key_columns() {
        let cache = test_schema();
        let table = cache.get_table("users").unwrap();
        let pks = table.primary_key_columns();
        assert_eq!(pks.len(), 1);
        assert_eq!(pks[0].name, "id");
    }

    #[test]
    fn primary_key_columns_empty_when_none() {
        let cache = test_schema();
        let table = cache.get_table("orders").unwrap();
        let pks = table.primary_key_columns();
        assert!(pks.is_empty());
    }

    // ── ColumnInfo ─────────────────────────────────────────────────────
    #[test]
    fn type_description_pk_not_null() {
        let col = ColumnInfo {
            name: "id".to_string(),
            sql_type: "uuid".to_string(),
            is_nullable: false,
            is_primary_key: true,
        };
        let desc = col.type_description();
        assert!(desc.contains("uuid"));
        assert!(desc.contains("PK"));
        assert!(desc.contains("NOT NULL"));
    }

    #[test]
    fn type_description_nullable_no_pk() {
        let col = ColumnInfo {
            name: "name".to_string(),
            sql_type: "text".to_string(),
            is_nullable: true,
            is_primary_key: false,
        };
        let desc = col.type_description();
        assert_eq!(desc, "text");
        assert!(!desc.contains("PK"));
        assert!(!desc.contains("NOT NULL"));
    }

    #[test]
    fn type_description_not_null_no_pk() {
        let col = ColumnInfo {
            name: "total".to_string(),
            sql_type: "numeric".to_string(),
            is_nullable: false,
            is_primary_key: false,
        };
        let desc = col.type_description();
        assert_eq!(desc, "numeric NOT NULL");
    }

    #[test]
    fn type_description_nullable_pk() {
        let col = ColumnInfo {
            name: "weird".to_string(),
            sql_type: "integer".to_string(),
            is_nullable: true,
            is_primary_key: true,
        };
        let desc = col.type_description();
        assert_eq!(desc, "integer (PK)");
    }
}

#[cfg(test)]
mod scope_tests {
    use crate::scope::ScopeMap;

    #[test]
    fn new_scope_is_empty() {
        let scope = ScopeMap::new();
        assert!(scope.binding_names().is_empty());
        assert!(scope.table_names().is_empty());
    }

    #[test]
    fn add_and_retrieve_binding() {
        let mut scope = ScopeMap::new();
        scope.add_binding("x".to_string(), 5, 10);
        assert!(scope.has_binding("x"));
        assert!(!scope.has_binding("y"));
        let info = scope.get_binding("x").unwrap();
        assert_eq!(info.name, "x");
        assert_eq!(info.line, 5);
        assert_eq!(info.col, 10);
    }

    #[test]
    fn binding_names_returns_all() {
        let mut scope = ScopeMap::new();
        scope.add_binding("a".to_string(), 0, 0);
        scope.add_binding("b".to_string(), 1, 0);
        let names = scope.binding_names();
        assert_eq!(names.len(), 2);
        assert!(names.contains(&"a"));
        assert!(names.contains(&"b"));
    }

    #[test]
    fn overwrite_binding() {
        let mut scope = ScopeMap::new();
        scope.add_binding("x".to_string(), 0, 0);
        scope.add_binding("x".to_string(), 5, 10);
        let info = scope.get_binding("x").unwrap();
        assert_eq!(info.line, 5);
        assert_eq!(scope.binding_names().len(), 1);
    }

    #[test]
    fn get_binding_none_for_unknown() {
        let scope = ScopeMap::new();
        assert!(scope.get_binding("nope").is_none());
    }

    #[test]
    fn add_and_retrieve_table() {
        let mut scope = ScopeMap::new();
        scope.add_table("users".to_string());
        let names = scope.table_names();
        assert_eq!(names.len(), 1);
        assert_eq!(names[0], "users");
    }

    #[test]
    fn add_table_deduplicates() {
        let mut scope = ScopeMap::new();
        scope.add_table("users".to_string());
        scope.add_table("users".to_string());
        assert_eq!(scope.table_names().len(), 1);
    }

    #[test]
    fn add_multiple_tables() {
        let mut scope = ScopeMap::new();
        scope.add_table("users".to_string());
        scope.add_table("orders".to_string());
        assert_eq!(scope.table_names().len(), 2);
    }

    #[test]
    fn default_trait() {
        let scope = ScopeMap::default();
        assert!(scope.binding_names().is_empty());
        assert!(scope.table_names().is_empty());
    }
}

#[cfg(test)]
mod symbol_tests {
    use crate::symbols::{extract_symbols, SymbolKind};

    #[test]
    fn empty_source_no_symbols() {
        let symbols = extract_symbols("");
        assert!(symbols.is_empty());
    }

    #[test]
    fn single_let_binding() {
        let symbols = extract_symbols("let x = users |> select(users.id)");
        assert_eq!(symbols.len(), 1);
        assert_eq!(symbols[0].name, "x");
        assert_eq!(symbols[0].kind, SymbolKind::Variable);
        assert_eq!(symbols[0].line, 0);
    }

    #[test]
    fn multiple_let_bindings() {
        let source = "let a = users |> select(users.id)\nlet b = orders |> limit(10)";
        let symbols = extract_symbols(source);
        assert_eq!(symbols.len(), 2);
        assert_eq!(symbols[0].name, "a");
        assert_eq!(symbols[0].line, 0);
        assert_eq!(symbols[1].name, "b");
        assert_eq!(symbols[1].line, 1);
    }

    #[test]
    fn let_with_underscore_name() {
        let symbols = extract_symbols("let my_var = 42");
        assert_eq!(symbols.len(), 1);
        assert_eq!(symbols[0].name, "my_var");
    }

    #[test]
    fn no_let_binding_in_comment() {
        let symbols = extract_symbols("-- let x = 42");
        assert!(symbols.is_empty());
    }

    #[test]
    fn let_at_line_start_vs_indented() {
        let source = "let a = 1\n    let b = 2";
        let symbols = extract_symbols(source);
        assert_eq!(symbols.len(), 2);
        assert_eq!(symbols[0].name, "a");
        assert_eq!(symbols[1].name, "b");
    }

    #[test]
    fn let_with_no_name_parsed_by_antlr() {
        // ANTLR error recovery still finds an IDENT in "let = something"
        let symbols = extract_symbols("let = something");
        // The parser may or may not produce a symbol — just verify no panic
        let _ = symbols;
    }

    #[test]
    fn let_with_numeric_name() {
        let symbols = extract_symbols("let x123 = 42");
        assert_eq!(symbols.len(), 1);
        assert_eq!(symbols[0].name, "x123");
    }

    #[test]
    fn symbol_positions() {
        let source = "  let foo = bar";
        let symbols = extract_symbols(source);
        assert_eq!(symbols.len(), 1);
        assert_eq!(symbols[0].col, 2); // "let" starts at col 2
        assert_eq!(symbols[0].end_col, 2 + "let foo = bar".len() as u32);
    }

    #[test]
    fn non_let_lines_ignored() {
        let source = "users |> select(users.id)\nfilter(fn(r) => r.users.age > 18)";
        let symbols = extract_symbols(source);
        assert!(symbols.is_empty());
    }
}

/// Integration tests exercising lql-parser token and span utilities
/// from the analyzer context, ensuring transitive coverage.
#[cfg(test)]
mod parser_integration_tests {
    use lql_parser::error::ParseError;
    use lql_parser::span::Span;
    use lql_parser::tokens::{
        get_pipe_token_positions, get_token_structure, lex_pipe_positions, lex_tokens, token_types,
    };

    // ── lex_tokens ────────────────────────────────────────────────────────
    #[test]
    fn lex_tokens_simple_pipeline() {
        let tokens = lex_tokens("users |> select(users.id)");
        assert!(!tokens.is_empty());
        let idents: Vec<_> = tokens
            .iter()
            .filter(|t| t.token_type == token_types::IDENT)
            .collect();
        assert!(idents.len() >= 2);
        assert_eq!(idents[0].text, "users");
    }

    #[test]
    fn lex_tokens_finds_pipe_token() {
        let tokens = lex_tokens("a |> b");
        let pipes: Vec<_> = tokens
            .iter()
            .filter(|t| t.token_type == token_types::PIPE)
            .collect();
        assert_eq!(pipes.len(), 1);
        assert_eq!(pipes[0].text, "|>");
    }

    #[test]
    fn lex_tokens_empty_source() {
        let tokens = lex_tokens("");
        assert!(tokens.is_empty());
    }

    #[test]
    fn lex_tokens_parens() {
        let tokens = lex_tokens("select(x)");
        let opens: Vec<_> = tokens
            .iter()
            .filter(|t| t.token_type == token_types::OPEN_PAREN)
            .collect();
        let closes: Vec<_> = tokens
            .iter()
            .filter(|t| t.token_type == token_types::CLOSE_PAREN)
            .collect();
        assert_eq!(opens.len(), 1);
        assert_eq!(closes.len(), 1);
    }

    #[test]
    fn lex_tokens_multiline() {
        let tokens = lex_tokens("users\n|> select(x)");
        let pipe = tokens.iter().find(|t| t.token_type == token_types::PIPE);
        assert!(pipe.is_some());
        assert_eq!(pipe.unwrap().line, 1);
    }

    #[test]
    fn lex_tokens_complex_source() {
        let source = "users\n|> filter(fn(r) => r.age > 18)\n|> select(users.id)";
        let tokens = lex_tokens(source);
        let pipes: Vec<_> = tokens
            .iter()
            .filter(|t| t.token_type == token_types::PIPE)
            .collect();
        assert_eq!(pipes.len(), 2);
    }

    // ── lex_pipe_positions ────────────────────────────────────────────────
    #[test]
    fn lex_pipe_positions_simple() {
        let positions = lex_pipe_positions("users |> select(x)");
        assert_eq!(positions.len(), 1);
        assert_eq!(positions[0].line, 0);
    }

    #[test]
    fn lex_pipe_positions_empty() {
        let positions = lex_pipe_positions("users");
        assert!(positions.is_empty());
    }

    #[test]
    fn lex_pipe_positions_multiline() {
        let positions = lex_pipe_positions("users\n|> a\n|> b");
        assert_eq!(positions.len(), 2);
    }

    // ── get_token_structure ───────────────────────────────────────────────
    #[test]
    fn token_structure_empty() {
        let ts = get_token_structure("");
        assert!(ts.pipe_positions.is_empty());
        assert!(ts.lines_ending_open_paren.is_empty());
        assert!(ts.lines_starting_close_paren.is_empty());
        assert!(ts.comment_lines.is_empty());
    }

    #[test]
    fn token_structure_comment_detection() {
        let ts = get_token_structure("-- comment\nusers\n-- another");
        assert_eq!(ts.comment_lines, vec![0, 2]);
    }

    #[test]
    fn token_structure_paren_tracking() {
        let ts = get_token_structure("select(\n  x\n)");
        assert_eq!(ts.lines_ending_open_paren, vec![0]);
        assert_eq!(ts.lines_starting_close_paren, vec![2]);
    }

    #[test]
    fn token_structure_pipes() {
        let ts = get_token_structure("users |> select(x) |> limit(10)");
        assert_eq!(ts.pipe_positions.len(), 2);
    }

    #[test]
    fn token_structure_complex() {
        let source = "-- query\nusers\n|> select(\n  users.id\n)\n|> limit(10)";
        let ts = get_token_structure(source);
        assert_eq!(ts.comment_lines, vec![0]);
        assert_eq!(ts.pipe_positions.len(), 2);
        assert_eq!(ts.lines_ending_open_paren, vec![2]);
        assert_eq!(ts.lines_starting_close_paren, vec![4]);
    }

    #[test]
    fn token_structure_open_paren_with_comment() {
        let ts = get_token_structure("select( -- args follow");
        assert_eq!(ts.lines_ending_open_paren, vec![0]);
    }

    // ── Span ──────────────────────────────────────────────────────────────
    #[test]
    fn span_new_and_empty() {
        let s = Span::new(5, 10);
        assert_eq!(s.start, 5);
        assert_eq!(s.end, 10);

        let e = Span::empty(42);
        assert_eq!(e.start, 42);
        assert_eq!(e.end, 42);
    }

    #[test]
    fn span_merge() {
        let a = Span::new(5, 10);
        let b = Span::new(2, 15);
        let merged = a.merge(b);
        assert_eq!(merged.start, 2);
        assert_eq!(merged.end, 15);
    }

    #[test]
    fn span_line_col_methods() {
        let src = "hello\nworld";
        let s = Span::new(6, 11);
        assert_eq!(s.start_line_col(src), (1, 0));
        assert_eq!(s.end_line_col(src), (1, 5));
    }

    // ── ParseError ────────────────────────────────────────────────────────
    #[test]
    fn parse_error_warning_constructor() {
        let err = ParseError::warning("test warning", Span::new(10, 20));
        assert_eq!(err.message, "test warning");
        assert_eq!(err.severity, lql_parser::error::Severity::Warning);
    }

    #[test]
    fn parse_error_error_constructor() {
        let err = ParseError::error("test error", Span::new(0, 5));
        assert_eq!(err.message, "test error");
        assert_eq!(err.severity, lql_parser::error::Severity::Error);
    }

    // ── get_pipe_token_positions ──────────────────────────────────────────
    #[test]
    fn pipe_positions_multiline() {
        let positions = get_pipe_token_positions("users\n|> filter(x)\n|> select(y)");
        assert_eq!(positions.len(), 2);
        assert_eq!(positions[0].line, 1);
        assert_eq!(positions[1].line, 2);
    }
}
