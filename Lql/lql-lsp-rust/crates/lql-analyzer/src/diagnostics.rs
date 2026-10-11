use lql_parser::tokens::get_pipe_token_positions;

use crate::scope::{collect_function_calls, ScopeMap};

/// Diagnostic severity level.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum DiagnosticSeverity {
    Error,
    Warning,
    Info,
    Hint,
}

/// A diagnostic message with location.
#[derive(Debug, Clone)]
pub struct Diagnostic {
    pub line: u32,
    pub col: u32,
    pub end_col: u32,
    pub message: String,
    pub severity: DiagnosticSeverity,
}

/// Known pipeline and built-in functions (case-insensitive check).
const KNOWN_FUNCTIONS: &[&str] = &[
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

/// Run semantic analysis on LQL source text and return diagnostics.
///
/// All structural analysis uses the ANTLR parse tree (same Lql.g4 grammar as C#).
/// Bracket matching is handled by ANTLR parse errors (reported separately).
/// Pipe spacing is a style lint that checks whitespace around `|>` tokens.
pub fn analyze(source: &str, scope: &ScopeMap) -> Vec<Diagnostic> {
    let mut diagnostics = Vec::new();

    check_pipe_spacing_from_tokens(source, &mut diagnostics);
    check_unknown_functions_from_tree(source, scope, &mut diagnostics);

    diagnostics
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Pushes a warning diagnostic covering the two-character pipe operator.
fn push_pipe_spacing_diagnostic(
    diagnostics: &mut Vec<Diagnostic>,
    line: u32,
    col: u32,
    message: &str,
) {
    diagnostics.push(Diagnostic {
        line,
        col,
        end_col: col + 2,
        message: message.into(),
        severity: DiagnosticSeverity::Warning,
    });
}

/// Check pipe operator spacing using token positions from the lexer.
fn check_pipe_spacing_from_tokens(source: &str, diagnostics: &mut Vec<Diagnostic>) {
    let lines: Vec<&str> = source.lines().collect();
    let pipe_positions = get_pipe_token_positions(source);

    for pos in &pipe_positions {
        let line_idx = pos.line as usize;
        let col = pos.col as usize;

        if line_idx >= lines.len() {
            continue;
        }
        let line = lines[line_idx];
        let bytes = line.as_bytes();

        // Check space before |>
        if col > 0 && bytes[col - 1] != b' ' && bytes[col - 1] != b'\t' {
            push_pipe_spacing_diagnostic(
                diagnostics,
                pos.line,
                pos.col,
                "Pipeline operator `|>` should be preceded by a space",
            );
        }

        // Check space after |>
        let after = col + 2;
        if after < bytes.len() && bytes[after] != b' ' && bytes[after] != b'\t' {
            push_pipe_spacing_diagnostic(
                diagnostics,
                pos.line,
                pos.col,
                "Pipeline operator `|>` should be followed by a space",
            );
        }
    }
}

/// Check for unknown functions using the ANTLR parse tree.
/// Walks the tree to find all function calls, then checks each against
/// the known function list and scope bindings.
fn check_unknown_functions_from_tree(
    source: &str,
    scope: &ScopeMap,
    diagnostics: &mut Vec<Diagnostic>,
) {
    let calls = collect_function_calls(source);

    for call in &calls {
        let lower = call.name.to_ascii_lowercase();
        if !KNOWN_FUNCTIONS.contains(&lower.as_str()) && !scope.has_binding(&call.name) {
            diagnostics.push(Diagnostic {
                line: call.line,
                col: call.col,
                end_col: call.end_col,
                message: format!("Unknown function: `{}`", call.name),
                severity: DiagnosticSeverity::Info,
            });
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn empty_scope() -> ScopeMap {
        ScopeMap::new()
    }

    // ── analyze basics ──

    #[test]
    // Implements [LQL-RUST-NAMED-CASE-MATRICES].
    fn test_analyze_empty_source_cases() {
        for &(input0,) in named_cases(NAMED_CASES_1) {
            verify_expanded_test_analyze_empty_source(input0);
        }
    }

    // ── pipe spacing ──

    #[test]
    fn test_pipe_no_space_before() {
        let diags = analyze("users|> select(users.id)", &empty_scope());
        let pipe_diags: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("preceded by"))
            .collect();
        assert_eq!(pipe_diags.len(), 1);
        assert_eq!(pipe_diags[0].severity, DiagnosticSeverity::Warning);
    }

    #[test]
    fn test_pipe_no_space_after() {
        let diags = analyze("users |>select(users.id)", &empty_scope());
        let pipe_diags: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("followed by"))
            .collect();
        assert_eq!(pipe_diags.len(), 1);
    }

    #[test]
    fn test_pipe_no_space_both_sides() {
        let diags = analyze("users|>select(users.id)", &empty_scope());
        let pipe_diags: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("Pipeline operator"))
            .collect();
        assert_eq!(pipe_diags.len(), 2);
    }

    #[test]
    fn test_pipe_with_tab_is_ok() {
        verify_expanded_test_pipe_with_tab_is_ok(
            "users\t|>\tselect(users.id)",
            "Pipeline operator",
        );
    }

    #[test]
    fn test_pipe_at_start_of_line() {
        verify_expanded_test_pipe_with_tab_is_ok("|> select(users.id)", "preceded by");
    }

    #[test]
    fn test_pipe_at_end_of_line() {
        verify_expanded_test_pipe_with_tab_is_ok("users |>", "followed by");
    }

    #[test]
    fn test_multiple_pipes_on_one_line() {
        verify_expanded_test_pipe_with_tab_is_ok(
            "a |> filter(x) |> select(y)",
            "Pipeline operator",
        );
    }

    // ── unknown functions (via ANTLR tree) ──

    #[test]
    fn test_unknown_function_detected() {
        let diags = analyze("users |> foobar(x)", &empty_scope());
        let unk: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("Unknown function"))
            .collect();
        assert_eq!(unk.len(), 1);
        assert!(unk[0].message.contains("foobar"));
        assert_eq!(unk[0].severity, DiagnosticSeverity::Info);
    }

    #[test]
    fn test_known_functions_not_flagged() {
        // These are pipeline functions the ANTLR tree will parse as function calls
        let known_pipeline_fns = [
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
        ];
        for func in &known_pipeline_fns {
            let src = format!("users |> {func}(x)");
            let diags = analyze(&src, &empty_scope());
            let unk: Vec<_> = diags
                .iter()
                .filter(|d| d.message.contains("Unknown function"))
                .collect();
            assert!(
                unk.is_empty(),
                "Function '{func}' should be known but was flagged"
            );
        }
    }

    #[test]
    fn test_scope_binding_not_flagged() {
        let mut scope = ScopeMap::new();
        scope.add_binding("my_func".to_string(), 0, 0);
        let diags = analyze("users |> my_func(x)", &scope);
        let unk: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("Unknown function"))
            .collect();
        assert!(unk.is_empty());
    }

    // ── combined analysis ──

    #[test]
    fn test_multiple_diagnostics() {
        let source = "users|>foobar(x)";
        let diags = analyze(source, &empty_scope());
        // pipe spacing warnings + unknown function
        assert!(diags.len() >= 2);
    }

    #[test]
    fn test_diagnostic_line_numbers() {
        let source = "users\nusers|>select(x)";
        let diags = analyze(source, &empty_scope());
        let pipe_diag = diags
            .iter()
            .find(|d| d.message.contains("preceded by"))
            .unwrap();
        assert_eq!(pipe_diag.line, 1);
    }

    // Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
    fn verify_expanded_test_pipe_with_tab_is_ok(input0: &str, input1: &str) {
        crate::test_support::verify_no_diags_matching(input0, input1);
    }

    // Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
    fn verify_expanded_test_analyze_empty_source(input0: &str) {
        crate::test_support::verify_analyze_empty_source(input0);
    }

    include!("diagnostics_case_inputs.fixtures.rs");
    include!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../../Tests.Shared/RustNamedCases.rs"
    ));
}
