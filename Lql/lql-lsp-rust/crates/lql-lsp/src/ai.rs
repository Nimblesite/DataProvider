use lql_analyzer::{CompletionItem, CompletionKind};

/// Context provided to AI completion providers.
/// Contains everything a language model needs to generate relevant completions.
#[derive(Debug, Clone)]
pub struct AiCompletionContext {
    /// Full document text.
    pub document_text: String,
    /// Cursor line (0-indexed).
    pub line: u32,
    /// Cursor column (0-indexed).
    pub column: u32,
    /// Text before cursor on the current line.
    pub line_prefix: String,
    /// Word prefix currently being typed (may be empty).
    pub word_prefix: String,
    /// URI of the file being edited.
    pub file_uri: String,
    /// Database table names available (from schema, if loaded).
    pub available_tables: Vec<String>,
    /// Full database schema as a compact text description for AI context.
    /// Format: "table_name(col1 type [PK] [NOT NULL], col2 type, ...)"
    pub schema_description: String,
}

/// Configuration for an AI completion provider, parsed from LSP initializationOptions.
///
/// Example initializationOptions JSON:
/// ```json
/// {
///   "connectionString": "host=localhost ...",
///   "aiProvider": {
///     "provider": "openai",
///     "endpoint": "https://api.openai.com/v1/completions",
///     "model": "gpt-4",
///     "apiKey": "sk-...",
///     "timeoutMs": 2000,
///     "enabled": true
///   }
/// }
/// ```
#[derive(Debug, Clone)]
pub struct AiConfig {
    /// Provider type identifier (e.g., "openai", "anthropic", "ollama", "custom").
    pub provider: String,
    /// API endpoint URL.
    pub endpoint: String,
    /// Model identifier.
    pub model: String,
    /// API key (optional — some providers use other auth mechanisms).
    pub api_key: Option<String>,
    /// Maximum time to wait for AI completions in milliseconds.
    /// AI completions that exceed this are silently dropped.
    pub timeout_ms: u64,
    /// Whether AI completions are enabled.
    pub enabled: bool,
}

/// Trait for AI-powered completion providers.
///
/// Implement this to integrate custom language models for LQL autocomplete.
/// The LSP server calls this alongside schema and keyword completions, merging
/// all results. A timeout is enforced so slow AI responses never block the editor.
///
/// # Implementing a custom provider
///
/// ```rust,ignore
/// use lql_lsp::ai::{AiCompletionProvider, AiCompletionContext, AiConfig};
/// use lql_analyzer::CompletionItem;
///
/// struct MyModelProvider {
///     client: reqwest::Client,
///     config: AiConfig,
/// }
///
/// #[tower_lsp::async_trait]
/// impl AiCompletionProvider for MyModelProvider {
///     async fn complete(&self, ctx: &AiCompletionContext) -> Vec<CompletionItem> {
///         // POST to your model endpoint with ctx.document_text, ctx.line_prefix, etc.
///         // Parse response and return CompletionItems
///         vec![]
///     }
/// }
/// ```
#[tower_lsp::async_trait]
pub trait AiCompletionProvider: Send + Sync {
    /// Generate AI-powered completion suggestions.
    ///
    /// Called on every completion request alongside schema and keyword completions.
    /// Results are merged and sorted by priority. AI items should use
    /// `CompletionKind::Snippet` and `sort_priority: 6` to appear after
    /// schema-based completions.
    ///
    /// Implementations should return quickly — the LSP enforces a timeout
    /// (default 2000ms) and silently drops results that arrive too late.
    async fn complete(&self, context: &AiCompletionContext) -> Vec<CompletionItem>;
}

impl AiConfig {
    /// Parse AI provider configuration from the `aiProvider` key in initializationOptions.
    pub fn from_json(value: &serde_json::Value) -> Option<Self> {
        let obj = value.as_object()?;
        Some(AiConfig {
            provider: obj.get("provider")?.as_str()?.to_string(),
            endpoint: obj.get("endpoint")?.as_str()?.to_string(),
            model: obj
                .get("model")
                .and_then(|v| v.as_str())
                .unwrap_or("default")
                .to_string(),
            api_key: obj.get("apiKey").and_then(|v| v.as_str()).map(String::from),
            timeout_ms: obj
                .get("timeoutMs")
                .and_then(|v| v.as_u64())
                .unwrap_or(2000),
            enabled: obj.get("enabled").and_then(|v| v.as_bool()).unwrap_or(true),
        })
    }
}

/// Create an AI completion item with the standard priority for AI suggestions.
pub fn ai_completion(
    label: String,
    detail: String,
    documentation: String,
    insert_text: Option<String>,
) -> CompletionItem {
    CompletionItem {
        label,
        kind: CompletionKind::Snippet,
        detail,
        documentation,
        insert_text,
        sort_priority: 6, // After all schema/keyword completions
    }
}

/// Real Ollama-backed AI completion provider.
///
/// Calls the Ollama `/api/generate` endpoint with the LQL reference doc as system
/// context and the current editing context as the prompt. Parses the model's response
/// into completion items.
///
/// Activated when `aiProvider.provider` is `"ollama"` in initializationOptions.
pub struct OllamaProvider {
    /// HTTP client (reused across requests for connection pooling).
    client: reqwest::Client,
    /// Ollama API endpoint (e.g., `http://localhost:11434/api/generate`).
    endpoint: String,
    /// Model identifier (e.g., `qwen2.5-coder:1.5b`).
    model: String,
    /// LQL language reference injected as system context.
    lql_reference: String,
}

impl OllamaProvider {
    /// Create a new OllamaProvider from AI config and the LQL reference document.
    pub fn new(config: &AiConfig, lql_reference: String) -> Self {
        Self {
            client: reqwest::Client::new(),
            endpoint: config.endpoint.clone(),
            model: config.model.clone(),
            lql_reference,
        }
    }

    /// Build the prompt sent to the model.
    fn build_prompt(context: &AiCompletionContext) -> String {
        let schema_section = if context.schema_description.is_empty() {
            String::new()
        } else {
            format!("\nDatabase schema:\n{}\n", context.schema_description)
        };

        format!(
            "You are an LQL (Lambda Query Language) autocomplete engine.\n\
             Complete the code at line {line}, column {col}.\n\
             The user is typing: \"{prefix}\"\n\
             {schema}\n\
             Current file:\n```lql\n{doc}\n```\n\n\
             Return ONLY a JSON array of completion objects. Each object must have:\n\
             - \"label\": short display text\n\
             - \"insertText\": the text to insert\n\
             - \"detail\": one-line description\n\n\
             Return between 1 and 5 completions. Return ONLY the JSON array, no markdown fences, no explanation.",
            line = context.line + 1,
            col = context.column + 1,
            prefix = context.line_prefix,
            schema = schema_section,
            doc = context.document_text,
        )
    }

    /// Parse the model response into completion items.
    fn parse_response(response_text: &str) -> Vec<CompletionItem> {
        // Strip markdown code fences if the model wraps them
        let cleaned = response_text
            .trim()
            .trim_start_matches("```json")
            .trim_start_matches("```")
            .trim_end_matches("```")
            .trim();

        let items: Vec<serde_json::Value> = match serde_json::from_str(cleaned) {
            Ok(v) => v,
            Err(_) => return Vec::new(),
        };

        items
            .iter()
            .filter_map(|item| {
                let label = item.get("label")?.as_str()?.to_string();
                let insert_text = item
                    .get("insertText")
                    .and_then(|v| v.as_str())
                    .map(String::from);
                let detail = item
                    .get("detail")
                    .and_then(|v| v.as_str())
                    .unwrap_or("AI suggestion")
                    .to_string();
                Some(ai_completion(
                    label,
                    detail,
                    "Generated by Ollama".to_string(),
                    insert_text,
                ))
            })
            .collect()
    }
}

#[tower_lsp::async_trait]
impl AiCompletionProvider for OllamaProvider {
    async fn complete(&self, context: &AiCompletionContext) -> Vec<CompletionItem> {
        let prompt = Self::build_prompt(context);

        let body = serde_json::json!({
            "model": self.model,
            "prompt": prompt,
            "system": self.lql_reference,
            "stream": false,
            "options": {
                "temperature": 0.1,
                "num_predict": 256,
            }
        });

        let response = match self.client.post(&self.endpoint).json(&body).send().await {
            Ok(r) => r,
            Err(_) => return Vec::new(),
        };

        let json: serde_json::Value = match response.json().await {
            Ok(j) => j,
            Err(_) => return Vec::new(),
        };

        let text = match json.get("response").and_then(|v| v.as_str()) {
            Some(t) => t,
            None => return Vec::new(),
        };

        Self::parse_response(text)
    }
}

/// Built-in test AI provider that returns deterministic completions.
/// Activated when `aiProvider.provider` is `"test"` in initializationOptions.
/// This proves the full AI pipeline works end-to-end without an external service.
pub struct TestAiProvider;

#[tower_lsp::async_trait]
impl AiCompletionProvider for TestAiProvider {
    async fn complete(&self, context: &AiCompletionContext) -> Vec<CompletionItem> {
        let mut items = vec![
            ai_completion(
                "ai_suggest_filter".to_string(),
                "AI Suggestion".to_string(),
                format!(
                    "AI-generated filter suggestion based on context at line {}, col {}",
                    context.line, context.column
                ),
                Some("filter(x => x.${1:column} == ${2:value})".to_string()),
            ),
            ai_completion(
                "ai_suggest_join".to_string(),
                "AI Suggestion".to_string(),
                "AI-generated join suggestion".to_string(),
                Some("join(${1:table}, on: x => x.${2:fk} == y.${3:pk})".to_string()),
            ),
            ai_completion(
                "ai_suggest_aggregate".to_string(),
                "AI Suggestion".to_string(),
                "AI-generated aggregation pattern".to_string(),
                Some("group_by(x => x.${1:key}) |> select(g => { key: g.key, total: sum(g.${2:value}) })".to_string()),
            ),
        ];

        // Context-aware: if tables are available, suggest table-specific completions
        for table in &context.available_tables {
            items.push(ai_completion(
                format!("ai_query_{table}"),
                format!("AI: Query {table}"),
                format!("AI-generated query pattern for table '{table}'"),
                Some(format!(
                    "{table} |> filter(x => x.${{1:column}} == ${{2:value}}) |> select(x => x)"
                )),
            ));
        }

        // Surface schema description so tests can prove it flows through
        if !context.schema_description.is_empty() {
            items.push(ai_completion(
                "ai_schema_context".to_string(),
                "AI: Schema Loaded".to_string(),
                context.schema_description.clone(),
                None,
            ));
        }

        // Prefix filtering: only return items matching the word prefix
        if !context.word_prefix.is_empty() {
            let prefix = context.word_prefix.to_lowercase();
            items.retain(|item| item.label.to_lowercase().starts_with(&prefix));
        }

        items
    }
}

/// Built-in slow AI provider for testing timeout enforcement.
/// Activated when `aiProvider.provider` is `"test_slow"` in initializationOptions.
/// Sleeps for the configured duration to prove timeouts work.
pub struct SlowAiProvider {
    /// How long to sleep before returning results (milliseconds).
    pub delay_ms: u64,
}

#[tower_lsp::async_trait]
impl AiCompletionProvider for SlowAiProvider {
    async fn complete(&self, _context: &AiCompletionContext) -> Vec<CompletionItem> {
        tokio::time::sleep(std::time::Duration::from_millis(self.delay_ms)).await;
        vec![ai_completion(
            "ai_slow_result".to_string(),
            "Slow AI Result".to_string(),
            "This should never appear if timeout works".to_string(),
            None,
        )]
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    // ── AiConfig::from_json ────────────────────────────────────────────
    #[test]
    fn parse_full_config() {
        let json: serde_json::Value = serde_json::json!({
            "provider": "ollama",
            "endpoint": "http://localhost:11434/api/generate",
            "model": "qwen2.5-coder:1.5b",
            "apiKey": "sk-test",
            "timeoutMs": 3000,
            "enabled": true
        });
        let config = AiConfig::from_json(&json).unwrap();
        assert_eq!(config.provider, "ollama");
        assert_eq!(config.endpoint, "http://localhost:11434/api/generate");
        assert_eq!(config.model, "qwen2.5-coder:1.5b");
        assert_eq!(config.api_key, Some("sk-test".to_string()));
        assert_eq!(config.timeout_ms, 3000);
        assert!(config.enabled);
    }

    #[test]
    fn parse_minimal_config() {
        let json: serde_json::Value = serde_json::json!({
            "provider": "test",
            "endpoint": "http://localhost"
        });
        let config = AiConfig::from_json(&json).unwrap();
        assert_eq!(config.provider, "test");
        assert_eq!(config.model, "default");
        assert_eq!(config.api_key, None);
        assert_eq!(config.timeout_ms, 2000);
        assert!(config.enabled);
    }

    #[test]
    fn parse_disabled_config() {
        let json: serde_json::Value = serde_json::json!({
            "provider": "ollama",
            "endpoint": "http://localhost",
            "enabled": false
        });
        let config = AiConfig::from_json(&json).unwrap();
        assert!(!config.enabled);
    }

    #[test]
    fn parse_missing_provider_returns_none() {
        let json: serde_json::Value = serde_json::json!({
            "endpoint": "http://localhost"
        });
        assert!(AiConfig::from_json(&json).is_none());
    }

    #[test]
    fn parse_missing_endpoint_returns_none() {
        let json: serde_json::Value = serde_json::json!({
            "provider": "test"
        });
        assert!(AiConfig::from_json(&json).is_none());
    }

    #[test]
    fn parse_not_an_object_returns_none() {
        let json: serde_json::Value = serde_json::json!("a string");
        assert!(AiConfig::from_json(&json).is_none());
    }

    #[test]
    fn parse_null_returns_none() {
        let json: serde_json::Value = serde_json::Value::Null;
        assert!(AiConfig::from_json(&json).is_none());
    }

    // ── ai_completion ──────────────────────────────────────────────────
    #[test]
    fn ai_completion_creates_item() {
        let item = ai_completion(
            "test".to_string(),
            "detail".to_string(),
            "doc".to_string(),
            Some("insert".to_string()),
        );
        assert_eq!(item.label, "test");
        assert_eq!(item.kind, CompletionKind::Snippet);
        assert_eq!(item.detail, "detail");
        assert_eq!(item.documentation, "doc");
        assert_eq!(item.insert_text, Some("insert".to_string()));
        assert_eq!(item.sort_priority, 6);
    }

    #[test]
    fn ai_completion_without_insert_text() {
        let item = ai_completion("x".into(), "d".into(), "doc".into(), None);
        assert!(item.insert_text.is_none());
    }

    // ── OllamaProvider::build_prompt ───────────────────────────────────
    #[test]
    fn build_prompt_without_schema() {
        let ctx = AiCompletionContext {
            document_text: "users |> select(users.id)".to_string(),
            line: 0,
            column: 25,
            line_prefix: "users |> select(users.id)".to_string(),
            word_prefix: "".to_string(),
            file_uri: "file:///test.lql".to_string(),
            available_tables: vec![],
            schema_description: String::new(),
        };
        let prompt = OllamaProvider::build_prompt(&ctx);
        assert!(prompt.contains("line 1"));
        assert!(prompt.contains("column 26"));
        assert!(prompt.contains("users |> select(users.id)"));
        assert!(!prompt.contains("Database schema"));
    }

    #[test]
    fn build_prompt_with_schema() {
        let ctx = AiCompletionContext {
            document_text: "users |>".to_string(),
            line: 0,
            column: 8,
            line_prefix: "users |>".to_string(),
            word_prefix: "".to_string(),
            file_uri: "file:///test.lql".to_string(),
            available_tables: vec!["users".to_string()],
            schema_description: "users(id uuid PK NOT NULL, name text)".to_string(),
        };
        let prompt = OllamaProvider::build_prompt(&ctx);
        assert!(prompt.contains("Database schema"));
        assert!(prompt.contains("users(id uuid PK NOT NULL"));
    }

    // ── OllamaProvider::parse_response ─────────────────────────────────
    #[test]
    fn parse_valid_json_array() {
        let response =
            r#"[{"label": "select", "insertText": "select($0)", "detail": "Select columns"}]"#;
        let items = OllamaProvider::parse_response(response);
        assert_eq!(items.len(), 1);
        assert_eq!(items[0].label, "select");
        assert_eq!(items[0].insert_text, Some("select($0)".to_string()));
        assert_eq!(items[0].detail, "Select columns");
    }

    #[test]
    fn parse_json_with_markdown_fences() {
        let response = "```json\n[{\"label\": \"filter\", \"insertText\": \"filter($0)\"}]\n```";
        let items = OllamaProvider::parse_response(response);
        assert_eq!(items.len(), 1);
        assert_eq!(items[0].label, "filter");
    }

    #[test]
    fn parse_json_with_plain_fences() {
        let response = "```\n[{\"label\": \"join\"}]\n```";
        let items = OllamaProvider::parse_response(response);
        assert_eq!(items.len(), 1);
        assert_eq!(items[0].label, "join");
    }

    #[test]
    fn parse_empty_array() {
        let items = OllamaProvider::parse_response("[]");
        assert!(items.is_empty());
    }

    #[test]
    fn parse_invalid_json() {
        let items = OllamaProvider::parse_response("not json at all");
        assert!(items.is_empty());
    }

    #[test]
    fn parse_missing_label_skipped() {
        let response = r#"[{"insertText": "foo"}, {"label": "valid"}]"#;
        let items = OllamaProvider::parse_response(response);
        assert_eq!(items.len(), 1);
        assert_eq!(items[0].label, "valid");
    }

    #[test]
    fn parse_missing_detail_defaults() {
        let response = r#"[{"label": "test"}]"#;
        let items = OllamaProvider::parse_response(response);
        assert_eq!(items[0].detail, "AI suggestion");
    }

    #[test]
    fn parse_multiple_items() {
        let response = r#"[
            {"label": "a", "insertText": "a()"},
            {"label": "b", "insertText": "b()"},
            {"label": "c", "detail": "third"}
        ]"#;
        let items = OllamaProvider::parse_response(response);
        assert_eq!(items.len(), 3);
        assert_eq!(items[2].detail, "third");
    }

    // ── OllamaProvider::new ────────────────────────────────────────────
    #[test]
    fn ollama_provider_new_stores_fields() {
        let config = AiConfig {
            provider: "ollama".to_string(),
            endpoint: "http://localhost:11434/api/generate".to_string(),
            model: "qwen2.5-coder:1.5b".to_string(),
            api_key: None,
            timeout_ms: 2000,
            enabled: true,
        };
        let provider = OllamaProvider::new(&config, "LQL reference text".to_string());
        assert_eq!(provider.endpoint, "http://localhost:11434/api/generate");
        assert_eq!(provider.model, "qwen2.5-coder:1.5b");
        assert_eq!(provider.lql_reference, "LQL reference text");
    }

    // ── SlowAiProvider ──────────────────────────────────────────────────
    #[tokio::test]
    async fn slow_provider_returns_after_delay() {
        let provider = SlowAiProvider { delay_ms: 10 };
        let ctx = AiCompletionContext {
            document_text: "".to_string(),
            line: 0,
            column: 0,
            line_prefix: "".to_string(),
            word_prefix: "".to_string(),
            file_uri: "".to_string(),
            available_tables: vec![],
            schema_description: String::new(),
        };
        let items = provider.complete(&ctx).await;
        assert_eq!(items.len(), 1);
        assert_eq!(items[0].label, "ai_slow_result");
    }

    // ── TestAiProvider ─────────────────────────────────────────────────
    #[tokio::test]
    async fn test_provider_returns_items() {
        let provider = TestAiProvider;
        let ctx = AiCompletionContext {
            document_text: "users |>".to_string(),
            line: 0,
            column: 8,
            line_prefix: "users |>".to_string(),
            word_prefix: "".to_string(),
            file_uri: "file:///test.lql".to_string(),
            available_tables: vec![],
            schema_description: String::new(),
        };
        let items = provider.complete(&ctx).await;
        assert!(items.len() >= 3);
        assert!(items.iter().any(|i| i.label == "ai_suggest_filter"));
        assert!(items.iter().any(|i| i.label == "ai_suggest_join"));
        assert!(items.iter().any(|i| i.label == "ai_suggest_aggregate"));
    }

    #[tokio::test]
    async fn test_provider_with_tables() {
        let provider = TestAiProvider;
        let ctx = AiCompletionContext {
            document_text: "".to_string(),
            line: 0,
            column: 0,
            line_prefix: "".to_string(),
            word_prefix: "".to_string(),
            file_uri: "file:///test.lql".to_string(),
            available_tables: vec!["users".to_string(), "orders".to_string()],
            schema_description: String::new(),
        };
        let items = provider.complete(&ctx).await;
        assert!(items.iter().any(|i| i.label == "ai_query_users"));
        assert!(items.iter().any(|i| i.label == "ai_query_orders"));
    }

    #[tokio::test]
    async fn test_provider_with_schema_description() {
        let provider = TestAiProvider;
        let ctx = AiCompletionContext {
            document_text: "".to_string(),
            line: 0,
            column: 0,
            line_prefix: "".to_string(),
            word_prefix: "".to_string(),
            file_uri: "file:///test.lql".to_string(),
            available_tables: vec![],
            schema_description: "users(id uuid PK)".to_string(),
        };
        let items = provider.complete(&ctx).await;
        assert!(items.iter().any(|i| i.label == "ai_schema_context"));
    }

    #[tokio::test]
    async fn test_provider_with_prefix_filter() {
        let provider = TestAiProvider;
        let ctx = AiCompletionContext {
            document_text: "".to_string(),
            line: 0,
            column: 0,
            line_prefix: "ai_s".to_string(),
            word_prefix: "ai_s".to_string(),
            file_uri: "file:///test.lql".to_string(),
            available_tables: vec![],
            schema_description: String::new(),
        };
        let items = provider.complete(&ctx).await;
        for item in &items {
            assert!(item.label.starts_with("ai_s"), "unexpected: {}", item.label);
        }
    }

    #[tokio::test]
    async fn test_provider_items_are_snippet_kind() {
        let provider = TestAiProvider;
        let ctx = AiCompletionContext {
            document_text: "".to_string(),
            line: 0,
            column: 0,
            line_prefix: "".to_string(),
            word_prefix: "".to_string(),
            file_uri: "".to_string(),
            available_tables: vec![],
            schema_description: String::new(),
        };
        let items = provider.complete(&ctx).await;
        for item in &items {
            assert_eq!(item.kind, CompletionKind::Snippet);
            assert_eq!(item.sort_priority, 6);
        }
    }

    // ── OllamaProvider::complete via unreachable endpoint ──────────────

    #[tokio::test]
    async fn ollama_complete_returns_empty_on_connection_failure() {
        // Port 1 (tcpmux) is reserved and refuses connections — this exercises
        // the request-error branch at line 250-251 of ai.rs without depending
        // on any external service.
        let config = AiConfig {
            provider: "ollama".to_string(),
            endpoint: "http://127.0.0.1:1/api/generate".to_string(),
            model: "test-model".to_string(),
            api_key: None,
            timeout_ms: 1000,
            enabled: true,
        };
        let provider = OllamaProvider::new(&config, "ref".to_string());
        let ctx = AiCompletionContext {
            document_text: "users |> ".to_string(),
            line: 0,
            column: 9,
            line_prefix: "users |> ".to_string(),
            word_prefix: "".to_string(),
            file_uri: "file:///x.lql".to_string(),
            available_tables: vec!["users".to_string()],
            schema_description: "users(id uuid PK)".to_string(),
        };

        let items = provider.complete(&ctx).await;
        assert!(
            items.is_empty(),
            "unreachable endpoint must yield empty completions"
        );
    }

    #[tokio::test]
    async fn ollama_complete_returns_empty_on_non_json_body() {
        // Hit a plain HTTP service that returns non-JSON — this exercises
        // the response.json() error branch (line 254-256). Use httpbin's
        // /html endpoint via a local stub: spin up a one-shot tokio server
        // that returns text/plain. Avoids external network dependence by
        // binding to 127.0.0.1:0 and serving a minimal HTTP response.
        use tokio::io::AsyncWriteExt;
        use tokio::net::TcpListener;

        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let addr = listener.local_addr().unwrap();
        let endpoint = format!("http://{addr}/api/generate");

        tokio::spawn(async move {
            if let Ok((mut stream, _)) = listener.accept().await {
                // Drain request headers (don't bother parsing the body)
                let mut buf = [0u8; 1024];
                use tokio::io::AsyncReadExt;
                let _ = stream.read(&mut buf).await;
                let body = "not valid json at all";
                let resp = format!(
                    "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{}",
                    body.len(),
                    body
                );
                let _ = stream.write_all(resp.as_bytes()).await;
                let _ = stream.shutdown().await;
            }
        });

        let config = AiConfig {
            provider: "ollama".to_string(),
            endpoint,
            model: "m".to_string(),
            api_key: None,
            timeout_ms: 5000,
            enabled: true,
        };
        let provider = OllamaProvider::new(&config, "ref".to_string());
        let ctx = AiCompletionContext {
            document_text: String::new(),
            line: 0,
            column: 0,
            line_prefix: String::new(),
            word_prefix: String::new(),
            file_uri: String::new(),
            available_tables: vec![],
            schema_description: String::new(),
        };
        let items = provider.complete(&ctx).await;
        assert!(items.is_empty(), "non-JSON body must yield empty");
    }

    #[tokio::test]
    async fn ollama_complete_returns_empty_when_response_field_missing() {
        // Server returns valid JSON but no "response" field — exercises
        // the response-field-missing branch (line 259-261).
        use tokio::io::AsyncWriteExt;
        use tokio::net::TcpListener;

        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let addr = listener.local_addr().unwrap();
        let endpoint = format!("http://{addr}/api/generate");

        tokio::spawn(async move {
            if let Ok((mut stream, _)) = listener.accept().await {
                use tokio::io::AsyncReadExt;
                let mut buf = [0u8; 1024];
                let _ = stream.read(&mut buf).await;
                let body = r#"{"other_field": "no response key here"}"#;
                let resp = format!(
                    "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{}",
                    body.len(),
                    body
                );
                let _ = stream.write_all(resp.as_bytes()).await;
                let _ = stream.shutdown().await;
            }
        });

        let config = AiConfig {
            provider: "ollama".to_string(),
            endpoint,
            model: "m".to_string(),
            api_key: None,
            timeout_ms: 5000,
            enabled: true,
        };
        let provider = OllamaProvider::new(&config, "ref".to_string());
        let ctx = AiCompletionContext {
            document_text: String::new(),
            line: 0,
            column: 0,
            line_prefix: String::new(),
            word_prefix: String::new(),
            file_uri: String::new(),
            available_tables: vec![],
            schema_description: String::new(),
        };
        let items = provider.complete(&ctx).await;
        assert!(items.is_empty());
    }

    #[tokio::test]
    async fn ollama_complete_parses_completions_when_response_field_present() {
        // Server returns Ollama-shaped JSON with a "response" field that is
        // a JSON-array string of completions — covers the success path
        // (line 264 -> parse_response).
        use tokio::io::AsyncWriteExt;
        use tokio::net::TcpListener;

        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let addr = listener.local_addr().unwrap();
        let endpoint = format!("http://{addr}/api/generate");

        tokio::spawn(async move {
            if let Ok((mut stream, _)) = listener.accept().await {
                use tokio::io::AsyncReadExt;
                let mut buf = [0u8; 1024];
                let _ = stream.read(&mut buf).await;
                let inner = r#"[{"label":"foo","insertText":"foo()","detail":"AI"}]"#;
                let body = serde_json::json!({ "response": inner }).to_string();
                let resp = format!(
                    "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{}",
                    body.len(),
                    body
                );
                let _ = stream.write_all(resp.as_bytes()).await;
                let _ = stream.shutdown().await;
            }
        });

        let config = AiConfig {
            provider: "ollama".to_string(),
            endpoint,
            model: "m".to_string(),
            api_key: None,
            timeout_ms: 5000,
            enabled: true,
        };
        let provider = OllamaProvider::new(&config, "ref".to_string());
        let ctx = AiCompletionContext {
            document_text: String::new(),
            line: 0,
            column: 0,
            line_prefix: String::new(),
            word_prefix: String::new(),
            file_uri: String::new(),
            available_tables: vec![],
            schema_description: String::new(),
        };
        let items = provider.complete(&ctx).await;
        assert!(items.iter().any(|i| i.label == "foo"), "should parse foo");
    }
}
