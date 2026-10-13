#![allow(dead_code)]
#![allow(nonstandard_style)]

pub mod error;
#[cfg(not(tarpaulin_include))]
pub mod generated;
pub mod span;
pub mod tokens;

use std::rc::Rc;

use antlr_rust::{
    common_token_stream::CommonTokenStream, error_listener::ErrorListener,
    parser::Parser as AntlrParser, recognizer::Recognizer, InputStream,
};
use error::ParseError;
pub use generated::{
    lqllexer::LqlLexer, lqllistener::LqlListener, lqlparser::*, lqlvisitor::LqlVisitor,
};
use span::Span;

/// Collected errors from parsing.
pub struct LqlParseResult<'input> {
    pub tree: Rc<ProgramContextAll<'input>>,
    pub errors: Vec<ParseError>,
}

/// Parse LQL source text, returning the parse tree and any errors.
/// This function always returns a (possibly partial) tree — essential for LSP use.
pub fn parse_lql(source: &str) -> LqlParseResult<'_> {
    let input = InputStream::new(source);
    let lexer = LqlLexer::new(input);
    let error_collector = ErrorCollector::new(source);
    let error_collector_ref = Box::new(error_collector.clone());

    let token_source = CommonTokenStream::new(lexer);
    let mut parser = LqlParser::new(token_source);
    parser.remove_error_listeners();
    parser.add_error_listener(error_collector_ref);

    let tree = parser
        .program()
        .expect("ANTLR parser should always produce a tree");

    LqlParseResult {
        tree,
        errors: error_collector.take_errors(),
    }
}

/// Error listener that collects parse errors with span information.
#[derive(Clone)]
struct ErrorCollector {
    errors: Rc<std::cell::RefCell<Vec<ParseError>>>,
    source: String,
}

impl ErrorCollector {
    fn new(source: &str) -> Self {
        Self {
            errors: Rc::new(std::cell::RefCell::new(Vec::new())),
            source: source.to_string(),
        }
    }

    fn take_errors(&self) -> Vec<ParseError> {
        self.errors.borrow_mut().drain(..).collect()
    }
}

impl<'input, T: Recognizer<'input>> ErrorListener<'input, T> for ErrorCollector {
    fn syntax_error(
        &self,
        _recognizer: &T,
        _offending_symbol: Option<
            &<T::TF as antlr_rust::token_factory::TokenFactory<'input>>::Inner,
        >,
        line: isize,
        column: isize,
        msg: &str,
        _error: Option<&antlr_rust::errors::ANTLRError>,
    ) {
        let byte_offset = line_col_to_byte_offset(&self.source, line as u32, column as u32);
        let span = Span::new(byte_offset, byte_offset + 1);
        self.errors
            .borrow_mut()
            .push(ParseError::error(msg.to_string(), span));
    }
}

fn line_col_to_byte_offset(source: &str, line: u32, col: u32) -> u32 {
    let mut current_line = 1u32;
    let mut current_col = 0u32;
    for (i, ch) in source.char_indices() {
        if current_line == line && current_col == col {
            return i as u32;
        }
        if ch == '\n' {
            current_line += 1;
            current_col = 0;
        } else {
            current_col += 1;
        }
    }
    source.len() as u32
}

#[cfg(test)]
mod tests {
    use antlr_rust::tree::ParseTree;

    use super::*;

    #[test]
    fn test_parse_simple_select() {
        let result = parse_lql("users |> select(users.id, users.name)");
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
        let text = result.tree.get_text();
        assert!(text.contains("users"));
    }

    #[test]
    // Implements [LQL-RUST-NAMED-CASE-MATRICES].
    fn test_parse_filter_cases() {
        for &(input0,) in named_cases(NAMED_CASES_2) {
            verify_expanded_parse_ok(input0);
        }
    }

    #[test]
    // Implements [LQL-RUST-NAMED-CASE-MATRICES].
    fn test_parse_let_binding_cases() {
        for &(input0,) in named_cases(NAMED_CASES_3) {
            verify_expanded_parse_ok(input0);
        }
    }

    #[test]
    // Implements [LQL-RUST-NAMED-CASE-MATRICES].
    fn test_parse_aggregation_cases() {
        for &(input0,) in named_cases(NAMED_CASES_1) {
            verify_expanded_parse_ok(input0);
        }
    }

    #[test]
    fn test_parse_window_function() {
        verify_expanded_parse_ok("orders |> select(row_number() over (partition by orders.user_id order by orders.total desc))");
    }

    #[test]
    // Implements [LQL-RUST-NAMED-CASE-MATRICES].
    fn test_error_recovery_cases() {
        for &(input0,) in named_cases(NAMED_CASES_4) {
            verify_expanded_test_error_recovery(input0);
        }
    }

    #[test]
    // Implements [LQL-RUST-NAMED-CASE-MATRICES].
    fn test_parse_left_join_cases() {
        for &(input0,) in named_cases(NAMED_CASES_5) {
            verify_expanded_parse_ok(input0);
        }
    }

    #[test]
    fn test_parse_between_not_supported() {
        verify_expanded_test_error_recovery(
            "orders |> filter(fn(row) => row.orders.total between 100 and 500)",
        );
    }

    #[test]
    fn test_parse_coalesce_not_in_grammar() {
        verify_expanded_test_error_recovery(
            "users |> select(coalesce(users.nickname, users.name) as display_name)",
        );
    }

    #[test]
    fn test_parse_chained_pipeline() {
        verify_expanded_parse_ok("users |> filter(fn(r) => r.users.active = true) |> order_by(users.name asc) |> limit(50) |> offset(100)");
    }

    #[test]
    fn test_parse_empty_string_produces_tree() {
        let result = parse_lql("");
        // Empty input should still produce a tree (possibly empty program)
        let _tree = result.tree;
    }

    // ── error recovery tests ───────────────────────────────────────────

    #[test]
    fn test_error_has_message_and_span() {
        let result = parse_lql("users |> select(");
        assert!(!result.errors.is_empty());
        let err = &result.errors[0];
        assert!(!err.message.is_empty());
        assert_eq!(err.severity, error::Severity::Error);
    }

    // Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
    fn verify_expanded_parse_ok(input0: &str) {
        let result = parse_lql(input0);
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    // Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
    fn verify_expanded_test_error_recovery(input0: &str) {
        let result = parse_lql(input0);
        // Should have errors but not panic
        assert!(!result.errors.is_empty());
    }

    include!("lib_case_inputs.fixtures.rs");
    include!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../../Tests.Shared/RustNamedCases.rs"
    ));
}
