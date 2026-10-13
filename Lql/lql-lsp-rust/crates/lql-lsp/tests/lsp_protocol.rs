//! Integration tests that exercise the LSP server over the JSON-RPC protocol,
//! exactly as VS Code communicates with it (stdin/stdout, Content-Length framing).

use std::{
    io::{BufRead, BufReader, Read, Write},
    process::{Child, Command, Stdio},
};

use serde_json::{json, Value};

// ── Helpers ─────────────────────────────────────────────────────────────────

struct LspClient {
    child: Child,
    reader: BufReader<std::process::ChildStdout>,
    next_id: i64,
}

impl LspClient {
    fn spawn() -> Self {
        let binary = env!("CARGO_BIN_EXE_lql-lsp");
        let mut child = Command::new(binary)
            .stdin(Stdio::piped())
            .stdout(Stdio::piped())
            .stderr(Stdio::null())
            .spawn()
            .expect("failed to spawn lql-lsp");

        let stdout = child.stdout.take().expect("no stdout");
        let reader = BufReader::new(stdout);

        Self {
            child,
            reader,
            next_id: 1,
        }
    }

    fn send_raw(&mut self, msg: &Value) {
        let body = serde_json::to_string(msg).unwrap();
        let header = format!("Content-Length: {}\r\n\r\n", body.len());
        let stdin = self.child.stdin.as_mut().expect("no stdin");
        stdin.write_all(header.as_bytes()).unwrap();
        stdin.write_all(body.as_bytes()).unwrap();
        stdin.flush().unwrap();
    }

    fn send_request(&mut self, method: &str, params: Option<Value>) -> i64 {
        let id = self.next_id;
        self.next_id += 1;
        let mut msg = json!({
            "jsonrpc": "2.0",
            "id": id,
            "method": method,
        });
        if let Some(p) = params {
            msg["params"] = p;
        }
        self.send_raw(&msg);
        id
    }

    fn send_notification(&mut self, method: &str, params: Value) {
        let msg = json!({
            "jsonrpc": "2.0",
            "method": method,
            "params": params,
        });
        self.send_raw(&msg);
    }

    fn read_message(&mut self) -> Value {
        // Read Content-Length header
        let mut header_line = String::new();
        loop {
            header_line.clear();
            self.reader.read_line(&mut header_line).unwrap();
            let trimmed = header_line.trim();
            if trimmed.is_empty() {
                continue;
            }
            if trimmed.starts_with("Content-Length:") {
                break;
            }
        }
        let content_length: usize = header_line
            .trim()
            .strip_prefix("Content-Length:")
            .unwrap()
            .trim()
            .parse()
            .unwrap();

        // Read blank line after headers
        let mut blank = String::new();
        self.reader.read_line(&mut blank).unwrap();

        // Read body
        let mut body = vec![0u8; content_length];
        self.reader.read_exact(&mut body).unwrap();
        serde_json::from_slice(&body).unwrap()
    }

    /// Read messages until we get a response matching the given request id.
    fn read_response(&mut self, id: i64) -> Value {
        loop {
            let msg = self.read_message();
            if msg.get("id").and_then(|v| v.as_i64()) == Some(id) {
                return msg;
            }
            // Otherwise it's a notification (e.g. publishDiagnostics) — skip.
        }
    }

    /// Read messages until we get a notification with the given method.
    fn read_notification(&mut self, method: &str) -> Value {
        loop {
            let msg = self.read_message();
            if msg.get("method").and_then(|v| v.as_str()) == Some(method) {
                return msg;
            }
        }
    }

    fn initialize(&mut self) -> Value {
        let id = self.send_request(
            "initialize",
            Some(json!({
                "processId": std::process::id(),
                "capabilities": {},
                "rootUri": null,
            })),
        );
        let resp = self.read_response(id);
        self.send_notification("initialized", json!({}));
        resp
    }

    fn open_document(&mut self, uri: &str, text: &str) {
        self.send_notification(
            "textDocument/didOpen",
            json!({
                "textDocument": {
                    "uri": uri,
                    "languageId": "lql",
                    "version": 1,
                    "text": text,
                }
            }),
        );
    }

    fn change_document(&mut self, uri: &str, version: i32, text: &str) {
        self.send_notification(
            "textDocument/didChange",
            json!({
                "textDocument": { "uri": uri, "version": version },
                "contentChanges": [{ "text": text }],
            }),
        );
    }

    fn request_position(&mut self, method: &str, uri: &str, line: u32, character: u32) -> Value {
        let id = self.send_request(
            method,
            Some(json!({
                "textDocument": { "uri": uri },
                "position": { "line": line, "character": character },
            })),
        );
        self.read_response(id)
    }

    fn request_completion(&mut self, uri: &str, line: u32, character: u32) -> Value {
        self.request_position("textDocument/completion", uri, line, character)
    }

    fn request_hover(&mut self, uri: &str, line: u32, character: u32) -> Value {
        self.request_position("textDocument/hover", uri, line, character)
    }

    fn request_document_symbols(&mut self, uri: &str) -> Value {
        let id = self.send_request(
            "textDocument/documentSymbol",
            Some(json!({ "textDocument": { "uri": uri } })),
        );
        self.read_response(id)
    }

    fn request_formatting(&mut self, uri: &str) -> Value {
        let id = self.send_request(
            "textDocument/formatting",
            Some(json!({
                "textDocument": { "uri": uri },
                "options": { "tabSize": 4, "insertSpaces": true },
            })),
        );
        self.read_response(id)
    }

    fn shutdown(&mut self) {
        let id = self.send_request("shutdown", None);
        let _ = self.read_response(id);
        self.send_notification("exit", json!(null));
        let _ = self.child.wait();
    }
}

impl Drop for LspClient {
    fn drop(&mut self) {
        let _ = self.child.kill();
    }
}

const DOC_URI: &str = "file:///test.lql";

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
fn open_client_with_pending_diagnostics(source: &str) -> LspClient {
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(DOC_URI, source);
    client
}

/// Spawns an LSP client, initializes it, opens `source`, and drains the
/// initial publishDiagnostics notification so tests start from a clean state.
fn open_client(source: &str) -> LspClient {
    let mut client = open_client_with_pending_diagnostics(source);
    let _ = client.read_notification("textDocument/publishDiagnostics");
    client
}

// ── Tests ───────────────────────────────────────────────────────────────────

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Runs one request against a freshly opened document and returns the full
/// JSON-RPC response, shutting the server down afterwards.
fn request_response(source: &str, request: impl FnOnce(&mut LspClient) -> Value) -> Value {
    let mut client = open_client(source);
    let resp = request(&mut client);
    client.shutdown();
    resp
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Like [`request_response`], but returns just the `result` field.
fn request_result(source: &str, request: impl FnOnce(&mut LspClient) -> Value) -> Value {
    request_response(source, request)["result"].clone()
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Completion labels returned for `source` at the given position.
fn completion_labels(source: &str, line: u32, character: u32) -> Vec<String> {
    request_result(source, |c| c.request_completion(DOC_URI, line, character))
        .as_array()
        .unwrap()
        .iter()
        .map(|i| i["label"].as_str().unwrap().to_string())
        .collect()
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Extracts `key` from every object in the JSON array `result`.
fn field_values(result: &Value, key: &str) -> Vec<String> {
    result
        .as_array()
        .unwrap()
        .iter()
        .map(|v| v[key].as_str().unwrap().to_string())
        .collect()
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Hovers `source` and returns the MarkupContent value, asserting a result.
fn hover_value(source: &str, line: u32, character: u32) -> String {
    let result = request_result(source, |c| c.request_hover(DOC_URI, line, character));
    assert!(!result.is_null(), "hover should return info");
    result["contents"]["value"]
        .as_str()
        .unwrap_or("")
        .to_string()
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Applies a document change and returns the resulting diagnostics.
fn diagnostics_after_change(client: &mut LspClient, version: i32, text: &str) -> Vec<Value> {
    client.change_document(DOC_URI, version, text);
    let notif = client.read_notification("textDocument/publishDiagnostics");
    notif["params"]["diagnostics"].as_array().unwrap().clone()
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Requests completion on an already-initialized client, asserting no error.
fn completion_items_on_client(client: &mut LspClient, line: u32, character: u32) -> Vec<Value> {
    let resp = client.request_completion(DOC_URI, line, character);
    assert!(resp.get("error").is_none());
    resp["result"].as_array().unwrap().clone()
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Runs the binary with `arg` and returns its stdout, asserting success.
fn version_output(arg: &str) -> String {
    let binary = env!("CARGO_BIN_EXE_lql-lsp");
    let output = Command::new(binary)
        .arg(arg)
        .stdin(Stdio::null())
        .stdout(Stdio::piped())
        .stderr(Stdio::null())
        .output()
        .expect("failed to invoke version flag");
    assert!(output.status.success(), "{arg} must exit 0");
    String::from_utf8_lossy(&output.stdout).to_string()
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Creates a temp SQLite database named `name` seeded with `ddl`.
fn seed_temp_sqlite(name: &str, ddl: &str) -> String {
    let path = std::env::temp_dir()
        .join(format!("{name}_{}.db", std::process::id()))
        .to_str()
        .unwrap()
        .to_string();
    let conn = rusqlite::Connection::open(&path).unwrap();
    conn.execute_batch(ddl).unwrap();
    path
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Completion items returned for `source` at the given position.
fn completion_items(source: &str, line: u32, character: u32) -> Vec<Value> {
    request_result(source, |c| c.request_completion(DOC_URI, line, character))
        .as_array()
        .unwrap()
        .clone()
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Runs a document-symbols request against `source` and returns the raw result.
fn document_symbols(source: &str) -> Value {
    request_result(source, |c| c.request_document_symbols(DOC_URI))
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Opens `source` on `client`, asserts completions are non-empty, then shuts down.
fn assert_completions_after_open(
    client: &mut LspClient,
    source: &str,
    line: u32,
    character: u32,
    message: &str,
) {
    open_on_client(client, source);
    let items = completion_items_on_client(client, line, character);
    assert!(!items.is_empty(), "{message}");
    client.shutdown();
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// The published diagnostics URI and payload for a freshly opened document.
fn diagnostics_for(source: &str) -> (String, Vec<Value>) {
    let mut client = open_client_with_pending_diagnostics(source);
    let notif = client.read_notification("textDocument/publishDiagnostics");
    let params = &notif["params"];
    let uri = params["uri"].as_str().unwrap().to_string();
    let diags = params["diagnostics"].as_array().unwrap().clone();
    client.shutdown();
    (uri, diags)
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Initializes a server with the given `initializationOptions` and returns the
/// client plus the initialize response.
fn init_with_options(options: Value) -> (LspClient, Value) {
    let mut client = LspClient::spawn();
    let id = client.send_request(
        "initialize",
        Some(json!({
            "processId": std::process::id(),
            "capabilities": {},
            "rootUri": null,
            "initializationOptions": options,
        })),
    );
    let resp = client.read_response(id);
    client.send_notification("initialized", json!({}));
    (client, resp)
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Opens `text` on an already initialized client and drains diagnostics.
fn open_on_client(client: &mut LspClient, text: &str) {
    client.open_document(DOC_URI, text);
    let _ = client.read_notification("textDocument/publishDiagnostics");
}

#[test]
fn test_initialize_returns_capabilities() {
    let mut client = LspClient::spawn();
    let resp = client.initialize();

    let caps = &resp["result"]["capabilities"];
    // Text document sync
    assert!(caps["textDocumentSync"].is_number() || caps["textDocumentSync"].is_object());
    // Completion
    assert!(caps["completionProvider"].is_object());
    let triggers = &caps["completionProvider"]["triggerCharacters"];
    assert!(triggers.is_array());
    let trigger_chars: Vec<&str> = triggers
        .as_array()
        .unwrap()
        .iter()
        .map(|v| v.as_str().unwrap())
        .collect();
    assert!(trigger_chars.contains(&"."));
    assert!(trigger_chars.contains(&"|"));
    assert!(trigger_chars.contains(&">"));
    // Hover
    assert!(caps.get("hoverProvider").is_some());
    // Document symbol
    assert!(caps.get("documentSymbolProvider").is_some());
    // Formatting
    assert!(caps.get("documentFormattingProvider").is_some());

    client.shutdown();
}

#[test]
fn test_did_open_publishes_diagnostics_clean() {
    let (uri, diags) = diagnostics_for("users |> select(users.id)");
    assert_eq!(uri, DOC_URI);
    assert!(diags.is_empty(), "clean code should have no diagnostics");
}

#[test]
fn test_did_open_publishes_diagnostics_parse_error() {
    let (_, diags) = diagnostics_for("users |> select(");
    assert!(
        !diags.is_empty(),
        "unclosed paren should produce diagnostics"
    );
    // At least one error-level diagnostic
    let has_error = diags.iter().any(|d| d["severity"] == 1);
    assert!(has_error, "should contain an error-level diagnostic");
}

#[test]
fn test_did_change_updates_diagnostics() {
    let mut client = open_client_with_pending_diagnostics("users |> select(");
    let notif = client.read_notification("textDocument/publishDiagnostics");
    assert!(!notif["params"]["diagnostics"]
        .as_array()
        .unwrap()
        .is_empty());

    // Fix the error
    let diags = diagnostics_after_change(&mut client, 2, "users |> select(users.id)");
    assert!(diags.is_empty(), "fixed code should clear diagnostics");

    client.shutdown();
}

#[test]
fn test_did_change_introduces_diagnostics() {
    let mut client = open_client("users |> select(users.id)");

    // Break it
    let diags = diagnostics_after_change(&mut client, 2, "users |> select(");
    assert!(!diags.is_empty(), "broken code should produce diagnostics");

    client.shutdown();
}

#[test]
fn test_completion_after_pipe_returns_pipeline_ops() {
    let labels = completion_labels("users |> ", 0, 9);
    assert!(!labels.is_empty(), "should suggest pipeline operations");
    assert!(
        labels.contains(&"select".to_string()),
        "should suggest select after pipe"
    );
    assert!(
        labels.contains(&"filter".to_string()),
        "should suggest filter after pipe"
    );
}

#[test]
fn test_completion_with_prefix_filters() {
    let labels = completion_labels("users |> sel", 0, 12);
    assert!(
        labels.contains(&"select".to_string()),
        "should suggest select for 'sel'"
    );
    // Should not suggest things that don't match
    assert!(
        !labels.contains(&"filter".to_string()),
        "filter shouldn't match 'sel' prefix"
    );
}

#[test]
fn test_completion_in_lambda() {
    check_test_completion_in_lambda(
        "users |> filter(fn(r) => r.",
        26,
        "completion should return valid result",
    );
}

#[test]
fn test_completion_at_beginning_returns_items() {
    check_test_completion_in_lambda("", 0, "completion on empty doc should not error");
}

#[test]
fn test_hover_on_keyword() {
    // Hover over "select" (position 9-14 on line 0); tower-lsp returns MarkupContent
    let value = hover_value("users |> select(users.id)", 0, 10);
    assert!(
        value.to_lowercase().contains("select"),
        "hover content should mention select"
    );
}

#[test]
fn test_hover_on_non_keyword() {
    // Hover over "users" — no schema, so table hover won't have info
    let resp = request_response("users |> select(users.id)", |c| {
        c.request_hover(DOC_URI, 0, 2)
    });
    let result = &resp["result"];
    // Without schema, hovering on a table name may return null
    // This is valid — we just verify no error
    assert!(resp.get("error").is_none(), "hover should not return error");
    // Result can be null or an object
    assert!(
        result.is_null() || result.is_object(),
        "hover result should be null or object"
    );
}

#[test]
fn test_hover_multiline() {
    // Hover over "filter" on line 1
    let result = request_result(
        "users\n|> filter(fn(r) => r.id > 0)\n|> select(users.id)",
        |c| c.request_hover(DOC_URI, 1, 4),
    );
    assert!(!result.is_null(), "hover on 'filter' should return info");
}

#[test]
fn test_document_symbols_let_bindings() {
    let result = document_symbols("let x = users |> select(users.id)\nlet y = orders");
    let symbols = result.as_array().unwrap();
    assert!(symbols.len() >= 2, "should find at least 2 let bindings");

    let names = field_values(&result, "name");
    assert!(names.contains(&"x".to_string()), "should find symbol 'x'");
    assert!(names.contains(&"y".to_string()), "should find symbol 'y'");
}

#[test]
fn test_document_symbols_empty() {
    let result = document_symbols("users |> select(users.id)");
    let symbols = result.as_array().unwrap();
    // No let bindings = no symbols
    assert!(symbols.is_empty(), "no let bindings means no symbols");
}

#[test]
fn test_formatting_indents_pipeline() {
    let result = request_result("users\n|> select(users.id)\n|> limit(10)", |c| {
        c.request_formatting(DOC_URI)
    });
    let edits = result.as_array().unwrap();
    assert!(!edits.is_empty(), "should produce formatting edits");

    // The single edit should replace entire document
    let new_text = edits[0]["newText"].as_str().unwrap();
    assert!(
        new_text.contains("    |> select"),
        "pipes should be indented"
    );
    assert!(
        new_text.contains("    |> limit"),
        "pipes should be indented"
    );
}

#[test]
fn test_formatting_already_formatted() {
    let result = request_result("users\n    |> select(users.id)\n", |c| {
        c.request_formatting(DOC_URI)
    });
    // If already formatted, should return empty array or null
    let edits = result.as_array();
    if let Some(edits) = edits {
        assert!(
            edits.is_empty(),
            "already-formatted doc should not produce edits"
        );
    }
}

#[test]
fn test_formatting_nested_parens() {
    let result = request_result("select(\nusers.id,\nusers.name\n)", |c| {
        c.request_formatting(DOC_URI)
    });
    let edits = result.as_array().unwrap();
    assert!(!edits.is_empty());

    let new_text = edits[0]["newText"].as_str().unwrap();
    assert!(
        new_text.contains("    users.id"),
        "contents inside parens should be indented"
    );
}

#[test]
fn test_diagnostics_spacing_warning() {
    let (_, diags) = diagnostics_for("users|>select(users.id)");
    let has_warning = diags.iter().any(|d| d["severity"] == 2);
    assert!(has_warning, "should warn about pipe spacing");
}

#[test]
fn test_diagnostics_source_is_lql() {
    let (_, diags) = diagnostics_for("users |> select(");
    for d in diags {
        assert_eq!(d["source"].as_str().unwrap(), "lql");
    }
}

#[test]
fn test_completion_items_have_kind() {
    let items = completion_items("users |> ", 0, 9);
    for item in items {
        assert!(
            item.get("kind").is_some(),
            "every completion item should have a kind"
        );
    }
}

#[test]
fn test_completion_scope_bindings() {
    let labels = completion_labels(
        "let active_users = users |> filter(fn(r) => r.active)\nactive_users |> ",
        1,
        16,
    );
    // Pipeline operations should be suggested
    assert!(
        labels.contains(&"select".to_string()),
        "should suggest select after pipe"
    );
}

#[test]
fn test_did_close_removes_document() {
    let mut client = open_client("users |> select(users.id)");

    // Close the document
    client.send_notification(
        "textDocument/didClose",
        json!({
            "textDocument": { "uri": DOC_URI }
        }),
    );

    // After close, completion should return null result (no document)
    let resp = client.request_completion(DOC_URI, 0, 0);
    let result = &resp["result"];
    // Should be null or empty array (document no longer in cache)
    assert!(
        result.is_null() || result.as_array().is_some_and(|a| a.is_empty()),
        "closed document should not produce completions"
    );

    client.shutdown();
}

#[test]
fn test_shutdown_and_exit() {
    let mut client = LspClient::spawn();
    client.initialize();

    let id = client.send_request("shutdown", None);
    let resp = client.read_response(id);
    // shutdown should not return an error
    assert!(
        resp.get("error").is_none(),
        "shutdown should not return error, got: {resp}"
    );

    client.send_notification("exit", json!(null));
    // Process should terminate
    let status = client.child.wait().unwrap();
    let _ = status;
}

#[test]
fn test_completion_complex_pipeline() {
    let mut client = LspClient::spawn();
    client.initialize();
    let source =
        "users\n|> filter(fn(r) => r.age > 18)\n|> select(\n    users.id,\n    users.name\n)\n|> ";
    open_on_client(&mut client, source);

    // Request completion at the end (line 6, after "|> ")
    let resp = client.request_completion(DOC_URI, 6, 3);
    let labels = field_values(&resp["result"], "label");
    assert!(
        labels.contains(&"limit".to_string()),
        "should suggest limit in pipeline"
    );
    assert!(
        labels.contains(&"order_by".to_string()),
        "should suggest order_by in pipeline"
    );

    client.shutdown();
}

#[test]
fn test_hover_on_filter_keyword() {
    let value = hover_value("users |> filter(fn(r) => r.age > 18)", 0, 10);
    assert!(
        value.to_lowercase().contains("filter"),
        "hover should describe filter"
    );
}

#[test]
fn test_formatting_preserves_comments() {
    let result = request_result(
        "-- Get all active users\nusers\n|> filter(fn(r) => r.active)",
        |c| c.request_formatting(DOC_URI),
    );
    let edits = result.as_array().unwrap();
    if !edits.is_empty() {
        let new_text = edits[0]["newText"].as_str().unwrap();
        assert!(
            new_text.contains("-- Get all active users"),
            "formatting should preserve comments"
        );
    }
}

#[test]
fn test_diagnostics_multiple_errors() {
    let (_, diags) = diagnostics_for("users|>select(");
    assert!(
        diags.len() >= 2,
        "should report multiple diagnostics, got {}",
        diags.len()
    );
}

#[test]
fn test_initialize_with_ai_config() {
    let (mut client, resp) = init_with_options(json!({
        "aiProvider": {
            "provider": "test",
            "enabled": true,
        }
    }));
    assert!(
        resp.get("error").is_none(),
        "initialize with AI config should succeed"
    );

    // AI completions should now be available
    open_on_client(&mut client, "users |> ");

    let resp = client.request_completion(DOC_URI, 0, 9);
    let items = resp["result"].as_array().unwrap();
    assert!(
        !items.is_empty(),
        "should return completions with AI provider"
    );

    client.shutdown();
}

#[test]
fn test_hover_on_space_returns_null() {
    // Hover on the space between "users" and "|>"
    let result = request_result("users |> select(users.id)", |c| {
        c.request_hover(DOC_URI, 0, 5)
    });
    // Space is not on any word, so result should be null
    assert!(
        result.is_null(),
        "hover on space should return null, got: {result}"
    );
}

#[test]
fn test_completion_after_dot_without_qualifier() {
    let resp = request_response(".", |c| c.request_completion(DOC_URI, 0, 1));
    // Should not error
    assert!(resp.get("error").is_none());
}

#[test]
fn test_version_flag_prints_and_exits() {
    // Drives main.rs lines 640-641: `lql-lsp --version` must print
    // "lql-lsp <semver>" to stdout and exit 0 without starting the LSP loop.
    let text = version_output("--version");
    assert!(
        text.starts_with("lql-lsp "),
        "version line must start with 'lql-lsp ', got: {text}"
    );
    assert!(
        text.split_whitespace().count() == 2,
        "version line must be 'lql-lsp <version>', got: {text}"
    );
}

#[test]
fn test_short_version_flag_prints_and_exits() {
    let text = version_output("-V");
    assert!(text.starts_with("lql-lsp "), "got: {text}");
}

#[test]
fn test_completion_with_ai_provider_and_schema_loaded() {
    // Drives main.rs lines 413-441 (the Some(s) schema arm of the AI
    // completion-merge path) by initializing with both an aiProvider and
    // a SQLite connection string so a schema is loaded into the cache.
    // Seed the database before spawning the LSP.
    let path = seed_temp_sqlite(
        "lql_ai_schema_test",
        "CREATE TABLE IF NOT EXISTS users_ai_schema_test (
                id TEXT PRIMARY KEY NOT NULL,
                name TEXT NOT NULL,
                email TEXT
            );",
    );

    let (mut client, resp) = init_with_options(json!({
        "connectionString": path.as_str(),
        "aiProvider": {
            "provider": "test",
            "enabled": true,
        }
    }));
    assert!(resp.get("error").is_none(), "init must succeed");

    // Give the schema fetch a moment to populate.
    std::thread::sleep(std::time::Duration::from_millis(500));

    assert_completions_after_open(
        &mut client,
        "users_ai_schema_test |> ",
        0,
        24,
        "completion must return items",
    );
    std::fs::remove_file(&path).ok();
}

#[test]
fn test_completion_with_test_slow_provider_times_out() {
    // Drives the AI timeout branch (main.rs around line 460-464) and the
    // "test_slow" arm of the initialized() AI dispatch (lines 267-280).
    let (mut client, _) = init_with_options(json!({
        "aiProvider": {
            "provider": "test_slow",
            "enabled": true,
            "timeoutMs": 100,
        }
    }));

    // Even when AI times out, schema/keyword completions should still be returned.
    assert_completions_after_open(
        &mut client,
        "users |> ",
        0,
        9,
        "must still return non-AI completions when AI times out",
    );
}

#[test]
fn test_completion_with_unknown_ai_provider_uses_default_branch() {
    // Drives the `_` arm in initialized()'s AI dispatch (main.rs line 295).
    let (mut client, _) = init_with_options(json!({
        "aiProvider": {
            "provider": "some-unrecognised-provider",
            "enabled": true,
        }
    }));

    open_on_client(&mut client, "users |> ");

    let _ = completion_items_on_client(&mut client, 0, 9);

    client.shutdown();
}

#[test]
fn test_initialize_with_connection_string_only_loads_schema() {
    // Drives the connectionString-without-aiProvider path in initialized()
    // — exercises lines 311-349 (DB connect + schema fetch success arm).
    let path = seed_temp_sqlite(
        "lql_conn_only_test",
        "CREATE TABLE IF NOT EXISTS conn_only_users (id TEXT PRIMARY KEY NOT NULL);",
    );

    let (mut client, resp) = init_with_options(json!({
        "connectionString": path.as_str(),
    }));
    assert!(resp.get("error").is_none());

    std::thread::sleep(std::time::Duration::from_millis(300));

    open_on_client(&mut client, "conn_only_users.");

    // Trigger column completion after the dot
    let _ = completion_items_on_client(&mut client, 0, 16);

    client.shutdown();
    std::fs::remove_file(&path).ok();
}

#[test]
fn test_document_symbols_positions() {
    let result = document_symbols("let x = users\nlet y = orders");
    let symbols = result.as_array().unwrap();

    for sym in symbols {
        // Each symbol should have a location with range
        let location = &sym["location"];
        assert!(
            location.get("range").is_some(),
            "symbol should have a location range"
        );
        let range = &location["range"];
        assert!(range["start"]["line"].is_number());
        assert!(range["start"]["character"].is_number());
    }
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
fn check_test_completion_in_lambda(input0: &str, input2: u32, input4: &str) {
    // Result is an array (CompletionResponse::Array) or null
    let result = request_result(input0, |c| c.request_completion(DOC_URI, 0, input2));
    assert!(result.is_array() || result.is_null(), "{}", input4);
}
