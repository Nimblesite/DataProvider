//! Integration tests that exercise the LSP server over the JSON-RPC protocol,
//! exactly as VS Code communicates with it (stdin/stdout, Content-Length framing).

use serde_json::{json, Value};
use std::io::{BufRead, BufReader, Read, Write};
use std::process::{Child, Command, Stdio};

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

    fn request_completion(&mut self, uri: &str, line: u32, character: u32) -> Value {
        let id = self.send_request(
            "textDocument/completion",
            Some(json!({
                "textDocument": { "uri": uri },
                "position": { "line": line, "character": character },
            })),
        );
        self.read_response(id)
    }

    fn request_hover(&mut self, uri: &str, line: u32, character: u32) -> Value {
        let id = self.send_request(
            "textDocument/hover",
            Some(json!({
                "textDocument": { "uri": uri },
                "position": { "line": line, "character": character },
            })),
        );
        self.read_response(id)
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

// ── Tests ───────────────────────────────────────────────────────────────────

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
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(DOC_URI, "users |> select(users.id)");

    let notif = client.read_notification("textDocument/publishDiagnostics");
    let params = &notif["params"];
    assert_eq!(params["uri"].as_str().unwrap(), DOC_URI);
    let diags = params["diagnostics"].as_array().unwrap();
    assert!(diags.is_empty(), "clean code should have no diagnostics");

    client.shutdown();
}

#[test]
fn test_did_open_publishes_diagnostics_parse_error() {
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(DOC_URI, "users |> select(");

    let notif = client.read_notification("textDocument/publishDiagnostics");
    let diags = notif["params"]["diagnostics"].as_array().unwrap();
    assert!(
        !diags.is_empty(),
        "unclosed paren should produce diagnostics"
    );
    // At least one error-level diagnostic
    let has_error = diags.iter().any(|d| d["severity"] == 1);
    assert!(has_error, "should contain an error-level diagnostic");

    client.shutdown();
}

#[test]
fn test_did_change_updates_diagnostics() {
    let mut client = LspClient::spawn();
    client.initialize();

    // Open with error
    client.open_document(DOC_URI, "users |> select(");
    let notif = client.read_notification("textDocument/publishDiagnostics");
    assert!(!notif["params"]["diagnostics"]
        .as_array()
        .unwrap()
        .is_empty());

    // Fix the error
    client.change_document(DOC_URI, 2, "users |> select(users.id)");
    let notif = client.read_notification("textDocument/publishDiagnostics");
    assert!(
        notif["params"]["diagnostics"]
            .as_array()
            .unwrap()
            .is_empty(),
        "fixed code should clear diagnostics"
    );

    client.shutdown();
}

#[test]
fn test_did_change_introduces_diagnostics() {
    let mut client = LspClient::spawn();
    client.initialize();

    // Open clean
    client.open_document(DOC_URI, "users |> select(users.id)");
    let _ = client.read_notification("textDocument/publishDiagnostics");

    // Break it
    client.change_document(DOC_URI, 2, "users |> select(");
    let notif = client.read_notification("textDocument/publishDiagnostics");
    assert!(
        !notif["params"]["diagnostics"]
            .as_array()
            .unwrap()
            .is_empty(),
        "broken code should produce diagnostics"
    );

    client.shutdown();
}

#[test]
fn test_completion_after_pipe_returns_pipeline_ops() {
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(DOC_URI, "users |> ");
    let _ = client.read_notification("textDocument/publishDiagnostics");

    let resp = client.request_completion(DOC_URI, 0, 9);
    let items = resp["result"].as_array().unwrap();
    assert!(!items.is_empty(), "should suggest pipeline operations");

    let labels: Vec<&str> = items.iter().map(|i| i["label"].as_str().unwrap()).collect();
    assert!(
        labels.contains(&"select"),
        "should suggest select after pipe"
    );
    assert!(
        labels.contains(&"filter"),
        "should suggest filter after pipe"
    );

    client.shutdown();
}

#[test]
fn test_completion_with_prefix_filters() {
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(DOC_URI, "users |> sel");
    let _ = client.read_notification("textDocument/publishDiagnostics");

    let resp = client.request_completion(DOC_URI, 0, 12);
    let items = resp["result"].as_array().unwrap();
    let labels: Vec<&str> = items.iter().map(|i| i["label"].as_str().unwrap()).collect();
    assert!(
        labels.contains(&"select"),
        "should suggest select for 'sel'"
    );
    // Should not suggest things that don't match
    assert!(
        !labels.contains(&"filter"),
        "filter shouldn't match 'sel' prefix"
    );

    client.shutdown();
}

#[test]
fn test_completion_in_lambda() {
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(DOC_URI, "users |> filter(fn(r) => r.");
    let _ = client.read_notification("textDocument/publishDiagnostics");

    let resp = client.request_completion(DOC_URI, 0, 26);
    // Result is an array (CompletionResponse::Array) or null
    let result = &resp["result"];
    assert!(
        result.is_array() || result.is_null(),
        "completion should return valid result"
    );

    client.shutdown();
}

#[test]
fn test_completion_at_beginning_returns_items() {
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(DOC_URI, "");
    let _ = client.read_notification("textDocument/publishDiagnostics");

    let resp = client.request_completion(DOC_URI, 0, 0);
    // Result is an array or null
    let result = &resp["result"];
    assert!(
        result.is_array() || result.is_null(),
        "completion on empty doc should not error"
    );

    client.shutdown();
}

#[test]
fn test_hover_on_keyword() {
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(DOC_URI, "users |> select(users.id)");
    let _ = client.read_notification("textDocument/publishDiagnostics");

    // Hover over "select" (position 9-14 on line 0)
    let resp = client.request_hover(DOC_URI, 0, 10);
    let result = &resp["result"];
    assert!(!result.is_null(), "hover on 'select' should return info");
    let contents = &result["contents"];
    // tower-lsp returns MarkupContent
    let value = contents["value"].as_str().unwrap_or("");
    assert!(
        value.to_lowercase().contains("select"),
        "hover content should mention select"
    );

    client.shutdown();
}

#[test]
fn test_hover_on_non_keyword() {
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(DOC_URI, "users |> select(users.id)");
    let _ = client.read_notification("textDocument/publishDiagnostics");

    // Hover over "users" — no schema, so table hover won't have info
    let resp = client.request_hover(DOC_URI, 0, 2);
    let result = &resp["result"];
    // Without schema, hovering on a table name may return null
    // This is valid — we just verify no error
    assert!(resp.get("error").is_none(), "hover should not return error");
    // Result can be null or an object
    assert!(
        result.is_null() || result.is_object(),
        "hover result should be null or object"
    );

    client.shutdown();
}

#[test]
fn test_hover_multiline() {
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(
        DOC_URI,
        "users\n|> filter(fn(r) => r.id > 0)\n|> select(users.id)",
    );
    let _ = client.read_notification("textDocument/publishDiagnostics");

    // Hover over "filter" on line 1
    let resp = client.request_hover(DOC_URI, 1, 4);
    let result = &resp["result"];
    assert!(!result.is_null(), "hover on 'filter' should return info");

    client.shutdown();
}

#[test]
fn test_document_symbols_let_bindings() {
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(DOC_URI, "let x = users |> select(users.id)\nlet y = orders");
    let _ = client.read_notification("textDocument/publishDiagnostics");

    let resp = client.request_document_symbols(DOC_URI);
    let symbols = resp["result"].as_array().unwrap();
    assert!(symbols.len() >= 2, "should find at least 2 let bindings");

    let names: Vec<&str> = symbols
        .iter()
        .map(|s| s["name"].as_str().unwrap())
        .collect();
    assert!(names.contains(&"x"), "should find symbol 'x'");
    assert!(names.contains(&"y"), "should find symbol 'y'");

    client.shutdown();
}

#[test]
fn test_document_symbols_empty() {
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(DOC_URI, "users |> select(users.id)");
    let _ = client.read_notification("textDocument/publishDiagnostics");

    let resp = client.request_document_symbols(DOC_URI);
    let symbols = resp["result"].as_array().unwrap();
    // No let bindings = no symbols
    assert!(symbols.is_empty(), "no let bindings means no symbols");

    client.shutdown();
}

#[test]
fn test_formatting_indents_pipeline() {
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(DOC_URI, "users\n|> select(users.id)\n|> limit(10)");
    let _ = client.read_notification("textDocument/publishDiagnostics");

    let resp = client.request_formatting(DOC_URI);
    let edits = resp["result"].as_array().unwrap();
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

    client.shutdown();
}

#[test]
fn test_formatting_already_formatted() {
    let mut client = LspClient::spawn();
    client.initialize();
    // Already properly formatted
    client.open_document(DOC_URI, "users\n    |> select(users.id)\n");
    let _ = client.read_notification("textDocument/publishDiagnostics");

    let resp = client.request_formatting(DOC_URI);
    let result = &resp["result"];
    // If already formatted, should return empty array or null
    let edits = result.as_array();
    if let Some(edits) = edits {
        assert!(
            edits.is_empty(),
            "already-formatted doc should not produce edits"
        );
    }

    client.shutdown();
}

#[test]
fn test_formatting_nested_parens() {
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(DOC_URI, "select(\nusers.id,\nusers.name\n)");
    let _ = client.read_notification("textDocument/publishDiagnostics");

    let resp = client.request_formatting(DOC_URI);
    let edits = resp["result"].as_array().unwrap();
    assert!(!edits.is_empty());

    let new_text = edits[0]["newText"].as_str().unwrap();
    assert!(
        new_text.contains("    users.id"),
        "contents inside parens should be indented"
    );

    client.shutdown();
}

#[test]
fn test_diagnostics_spacing_warning() {
    let mut client = LspClient::spawn();
    client.initialize();
    // Missing spaces around |>
    client.open_document(DOC_URI, "users|>select(users.id)");

    let notif = client.read_notification("textDocument/publishDiagnostics");
    let diags = notif["params"]["diagnostics"].as_array().unwrap();
    let has_warning = diags.iter().any(|d| d["severity"] == 2);
    assert!(has_warning, "should warn about pipe spacing");

    client.shutdown();
}

#[test]
fn test_diagnostics_source_is_lql() {
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(DOC_URI, "users |> select(");

    let notif = client.read_notification("textDocument/publishDiagnostics");
    let diags = notif["params"]["diagnostics"].as_array().unwrap();
    for d in diags {
        assert_eq!(d["source"].as_str().unwrap(), "lql");
    }

    client.shutdown();
}

#[test]
fn test_completion_items_have_kind() {
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(DOC_URI, "users |> ");
    let _ = client.read_notification("textDocument/publishDiagnostics");

    let resp = client.request_completion(DOC_URI, 0, 9);
    let items = resp["result"].as_array().unwrap();
    for item in items {
        assert!(
            item.get("kind").is_some(),
            "every completion item should have a kind"
        );
    }

    client.shutdown();
}

#[test]
fn test_completion_scope_bindings() {
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(
        DOC_URI,
        "let active_users = users |> filter(fn(r) => r.active)\nactive_users |> ",
    );
    let _ = client.read_notification("textDocument/publishDiagnostics");

    let resp = client.request_completion(DOC_URI, 1, 16);
    let items = resp["result"].as_array().unwrap();
    let labels: Vec<&str> = items.iter().map(|i| i["label"].as_str().unwrap()).collect();
    // Pipeline operations should be suggested
    assert!(
        labels.contains(&"select"),
        "should suggest select after pipe"
    );

    client.shutdown();
}

#[test]
fn test_did_close_removes_document() {
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(DOC_URI, "users |> select(users.id)");
    let _ = client.read_notification("textDocument/publishDiagnostics");

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
    client.open_document(DOC_URI, source);
    let _ = client.read_notification("textDocument/publishDiagnostics");

    // Request completion at the end (line 6, after "|> ")
    let resp = client.request_completion(DOC_URI, 6, 3);
    let items = resp["result"].as_array().unwrap();
    let labels: Vec<&str> = items.iter().map(|i| i["label"].as_str().unwrap()).collect();
    assert!(
        labels.contains(&"limit"),
        "should suggest limit in pipeline"
    );
    assert!(
        labels.contains(&"order_by"),
        "should suggest order_by in pipeline"
    );

    client.shutdown();
}

#[test]
fn test_hover_on_filter_keyword() {
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(DOC_URI, "users |> filter(fn(r) => r.age > 18)");
    let _ = client.read_notification("textDocument/publishDiagnostics");

    let resp = client.request_hover(DOC_URI, 0, 10);
    let result = &resp["result"];
    assert!(!result.is_null(), "hover on 'filter' should return info");
    let value = result["contents"]["value"].as_str().unwrap_or("");
    assert!(
        value.to_lowercase().contains("filter"),
        "hover should describe filter"
    );

    client.shutdown();
}

#[test]
fn test_formatting_preserves_comments() {
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(
        DOC_URI,
        "-- Get all active users\nusers\n|> filter(fn(r) => r.active)",
    );
    let _ = client.read_notification("textDocument/publishDiagnostics");

    let resp = client.request_formatting(DOC_URI);
    let edits = resp["result"].as_array().unwrap();
    if !edits.is_empty() {
        let new_text = edits[0]["newText"].as_str().unwrap();
        assert!(
            new_text.contains("-- Get all active users"),
            "formatting should preserve comments"
        );
    }

    client.shutdown();
}

#[test]
fn test_diagnostics_multiple_errors() {
    let mut client = LspClient::spawn();
    client.initialize();
    // Multiple issues: missing space around |> AND unclosed paren
    client.open_document(DOC_URI, "users|>select(");

    let notif = client.read_notification("textDocument/publishDiagnostics");
    let diags = notif["params"]["diagnostics"].as_array().unwrap();
    assert!(
        diags.len() >= 2,
        "should report multiple diagnostics, got {}",
        diags.len()
    );

    client.shutdown();
}

#[test]
fn test_initialize_with_ai_config() {
    let mut client = LspClient::spawn();
    let id = client.send_request(
        "initialize",
        Some(json!({
            "processId": std::process::id(),
            "capabilities": {},
            "rootUri": null,
            "initializationOptions": {
                "aiProvider": {
                    "provider": "test",
                    "enabled": true,
                }
            }
        })),
    );
    let resp = client.read_response(id);
    assert!(
        resp.get("error").is_none(),
        "initialize with AI config should succeed"
    );
    client.send_notification("initialized", json!({}));

    // AI completions should now be available
    client.open_document(DOC_URI, "users |> ");
    let _ = client.read_notification("textDocument/publishDiagnostics");

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
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(DOC_URI, "users |> select(users.id)");
    let _ = client.read_notification("textDocument/publishDiagnostics");

    // Hover on the space between "users" and "|>"
    let resp = client.request_hover(DOC_URI, 0, 5);
    let result = &resp["result"];
    // Space is not on any word, so result should be null
    assert!(
        result.is_null(),
        "hover on space should return null, got: {result}"
    );

    client.shutdown();
}

#[test]
fn test_completion_after_dot_without_qualifier() {
    let mut client = LspClient::spawn();
    client.initialize();
    // Just a dot — no table qualifier
    client.open_document(DOC_URI, ".");
    let _ = client.read_notification("textDocument/publishDiagnostics");

    let resp = client.request_completion(DOC_URI, 0, 1);
    // Should not error
    assert!(resp.get("error").is_none());

    client.shutdown();
}

#[test]
fn test_version_flag_prints_and_exits() {
    // Drives main.rs lines 640-641: `lql-lsp --version` must print
    // "lql-lsp <semver>" to stdout and exit 0 without starting the LSP loop.
    let binary = env!("CARGO_BIN_EXE_lql-lsp");
    let output = Command::new(binary)
        .arg("--version")
        .stdin(Stdio::null())
        .stdout(Stdio::piped())
        .stderr(Stdio::null())
        .output()
        .expect("failed to invoke --version");

    assert!(output.status.success(), "--version must exit 0");
    let text = String::from_utf8_lossy(&output.stdout);
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
    let binary = env!("CARGO_BIN_EXE_lql-lsp");
    let output = Command::new(binary)
        .arg("-V")
        .stdin(Stdio::null())
        .stdout(Stdio::piped())
        .stderr(Stdio::null())
        .output()
        .expect("failed to invoke -V");

    assert!(output.status.success(), "-V must exit 0");
    let text = String::from_utf8_lossy(&output.stdout);
    assert!(text.starts_with("lql-lsp "), "got: {text}");
}

#[test]
fn test_completion_with_ai_provider_and_schema_loaded() {
    // Drives main.rs lines 413-441 (the Some(s) schema arm of the AI
    // completion-merge path) by initializing with both an aiProvider and
    // a SQLite connection string so a schema is loaded into the cache.
    let dir = std::env::temp_dir().join(format!("lql_ai_schema_test_{}.db", std::process::id()));
    let path = dir.to_str().unwrap();

    // Seed the database before spawning the LSP.
    {
        let conn = rusqlite::Connection::open(path).unwrap();
        conn.execute_batch(
            "CREATE TABLE IF NOT EXISTS users_ai_schema_test (
                id TEXT PRIMARY KEY NOT NULL,
                name TEXT NOT NULL,
                email TEXT
            );",
        )
        .unwrap();
    }

    let mut client = LspClient::spawn();
    let id = client.send_request(
        "initialize",
        Some(json!({
            "processId": std::process::id(),
            "capabilities": {},
            "rootUri": null,
            "initializationOptions": {
                "connectionString": path,
                "aiProvider": {
                    "provider": "test",
                    "enabled": true,
                }
            }
        })),
    );
    let resp = client.read_response(id);
    assert!(resp.get("error").is_none(), "init must succeed");
    client.send_notification("initialized", json!({}));

    // Give the schema fetch a moment to populate.
    std::thread::sleep(std::time::Duration::from_millis(500));

    client.open_document(DOC_URI, "users_ai_schema_test |> ");
    let _ = client.read_notification("textDocument/publishDiagnostics");

    let resp = client.request_completion(DOC_URI, 0, 24);
    assert!(resp.get("error").is_none());
    let items = resp["result"].as_array().unwrap();
    assert!(!items.is_empty(), "completion must return items");

    client.shutdown();
    std::fs::remove_file(path).ok();
}

#[test]
fn test_completion_with_test_slow_provider_times_out() {
    // Drives the AI timeout branch (main.rs around line 460-464) and the
    // "test_slow" arm of the initialized() AI dispatch (lines 267-280).
    let mut client = LspClient::spawn();
    let id = client.send_request(
        "initialize",
        Some(json!({
            "processId": std::process::id(),
            "capabilities": {},
            "rootUri": null,
            "initializationOptions": {
                "aiProvider": {
                    "provider": "test_slow",
                    "enabled": true,
                    "timeoutMs": 100,
                }
            }
        })),
    );
    let _ = client.read_response(id);
    client.send_notification("initialized", json!({}));

    client.open_document(DOC_URI, "users |> ");
    let _ = client.read_notification("textDocument/publishDiagnostics");

    let resp = client.request_completion(DOC_URI, 0, 9);
    // Even when AI times out, schema/keyword completions should still be returned.
    assert!(resp.get("error").is_none());
    let items = resp["result"].as_array().unwrap();
    assert!(
        !items.is_empty(),
        "must still return non-AI completions when AI times out"
    );

    client.shutdown();
}

#[test]
fn test_completion_with_unknown_ai_provider_uses_default_branch() {
    // Drives the `_` arm in initialized()'s AI dispatch (main.rs line 295).
    let mut client = LspClient::spawn();
    let id = client.send_request(
        "initialize",
        Some(json!({
            "processId": std::process::id(),
            "capabilities": {},
            "rootUri": null,
            "initializationOptions": {
                "aiProvider": {
                    "provider": "some-unrecognised-provider",
                    "enabled": true,
                }
            }
        })),
    );
    let _ = client.read_response(id);
    client.send_notification("initialized", json!({}));

    client.open_document(DOC_URI, "users |> ");
    let _ = client.read_notification("textDocument/publishDiagnostics");

    let resp = client.request_completion(DOC_URI, 0, 9);
    assert!(resp.get("error").is_none());

    client.shutdown();
}

#[test]
fn test_initialize_with_connection_string_only_loads_schema() {
    // Drives the connectionString-without-aiProvider path in initialized()
    // — exercises lines 311-349 (DB connect + schema fetch success arm).
    let dir = std::env::temp_dir().join(format!("lql_conn_only_test_{}.db", std::process::id()));
    let path = dir.to_str().unwrap();

    {
        let conn = rusqlite::Connection::open(path).unwrap();
        conn.execute_batch(
            "CREATE TABLE IF NOT EXISTS conn_only_users (id TEXT PRIMARY KEY NOT NULL);",
        )
        .unwrap();
    }

    let mut client = LspClient::spawn();
    let id = client.send_request(
        "initialize",
        Some(json!({
            "processId": std::process::id(),
            "capabilities": {},
            "rootUri": null,
            "initializationOptions": {
                "connectionString": path,
            }
        })),
    );
    let resp = client.read_response(id);
    assert!(resp.get("error").is_none());
    client.send_notification("initialized", json!({}));

    std::thread::sleep(std::time::Duration::from_millis(300));

    client.open_document(DOC_URI, "conn_only_users.");
    let _ = client.read_notification("textDocument/publishDiagnostics");

    // Trigger column completion after the dot
    let resp = client.request_completion(DOC_URI, 0, 16);
    assert!(resp.get("error").is_none());

    client.shutdown();
    std::fs::remove_file(path).ok();
}

#[test]
fn test_document_symbols_positions() {
    let mut client = LspClient::spawn();
    client.initialize();
    client.open_document(DOC_URI, "let x = users\nlet y = orders");
    let _ = client.read_notification("textDocument/publishDiagnostics");

    let resp = client.request_document_symbols(DOC_URI);
    let symbols = resp["result"].as_array().unwrap();

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

    client.shutdown();
}
