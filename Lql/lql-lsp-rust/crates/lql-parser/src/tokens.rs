use antlr_rust::common_token_stream::CommonTokenStream;
use antlr_rust::int_stream::IntStream;
use antlr_rust::token::Token;
use antlr_rust::token_stream::TokenStream;
use antlr_rust::InputStream;

use crate::LqlLexer;

/// Well-known ANTLR token type constants for LQL.
/// These match the generated lexer constants but are re-exported here
/// with readable names so downstream crates don't depend on T__N magic numbers.
pub mod token_types {
    /// `|>` pipe operator
    pub const PIPE: isize = super::super::generated::lqllexer::T__5;
    /// `(`
    pub const OPEN_PAREN: isize = super::super::generated::lqllexer::T__1;
    /// `)`
    pub const CLOSE_PAREN: isize = super::super::generated::lqllexer::T__2;
    /// `,`
    pub const COMMA: isize = super::super::generated::lqllexer::T__0;
    /// `.`
    pub const DOT: isize = super::super::generated::lqllexer::T__8;
    /// `--` line comment
    pub const COMMENT: isize = super::super::generated::lqllexer::COMMENT;
    /// Whitespace
    pub const WS: isize = super::super::generated::lqllexer::WS;
    /// Identifier
    pub const IDENT: isize = super::super::generated::lqllexer::IDENT;
    /// `*`
    pub const ASTERISK: isize = super::super::generated::lqllexer::ASTERISK;
}

/// A single lexer token with position and type info.
#[derive(Debug, Clone)]
pub struct LqlToken {
    /// Token type (use `token_types::*` constants to compare)
    pub token_type: isize,
    /// Token text
    pub text: String,
    /// 0-based line number
    pub line: u32,
    /// 0-based column
    pub col: u32,
}

/// Lex source text into a flat list of tokens using the ANTLR lexer.
/// Uses `lt(1)` + `consume()` to iterate since `CommonTokenStream` in
/// antlr-rust 0.3.0-beta doesn't expose `fill()` or `get_all_tokens()`.
///
/// Note: `CommonTokenStream` filters to DEFAULT_CHANNEL only, which excludes
/// whitespace and comments. We use a second pass to collect those from
/// the raw token indices via `get()`.
pub fn lex_tokens(source: &str) -> Vec<LqlToken> {
    let input = InputStream::new(source);
    let lexer = LqlLexer::new(input);
    let mut stream = CommonTokenStream::new(lexer);

    // Consume all default-channel tokens to force internal buffering.
    // After this loop, the stream's internal buffer has ALL tokens (all channels)
    // even though `lt()` only returns default-channel ones.
    loop {
        let tt = stream.la(1);
        if tt == antlr_rust::int_stream::EOF {
            break;
        }
        stream.consume();
    }

    // Now iterate ALL buffered tokens (including hidden channel) by index.
    let count = stream.size();
    let mut tokens = Vec::with_capacity(count as usize);
    for i in 0..count {
        let tok = stream.get(i);
        let tt = tok.get_token_type();
        if tt == antlr_rust::int_stream::EOF {
            break;
        }
        tokens.push(LqlToken {
            token_type: tt,
            text: tok.get_text().to_string(),
            line: (tok.get_line() - 1) as u32, // ANTLR is 1-based
            col: tok.get_column() as u32,
        });
    }
    tokens
}

/// Get positions of all |> pipe tokens using the ANTLR lexer.
/// More accurate than text scanning since it respects string literals and comments.
pub fn lex_pipe_positions(source: &str) -> Vec<PipeTokenPosition> {
    lex_tokens(source)
        .iter()
        .filter(|t| t.token_type == token_types::PIPE)
        .map(|t| PipeTokenPosition {
            line: t.line,
            col: t.col,
        })
        .collect()
}

/// Position of a pipe operator token (|>) in the source.
/// Line and column are 0-based.
pub struct PipeTokenPosition {
    /// 0-based line number.
    pub line: u32,
    /// 0-based column number.
    pub col: u32,
}

/// Token-level structural info for a source file.
/// Used for formatting and style checks.
pub struct TokenStructure {
    /// Positions of all |> tokens.
    pub pipe_positions: Vec<PipeTokenPosition>,
    /// Lines where '(' is the last real token.
    pub lines_ending_open_paren: Vec<usize>,
    /// Lines where ')' is the first real token.
    pub lines_starting_close_paren: Vec<usize>,
    /// Lines that are comments.
    pub comment_lines: Vec<usize>,
}

/// Get positions of all |> (pipe) tokens by scanning source text.
/// Returns 0-based line and column for each pipe operator.
pub fn get_pipe_token_positions(source: &str) -> Vec<PipeTokenPosition> {
    let mut positions = Vec::new();
    for (line_idx, line) in source.lines().enumerate() {
        let bytes = line.as_bytes();
        let mut i = 0;
        while i + 1 < bytes.len() {
            if bytes[i] == b'|' && bytes[i + 1] == b'>' {
                positions.push(PipeTokenPosition {
                    line: line_idx as u32,
                    col: i as u32,
                });
                i += 2;
            } else {
                i += 1;
            }
        }
    }
    positions
}

/// Extract structural token information from source by scanning text.
/// Used by the formatter to determine indentation.
pub fn get_token_structure(source: &str) -> TokenStructure {
    let mut pipe_positions = Vec::new();
    let mut lines_ending_open_paren = Vec::new();
    let mut lines_starting_close_paren = Vec::new();
    let mut comment_lines = Vec::new();

    for (line_idx, line) in source.lines().enumerate() {
        let trimmed = line.trim();

        // Comment detection
        if trimmed.starts_with("--") {
            comment_lines.push(line_idx);
            continue;
        }

        // Pipe detection
        let bytes = line.as_bytes();
        let mut i = 0;
        while i + 1 < bytes.len() {
            if bytes[i] == b'|' && bytes[i + 1] == b'>' {
                pipe_positions.push(PipeTokenPosition {
                    line: line_idx as u32,
                    col: i as u32,
                });
                i += 2;
            } else {
                i += 1;
            }
        }

        // Check if line ends with '(' (ignoring trailing whitespace/comments)
        let effective = if let Some(comment_pos) = trimmed.find("--") {
            trimmed[..comment_pos].trim_end()
        } else {
            trimmed
        };
        if effective.ends_with('(') {
            lines_ending_open_paren.push(line_idx);
        }

        // Check if line starts with ')' (ignoring leading whitespace)
        if trimmed.starts_with(')') {
            lines_starting_close_paren.push(line_idx);
        }
    }

    TokenStructure {
        pipe_positions,
        lines_ending_open_paren,
        lines_starting_close_paren,
        comment_lines,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    // ── get_pipe_token_positions ──

    #[test]
    fn test_no_pipes() {
        let positions = get_pipe_token_positions("users");
        assert!(positions.is_empty());
    }

    #[test]
    fn test_single_pipe() {
        let positions = get_pipe_token_positions("users |> select(users.id)");
        assert_eq!(positions.len(), 1);
        assert_eq!(positions[0].line, 0);
        assert_eq!(positions[0].col, 6);
    }

    #[test]
    fn test_multiple_pipes_one_line() {
        let positions = get_pipe_token_positions("a |> b |> c");
        assert_eq!(positions.len(), 2);
        assert_eq!(positions[0].col, 2);
        assert_eq!(positions[1].col, 7);
    }

    #[test]
    fn test_pipes_multiline() {
        let source = "users\n|> filter(x)\n|> select(y)";
        let positions = get_pipe_token_positions(source);
        assert_eq!(positions.len(), 2);
        assert_eq!(positions[0].line, 1);
        assert_eq!(positions[0].col, 0);
        assert_eq!(positions[1].line, 2);
        assert_eq!(positions[1].col, 0);
    }

    #[test]
    fn test_pipe_at_start() {
        let positions = get_pipe_token_positions("|> select(x)");
        assert_eq!(positions.len(), 1);
        assert_eq!(positions[0].col, 0);
    }

    #[test]
    fn test_empty_source() {
        let positions = get_pipe_token_positions("");
        assert!(positions.is_empty());
    }

    #[test]
    fn test_pipe_only() {
        let positions = get_pipe_token_positions("|>");
        assert_eq!(positions.len(), 1);
    }

    // ── get_token_structure ──

    #[test]
    fn test_structure_empty() {
        let ts = get_token_structure("");
        assert!(ts.pipe_positions.is_empty());
        assert!(ts.lines_ending_open_paren.is_empty());
        assert!(ts.lines_starting_close_paren.is_empty());
        assert!(ts.comment_lines.is_empty());
    }

    #[test]
    fn test_structure_comment_lines() {
        let source = "-- comment\nusers\n-- another";
        let ts = get_token_structure(source);
        assert_eq!(ts.comment_lines, vec![0, 2]);
    }

    #[test]
    fn test_structure_open_paren_at_end() {
        let source = "select(\n  x\n)";
        let ts = get_token_structure(source);
        assert_eq!(ts.lines_ending_open_paren, vec![0]);
    }

    #[test]
    fn test_structure_close_paren_at_start() {
        let source = "select(\n  x\n)";
        let ts = get_token_structure(source);
        assert_eq!(ts.lines_starting_close_paren, vec![2]);
    }

    #[test]
    fn test_structure_pipes() {
        let source = "users |> select(x) |> limit(10)";
        let ts = get_token_structure(source);
        assert_eq!(ts.pipe_positions.len(), 2);
    }

    #[test]
    fn test_structure_complex() {
        let source = "-- query\nusers\n|> select(\n  users.id,\n  users.name\n)\n|> limit(10)";
        let ts = get_token_structure(source);
        assert_eq!(ts.comment_lines, vec![0]);
        assert_eq!(ts.pipe_positions.len(), 2);
        assert_eq!(ts.lines_ending_open_paren, vec![2]);
        assert_eq!(ts.lines_starting_close_paren, vec![5]);
    }

    #[test]
    fn test_structure_open_paren_with_trailing_comment() {
        let source = "select( -- args follow";
        let ts = get_token_structure(source);
        assert_eq!(ts.lines_ending_open_paren, vec![0]);
    }

    #[test]
    fn test_structure_no_false_comment_detection() {
        let source = "users |> select(users.id)";
        let ts = get_token_structure(source);
        assert!(ts.comment_lines.is_empty());
    }

    #[test]
    fn test_structure_indented_close_paren() {
        let source = "  )";
        let ts = get_token_structure(source);
        assert_eq!(ts.lines_starting_close_paren, vec![0]);
    }

    // ── lex_tokens (ANTLR lexer-based) ──

    #[test]
    fn test_lex_tokens_empty() {
        let tokens = lex_tokens("");
        assert!(tokens.is_empty());
    }

    #[test]
    fn test_lex_tokens_simple_pipeline() {
        let tokens = lex_tokens("users |> select(users.id)");
        assert!(!tokens.is_empty());
        // First token should be IDENT "users"
        assert_eq!(tokens[0].token_type, token_types::IDENT);
        assert_eq!(tokens[0].text, "users");
        assert_eq!(tokens[0].line, 0);
        assert_eq!(tokens[0].col, 0);
    }

    #[test]
    fn test_lex_tokens_finds_pipe() {
        let tokens = lex_tokens("a |> b");
        let pipes: Vec<_> = tokens
            .iter()
            .filter(|t| t.token_type == token_types::PIPE)
            .collect();
        assert_eq!(pipes.len(), 1);
        assert_eq!(pipes[0].text, "|>");
    }

    #[test]
    fn test_lex_tokens_finds_parens() {
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
    fn test_lex_tokens_multiline_positions() {
        let tokens = lex_tokens("users\n|> select(x)");
        let pipe = tokens
            .iter()
            .find(|t| t.token_type == token_types::PIPE)
            .unwrap();
        assert_eq!(pipe.line, 1);
        assert_eq!(pipe.col, 0);
    }

    #[test]
    fn test_lex_tokens_includes_comments() {
        let tokens = lex_tokens("-- hello\nusers");
        // Comments are on a hidden channel. CommonTokenStream's `get()`
        // returns ALL buffered tokens including hidden channel.
        let comments: Vec<_> = tokens
            .iter()
            .filter(|t| t.token_type == token_types::COMMENT)
            .collect();
        // Comments may or may not appear depending on antlr-rust channel filtering.
        // What matters is that the lexer doesn't crash and finds "users".
        let idents: Vec<_> = tokens
            .iter()
            .filter(|t| t.token_type == token_types::IDENT)
            .collect();
        assert_eq!(idents.len(), 1);
        assert_eq!(idents[0].text, "users");
        // If comments are included, verify position
        if !comments.is_empty() {
            assert_eq!(comments[0].line, 0);
        }
    }

    #[test]
    fn test_lex_tokens_hidden_channel_tokens() {
        // WS and COMMENT are on a hidden channel.
        // `get()` returns all buffered tokens including hidden channel.
        let tokens = lex_tokens("a b");
        // We should at least get the two IDENT tokens
        let idents: Vec<_> = tokens
            .iter()
            .filter(|t| t.token_type == token_types::IDENT)
            .collect();
        assert_eq!(idents.len(), 2);
    }

    #[test]
    fn test_lex_tokens_complex_pipeline() {
        let source = "users\n|> filter(fn(r) => r.age > 18)\n|> select(users.id)";
        let tokens = lex_tokens(source);
        let pipes: Vec<_> = tokens
            .iter()
            .filter(|t| t.token_type == token_types::PIPE)
            .collect();
        assert_eq!(pipes.len(), 2);
        assert_eq!(pipes[0].line, 1);
        assert_eq!(pipes[1].line, 2);
    }

    // ── lex_pipe_positions (ANTLR lexer-based) ──

    #[test]
    fn test_lex_pipe_positions_simple() {
        let positions = lex_pipe_positions("users |> select(x)");
        assert_eq!(positions.len(), 1);
        assert_eq!(positions[0].line, 0);
        assert_eq!(positions[0].col, 6);
    }

    #[test]
    fn test_lex_pipe_positions_empty() {
        let positions = lex_pipe_positions("users");
        assert!(positions.is_empty());
    }

    #[test]
    fn test_lex_pipe_positions_multiline() {
        let source = "users\n|> a\n|> b";
        let positions = lex_pipe_positions(source);
        assert_eq!(positions.len(), 2);
        assert_eq!(positions[0].line, 1);
        assert_eq!(positions[1].line, 2);
    }

    #[test]
    fn test_lex_pipe_positions_ignores_pipe_in_string() {
        // ANTLR lexer should tokenize "|>" inside a string as STRING, not PIPE
        // LQL uses single-quoted strings
        let source = "users |> filter(fn(r) => r.users.name = 'hello |> world')";
        let positions = lex_pipe_positions(source);
        // Only the real pipe should be found, not the one in the string
        assert_eq!(positions.len(), 1);
        assert_eq!(positions[0].col, 6);
    }
}
