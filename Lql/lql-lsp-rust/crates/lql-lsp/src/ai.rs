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

/// Reads a string field from `value`, falling back to `default`.
fn json_string(value: &serde_json::Value, key: &str, default: &str) -> String {
    value
        .get(key)
        .and_then(|v| v.as_str())
        .unwrap_or(default)
        .to_string()
}

impl AiConfig {
    /// Parse AI provider configuration from the `aiProvider` key in initializationOptions.
    pub fn from_json(value: &serde_json::Value) -> Option<Self> {
        let obj = value.as_object()?;
        Some(AiConfig {
            provider: obj.get("provider")?.as_str()?.to_string(),
            endpoint: obj.get("endpoint")?.as_str()?.to_string(),
            model: json_string(value, "model", "default"),
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
                let detail = json_string(item, "detail", "AI suggestion");
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

    fn empty_context() -> AiCompletionContext {
        AiCompletionContext {
            document_text: String::new(),
            line: 0,
            column: 0,
            line_prefix: String::new(),
            word_prefix: String::new(),
            file_uri: String::new(),
            available_tables: vec![],
            schema_description: String::new(),
        }
    }

    // Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
    /// Completes `provider` with an empty context and returns the items.
    async fn complete_empty(provider: &dyn AiCompletionProvider) -> Vec<CompletionItem> {
        let ctx = empty_context();
        provider.complete(&ctx).await
    }

    /// Spawns a one-shot HTTP server responding with `body` and returns its endpoint URL.
    async fn stub_endpoint(body: String) -> String {
        use tokio::{io::AsyncWriteExt, net::TcpListener};

        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let addr = listener.local_addr().unwrap();

        tokio::spawn(async move {
            if let Ok((mut stream, _)) = listener.accept().await {
                use tokio::io::AsyncReadExt;
                let mut buf = [0u8; 1024];
                let _ = stream.read(&mut buf).await;
                let resp = format!(
                    "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{}",
                    body.len(),
                    body
                );
                let _ = stream.write_all(resp.as_bytes()).await;
                let _ = stream.shutdown().await;
            }
        });

        format!("http://{addr}/api/generate")
    }

    // Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
    /// Builds an Ollama [`AiConfig`] with the given endpoint, model, and timeout.
    fn ollama_config(endpoint: &str, model: &str, timeout_ms: u64) -> AiConfig {
        AiConfig {
            provider: "ollama".to_string(),
            endpoint: endpoint.to_string(),
            model: model.to_string(),
            api_key: None,
            timeout_ms,
            enabled: true,
        }
    }

    // Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
    /// Builds an [`AiCompletionContext`] at line 0 with the given details.
    fn context(
        document_text: &str,
        column: u32,
        line_prefix: &str,
        file_uri: &str,
        available_tables: Vec<String>,
        schema_description: &str,
    ) -> AiCompletionContext {
        AiCompletionContext {
            document_text: document_text.to_string(),
            line: 0,
            column,
            line_prefix: line_prefix.to_string(),
            word_prefix: String::new(),
            file_uri: file_uri.to_string(),
            available_tables,
            schema_description: schema_description.to_string(),
        }
    }

    /// Builds an Ollama provider pointed at `endpoint` using the test defaults.
    fn ollama_provider(endpoint: String) -> OllamaProvider {
        OllamaProvider::new(&ollama_config(&endpoint, "m", 5000), "ref".to_string())
    }

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
        let ctx = context(
            "users |> select(users.id)",
            25,
            "users |> select(users.id)",
            "file:///test.lql",
            vec![],
            "",
        );
        let prompt = OllamaProvider::build_prompt(&ctx);
        crate::test_support::assert_contains_all(
            &prompt,
            &["line 1", "column 26", "users |> select(users.id)"],
        );
        assert!(!prompt.contains("Database schema"));
    }

    #[test]
    fn build_prompt_with_schema() {
        let ctx = context(
            "users |>",
            8,
            "users |>",
            "file:///test.lql",
            vec!["users".to_string()],
            "users(id uuid PK NOT NULL, name text)",
        );
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
    // Implements [LQL-RUST-NAMED-CASE-MATRICES].
    fn parse_json_with_markdown_fences_cases() {
        for &(input0, input1) in named_cases(NAMED_CASES_1) {
            verify_expanded_parse_json_with_markdown_fences(input0, input1);
        }
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
        let config = ollama_config(
            "http://localhost:11434/api/generate",
            "qwen2.5-coder:1.5b",
            2000,
        );
        let provider = OllamaProvider::new(&config, "LQL reference text".to_string());
        assert_eq!(provider.endpoint, "http://localhost:11434/api/generate");
        assert_eq!(provider.model, "qwen2.5-coder:1.5b");
        assert_eq!(provider.lql_reference, "LQL reference text");
    }

    // ── SlowAiProvider ──────────────────────────────────────────────────
    #[tokio::test]
    async fn slow_provider_returns_after_delay() {
        let provider = SlowAiProvider { delay_ms: 10 };
        let ctx = empty_context();
        let items = provider.complete(&ctx).await;
        assert_eq!(items.len(), 1);
        assert_eq!(items[0].label, "ai_slow_result");
    }

    // ── TestAiProvider ─────────────────────────────────────────────────
    #[tokio::test]
    async fn test_provider_returns_items() {
        let provider = TestAiProvider;
        let ctx = context("users |>", 8, "users |>", "file:///test.lql", vec![], "");
        let items = provider.complete(&ctx).await;
        assert!(items.len() >= 3);
        assert!(items.iter().any(|i| i.label == "ai_suggest_filter"));
        assert!(items.iter().any(|i| i.label == "ai_suggest_join"));
        assert!(items.iter().any(|i| i.label == "ai_suggest_aggregate"));
    }

    #[tokio::test]
    async fn test_provider_with_tables() {
        let provider = TestAiProvider;
        let ctx = context(
            "",
            0,
            "",
            "file:///test.lql",
            vec!["users".to_string(), "orders".to_string()],
            "",
        );
        let items = provider.complete(&ctx).await;
        assert!(items.iter().any(|i| i.label == "ai_query_users"));
        assert!(items.iter().any(|i| i.label == "ai_query_orders"));
    }

    #[tokio::test]
    async fn test_provider_with_schema_description() {
        let provider = TestAiProvider;
        let ctx = context("", 0, "", "file:///test.lql", vec![], "users(id uuid PK)");
        let items = provider.complete(&ctx).await;
        assert!(items.iter().any(|i| i.label == "ai_schema_context"));
    }

    #[tokio::test]
    async fn test_provider_with_prefix_filter() {
        let provider = TestAiProvider;
        let ctx = AiCompletionContext {
            line_prefix: "ai_s".to_string(),
            word_prefix: "ai_s".to_string(),
            file_uri: "file:///test.lql".to_string(),
            ..empty_context()
        };
        let items = provider.complete(&ctx).await;
        for item in &items {
            assert!(item.label.starts_with("ai_s"), "unexpected: {}", item.label);
        }
    }

    #[tokio::test]
    async fn test_provider_items_are_snippet_kind() {
        let provider = TestAiProvider;
        let ctx = empty_context();
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
        let provider = OllamaProvider::new(
            &ollama_config("http://127.0.0.1:1/api/generate", "test-model", 1000),
            "ref".to_string(),
        );
        let ctx = context(
            "users |> ",
            9,
            "users |> ",
            "file:///x.lql",
            vec!["users".to_string()],
            "users(id uuid PK)",
        );

        let items = provider.complete(&ctx).await;
        assert!(
            items.is_empty(),
            "unreachable endpoint must yield empty completions"
        );
    }

    #[tokio::test]
    async fn ollama_complete_returns_empty_on_non_json_body() {
        // A plain HTTP service that returns non-JSON exercises the
        // response.json() error branch.
        let endpoint = stub_endpoint("not valid json at all".to_string()).await;
        let items = complete_empty(&ollama_provider(endpoint)).await;
        assert!(items.is_empty(), "non-JSON body must yield empty");
    }

    #[tokio::test]
    async fn ollama_complete_returns_empty_when_response_field_missing() {
        // Server returns valid JSON but no "response" field.
        let endpoint =
            stub_endpoint(r#"{"other_field": "no response key here"}"#.to_string()).await;
        let items = complete_empty(&ollama_provider(endpoint)).await;
        assert!(items.is_empty());
    }

    #[tokio::test]
    async fn ollama_complete_parses_completions_when_response_field_present() {
        // Server returns Ollama-shaped JSON with a JSON-array "response" field.
        let inner = r#"[{"label":"foo","insertText":"foo()","detail":"AI"}]"#;
        let endpoint = stub_endpoint(serde_json::json!({ "response": inner }).to_string()).await;
        let provider = ollama_provider(endpoint);
        let ctx = empty_context();
        let items = provider.complete(&ctx).await;
        assert!(items.iter().any(|i| i.label == "foo"), "should parse foo");
    }

    // Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
    fn verify_expanded_parse_json_with_markdown_fences(input0: &str, input1: &str) {
        let response = input0;
        let items = OllamaProvider::parse_response(response);
        assert_eq!(items.len(), 1);
        assert_eq!(items[0].label, input1);
    }

    include!("ai_case_inputs.fixtures.rs");
    include!(concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/../../../../Tests.Shared/RustNamedCases.rs"
    ));
}
