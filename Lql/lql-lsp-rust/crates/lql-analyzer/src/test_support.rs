use crate::{extract_symbols, schema::ColumnInfo, ScopeMap, SymbolKind};

// Implements [LQL-TEST-COLUMN-FIXTURE].
/// Builds a column fixture for schema-aware analyzer tests.
pub(crate) fn make_col(name: &str, sql_type: &str, nullable: bool, pk: bool) -> ColumnInfo {
    ColumnInfo {
        name: name.to_string(),
        sql_type: sql_type.to_string(),
        is_nullable: nullable,
        is_primary_key: pk,
    }
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
pub(crate) fn verify_cross_module_test_binding_names(input0: &str, input1: &str) {
    let mut scope = ScopeMap::new();
    scope.add_binding(input0.to_string(), 0, 0);
    scope.add_binding(input1.to_string(), 1, 0);
    let names = scope.binding_names();
    assert_eq!(names.len(), 2);
    assert!(names.contains(&input0));
    assert!(names.contains(&input1));
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
pub(crate) fn verify_cross_module_single_let_binding() {
    let symbols = extract_symbols("let x = users |> select(users.id)");
    assert_eq!(symbols.len(), 1);
    assert_eq!(symbols[0].name, "x");
    assert_eq!(symbols[0].kind, SymbolKind::Variable);
    assert_eq!(symbols[0].line, 0);
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
pub(crate) fn verify_cross_module_empty_source_no_symbols(input0: &str) {
    let symbols = extract_symbols(input0);
    assert!(symbols.is_empty());
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts `scope` has no bindings and no tables.
fn verify_scope_lists_empty(scope: &ScopeMap) {
    assert!(scope.binding_names().is_empty());
    assert!(scope.table_names().is_empty());
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts a fresh [`ScopeMap`] has no bindings and no tables.
pub(crate) fn verify_scope_is_empty() {
    verify_scope_lists_empty(&ScopeMap::new());
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts a binding added at `line`/`col` round-trips through the scope map.
pub(crate) fn verify_scope_add_binding(line: u32, col: u32) {
    let mut scope = ScopeMap::new();
    scope.add_binding("x".to_string(), line, col);
    assert!(scope.has_binding("x"));
    assert!(!scope.has_binding("y"));
    let info = scope.get_binding("x").unwrap();
    assert_eq!(info.name, "x");
    assert_eq!(info.line, line);
    assert_eq!(info.col, col);
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts a later binding for the same name replaces the earlier one.
pub(crate) fn verify_scope_overwrite_binding() {
    let mut scope = ScopeMap::new();
    scope.add_binding("x".to_string(), 0, 0);
    scope.add_binding("x".to_string(), 5, 10);
    let info = scope.get_binding("x").unwrap();
    assert_eq!(info.line, 5);
    assert_eq!(scope.binding_names().len(), 1);
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts an unknown binding name is absent from the scope map.
pub(crate) fn verify_scope_get_binding_none(needle: &str) {
    let scope = ScopeMap::new();
    assert!(scope.get_binding(needle).is_none());
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts a single table is recorded in insertion order.
pub(crate) fn verify_scope_add_single_table(name: &str) {
    let mut scope = ScopeMap::new();
    scope.add_table(name.to_string());
    let names = scope.table_names();
    assert_eq!(names.len(), 1);
    assert_eq!(names[0], name);
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts adding the same table twice is idempotent.
pub(crate) fn verify_scope_add_duplicate_table(name: &str) {
    let mut scope = ScopeMap::new();
    scope.add_table(name.to_string());
    scope.add_table(name.to_string());
    assert_eq!(scope.table_names().len(), 1);
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts every table named in `names` is recorded exactly once.
pub(crate) fn verify_scope_add_tables(names: &[&str]) {
    let mut scope = ScopeMap::new();
    for name in names {
        scope.add_table((*name).to_string());
    }
    let tables = scope.table_names();
    assert_eq!(tables.len(), names.len());
    for name in names {
        assert!(tables.contains(name));
    }
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts [`ScopeMap::default`] matches a freshly constructed empty scope.
pub(crate) fn verify_scope_default_trait() {
    verify_scope_lists_empty(&ScopeMap::default());
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts `build_scope(source)` records `table` in its table set.
pub(crate) fn verify_scope_has_table(source: &str, table: &str) {
    let scope = crate::scope::build_scope(source);
    assert!(
        scope.table_names().contains(&table),
        "missing table '{table}' in '{source}'"
    );
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts two consecutive `let` bindings are extracted with their line numbers.
pub(crate) fn verify_symbols_multiple_lets(source: &str, first: &str, second: &str) {
    let symbols = extract_symbols(source);
    assert_eq!(symbols.len(), 2);
    assert_eq!(symbols[0].name, first);
    assert_eq!(symbols[0].line, 0);
    assert_eq!(symbols[1].name, second);
    assert_eq!(symbols[1].line, 1);
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Returns the completion item labelled `label`, panicking when absent.
pub(crate) fn completion_by_label<'a>(
    items: &'a [crate::completion::CompletionItem],
    label: &str,
) -> &'a crate::completion::CompletionItem {
    items.iter().find(|i| i.label == label).unwrap()
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts exactly one completion of `kind` is labelled `label` and returns it.
pub(crate) fn single_completion_of_kind<'a>(
    items: &'a [crate::completion::CompletionItem],
    kind: crate::completion::CompletionKind,
    label: &str,
) -> &'a crate::completion::CompletionItem {
    let matching = completion_items_of_kind(items, kind);
    assert_eq!(matching.len(), 1);
    assert_eq!(matching[0].label, label);
    matching[0]
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Filters completion `items` down to entries of `kind`.
pub(crate) fn completion_items_of_kind(
    items: &[crate::completion::CompletionItem],
    kind: crate::completion::CompletionKind,
) -> Vec<&crate::completion::CompletionItem> {
    items.iter().filter(|i| i.kind == kind).collect()
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts the expected labels are present (`present = true`) or absent.
pub(crate) fn assert_labels<S: AsRef<str>, E: AsRef<str>>(
    labels: &[S],
    expected: &[E],
    present: bool,
) {
    for label in expected {
        let found = labels.iter().any(|l| l.as_ref() == label.as_ref());
        assert_eq!(
            found,
            present,
            "label '{}' presence mismatch",
            label.as_ref()
        );
    }
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts completion items are sorted by priority then label.
pub(crate) fn assert_completions_sorted(items: &[crate::completion::CompletionItem]) {
    for window in items.windows(2) {
        assert!(
            window[0].sort_priority <= window[1].sort_priority
                || (window[0].sort_priority == window[1].sort_priority
                    && window[0].label <= window[1].label),
            "Items not sorted: {} (pri {}) before {} (pri {})",
            window[0].label,
            window[0].sort_priority,
            window[1].label,
            window[1].sort_priority
        );
    }
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts `lookup` succeeds for every variant in `variants` and fails for an
/// unknown name.
pub(crate) fn verify_case_insensitive(variants: &[&str], lookup: impl Fn(&str) -> bool) {
    for variant in variants {
        assert!(lookup(variant), "missing '{variant}'");
    }
    assert!(!lookup("nonexistent"));
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts `cache.get_columns` returns nothing for an unknown table.
pub(crate) fn verify_cache_columns_empty(cache: &crate::schema::SchemaCache) {
    assert!(cache.get_columns("nonexistent").is_empty());
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts `cache` records an age once constructed.
pub(crate) fn verify_cache_age_some(cache: &crate::schema::SchemaCache) {
    assert!(cache.age().is_some());
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts `cache`'s staleness for `secs` matches `stale`.
pub(crate) fn verify_staleness(cache: &crate::schema::SchemaCache, secs: u64, stale: bool) {
    assert_eq!(cache.is_stale(std::time::Duration::from_secs(secs)), stale);
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts a default [`SchemaCache`](crate::schema::SchemaCache) is empty and never refreshed.
pub(crate) fn verify_cache_default_is_empty() {
    let cache = crate::schema::SchemaCache::default();
    assert!(cache.is_empty());
    assert_eq!(cache.table_count(), 0);
    assert!(cache.table_names().is_empty());
    assert!(cache.age().is_none());
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts a cache built from tables is non-empty and records its table count.
pub(crate) fn verify_cache_from_tables(cache: &crate::schema::SchemaCache) {
    assert!(!cache.is_empty());
    assert_eq!(cache.table_count(), 2);
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts [`ColumnInfo::type_description`] for the given column shape.
pub(crate) fn verify_type_description(
    name: &str,
    sql_type: &str,
    nullable: bool,
    pk: bool,
    expected: &str,
) {
    let col = make_col(name, sql_type, nullable, pk);
    assert_eq!(col.type_description(), expected);
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts analyzing `source` against an empty scope produces no diagnostics.
pub(crate) fn verify_analyze_empty_source(source: &str) {
    let diags = crate::diagnostics::analyze(source, &ScopeMap::new());
    assert!(diags.is_empty());
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts the keyword hover title contains `needle` and, when requested,
/// carries a signature.
pub(crate) fn verify_hover_keyword(word: &str, needle: &str, expect_signature: bool) {
    let info = crate::hover::get_hover(word).unwrap();
    assert!(info.title.contains(needle));
    if expect_signature {
        assert!(info.signature.is_some());
    }
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts schema-aware hover for table `users` reports the table shape and
/// additionally mentions every string in `extra_detail`.
pub(crate) fn verify_hover_table(schema: &crate::schema::SchemaCache, extra_detail: &[&str]) {
    let info = crate::hover::get_hover_with_schema("users", None, Some(schema)).unwrap();
    assert!(info.title.contains("users"));
    assert!(info.title.contains("Table"));
    assert!(info.detail.contains("id"));
    for expected in extra_detail {
        assert!(info.detail.contains(expected));
    }
    assert!(info.signature.is_some());
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts `word` has no keyword hover entry.
pub(crate) fn verify_hover_unknown(word: &str) {
    assert!(crate::hover::get_hover(word).is_none());
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts no diagnostic message contains `needle`.
pub(crate) fn verify_no_diags_matching(source: &str, needle: &str) {
    let diags = crate::diagnostics::analyze(source, &ScopeMap::new());
    let matching: Vec<_> = diags
        .iter()
        .filter(|d| d.message.contains(needle))
        .collect();
    assert!(
        matching.is_empty(),
        "unexpected diagnostic matching '{needle}': {matching:?}"
    );
}

// Implements [LQL-RUST-NAMED-CASE-MATRICES].
/// Runs `run` once per named case, asserting each case name is present.
pub(crate) fn run_named_cases<T, F>(cases: &[(&str, T)], run: F)
where
    F: Fn(&T),
{
    for (name, input) in cases {
        assert!(!name.is_empty(), "Scenario name must be present");
        run(input);
    }
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts `source` lexes to `open` open and `close` close parentheses.
pub(crate) fn verify_lex_tokens_parens(source: &str, open: usize, close: usize) {
    use lql_parser::tokens::{lex_tokens, token_types, tokens_of_type};
    let tokens = lex_tokens(source);
    assert_eq!(tokens_of_type(&tokens, token_types::OPEN_PAREN).len(), open);
    assert_eq!(
        tokens_of_type(&tokens, token_types::CLOSE_PAREN).len(),
        close
    );
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts the token structure for `source` matches the expected positions.
pub(crate) fn verify_token_structure(
    source: &str,
    comment_lines: &[usize],
    pipe_count: usize,
    lines_ending_open_paren: &[usize],
    lines_starting_close_paren: &[usize],
) {
    use lql_parser::tokens::get_token_structure;
    let structure = get_token_structure(source);
    assert_eq!(structure.comment_lines, comment_lines);
    assert_eq!(structure.pipe_positions.len(), pipe_count);
    assert_eq!(structure.lines_ending_open_paren, lines_ending_open_paren);
    assert_eq!(
        structure.lines_starting_close_paren,
        lines_starting_close_paren
    );
}
