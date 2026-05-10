use lql_lsp::ai;
use lql_lsp::db;

use ai::{AiCompletionContext, AiCompletionProvider, AiConfig};
use lql_analyzer::{
    analyze, build_scope, extract_symbols, get_completions, get_hover_with_schema,
    CompletionContext, CompletionKind, DiagnosticSeverity as LqlSeverity, SchemaCache,
    SymbolKind as LqlSymbolKind,
};
use lql_parser::parse_lql;
use std::collections::HashMap;
use std::sync::{Arc, Mutex};
use std::time::Duration;
use tokio::sync::RwLock;
use tower_lsp::jsonrpc::Result;
use tower_lsp::lsp_types::*;
use tower_lsp::{Client, LanguageServer, LspService, Server};

struct LqlBackend {
    client: Client,
    documents: Mutex<HashMap<Url, String>>,
    schema: RwLock<Option<SchemaCache>>,
    init_connection_string: Mutex<Option<String>>,
    ai_provider: RwLock<Option<Arc<dyn AiCompletionProvider>>>,
    ai_config: Mutex<Option<AiConfig>>,
}

impl LqlBackend {
    fn new(client: Client) -> Self {
        Self {
            client,
            documents: Mutex::new(HashMap::new()),
            schema: RwLock::new(None),
            init_connection_string: Mutex::new(None),
            ai_provider: RwLock::new(None),
            ai_config: Mutex::new(None),
        }
    }

    /// Set an AI completion provider. Called externally to plug in a model.
    pub async fn set_ai_provider(&self, provider: Arc<dyn AiCompletionProvider>) {
        *self.ai_provider.write().await = Some(provider);
    }

    async fn publish_diagnostics(&self, uri: Url, source: &str) {
        let diags = Self::collect_diagnostics(source);
        self.client.publish_diagnostics(uri, diags, None).await;
    }

    fn collect_diagnostics(source: &str) -> Vec<tower_lsp::lsp_types::Diagnostic> {
        let scope = build_scope(source);
        let parse_result = parse_lql(source);
        let mut diags: Vec<tower_lsp::lsp_types::Diagnostic> = parse_result
            .errors
            .iter()
            .map(|e| {
                let (sl, sc) = e.span.start_line_col(source);
                let (el, ec) = e.span.end_line_col(source);
                tower_lsp::lsp_types::Diagnostic {
                    range: Range {
                        start: Position::new(sl, sc),
                        end: Position::new(el, ec),
                    },
                    severity: Some(tower_lsp::lsp_types::DiagnosticSeverity::ERROR),
                    source: Some("lql".into()),
                    message: e.message.clone(),
                    ..Default::default()
                }
            })
            .collect();
        drop(parse_result);

        for d in analyze(source, &scope) {
            let severity = match d.severity {
                LqlSeverity::Error => tower_lsp::lsp_types::DiagnosticSeverity::ERROR,
                LqlSeverity::Warning => tower_lsp::lsp_types::DiagnosticSeverity::WARNING,
                LqlSeverity::Info => tower_lsp::lsp_types::DiagnosticSeverity::INFORMATION,
                LqlSeverity::Hint => tower_lsp::lsp_types::DiagnosticSeverity::HINT,
            };
            diags.push(tower_lsp::lsp_types::Diagnostic {
                range: Range {
                    start: Position::new(d.line, d.col),
                    end: Position::new(d.line, d.end_col),
                },
                severity: Some(severity),
                source: Some("lql".into()),
                message: d.message,
                ..Default::default()
            });
        }
        diags
    }

    /// Detect qualified "Table.Column" at cursor for hover.
    /// Returns (word, Option<(table_name, column_name)>).
    fn get_qualified_at_position(
        source: &str,
        position: Position,
    ) -> (Option<String>, Option<(String, String)>) {
        let line = match source.lines().nth(position.line as usize) {
            Some(l) => l,
            None => return (None, None),
        };
        let col = position.character as usize;
        if col > line.len() {
            return (None, None);
        }

        let bytes = line.as_bytes();
        let mut start = col;
        let mut end = col;
        while start > 0 && (bytes[start - 1].is_ascii_alphanumeric() || bytes[start - 1] == b'_') {
            start -= 1;
        }
        while end < bytes.len() && (bytes[end].is_ascii_alphanumeric() || bytes[end] == b'_') {
            end += 1;
        }
        if start == end {
            return (None, None);
        }
        let word = line[start..end].to_string();

        // Check for "qualifier." before the word (need at least 1 char + dot)
        if start >= 1 && bytes[start - 1] == b'.' && start >= 2 {
            let dot_pos = start - 1;
            let mut q_start = dot_pos;
            while q_start > 0
                && (bytes[q_start - 1].is_ascii_alphanumeric() || bytes[q_start - 1] == b'_')
            {
                q_start -= 1;
            }
            if q_start < dot_pos {
                let qualifier = line[q_start..dot_pos].to_string();
                return (Some(word.clone()), Some((qualifier, word)));
            }
        }

        (Some(word), None)
    }

    fn compute_completion_context(source: &str, position: Position) -> CompletionContext {
        let line = source.lines().nth(position.line as usize).unwrap_or("");
        let col = (position.character as usize).min(line.len());
        let line_prefix = &line[..col];

        let word_prefix: String = line_prefix
            .chars()
            .rev()
            .take_while(|c| c.is_alphanumeric() || *c == '_')
            .collect::<String>()
            .chars()
            .rev()
            .collect();

        let trimmed_prefix = line_prefix.trim();
        let prefix_before_word =
            line_prefix.trim_end_matches(|c: char| c.is_alphanumeric() || c == '_');
        let after_pipe = trimmed_prefix.ends_with("|>")
            || trimmed_prefix.ends_with("|> ")
            || prefix_before_word.trim_end().ends_with("|>");

        let open_parens = line_prefix.matches('(').count();
        let close_parens = line_prefix.matches(')').count();
        let in_arg_list = open_parens > close_parens;

        let in_lambda = source[..source
            .lines()
            .take(position.line as usize + 1)
            .map(|l| l.len() + 1)
            .sum::<usize>()
            .min(source.len())]
            .contains("=>");

        // Detect table qualifier: "Table." or "Table.prefix"
        let before_word = &line_prefix[..line_prefix.len() - word_prefix.len()];
        let table_qualifier = if let Some(without_dot) = before_word.strip_suffix('.') {
            let q: String = without_dot
                .chars()
                .rev()
                .take_while(|c| c.is_alphanumeric() || *c == '_')
                .collect::<String>()
                .chars()
                .rev()
                .collect();
            if q.is_empty() {
                None
            } else {
                Some(q)
            }
        } else {
            None
        };

        CompletionContext {
            line_prefix: line_prefix.to_string(),
            in_arg_list,
            after_pipe,
            in_lambda,
            word_prefix,
            table_qualifier,
        }
    }
}

#[tower_lsp::async_trait]
impl LanguageServer for LqlBackend {
    async fn initialize(&self, params: InitializeParams) -> Result<InitializeResult> {
        // Extract connection string and AI config from initializationOptions
        if let Some(ref options) = params.initialization_options {
            if let Some(conn) = options.get("connectionString").and_then(|v| v.as_str()) {
                if !conn.is_empty() {
                    *self.init_connection_string.lock().unwrap() = Some(conn.to_string());
                }
            }
            if let Some(ai_obj) = options.get("aiProvider") {
                if let Some(config) = AiConfig::from_json(ai_obj) {
                    *self.ai_config.lock().unwrap() = Some(config);
                }
            }
        }

        Ok(InitializeResult {
            capabilities: ServerCapabilities {
                text_document_sync: Some(TextDocumentSyncCapability::Kind(
                    TextDocumentSyncKind::FULL,
                )),
                completion_provider: Some(CompletionOptions {
                    trigger_characters: Some(vec![
                        ".".into(),
                        "|".into(),
                        ">".into(),
                        "(".into(),
                        " ".into(),
                    ]),
                    resolve_provider: Some(false),
                    ..Default::default()
                }),
                hover_provider: Some(HoverProviderCapability::Simple(true)),
                document_symbol_provider: Some(OneOf::Left(true)),
                document_formatting_provider: Some(OneOf::Left(true)),
                ..Default::default()
            },
            ..Default::default()
        })
    }

    async fn initialized(&self, _: InitializedParams) {
        self.client
            .log_message(MessageType::INFO, "LQL Language Server initialized")
            .await;

        // Set up AI provider (before DB check, which may return early)
        let ai_config = self.ai_config.lock().unwrap().clone();
        if let Some(ref config) = ai_config {
            if config.enabled {
                let (provider, log_msg) = build_ai_provider(config);
                if let Some(p) = provider {
                    self.set_ai_provider(p).await;
                }
                self.client.log_message(MessageType::INFO, log_msg).await;
            }
        }

        // Resolve connection string: initializationOptions > env var
        let conn_str = self
            .init_connection_string
            .lock()
            .unwrap()
            .clone()
            .or_else(db::discover_connection_string);

        let conn_str = match conn_str {
            Some(s) => s,
            None => {
                self.client
                    .log_message(
                        MessageType::INFO,
                        "No DB connection configured (set LQL_CONNECTION_STRING)",
                    )
                    .await;
                return;
            }
        };

        self.client
            .log_message(MessageType::INFO, "Connecting to database for schema...")
            .await;

        // Initial schema fetch — write directly since we have &self
        match db::fetch_schema(&conn_str).await {
            Ok(cache) => {
                let count = cache.table_count();
                *self.schema.write().await = Some(cache);
                self.client
                    .log_message(MessageType::INFO, format!("Schema loaded: {count} tables"))
                    .await;
            }
            Err(e) => {
                self.client
                    .log_message(MessageType::WARNING, format!("Schema fetch failed: {e}"))
                    .await;
            }
        }
    }

    async fn shutdown(&self) -> Result<()> {
        Ok(())
    }

    async fn did_open(&self, params: DidOpenTextDocumentParams) {
        let uri = params.text_document.uri.clone();
        let text = params.text_document.text.clone();
        self.documents
            .lock()
            .unwrap()
            .insert(uri.clone(), text.clone());
        self.publish_diagnostics(uri, &text).await;
    }

    async fn did_change(&self, params: DidChangeTextDocumentParams) {
        let uri = params.text_document.uri.clone();
        if let Some(change) = params.content_changes.into_iter().next_back() {
            let text = change.text.clone();
            self.documents
                .lock()
                .unwrap()
                .insert(uri.clone(), text.clone());
            self.publish_diagnostics(uri, &text).await;
        }
    }

    async fn did_close(&self, params: DidCloseTextDocumentParams) {
        self.documents
            .lock()
            .unwrap()
            .remove(&params.text_document.uri);
    }

    async fn completion(&self, params: CompletionParams) -> Result<Option<CompletionResponse>> {
        let uri = &params.text_document_position.text_document.uri;
        let position = params.text_document_position.position;

        let source = {
            let docs = self.documents.lock().unwrap();
            match docs.get(uri) {
                Some(s) => s.clone(),
                None => return Ok(None),
            }
        };

        let scope = build_scope(&source);
        let ctx = Self::compute_completion_context(&source, position);

        // Read schema (cheap clone — SchemaCache uses Arc internally)
        let schema = self.schema.read().await.clone();
        let mut items = get_completions(&ctx, &scope, schema.as_ref());

        // Merge AI completions if a provider is configured
        let ai_provider = self.ai_provider.read().await.clone();
        if let Some(ref provider) = ai_provider {
            let ai_config = self.ai_config.lock().unwrap().clone();
            let timeout_ms = ai_config.as_ref().map(|c| c.timeout_ms).unwrap_or(2000);
            let enabled = ai_config.as_ref().map(|c| c.enabled).unwrap_or(true);

            if enabled {
                // Build compact schema description for AI context
                let (available_tables, schema_description) = match schema.as_ref() {
                    Some(s) => {
                        let names: Vec<String> =
                            s.table_names().iter().map(|n| n.to_string()).collect();
                        let desc = names
                            .iter()
                            .map(|name| {
                                let cols = s.get_columns(name);
                                let col_strs: Vec<String> = cols
                                    .iter()
                                    .map(|c| {
                                        let mut s = format!("{} {}", c.name, c.sql_type);
                                        if c.is_primary_key {
                                            s.push_str(" PK");
                                        }
                                        if !c.is_nullable {
                                            s.push_str(" NOT NULL");
                                        }
                                        s
                                    })
                                    .collect();
                                format!("{}({})", name, col_strs.join(", "))
                            })
                            .collect::<Vec<_>>()
                            .join("\n");
                        (names, desc)
                    }
                    None => (Vec::new(), String::new()),
                };

                let ai_ctx = AiCompletionContext {
                    document_text: source.clone(),
                    line: position.line,
                    column: position.character,
                    line_prefix: ctx.line_prefix.clone(),
                    word_prefix: ctx.word_prefix.clone(),
                    file_uri: uri.to_string(),
                    available_tables,
                    schema_description,
                };

                match tokio::time::timeout(
                    Duration::from_millis(timeout_ms),
                    provider.complete(&ai_ctx),
                )
                .await
                {
                    Ok(ai_items) => items.extend(ai_items),
                    Err(_) => {
                        // AI provider timed out — silently proceed with schema/keyword completions
                    }
                }
            }
        }

        let lsp_items: Vec<tower_lsp::lsp_types::CompletionItem> = items
            .into_iter()
            .map(|item| {
                let kind = match item.kind {
                    CompletionKind::Keyword => CompletionItemKind::KEYWORD,
                    CompletionKind::Function => CompletionItemKind::FUNCTION,
                    CompletionKind::Snippet => CompletionItemKind::SNIPPET,
                    CompletionKind::Variable => CompletionItemKind::VARIABLE,
                    CompletionKind::Table => CompletionItemKind::CLASS,
                    CompletionKind::Column => CompletionItemKind::FIELD,
                };
                tower_lsp::lsp_types::CompletionItem {
                    label: item.label,
                    kind: Some(kind),
                    detail: Some(item.detail),
                    documentation: Some(Documentation::String(item.documentation)),
                    insert_text: item.insert_text,
                    insert_text_format: Some(InsertTextFormat::SNIPPET),
                    ..Default::default()
                }
            })
            .collect();

        Ok(Some(CompletionResponse::Array(lsp_items)))
    }

    async fn hover(&self, params: HoverParams) -> Result<Option<Hover>> {
        let uri = &params.text_document_position_params.text_document.uri;
        let position = params.text_document_position_params.position;

        let source = {
            let docs = self.documents.lock().unwrap();
            match docs.get(uri) {
                Some(s) => s.clone(),
                None => return Ok(None),
            }
        };

        let (word, qualified) = Self::get_qualified_at_position(&source, position);
        let word = match word {
            Some(w) => w,
            None => return Ok(None),
        };

        let schema = self.schema.read().await.clone();
        let qualified_refs = qualified.as_ref().map(|(t, c)| (t.as_str(), c.as_str()));
        let info = match get_hover_with_schema(&word, qualified_refs, schema.as_ref()) {
            Some(info) => info,
            None => return Ok(None),
        };

        let mut content = format!("**{}**\n\n{}", info.title, info.detail);
        if let Some(sig) = info.signature {
            content.push_str(&format!("\n\n```lql\n{sig}\n```"));
        }
        Ok(Some(Hover {
            contents: HoverContents::Markup(MarkupContent {
                kind: MarkupKind::Markdown,
                value: content,
            }),
            range: None,
        }))
    }

    async fn document_symbol(
        &self,
        params: DocumentSymbolParams,
    ) -> Result<Option<DocumentSymbolResponse>> {
        let uri = &params.text_document.uri;

        let source = {
            let docs = self.documents.lock().unwrap();
            match docs.get(uri) {
                Some(s) => s.clone(),
                None => return Ok(None),
            }
        };

        let symbols = extract_symbols(&source);
        let lsp_symbols: Vec<tower_lsp::lsp_types::SymbolInformation> = symbols
            .into_iter()
            .map(|sym| {
                let kind = match sym.kind {
                    LqlSymbolKind::Variable => tower_lsp::lsp_types::SymbolKind::VARIABLE,
                    LqlSymbolKind::Function => tower_lsp::lsp_types::SymbolKind::FUNCTION,
                    LqlSymbolKind::Table => tower_lsp::lsp_types::SymbolKind::CLASS,
                };
                #[allow(deprecated)]
                tower_lsp::lsp_types::SymbolInformation {
                    name: sym.name,
                    kind,
                    tags: None,
                    deprecated: None,
                    location: Location {
                        uri: uri.clone(),
                        range: Range {
                            start: Position::new(sym.line, sym.col),
                            end: Position::new(sym.end_line, sym.end_col),
                        },
                    },
                    container_name: None,
                }
            })
            .collect();

        Ok(Some(DocumentSymbolResponse::Flat(lsp_symbols)))
    }

    async fn formatting(&self, params: DocumentFormattingParams) -> Result<Option<Vec<TextEdit>>> {
        let uri = &params.text_document.uri;

        let source = {
            let docs = self.documents.lock().unwrap();
            match docs.get(uri) {
                Some(s) => s.clone(),
                None => return Ok(None),
            }
        };

        let formatted = format_lql(&source);
        if formatted == source {
            return Ok(None);
        }

        let line_count = source.lines().count() as u32;
        let last_line_len = source.lines().last().map(|l| l.len()).unwrap_or(0) as u32;

        Ok(Some(vec![TextEdit {
            range: Range {
                start: Position::new(0, 0),
                end: Position::new(line_count, last_line_len),
            },
            new_text: formatted,
        }]))
    }
}

/// Build the AI completion provider for a given config and produce the
/// log message to surface to the client.
///
/// Pure function (no I/O, no state) so it can be unit-tested without the
/// full LSP runtime — closes the coverage gap on the AI dispatch arms in
/// `initialized()`.
pub fn build_ai_provider(
    config: &ai::AiConfig,
) -> (Option<Arc<dyn ai::AiCompletionProvider>>, String) {
    match config.provider.as_str() {
        "test" => (
            Some(Arc::new(ai::TestAiProvider) as Arc<dyn ai::AiCompletionProvider>),
            "AI test provider activated — returns deterministic completions".to_string(),
        ),
        "test_slow" => {
            let delay = config.timeout_ms.saturating_add(5000);
            (
                Some(Arc::new(ai::SlowAiProvider { delay_ms: delay })
                    as Arc<dyn ai::AiCompletionProvider>),
                format!(
                    "AI slow test provider activated — {}ms delay (timeout: {}ms)",
                    delay, config.timeout_ms
                ),
            )
        }
        "ollama" => {
            let lql_ref = include_str!("../../lql-reference.md").to_string();
            (
                Some(Arc::new(ai::OllamaProvider::new(config, lql_ref))
                    as Arc<dyn ai::AiCompletionProvider>),
                format!(
                    "Ollama AI provider activated (model: {}, endpoint: {})",
                    config.model, config.endpoint
                ),
            )
        }
        _ => (
            None,
            format!(
                "AI completion provider configured: {} (model: {}, endpoint: {})",
                config.provider, config.model, config.endpoint
            ),
        ),
    }
}

fn format_lql(source: &str) -> String {
    let mut result = String::new();
    let mut indent = 0usize;
    let indent_str = "    ";

    for line in source.lines() {
        let trimmed = line.trim();
        if trimmed.is_empty() {
            result.push('\n');
            continue;
        }
        if trimmed.starts_with(')') {
            indent = indent.saturating_sub(1);
        }
        if trimmed.starts_with("|>") && indent == 0 {
            indent = 1;
        }
        result.push_str(&indent_str.repeat(indent));
        result.push_str(trimmed);
        result.push('\n');
        if trimmed.ends_with('(') && !trimmed.starts_with("--") {
            indent += 1;
        }
    }

    result
}

#[tokio::main]
async fn main() {
    // Handle `--version` (and `-V`) for the VS Code extension's pre-flight
    // version check, so it can detect a matching binary on PATH without
    // downloading from GitHub releases.
    let args: Vec<String> = std::env::args().collect();
    if args.iter().any(|a| a == "--version" || a == "-V") {
        println!("lql-lsp {}", env!("CARGO_PKG_VERSION"));
        return;
    }

    let stdin = tokio::io::stdin();
    let stdout = tokio::io::stdout();

    let (service, socket) = LspService::new(LqlBackend::new);
    Server::new(stdin, stdout, socket).serve(service).await;
}

#[cfg(test)]
mod tests {
    use super::*;

    // ── format_lql ──

    #[test]
    fn test_format_empty() {
        assert_eq!(format_lql(""), "");
    }

    #[test]
    fn test_format_single_line() {
        let result = format_lql("users");
        assert_eq!(result, "users\n");
    }

    #[test]
    fn test_format_preserves_empty_lines() {
        let result = format_lql("a\n\nb");
        assert_eq!(result, "a\n\nb\n");
    }

    #[test]
    fn test_format_indents_pipe() {
        let result = format_lql("users\n|> select(users.id)");
        assert!(result.contains("    |> select"));
    }

    #[test]
    fn test_format_indents_after_open_paren() {
        let result = format_lql("select(\nusers.id\n)");
        assert!(result.contains("    users.id"));
    }

    #[test]
    fn test_format_dedents_on_close_paren() {
        let result = format_lql("select(\nusers.id\n)");
        let lines: Vec<&str> = result.lines().collect();
        let close_line = lines.iter().find(|l| l.trim() == ")").unwrap();
        assert_eq!(*close_line, ")");
    }

    #[test]
    fn test_format_comment_line_not_indented_after() {
        let result = format_lql("-- comment(\nfoo");
        // Comment ends with ( but should NOT increase indent
        let lines: Vec<&str> = result.lines().collect();
        assert_eq!(lines[1], "foo");
    }

    #[test]
    fn test_format_trims_leading_whitespace() {
        let result = format_lql("   users   ");
        assert_eq!(result, "users\n");
    }

    #[test]
    fn test_format_complex_pipeline() {
        let source = "users\n|> filter(fn(r) => r.age > 18)\n|> select(\nusers.id,\nusers.name\n)\n|> limit(10)";
        let result = format_lql(source);
        assert!(result.contains("    |> filter"));
        assert!(result.contains("        users.id"));
    }

    #[test]
    fn test_format_no_change_returns_same() {
        let source = "users\n";
        let formatted = format_lql(source);
        assert_eq!(formatted, source);
    }

    // ── build_scope (via ANTLR parse tree in lql_analyzer) ──

    #[test]
    fn test_build_scope_let_bindings() {
        let scope = build_scope("let x = users |> select(users.id)");
        assert!(scope.has_binding("x"));
    }

    #[test]
    fn test_build_scope_multiple_lets() {
        let source = "let a = users\nlet b = orders";
        let scope = build_scope(source);
        assert!(scope.has_binding("a"));
        assert!(scope.has_binding("b"));
    }

    #[test]
    fn test_build_scope_tables_detected() {
        let scope = build_scope("users |> select(users.id)");
        let tables = scope.table_names();
        assert!(tables.contains(&"users"));
    }

    #[test]
    fn test_build_scope_invalid_let_produces_no_panic() {
        // ANTLR error recovery handles invalid let syntax gracefully
        let _scope = build_scope("let = bad");
    }

    #[test]
    fn test_build_scope_empty_source() {
        let scope = build_scope("");
        assert!(scope.binding_names().is_empty());
        assert!(scope.table_names().is_empty());
    }

    // ── collect_diagnostics ──

    #[test]
    fn test_collect_diagnostics_clean() {
        let diags = LqlBackend::collect_diagnostics("users |> select(users.id)");
        assert!(diags.is_empty());
    }

    #[test]
    fn test_collect_diagnostics_parse_error() {
        let diags = LqlBackend::collect_diagnostics("users |> select(");
        assert!(!diags.is_empty());
        let has_error = diags
            .iter()
            .any(|d| d.severity == Some(tower_lsp::lsp_types::DiagnosticSeverity::ERROR));
        assert!(has_error);
    }

    #[test]
    fn test_collect_diagnostics_semantic() {
        let diags = LqlBackend::collect_diagnostics("users|>select(users.id)");
        let has_warning = diags
            .iter()
            .any(|d| d.severity == Some(tower_lsp::lsp_types::DiagnosticSeverity::WARNING));
        assert!(has_warning);
    }

    #[test]
    fn test_collect_diagnostics_source_is_lql() {
        let diags = LqlBackend::collect_diagnostics("users |> select(");
        for d in &diags {
            assert_eq!(d.source.as_deref(), Some("lql"));
        }
    }

    // ── get_qualified_at_position ──

    #[test]
    fn test_qualified_simple_word() {
        let (word, qualified) =
            LqlBackend::get_qualified_at_position("select", Position::new(0, 3));
        assert_eq!(word, Some("select".to_string()));
        assert!(qualified.is_none());
    }

    #[test]
    fn test_qualified_table_dot_column() {
        let (word, qualified) =
            LqlBackend::get_qualified_at_position("users.name", Position::new(0, 7));
        assert_eq!(word, Some("name".to_string()));
        let (table, col) = qualified.unwrap();
        assert_eq!(table, "users");
        assert_eq!(col, "name");
    }

    #[test]
    fn test_qualified_past_end_of_line() {
        let (word, qualified) = LqlBackend::get_qualified_at_position("abc", Position::new(0, 100));
        assert!(word.is_none());
        assert!(qualified.is_none());
    }

    #[test]
    fn test_qualified_empty_source() {
        let (word, qualified) = LqlBackend::get_qualified_at_position("", Position::new(0, 0));
        assert!(word.is_none());
        assert!(qualified.is_none());
    }

    #[test]
    fn test_qualified_line_out_of_range() {
        let (word, qualified) = LqlBackend::get_qualified_at_position("abc", Position::new(5, 0));
        assert!(word.is_none());
        assert!(qualified.is_none());
    }

    #[test]
    fn test_qualified_at_start_of_word() {
        let (word, _) = LqlBackend::get_qualified_at_position("users", Position::new(0, 0));
        assert_eq!(word, Some("users".to_string()));
    }

    #[test]
    fn test_qualified_at_word_boundary() {
        // Position 1 in "a b" is on 'a' boundary — scans backward to find "a"
        let (word, _) = LqlBackend::get_qualified_at_position("a b", Position::new(0, 1));
        // The function scans backward from col, picks up "a"
        assert_eq!(word, Some("a".to_string()));
    }

    #[test]
    fn test_qualified_multiline() {
        let source = "line1\nusers.email";
        let (word, qualified) = LqlBackend::get_qualified_at_position(source, Position::new(1, 7));
        assert_eq!(word, Some("email".to_string()));
        assert!(qualified.is_some());
    }

    // ── compute_completion_context ──

    #[test]
    fn test_completion_context_after_pipe() {
        let ctx = LqlBackend::compute_completion_context("users |> ", Position::new(0, 9));
        assert!(ctx.after_pipe);
    }

    #[test]
    fn test_completion_context_word_prefix() {
        let ctx = LqlBackend::compute_completion_context("users |> sel", Position::new(0, 12));
        assert_eq!(ctx.word_prefix, "sel");
    }

    #[test]
    fn test_completion_context_table_qualifier() {
        let ctx = LqlBackend::compute_completion_context("users.na", Position::new(0, 8));
        assert_eq!(ctx.table_qualifier, Some("users".to_string()));
        assert_eq!(ctx.word_prefix, "na");
    }

    #[test]
    fn test_completion_context_in_arg_list() {
        let ctx = LqlBackend::compute_completion_context("select(users.id, ", Position::new(0, 17));
        assert!(ctx.in_arg_list);
    }

    #[test]
    fn test_completion_context_in_lambda() {
        let source = "filter(fn(r) => r.users.";
        let ctx = LqlBackend::compute_completion_context(source, Position::new(0, 24));
        assert!(ctx.in_lambda);
    }

    #[test]
    fn test_completion_context_empty_prefix() {
        let ctx = LqlBackend::compute_completion_context("", Position::new(0, 0));
        assert_eq!(ctx.word_prefix, "");
        assert!(!ctx.after_pipe);
        assert!(!ctx.in_arg_list);
    }

    #[test]
    fn test_completion_context_no_qualifier_without_dot() {
        let ctx = LqlBackend::compute_completion_context("sel", Position::new(0, 3));
        assert!(ctx.table_qualifier.is_none());
    }

    #[test]
    fn test_completion_context_line_prefix() {
        let ctx = LqlBackend::compute_completion_context("abc def", Position::new(0, 5));
        assert_eq!(ctx.line_prefix, "abc d");
    }

    // ── collect_diagnostics edge cases ──

    #[test]
    fn test_collect_diagnostics_info_severity() {
        // Unknown function produces Info severity
        let diags = LqlBackend::collect_diagnostics("users |> foobar(x)");
        let has_info = diags
            .iter()
            .any(|d| d.severity == Some(tower_lsp::lsp_types::DiagnosticSeverity::INFORMATION));
        assert!(has_info);
    }

    #[test]
    fn test_collect_diagnostics_empty() {
        let diags = LqlBackend::collect_diagnostics("");
        assert!(diags.is_empty());
    }

    #[test]
    fn test_collect_diagnostics_multiple_issues() {
        // Parse error + semantic issue
        let diags = LqlBackend::collect_diagnostics("users|>select(");
        assert!(diags.len() >= 2);
    }

    // ── build_scope additional ──

    #[test]
    fn test_build_scope_let_with_underscore() {
        let scope = build_scope("let my_var = stuff");
        assert!(scope.has_binding("my_var"));
    }

    #[test]
    fn test_build_scope_bare_words_no_panic() {
        // ANTLR parses bare words via error recovery — just verify no panic
        let _scope = build_scope("just a word");
    }

    #[test]
    fn test_build_scope_multiple_tables() {
        let source = "users |> join(orders |> select(orders.id), on = users.id = orders.user_id)";
        let scope = build_scope(source);
        let tables = scope.table_names();
        assert!(tables.contains(&"users"));
        assert!(tables.contains(&"orders"));
    }

    // ── get_qualified_at_position additional ──

    #[test]
    fn test_qualified_at_end_of_word() {
        let (word, _) = LqlBackend::get_qualified_at_position("users", Position::new(0, 5));
        assert_eq!(word, Some("users".to_string()));
    }

    #[test]
    fn test_qualified_dot_at_start() {
        let (word, qualified) = LqlBackend::get_qualified_at_position(".col", Position::new(0, 2));
        assert_eq!(word, Some("col".to_string()));
        // No qualifier because there's nothing before the dot
        assert!(qualified.is_none());
    }

    #[test]
    fn test_qualified_on_space_returns_none() {
        // Cursor on a space between words — start==end path
        let (word, qualified) = LqlBackend::get_qualified_at_position("a  b", Position::new(0, 2));
        assert!(word.is_none());
        assert!(qualified.is_none());
    }

    #[test]
    fn test_qualified_with_underscore() {
        let (word, qualified) =
            LqlBackend::get_qualified_at_position("my_table.my_col", Position::new(0, 12));
        assert_eq!(word, Some("my_col".to_string()));
        let (table, col) = qualified.unwrap();
        assert_eq!(table, "my_table");
        assert_eq!(col, "my_col");
    }

    // ── compute_completion_context additional ──

    #[test]
    fn test_completion_context_after_pipe_with_prefix() {
        let ctx = LqlBackend::compute_completion_context("users |> sel", Position::new(0, 12));
        assert!(ctx.after_pipe);
        assert_eq!(ctx.word_prefix, "sel");
    }

    #[test]
    fn test_completion_context_multiline() {
        let source = "let x = users\n|> sel";
        let ctx = LqlBackend::compute_completion_context(source, Position::new(1, 5));
        // "|> sel" at col 5 -> prefix is "se" (0-indexed col means chars 0..5 = "|> se")
        assert_eq!(ctx.word_prefix, "se");
        assert!(ctx.after_pipe);
    }

    #[test]
    fn test_completion_context_not_in_arg_list() {
        let ctx = LqlBackend::compute_completion_context("users |> ", Position::new(0, 9));
        assert!(!ctx.in_arg_list);
    }

    #[test]
    fn test_completion_context_bare_dot_no_qualifier() {
        // Just a dot with nothing before it — q.is_empty() path
        let ctx = LqlBackend::compute_completion_context(".", Position::new(0, 1));
        assert!(ctx.table_qualifier.is_none());
        assert_eq!(ctx.word_prefix, "");
    }

    #[test]
    fn test_completion_context_dot_only() {
        let ctx = LqlBackend::compute_completion_context("users.", Position::new(0, 6));
        assert_eq!(ctx.table_qualifier, Some("users".to_string()));
        assert_eq!(ctx.word_prefix, "");
    }

    #[test]
    fn test_completion_context_col_past_end_clamped() {
        // Column past end of line should be clamped
        let ctx = LqlBackend::compute_completion_context("abc", Position::new(0, 100));
        assert_eq!(ctx.line_prefix, "abc");
    }

    #[test]
    fn test_completion_context_not_lambda_without_arrow() {
        let ctx = LqlBackend::compute_completion_context("select(x, y)", Position::new(0, 10));
        assert!(!ctx.in_lambda);
    }

    // ── format_lql additional ──

    #[test]
    fn test_format_nested_parens() {
        let source = "a(\nb(\nc\n)\n)";
        let result = format_lql(source);
        let lines: Vec<&str> = result.lines().collect();
        assert_eq!(lines[0], "a(");
        assert_eq!(lines[1], "    b(");
        assert_eq!(lines[2], "        c");
        assert_eq!(lines[3], "    )");
        assert_eq!(lines[4], ")");
    }

    #[test]
    fn test_format_multiline_pipeline() {
        let source = "users\n|> select(users.id)\n|> limit(10)";
        let result = format_lql(source);
        let lines: Vec<&str> = result.lines().collect();
        assert_eq!(lines[0], "users");
        assert!(lines[1].starts_with("    |>"));
        assert!(lines[2].starts_with("    |>"));
    }

    // ── LanguageServer integration tests ──

    #[tokio::test]
    async fn test_lsp_initialize() {
        let (service, _socket) = LspService::new(LqlBackend::new);
        let result = service
            .inner()
            .initialize(InitializeParams::default())
            .await
            .unwrap();
        assert!(result.capabilities.completion_provider.is_some());
        assert!(result.capabilities.hover_provider.is_some());
        assert!(result.capabilities.document_symbol_provider.is_some());
        assert!(result.capabilities.document_formatting_provider.is_some());
    }

    #[tokio::test]
    async fn test_lsp_shutdown() {
        let (service, _socket) = LspService::new(LqlBackend::new);
        let result = service.inner().shutdown().await;
        assert!(result.is_ok());
    }

    #[tokio::test]
    async fn test_lsp_completion_no_document() {
        let (service, _socket) = LspService::new(LqlBackend::new);
        let params = CompletionParams {
            text_document_position: TextDocumentPositionParams {
                text_document: TextDocumentIdentifier {
                    uri: Url::parse("file:///test.lql").unwrap(),
                },
                position: Position::new(0, 0),
            },
            work_done_progress_params: Default::default(),
            partial_result_params: Default::default(),
            context: None,
        };
        let result = service.inner().completion(params).await.unwrap();
        assert!(result.is_none());
    }

    #[tokio::test]
    async fn test_lsp_hover_no_document() {
        let (service, _socket) = LspService::new(LqlBackend::new);
        let params = HoverParams {
            text_document_position_params: TextDocumentPositionParams {
                text_document: TextDocumentIdentifier {
                    uri: Url::parse("file:///test.lql").unwrap(),
                },
                position: Position::new(0, 0),
            },
            work_done_progress_params: Default::default(),
        };
        let result = service.inner().hover(params).await.unwrap();
        assert!(result.is_none());
    }

    #[tokio::test]
    async fn test_lsp_document_symbol_no_document() {
        let (service, _socket) = LspService::new(LqlBackend::new);
        let params = DocumentSymbolParams {
            text_document: TextDocumentIdentifier {
                uri: Url::parse("file:///test.lql").unwrap(),
            },
            work_done_progress_params: Default::default(),
            partial_result_params: Default::default(),
        };
        let result = service.inner().document_symbol(params).await.unwrap();
        assert!(result.is_none());
    }

    #[tokio::test]
    async fn test_lsp_formatting_no_document() {
        let (service, _socket) = LspService::new(LqlBackend::new);
        let params = DocumentFormattingParams {
            text_document: TextDocumentIdentifier {
                uri: Url::parse("file:///test.lql").unwrap(),
            },
            options: FormattingOptions {
                tab_size: 4,
                insert_spaces: true,
                ..Default::default()
            },
            work_done_progress_params: Default::default(),
        };
        let result = service.inner().formatting(params).await.unwrap();
        assert!(result.is_none());
    }

    #[tokio::test]
    async fn test_lsp_did_open_and_completion() {
        let (service, _socket) = LspService::new(LqlBackend::new);
        let uri = Url::parse("file:///test.lql").unwrap();

        // Open a document
        service
            .inner()
            .did_open(DidOpenTextDocumentParams {
                text_document: TextDocumentItem {
                    uri: uri.clone(),
                    language_id: "lql".to_string(),
                    version: 1,
                    text: "users |> ".to_string(),
                },
            })
            .await;

        // Request completions
        let params = CompletionParams {
            text_document_position: TextDocumentPositionParams {
                text_document: TextDocumentIdentifier { uri: uri.clone() },
                position: Position::new(0, 9),
            },
            work_done_progress_params: Default::default(),
            partial_result_params: Default::default(),
            context: None,
        };
        let result = service.inner().completion(params).await.unwrap();
        assert!(result.is_some());
        if let Some(CompletionResponse::Array(items)) = result {
            assert!(!items.is_empty());
            let labels: Vec<&str> = items.iter().map(|i| i.label.as_str()).collect();
            assert!(labels.contains(&"select"));
        }
    }

    #[tokio::test]
    async fn test_lsp_did_open_and_hover() {
        let (service, _socket) = LspService::new(LqlBackend::new);
        let uri = Url::parse("file:///test.lql").unwrap();

        service
            .inner()
            .did_open(DidOpenTextDocumentParams {
                text_document: TextDocumentItem {
                    uri: uri.clone(),
                    language_id: "lql".to_string(),
                    version: 1,
                    text: "select".to_string(),
                },
            })
            .await;

        let params = HoverParams {
            text_document_position_params: TextDocumentPositionParams {
                text_document: TextDocumentIdentifier { uri: uri.clone() },
                position: Position::new(0, 3),
            },
            work_done_progress_params: Default::default(),
        };
        let result = service.inner().hover(params).await.unwrap();
        assert!(result.is_some());
        let hover = result.unwrap();
        if let HoverContents::Markup(content) = hover.contents {
            assert!(content.value.contains("select"));
        }
    }

    #[tokio::test]
    async fn test_lsp_did_open_and_symbols() {
        let (service, _socket) = LspService::new(LqlBackend::new);
        let uri = Url::parse("file:///test.lql").unwrap();

        service
            .inner()
            .did_open(DidOpenTextDocumentParams {
                text_document: TextDocumentItem {
                    uri: uri.clone(),
                    language_id: "lql".to_string(),
                    version: 1,
                    text: "let x = users\nlet y = orders".to_string(),
                },
            })
            .await;

        let params = DocumentSymbolParams {
            text_document: TextDocumentIdentifier { uri: uri.clone() },
            work_done_progress_params: Default::default(),
            partial_result_params: Default::default(),
        };
        let result = service.inner().document_symbol(params).await.unwrap();
        assert!(result.is_some());
        if let Some(DocumentSymbolResponse::Flat(symbols)) = result {
            assert_eq!(symbols.len(), 2);
            assert_eq!(symbols[0].name, "x");
            assert_eq!(symbols[1].name, "y");
        }
    }

    #[tokio::test]
    async fn test_lsp_did_change() {
        let (service, _socket) = LspService::new(LqlBackend::new);
        let uri = Url::parse("file:///test.lql").unwrap();

        service
            .inner()
            .did_open(DidOpenTextDocumentParams {
                text_document: TextDocumentItem {
                    uri: uri.clone(),
                    language_id: "lql".to_string(),
                    version: 1,
                    text: "users".to_string(),
                },
            })
            .await;

        service
            .inner()
            .did_change(DidChangeTextDocumentParams {
                text_document: VersionedTextDocumentIdentifier {
                    uri: uri.clone(),
                    version: 2,
                },
                content_changes: vec![TextDocumentContentChangeEvent {
                    range: None,
                    range_length: None,
                    text: "let x = users |> select(users.id)".to_string(),
                }],
            })
            .await;

        // Verify the change was applied by getting symbols
        let params = DocumentSymbolParams {
            text_document: TextDocumentIdentifier { uri: uri.clone() },
            work_done_progress_params: Default::default(),
            partial_result_params: Default::default(),
        };
        let result = service.inner().document_symbol(params).await.unwrap();
        if let Some(DocumentSymbolResponse::Flat(symbols)) = result {
            assert_eq!(symbols.len(), 1);
            assert_eq!(symbols[0].name, "x");
        }
    }

    #[tokio::test]
    async fn test_lsp_did_close() {
        let (service, _socket) = LspService::new(LqlBackend::new);
        let uri = Url::parse("file:///test.lql").unwrap();

        service
            .inner()
            .did_open(DidOpenTextDocumentParams {
                text_document: TextDocumentItem {
                    uri: uri.clone(),
                    language_id: "lql".to_string(),
                    version: 1,
                    text: "users".to_string(),
                },
            })
            .await;

        service
            .inner()
            .did_close(DidCloseTextDocumentParams {
                text_document: TextDocumentIdentifier { uri: uri.clone() },
            })
            .await;

        // After close, hover should return None
        let params = HoverParams {
            text_document_position_params: TextDocumentPositionParams {
                text_document: TextDocumentIdentifier { uri },
                position: Position::new(0, 0),
            },
            work_done_progress_params: Default::default(),
        };
        let result = service.inner().hover(params).await.unwrap();
        assert!(result.is_none());
    }

    #[tokio::test]
    async fn test_lsp_formatting() {
        let (service, _socket) = LspService::new(LqlBackend::new);
        let uri = Url::parse("file:///test.lql").unwrap();

        service
            .inner()
            .did_open(DidOpenTextDocumentParams {
                text_document: TextDocumentItem {
                    uri: uri.clone(),
                    language_id: "lql".to_string(),
                    version: 1,
                    text: "users\n|> select(\nusers.id\n)".to_string(),
                },
            })
            .await;

        let params = DocumentFormattingParams {
            text_document: TextDocumentIdentifier { uri },
            options: FormattingOptions {
                tab_size: 4,
                insert_spaces: true,
                ..Default::default()
            },
            work_done_progress_params: Default::default(),
        };
        let result = service.inner().formatting(params).await.unwrap();
        assert!(result.is_some());
        let edits = result.unwrap();
        assert_eq!(edits.len(), 1);
        assert!(edits[0].new_text.contains("    |> select("));
    }

    #[tokio::test]
    async fn test_lsp_formatting_no_change() {
        let (service, _socket) = LspService::new(LqlBackend::new);
        let uri = Url::parse("file:///test.lql").unwrap();

        service
            .inner()
            .did_open(DidOpenTextDocumentParams {
                text_document: TextDocumentItem {
                    uri: uri.clone(),
                    language_id: "lql".to_string(),
                    version: 1,
                    text: "users\n".to_string(),
                },
            })
            .await;

        let params = DocumentFormattingParams {
            text_document: TextDocumentIdentifier { uri },
            options: FormattingOptions {
                tab_size: 4,
                insert_spaces: true,
                ..Default::default()
            },
            work_done_progress_params: Default::default(),
        };
        let result = service.inner().formatting(params).await.unwrap();
        assert!(result.is_none());
    }

    #[tokio::test]
    async fn test_lsp_hover_on_unknown_word() {
        let (service, _socket) = LspService::new(LqlBackend::new);
        let uri = Url::parse("file:///test.lql").unwrap();

        service
            .inner()
            .did_open(DidOpenTextDocumentParams {
                text_document: TextDocumentItem {
                    uri: uri.clone(),
                    language_id: "lql".to_string(),
                    version: 1,
                    text: "zzz_unknown".to_string(),
                },
            })
            .await;

        let params = HoverParams {
            text_document_position_params: TextDocumentPositionParams {
                text_document: TextDocumentIdentifier { uri },
                position: Position::new(0, 3),
            },
            work_done_progress_params: Default::default(),
        };
        let result = service.inner().hover(params).await.unwrap();
        assert!(result.is_none());
    }

    #[tokio::test]
    async fn test_lsp_completion_with_kind_mapping() {
        let (service, _socket) = LspService::new(LqlBackend::new);
        let uri = Url::parse("file:///test.lql").unwrap();

        service
            .inner()
            .did_open(DidOpenTextDocumentParams {
                text_document: TextDocumentItem {
                    uri: uri.clone(),
                    language_id: "lql".to_string(),
                    version: 1,
                    text: "users |> ".to_string(),
                },
            })
            .await;

        let params = CompletionParams {
            text_document_position: TextDocumentPositionParams {
                text_document: TextDocumentIdentifier { uri },
                position: Position::new(0, 9),
            },
            work_done_progress_params: Default::default(),
            partial_result_params: Default::default(),
            context: None,
        };
        let result = service.inner().completion(params).await.unwrap().unwrap();
        if let CompletionResponse::Array(items) = result {
            // Check that kind mapping works
            let select_item = items.iter().find(|i| i.label == "select").unwrap();
            assert_eq!(select_item.kind, Some(CompletionItemKind::FUNCTION));
            let let_item = items.iter().find(|i| i.label == "let").unwrap();
            assert_eq!(let_item.kind, Some(CompletionItemKind::KEYWORD));
        }
    }

    #[tokio::test]
    async fn test_lsp_initialize_with_options() {
        let (service, _socket) = LspService::new(LqlBackend::new);
        let params = InitializeParams {
            initialization_options: Some(serde_json::json!({
                "connectionString": "host=localhost dbname=test",
                "aiProvider": {
                    "provider": "test",
                    "endpoint": "http://localhost",
                    "enabled": true
                }
            })),
            ..Default::default()
        };
        let result = service.inner().initialize(params).await.unwrap();
        assert!(result.capabilities.completion_provider.is_some());
    }

    #[tokio::test]
    async fn test_lsp_set_ai_provider() {
        let (service, _socket) = LspService::new(LqlBackend::new);
        let provider = Arc::new(ai::TestAiProvider);
        service.inner().set_ai_provider(provider).await;
        // Verify it was set by checking AI provider is Some
        let ai = service.inner().ai_provider.read().await;
        assert!(ai.is_some());
    }

    #[tokio::test]
    async fn test_lsp_completion_with_scope_bindings() {
        let (service, _socket) = LspService::new(LqlBackend::new);
        let uri = Url::parse("file:///test.lql").unwrap();

        service
            .inner()
            .did_open(DidOpenTextDocumentParams {
                text_document: TextDocumentItem {
                    uri: uri.clone(),
                    language_id: "lql".to_string(),
                    version: 1,
                    text: "let my_query = users\nmy_query |> ".to_string(),
                },
            })
            .await;

        let params = CompletionParams {
            text_document_position: TextDocumentPositionParams {
                text_document: TextDocumentIdentifier { uri },
                position: Position::new(1, 12),
            },
            work_done_progress_params: Default::default(),
            partial_result_params: Default::default(),
            context: None,
        };
        let result = service.inner().completion(params).await.unwrap().unwrap();
        if let CompletionResponse::Array(items) = result {
            let labels: Vec<&str> = items.iter().map(|i| i.label.as_str()).collect();
            // Variable binding should be in completions
            assert!(labels.contains(&"my_query"));
        }
    }

    // ── Parser token/span integration ─────────────────────────────────

    #[test]
    fn test_lex_tokens_pipeline() {
        use lql_parser::tokens::{lex_tokens, token_types};
        let tokens = lex_tokens("users |> select(users.id)");
        assert!(!tokens.is_empty());
        let pipes: Vec<_> = tokens
            .iter()
            .filter(|t| t.token_type == token_types::PIPE)
            .collect();
        assert_eq!(pipes.len(), 1);
    }

    #[test]
    fn test_lex_tokens_multiline() {
        use lql_parser::tokens::{lex_tokens, token_types};
        let tokens = lex_tokens("users\n|> select(x)\n|> limit(10)");
        let pipes: Vec<_> = tokens
            .iter()
            .filter(|t| t.token_type == token_types::PIPE)
            .collect();
        assert_eq!(pipes.len(), 2);
        assert_eq!(pipes[0].line, 1);
        assert_eq!(pipes[1].line, 2);
    }

    #[test]
    fn test_lex_tokens_parens_and_idents() {
        use lql_parser::tokens::{lex_tokens, token_types};
        let tokens = lex_tokens("select(x, y)");
        let opens: Vec<_> = tokens
            .iter()
            .filter(|t| t.token_type == token_types::OPEN_PAREN)
            .collect();
        assert_eq!(opens.len(), 1);
        let closes: Vec<_> = tokens
            .iter()
            .filter(|t| t.token_type == token_types::CLOSE_PAREN)
            .collect();
        assert_eq!(closes.len(), 1);
    }

    #[test]
    fn test_lex_tokens_empty() {
        use lql_parser::tokens::lex_tokens;
        let tokens = lex_tokens("");
        assert!(tokens.is_empty());
    }

    #[test]
    fn test_lex_pipe_positions() {
        use lql_parser::tokens::lex_pipe_positions;
        let positions = lex_pipe_positions("users |> select(x) |> limit(10)");
        assert_eq!(positions.len(), 2);
    }

    #[test]
    fn test_lex_pipe_positions_multiline() {
        use lql_parser::tokens::lex_pipe_positions;
        let positions = lex_pipe_positions("users\n|> a\n|> b");
        assert_eq!(positions.len(), 2);
        assert_eq!(positions[0].line, 1);
        assert_eq!(positions[1].line, 2);
    }

    #[test]
    fn test_get_token_structure_complex() {
        use lql_parser::tokens::get_token_structure;
        let source = "-- query\nusers\n|> select(\n  users.id\n)\n|> limit(10)";
        let ts = get_token_structure(source);
        assert_eq!(ts.comment_lines, vec![0]);
        assert_eq!(ts.pipe_positions.len(), 2);
        assert_eq!(ts.lines_ending_open_paren, vec![2]);
        assert_eq!(ts.lines_starting_close_paren, vec![4]);
    }

    #[test]
    fn test_get_token_structure_empty() {
        use lql_parser::tokens::get_token_structure;
        let ts = get_token_structure("");
        assert!(ts.pipe_positions.is_empty());
        assert!(ts.comment_lines.is_empty());
    }

    #[test]
    fn test_get_token_structure_open_paren_with_comment() {
        use lql_parser::tokens::get_token_structure;
        let ts = get_token_structure("select( -- args follow\n  x\n)");
        assert_eq!(ts.lines_ending_open_paren, vec![0]);
        assert_eq!(ts.lines_starting_close_paren, vec![2]);
    }

    #[test]
    fn test_span_operations() {
        use lql_parser::span::Span;
        let a = Span::new(5, 10);
        let b = Span::new(2, 15);
        let merged = a.merge(b);
        assert_eq!(merged.start, 2);
        assert_eq!(merged.end, 15);

        let empty = Span::empty(42);
        assert_eq!(empty.start, 42);
        assert_eq!(empty.end, 42);
    }

    #[test]
    fn test_span_line_col() {
        use lql_parser::span::Span;
        let src = "hello\nworld";
        let span = Span::new(6, 11);
        assert_eq!(span.start_line_col(src), (1, 0));
        assert_eq!(span.end_line_col(src), (1, 5));
    }

    #[test]
    fn test_parse_error_warning() {
        use lql_parser::error::{ParseError, Severity};
        use lql_parser::span::Span;
        let err = ParseError::warning("test warning", Span::new(0, 5));
        assert_eq!(err.severity, Severity::Warning);
        assert_eq!(err.message, "test warning");
    }

    // ── Schema-aware analyzer integration ─────────────────────────────

    #[test]
    fn test_schema_cache_operations() {
        use lql_analyzer::{ColumnInfo, SchemaCache, TableInfo};
        let cache = SchemaCache::from_tables(vec![TableInfo {
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
        }]);
        assert!(!cache.is_empty());
        assert_eq!(cache.table_count(), 1);
        let names = cache.table_names();
        assert!(names.contains(&"users"));

        let table = cache.get_table("users").unwrap();
        assert_eq!(table.columns.len(), 2);
        let col = table.get_column("id").unwrap();
        assert!(col.is_primary_key);
        let pks = table.primary_key_columns();
        assert_eq!(pks.len(), 1);

        let cols = cache.get_columns("users");
        assert_eq!(cols.len(), 2);
        let empty_cols = cache.get_columns("nonexistent");
        assert!(empty_cols.is_empty());

        assert!(cache.age().is_some());
        assert!(!cache.is_stale(std::time::Duration::from_secs(3600)));

        let default_cache = SchemaCache::default();
        assert!(default_cache.is_empty());
        assert!(default_cache.is_stale(std::time::Duration::from_secs(1)));
        assert!(default_cache.age().is_none());
    }

    #[test]
    fn test_column_type_description() {
        use lql_analyzer::ColumnInfo;
        let pk_col = ColumnInfo {
            name: "id".to_string(),
            sql_type: "uuid".to_string(),
            is_nullable: false,
            is_primary_key: true,
        };
        assert_eq!(pk_col.type_description(), "uuid (PK) NOT NULL");

        let nullable_col = ColumnInfo {
            name: "email".to_string(),
            sql_type: "text".to_string(),
            is_nullable: true,
            is_primary_key: false,
        };
        assert_eq!(nullable_col.type_description(), "text");
    }

    #[test]
    fn test_completions_with_schema_table_qualifier() {
        use lql_analyzer::ScopeMap;
        use lql_analyzer::{
            get_completions, ColumnInfo, CompletionContext, CompletionKind, SchemaCache, TableInfo,
        };
        let schema = SchemaCache::from_tables(vec![TableInfo {
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
        }]);
        let ctx = CompletionContext {
            line_prefix: "users.".to_string(),
            in_arg_list: false,
            after_pipe: false,
            in_lambda: false,
            word_prefix: "".to_string(),
            table_qualifier: Some("users".to_string()),
        };
        let scope = ScopeMap::new();
        let items = get_completions(&ctx, &scope, Some(&schema));
        assert_eq!(items.len(), 2);
        assert!(items.iter().all(|i| i.kind == CompletionKind::Column));
    }

    #[test]
    fn test_completions_schema_table_suggestions() {
        use lql_analyzer::ScopeMap;
        use lql_analyzer::{
            get_completions, ColumnInfo, CompletionContext, CompletionKind, SchemaCache, TableInfo,
        };
        let schema = SchemaCache::from_tables(vec![
            TableInfo {
                name: "users".to_string(),
                schema: "public".to_string(),
                columns: vec![ColumnInfo {
                    name: "id".to_string(),
                    sql_type: "uuid".to_string(),
                    is_nullable: false,
                    is_primary_key: true,
                }],
            },
            TableInfo {
                name: "orders".to_string(),
                schema: "public".to_string(),
                columns: vec![ColumnInfo {
                    name: "id".to_string(),
                    sql_type: "uuid".to_string(),
                    is_nullable: false,
                    is_primary_key: true,
                }],
            },
        ]);
        let ctx = CompletionContext {
            line_prefix: "".to_string(),
            in_arg_list: false,
            after_pipe: false,
            in_lambda: false,
            word_prefix: "".to_string(),
            table_qualifier: None,
        };
        let scope = ScopeMap::new();
        let items = get_completions(&ctx, &scope, Some(&schema));
        let tables: Vec<_> = items
            .iter()
            .filter(|i| i.kind == CompletionKind::Table)
            .collect();
        assert!(tables.len() >= 2);
    }

    #[test]
    fn test_hover_with_schema() {
        use lql_analyzer::{get_hover_with_schema, ColumnInfo, SchemaCache, TableInfo};
        let schema = SchemaCache::from_tables(vec![TableInfo {
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
        }]);

        // Qualified column hover
        let info = get_hover_with_schema("id", Some(("users", "id")), Some(&schema));
        assert!(info.is_some());
        let h = info.unwrap();
        assert!(h.title.contains("users.id"));

        // Table hover
        let info = get_hover_with_schema("users", None, Some(&schema));
        assert!(info.is_some());
        let h = info.unwrap();
        assert!(h.title.contains("Table"));

        // Column not found
        let info = get_hover_with_schema("missing", Some(("users", "missing")), Some(&schema));
        assert!(info.is_some());
        assert!(info.unwrap().title.contains("not found"));

        // Keyword fallback
        let info = get_hover_with_schema("select", None, Some(&schema));
        assert!(info.is_some());
    }

    #[test]
    fn test_many_columns_table_completion() {
        use lql_analyzer::ScopeMap;
        use lql_analyzer::{
            get_completions, ColumnInfo, CompletionContext, SchemaCache, TableInfo,
        };
        // Table with > 6 columns to trigger truncation
        let cols: Vec<ColumnInfo> = (0..10)
            .map(|i| ColumnInfo {
                name: format!("col_{i}"),
                sql_type: "text".to_string(),
                is_nullable: true,
                is_primary_key: false,
            })
            .collect();
        let schema = SchemaCache::from_tables(vec![TableInfo {
            name: "big_table".to_string(),
            schema: "public".to_string(),
            columns: cols,
        }]);
        let ctx = CompletionContext {
            line_prefix: "".to_string(),
            in_arg_list: false,
            after_pipe: false,
            in_lambda: false,
            word_prefix: "".to_string(),
            table_qualifier: None,
        };
        let scope = ScopeMap::new();
        let items = get_completions(&ctx, &scope, Some(&schema));
        let table_item = items.iter().find(|i| i.label == "big_table").unwrap();
        assert!(table_item.documentation.contains("10 total"));
    }

    // ── build_ai_provider — covers the AI dispatch arms ──────────────

    fn ai_cfg(provider: &str) -> ai::AiConfig {
        ai::AiConfig {
            provider: provider.to_string(),
            endpoint: "http://localhost:11434/api/generate".to_string(),
            model: "test-model".to_string(),
            api_key: None,
            timeout_ms: 1000,
            enabled: true,
        }
    }

    #[test]
    fn build_ai_provider_test_arm() {
        let (provider, msg) = build_ai_provider(&ai_cfg("test"));
        assert!(provider.is_some(), "test arm must yield a provider");
        assert!(
            msg.contains("AI test provider"),
            "log message must mention test provider, got: {msg}"
        );
    }

    #[test]
    fn build_ai_provider_test_slow_arm() {
        let cfg = ai_cfg("test_slow");
        let (provider, msg) = build_ai_provider(&cfg);
        assert!(provider.is_some());
        assert!(msg.contains("slow test provider"), "got: {msg}");
        // delay = timeout_ms + 5000
        assert!(
            msg.contains("6000ms delay"),
            "delay should be 1000 + 5000 = 6000ms, got: {msg}"
        );
        assert!(msg.contains("timeout: 1000ms"));
    }

    #[test]
    fn build_ai_provider_ollama_arm() {
        let (provider, msg) = build_ai_provider(&ai_cfg("ollama"));
        assert!(provider.is_some(), "ollama arm must yield a provider");
        assert!(msg.contains("Ollama AI provider"), "got: {msg}");
        assert!(msg.contains("test-model"));
        assert!(msg.contains("http://localhost:11434/api/generate"));
    }

    #[test]
    fn build_ai_provider_unknown_arm_returns_none() {
        let (provider, msg) = build_ai_provider(&ai_cfg("some-other-provider"));
        assert!(
            provider.is_none(),
            "unknown provider must NOT install a built-in"
        );
        assert!(
            msg.contains("AI completion provider configured"),
            "got: {msg}"
        );
        assert!(msg.contains("some-other-provider"));
    }

    #[test]
    fn build_ai_provider_test_slow_saturating_add() {
        let cfg = ai::AiConfig {
            provider: "test_slow".to_string(),
            endpoint: String::new(),
            model: String::new(),
            api_key: None,
            timeout_ms: u64::MAX,
            enabled: true,
        };
        let (provider, msg) = build_ai_provider(&cfg);
        assert!(provider.is_some());
        // saturating_add(u64::MAX, 5000) saturates at u64::MAX
        assert!(msg.contains(&format!("{}ms delay", u64::MAX)));
    }
}
