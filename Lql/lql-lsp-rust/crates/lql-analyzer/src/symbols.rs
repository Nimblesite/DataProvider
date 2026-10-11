use lql_parser::{parse_lql, LetStmtContextAttrs, ProgramContextAttrs, StatementContextAttrs};

/// A document symbol for outline/breadcrumb display.
#[derive(Debug, Clone)]
pub struct DocumentSymbol {
    pub name: String,
    pub kind: SymbolKind,
    pub line: u32,
    pub col: u32,
    pub end_line: u32,
    pub end_col: u32,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SymbolKind {
    Variable,
    Function,
    Table,
}

/// Extract document symbols (let bindings) from LQL source using the ANTLR parse tree.
pub fn extract_symbols(source: &str) -> Vec<DocumentSymbol> {
    let result = parse_lql(source);
    let mut symbols = Vec::new();

    for stmt in result.tree.statement_all() {
        if let Some(let_stmt) = stmt.letStmt() {
            if let Some(ident) = let_stmt.IDENT() {
                let name = ident.symbol.text.to_string();
                let line = (ident.symbol.line - 1) as u32;
                let col = ident.symbol.column as u32;

                // Calculate end position from the let statement's full extent
                // The let keyword starts the statement; use the line for end_line
                // and approximate end_col from the source line
                let end_line = line;
                let end_col = source
                    .lines()
                    .nth(line as usize)
                    .map(|l| l.len() as u32)
                    .unwrap_or(col + name.len() as u32);

                // Adjust col to point to "let" keyword, not the identifier
                let let_col = source
                    .lines()
                    .nth(line as usize)
                    .and_then(|l| l.find("let"))
                    .unwrap_or(0) as u32;

                symbols.push(DocumentSymbol {
                    name,
                    kind: SymbolKind::Variable,
                    line,
                    col: let_col,
                    end_line,
                    end_col,
                });
            }
        }
    }

    symbols
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_extract_single_let() {
        crate::test_support::verify_cross_module_single_let_binding();
    }

    #[test]
    fn test_extract_multiple_lets() {
        crate::test_support::verify_symbols_multiple_lets(
            "let alpha = users\nlet beta = orders",
            "alpha",
            "beta",
        );
    }

    #[test]
    fn test_extract_no_lets() {
        crate::test_support::verify_cross_module_empty_source_no_symbols(
            "users |> select(users.id)",
        );
    }

    #[test]
    // Implements [LQL-RUST-NAMED-CASE-MATRICES].
    fn test_extract_let_with_underscore_cases() {
        for &(input0, input1) in named_cases(NAMED_CASES_1) {
            verify_expanded_test_extract_let_with_underscore(input0, input1);
        }
    }

    #[test]
    fn test_extract_empty_source() {
        crate::test_support::verify_cross_module_empty_source_no_symbols("");
    }

    #[test]
    fn test_symbol_end_col() {
        let source = "let abc = stuff";
        let syms = extract_symbols(source);
        assert_eq!(syms.len(), 1);
        assert!(syms[0].end_col > syms[0].col);
    }

    #[test]
    fn test_mixed_content() {
        let source = "-- comment\nlet x = users\nusers |> select(users.id)\nlet y = orders";
        let syms = extract_symbols(source);
        assert_eq!(syms.len(), 2);
        assert_eq!(syms[0].name, "x");
        assert_eq!(syms[1].name, "y");
    }

    // Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
    fn verify_expanded_test_extract_let_with_underscore(input0: &str, input1: &str) {
        let syms = extract_symbols(input0);
        assert_eq!(syms.len(), 1);
        assert_eq!(syms[0].name, input1);
    }

    include!("symbols_case_inputs.fixtures.rs");
    include!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../../Tests.Shared/RustNamedCases.rs"
    ));
}
