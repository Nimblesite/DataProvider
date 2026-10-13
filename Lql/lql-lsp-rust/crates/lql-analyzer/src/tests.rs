#[cfg(test)]
mod diagnostics_tests {
    use crate::{
        diagnostics::{analyze, Diagnostic, DiagnosticSeverity},
        scope::ScopeMap,
    };

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
    // Implements [LQL-RUST-NAMED-CASE-MATRICES].
    fn empty_source_produces_no_diagnostics_cases() {
        for &(input0,) in named_cases(NAMED_CASES_4) {
            crate::test_support::verify_analyze_empty_source(input0);
        }
    }

    // ── pipe spacing ───────────────────────────────────────────────────
    #[test]
    // Implements [LQL-RUST-NAMED-CASE-MATRICES].
    fn correct_pipe_spacing_no_warning_cases() {
        for &(input0, input1) in named_cases(NAMED_CASES_1) {
            crate::test_support::verify_no_diags_matching(input0, input1);
        }
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
    // Implements [LQL-RUST-NAMED-CASE-MATRICES].
    fn missing_space_both_sides_cases() {
        for &(input0, input1, input2) in named_cases(NAMED_CASES_2) {
            let diags = analyze(input0, &empty_scope());
            let pipe_diags = diags_matching(&diags, input1);
            assert_eq!(pipe_diags.len(), input2);
        }
    }

    #[test]
    // Implements [LQL-RUST-NAMED-CASE-MATRICES].
    fn multiple_pipes_on_one_line_cases() {
        for &(input0, input1) in named_cases(NAMED_CASES_3) {
            crate::test_support::verify_no_diags_matching(input0, input1);
        }
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
        crate::test_support::verify_no_diags_matching(
            "users |> filter(fn(r) => r.users.id > 0)",
            "Unknown function: `fn`",
        );
    }

    #[test]
    fn all_known_functions_recognized() {
        let known = independent_all_known_functions_recognized_data_1();
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
    fn keyword_identifiers_not_flagged() {
        let keywords = independent_keyword_identifiers_not_flagged_data_2();
        for kw in keywords {
            let source = format!("x |> filter(fn(r) => {kw}(r.x.y))");
            let diags = analyze(&source, &empty_scope());
            let unknown = diags_matching(&diags, &format!("Unknown function: `{kw}`"));
            assert!(unknown.is_empty(), "Keyword `{kw}` flagged as unknown");
        }
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

    include!("tests_expectations_0.fixtures.rs");

    include!("tests_case_inputs.fixtures.rs");
    include!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../../Tests.Shared/RustNamedCases.rs"
    ));
}

#[cfg(test)]
mod completion_tests {
    use crate::{
        completion::{get_completions, CompletionContext, CompletionKind},
        schema::SchemaCache,
        scope::ScopeMap,
    };

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
        independent_schema_data_1()
    }

    // ── after pipe completions ─────────────────────────────────────────
    #[test]
    fn after_pipe_returns_pipeline_ops() {
        assert_default_completion_labels(
            "",
            true,
            false,
            &independent_after_pipe_returns_pipeline_ops_data_3(),
        );
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
        let select = crate::test_support::completion_by_label(&items, "select");
        assert!(select.insert_text.is_some());
        assert_eq!(select.kind, CompletionKind::Function);
        assert_eq!(select.sort_priority, 1);
    }

    // ── lambda completions ─────────────────────────────────────────────
    #[test]
    fn in_lambda_returns_logical_ops() {
        assert_default_completion_labels(
            "",
            false,
            true,
            &["and", "or", "not", "is", "in", "like", "exists"],
        );
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
        assert_default_completion_labels(
            "",
            false,
            false,
            &["let", "fn", "case", "as", "asc", "desc", "distinct"],
        );
    }

    #[test]
    fn keyword_completions_are_keyword_kind() {
        let items = get_completions(&ctx("le", false, false), &ScopeMap::new(), None);
        let let_item = crate::test_support::completion_by_label(&items, "let");
        assert_eq!(let_item.kind, CompletionKind::Keyword);
        assert!(let_item.insert_text.is_none());
        assert_eq!(let_item.sort_priority, 3);
    }

    // ── aggregate completions ──────────────────────────────────────────
    #[test]
    fn aggregate_completions_returned() {
        assert_default_completion_labels(
            "",
            false,
            false,
            &independent_aggregate_completions_returned_data_4(),
        );
    }

    #[test]
    fn aggregate_completions_have_snippets() {
        let items = get_completions(&ctx("cou", false, false), &ScopeMap::new(), None);
        let count = crate::test_support::completion_by_label(&items, "count");
        assert_eq!(count.insert_text.as_deref(), Some("count($0)"));
        assert_eq!(count.sort_priority, 2);
    }

    // ── string function completions ────────────────────────────────────
    #[test]
    fn string_functions_returned() {
        assert_default_completion_labels(
            "",
            false,
            false,
            &independent_string_functions_returned_data_5(),
        );
    }

    // ── schema-based completions ───────────────────────────────────────
    #[test]
    fn table_completions_from_schema() {
        let schema = test_schema();
        let items = get_completions(&ctx("us", false, false), &ScopeMap::new(), Some(&schema));
        let users =
            crate::test_support::single_completion_of_kind(&items, CompletionKind::Table, "users");
        assert!(users.detail.contains("3 columns"));
    }

    #[test]
    fn table_with_many_columns_truncates_doc() {
        let schema = test_schema();
        let items = get_completions(&ctx("or", false, false), &ScopeMap::new(), Some(&schema));
        let orders = crate::test_support::completion_by_label(&items, "orders");
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
        crate::test_support::assert_labels(&labels, &["id", "name", "email"], true);
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
        let table_items =
            crate::test_support::completion_items_of_kind(&items, CompletionKind::Table);
        assert!(table_items.is_empty());
    }

    // ── scope-based completions ────────────────────────────────────────
    #[test]
    fn scope_bindings_in_completions() {
        let mut scope = ScopeMap::new();
        scope.add_binding("my_var".to_string(), 0, 0);
        let items = get_completions(&ctx("my", false, false), &scope, None);
        let var = crate::test_support::single_completion_of_kind(
            &items,
            CompletionKind::Variable,
            "my_var",
        );
        assert_eq!(var.sort_priority, 5);
    }

    #[test]
    fn scope_tables_in_completions() {
        let mut scope = ScopeMap::new();
        scope.add_table("products".to_string());
        let items = get_completions(&ctx("pro", false, false), &scope, None);
        crate::test_support::single_completion_of_kind(&items, CompletionKind::Table, "products");
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
        crate::test_support::assert_completions_sorted(&items);
    }

    // Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
    fn assert_default_completion_labels(
        prefix: &str,
        after_pipe: bool,
        in_lambda: bool,
        expected: &[&str],
    ) {
        let items = get_completions(&ctx(prefix, after_pipe, in_lambda), &ScopeMap::new(), None);
        let labels: Vec<&str> = items.iter().map(|item| item.label.as_str()).collect();
        for label in expected {
            assert!(labels.contains(label), "missing completion: {label}");
        }
    }

    include!("tests_expectations_1.fixtures.rs");

    include!("tests_schema_data_0.fixtures.rs");
}

#[cfg(test)]
mod hover_tests {
    use crate::{
        hover::{get_hover, get_hover_with_schema},
        schema::SchemaCache,
    };

    fn test_schema() -> SchemaCache {
        independent_schema_data_2()
    }

    // ── get_hover ──────────────────────────────────────────────────────
    #[test]
    fn hover_known_keyword() {
        crate::test_support::verify_hover_keyword("select", "select", true);
    }

    #[test]
    fn hover_case_insensitive() {
        crate::test_support::verify_hover_keyword("SELECT", "select", false);
    }

    #[test]
    fn hover_unknown_word_returns_none() {
        crate::test_support::verify_hover_unknown("foobar");
    }

    #[test]
    fn hover_all_pipeline_ops() {
        for word in &independent_hover_all_pipeline_ops_data_6() {
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
        for word in &independent_hover_all_string_functions_data_7() {
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
        crate::test_support::verify_hover_table(&test_schema(), &["name"]);
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

    include!("tests_expectations_2.fixtures.rs");

    include!("tests_schema_data_1.fixtures.rs");
}

#[cfg(test)]
mod schema_tests {
    use crate::schema::SchemaCache;

    fn test_schema() -> SchemaCache {
        independent_schema_data_3()
    }

    #[test]
    fn default_cache_is_empty() {
        crate::test_support::verify_cache_default_is_empty();
    }

    #[test]
    fn from_tables_populates_cache() {
        crate::test_support::verify_cache_from_tables(&test_schema());
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
        crate::test_support::verify_case_insensitive(&["users", "USERS", "Users"], |v| {
            cache.get_table(v).is_some()
        });
    }

    #[test]
    fn get_columns_returns_all() {
        let cache = test_schema();
        let cols = cache.get_columns("users");
        assert_eq!(cols.len(), 2);
    }

    #[test]
    fn get_columns_unknown_table_empty() {
        crate::test_support::verify_cache_columns_empty(&test_schema());
    }

    #[test]
    fn age_populated_after_creation() {
        crate::test_support::verify_cache_age_some(&test_schema());
    }

    #[test]
    fn is_stale_true_for_default() {
        crate::test_support::verify_staleness(&SchemaCache::default(), 60, true);
    }

    #[test]
    fn is_stale_false_for_fresh() {
        crate::test_support::verify_staleness(&test_schema(), 60, false);
    }

    // ── TableInfo ──────────────────────────────────────────────────────
    #[test]
    fn get_column_case_insensitive() {
        let cache = test_schema();
        let table = cache.get_table("users").unwrap();
        crate::test_support::verify_case_insensitive(&["Name", "name", "NAME"], |v| {
            table.get_column(v).is_some()
        });
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
        crate::test_support::verify_type_description(
            "id",
            "uuid",
            false,
            true,
            "uuid (PK) NOT NULL",
        );
    }

    #[test]
    fn type_description_nullable_no_pk() {
        crate::test_support::verify_type_description("name", "text", true, false, "text");
    }

    #[test]
    fn type_description_not_null_no_pk() {
        crate::test_support::verify_type_description(
            "total",
            "numeric",
            false,
            false,
            "numeric NOT NULL",
        );
    }

    #[test]
    fn type_description_nullable_pk() {
        crate::test_support::verify_type_description(
            "weird",
            "integer",
            true,
            true,
            "integer (PK)",
        );
    }

    include!("tests_schema_data_2.fixtures.rs");
}

#[cfg(test)]
mod scope_tests {
    #[test]
    fn new_scope_is_empty() {
        crate::test_support::verify_scope_is_empty();
    }

    #[test]
    fn add_and_retrieve_binding() {
        crate::test_support::verify_scope_add_binding(5, 10);
    }

    #[test]
    fn binding_names_returns_all() {
        crate::test_support::verify_cross_module_test_binding_names("a", "b");
    }

    #[test]
    fn overwrite_binding() {
        crate::test_support::verify_scope_overwrite_binding();
    }

    #[test]
    fn get_binding_none_for_unknown() {
        crate::test_support::verify_scope_get_binding_none("nope");
    }

    #[test]
    fn add_and_retrieve_table() {
        crate::test_support::verify_scope_add_single_table("users");
    }

    #[test]
    fn add_table_deduplicates() {
        crate::test_support::verify_scope_add_duplicate_table("users");
    }

    #[test]
    fn add_multiple_tables() {
        crate::test_support::verify_scope_add_tables(&["users", "orders"]);
    }

    #[test]
    fn default_trait() {
        crate::test_support::verify_scope_default_trait();
    }
}

#[cfg(test)]
mod symbol_tests {
    use crate::symbols::extract_symbols;

    #[test]
    fn empty_source_no_symbols() {
        crate::test_support::verify_cross_module_empty_source_no_symbols("");
    }

    #[test]
    fn single_let_binding() {
        crate::test_support::verify_cross_module_single_let_binding();
    }

    #[test]
    fn multiple_let_bindings() {
        crate::test_support::verify_symbols_multiple_lets(
            "let a = users |> select(users.id)\nlet b = orders |> limit(10)",
            "a",
            "b",
        );
    }

    #[test]
    fn let_with_underscore_name() {
        let symbols = extract_symbols("let my_var = 42");
        assert_eq!(symbols.len(), 1);
        assert_eq!(symbols[0].name, "my_var");
    }

    #[test]
    fn no_let_binding_in_comment() {
        crate::test_support::verify_cross_module_empty_source_no_symbols("-- let x = 42");
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
    use lql_parser::{
        error::ParseError,
        span::Span,
        tokens::{
            get_pipe_token_positions, get_token_structure, lex_pipe_positions, lex_tokens,
            token_types, tokens_of_type,
        },
    };

    // ── lex_tokens ────────────────────────────────────────────────────────
    #[test]
    fn lex_tokens_simple_pipeline() {
        let tokens = lex_tokens("users |> select(users.id)");
        assert!(!tokens.is_empty());
        let idents = tokens_of_type(&tokens, token_types::IDENT);
        assert!(idents.len() >= 2);
        assert_eq!(idents[0].text, "users");
    }

    #[test]
    fn lex_tokens_finds_pipe_token() {
        let tokens = lex_tokens("a |> b");
        let pipes = tokens_of_type(&tokens, token_types::PIPE);
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
        crate::test_support::verify_lex_tokens_parens("select(x)", 1, 1);
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
        let pipes = tokens_of_type(&tokens, token_types::PIPE);
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
        crate::test_support::verify_token_structure("", &[], 0, &[], &[]);
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
        crate::test_support::verify_token_structure(
            "-- query\nusers\n|> select(\n  users.id\n)\n|> limit(10)",
            &[0],
            2,
            &[2],
            &[4],
        );
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
