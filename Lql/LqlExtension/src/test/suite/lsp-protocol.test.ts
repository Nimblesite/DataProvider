/**
 * E2E LSP Protocol Tests
 *
 * These tests hit the LSP server DIRECTLY via stdio with real JSON-RPC messages.
 * No mocks. No fakes. Real server binary, real protocol, real responses.
 *
 * This is the equivalent of WebSocket testing — we spawn the actual lql-lsp
 * binary and communicate with it using the LSP wire protocol.
 */

import * as assert from "assert";
import * as child_process from "child_process";
import * as path from "path";
import * as fs from "fs";

/** LSP JSON-RPC message shape (inbound from server) */
interface JsonRpcMessage {
  readonly id?: number;
  readonly method?: string;
  readonly params?: unknown;
  readonly result?: unknown;
  readonly error?: unknown;
}

/** Outbound JSON-RPC request */
interface JsonRpcRequest {
  readonly jsonrpc: "2.0";
  readonly id: number;
  readonly method: string;
  readonly params?: unknown;
}

/** Outbound JSON-RPC notification */
interface JsonRpcNotification {
  readonly jsonrpc: "2.0";
  readonly method: string;
  readonly params: unknown;
}

/** Server notification stored by LspClient */
interface LspNotification {
  readonly method: string;
  readonly params: unknown;
}

/**
 * LSP Diagnostic — fields are optional because this data arrives from
 * an external process via JSON and tests validate its shape.
 */
interface LspDiagnostic {
  readonly range?: LspRange;
  readonly message?: string;
  readonly severity?: number;
  readonly source?: string;
}

/** LSP Range — fields optional since validated at runtime from external data */
interface LspRange {
  readonly start?: LspPosition;
  readonly end?: LspPosition;
}

/** LSP Position */
interface LspPosition {
  readonly line: number;
  readonly character: number;
}

/** LSP Diagnostics notification params */
interface PublishDiagnosticsParams {
  readonly uri?: string;
  readonly diagnostics: readonly LspDiagnostic[];
}

/** LSP Completion Item — fields optional since validated at runtime */
interface LspCompletionItem {
  readonly label: string;
  readonly kind?: number;
  readonly detail?: string;
  readonly documentation?: string;
  readonly insertText?: string;
  readonly insertTextFormat?: number;
}

/** LSP Completion result */
interface LspCompletionList {
  readonly items: readonly LspCompletionItem[];
}

/** LSP Hover contents (MarkupContent or string) */
interface LspMarkupContent {
  readonly kind?: string;
  readonly value?: string;
}

/** LSP Hover result — fields optional since validated at runtime */
interface LspHoverResult {
  readonly contents?: LspMarkupContent | string;
  readonly range?: LspRange;
}

/** LSP Document Symbol — fields optional since validated at runtime */
interface LspDocumentSymbol {
  readonly name?: string;
  readonly kind?: number;
  readonly location?: {
    readonly uri?: string;
    readonly range?: LspRange;
  };
}

/** LSP Initialize result capabilities */
interface LspCapabilities {
  readonly textDocumentSync?: unknown;
  readonly completionProvider?: {
    readonly triggerCharacters?: readonly string[];
  };
  readonly hoverProvider?: unknown;
  readonly documentSymbolProvider?: unknown;
  readonly documentFormattingProvider?: unknown;
}

/** LSP Initialize result */
interface LspInitializeResult {
  readonly capabilities?: LspCapabilities;
}

/** LSP Text Edit — fields optional since validated at runtime */
interface LspTextEdit {
  readonly range?: LspRange;
  readonly newText?: string;
}

/** Encode a JSON-RPC message with LSP Content-Length header */
function encode(msg: object): string {
  const body = JSON.stringify(msg);
  return `Content-Length: ${String(Buffer.byteLength(body))}\r\n\r\n${body}`;
}

/**
 * Type guard: checks if value is a non-null object (Record-like).
 * Used to narrow `unknown` from JSON.parse.
 */
function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

/** Type guard for LspCompletionItem */
function isCompletionItem(value: unknown): value is LspCompletionItem {
  return isRecord(value) && typeof value["label"] === "string";
}

/**
 * Extracts completion items from an LSP completion response.
 * The response may be an array of items or a CompletionList with an items property.
 */
function extractCompletionItems(result: unknown): readonly LspCompletionItem[] {
  if (Array.isArray(result)) {
    return result.filter(isCompletionItem);
  }
  if (isRecord(result) && Array.isArray(result["items"])) {
    // Safe: we verified result has an array 'items' property above
    const list = result as unknown as LspCompletionList;
    return list.items.filter(isCompletionItem);
  }
  return [];
}

/**
 * Extracts hover text from an LSP hover response.
 * Contents may be a string or a MarkupContent { kind, value }.
 */
function extractHoverText(hover: LspHoverResult): string {
  if (typeof hover.contents === "string") {
    return hover.contents;
  }
  return hover.contents?.value ?? "";
}

/**
 * Type guard for PublishDiagnosticsParams.
 */
function isDiagnosticsParams(value: unknown): value is PublishDiagnosticsParams {
  return isRecord(value) && typeof value["uri"] === "string" && Array.isArray(value["diagnostics"]);
}

/**
 * Persistent LSP client that maintains a single data listener for the entire
 * server lifetime. This prevents issues with interleaved notifications and
 * buffered stdout data.
 */
class LspClient {
  private proc: child_process.ChildProcess;
  private messageId = 0;
  /** Raw byte buffer — Content-Length is in bytes, not characters */
  private rawBuffer: Buffer = Buffer.alloc(0);
  private pendingRequests = new Map<number, { resolve: (v: unknown) => void; reject: (e: Error) => void }>();
  private notifications: LspNotification[] = [];

  constructor(binary: string, env?: Record<string, string>) {
    this.proc = child_process.spawn(binary, [], {
      stdio: ["pipe", "pipe", "pipe"],
      env: env ? { ...process.env, ...env } : undefined,
    });

    const stdout = this.proc.stdout;
    if (stdout === null) {
      throw new Error("Failed to open stdout on LSP child process");
    }
    stdout.on("data", (data: Buffer) => {
      this.rawBuffer = Buffer.concat([this.rawBuffer, data]);
      this.processBuffer();
    });
  }

  private processBuffer(): void {
    for (;;) {
      const headerEnd = this.rawBuffer.indexOf("\r\n\r\n");
      if (headerEnd === -1) {
        break;
      }

      const header = this.rawBuffer.subarray(0, headerEnd).toString("utf-8");
      const match = header.match(/Content-Length:\s*(\d+)/i);
      if (match?.[1] === undefined) {
        break;
      }

      const contentLength = parseInt(match[1], 10);
      const bodyStart = headerEnd + 4;
      if (this.rawBuffer.length < bodyStart + contentLength) {
        break;
      }

      const body = this.rawBuffer.subarray(bodyStart, bodyStart + contentLength).toString("utf-8");
      this.rawBuffer = this.rawBuffer.subarray(bodyStart + contentLength);

      try {
        const parsed: unknown = JSON.parse(body);
        if (!isRecord(parsed)) {
          continue;
        }

        const msg = parsed as JsonRpcMessage;

        if (msg.id !== undefined) {
          // Response to a request
          const pending = this.pendingRequests.get(msg.id);
          if (pending) {
            this.pendingRequests.delete(msg.id);
            if (msg.error !== undefined) {
              pending.reject(new Error(`LSP error: ${JSON.stringify(msg.error)}`));
            } else {
              pending.resolve(msg.result);
            }
          }
        } else if (typeof msg.method === "string") {
          // Notification from server
          this.notifications.push({
            method: msg.method,
            params: msg.params,
          });
        }
      } catch {
        // Incomplete JSON, will get more data
      }
    }
  }

  async request(method: string, params?: unknown): Promise<unknown> {
    const id = ++this.messageId;
    const msg: JsonRpcRequest =
      params !== undefined ? { jsonrpc: "2.0", id, method, params } : { jsonrpc: "2.0", id, method };

    return new Promise((resolve, reject) => {
      this.pendingRequests.set(id, { resolve, reject });
      const stdin = this.proc.stdin;
      if (stdin === null) {
        reject(new Error("stdin is null — LSP process may have exited"));
        return;
      }
      stdin.write(encode(msg));

      setTimeout(() => {
        if (this.pendingRequests.has(id)) {
          this.pendingRequests.delete(id);
          reject(new Error(`Timeout waiting for response to ${method} (id=${String(id)})`));
        }
      }, 10000);
    });
  }

  notify(method: string, params: unknown): void {
    const msg: JsonRpcNotification = { jsonrpc: "2.0", method, params };
    const stdin = this.proc.stdin;
    if (stdin === null) {
      throw new Error("stdin is null — LSP process may have exited");
    }
    stdin.write(encode(msg));
  }

  /** Drain collected notifications, optionally filtering by method. */
  drainNotifications(method?: string): LspNotification[] {
    if (method !== undefined) {
      const matching = this.notifications.filter((n) => n.method === method);
      this.notifications = this.notifications.filter((n) => n.method !== method);
      return matching;
    }
    const all = [...this.notifications];
    this.notifications = [];
    return all;
  }

  /** Wait for at least one notification of a given method. */
  async waitForNotification(method: string, timeoutMs: number = 5000): Promise<unknown[]> {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
      const matching = this.notifications.filter((n) => n.method === method);
      if (matching.length > 0) {
        this.notifications = this.notifications.filter((n) => n.method !== method);
        return matching.map((n) => n.params);
      }
      await new Promise<void>((r) => setTimeout(r, 100));
    }
    // Return whatever we have
    const matching = this.notifications.filter((n) => n.method === method);
    this.notifications = this.notifications.filter((n) => n.method !== method);
    return matching.map((n) => n.params);
  }

  kill(): void {
    this.proc.kill();
  }
}

/** Find the LSP binary */
function findLspBinary(): string {
  const rootDir = path.resolve(__dirname, "..", "..", "..");
  const candidates = [
    path.join(rootDir, "..", "lql-lsp-rust", "target", "release", "lql-lsp"),
    path.join(rootDir, "..", "lql-lsp-rust", "target", "debug", "lql-lsp"),
    path.join(rootDir, "bin", "lql-lsp"),
  ];

  for (const candidate of candidates) {
    if (fs.existsSync(candidate)) {
      return candidate;
    }
  }

  throw new Error(`LSP binary not found. Searched:\n${candidates.join("\n")}\nBuild with: cargo build -p lql-lsp`);
}

/** Read a test fixture file */
function readFixture(name: string): string {
  const fixturePath = path.join(__dirname, "..", "fixtures", name);
  const altPath = path.join(__dirname, "..", "..", "..", "src", "test", "fixtures", name);
  if (fs.existsSync(fixturePath)) {
    return fs.readFileSync(fixturePath, "utf8");
  }
  if (fs.existsSync(altPath)) {
    return fs.readFileSync(altPath, "utf8");
  }
  throw new Error(`Fixture not found: ${name}`);
}

suite("LSP Protocol E2E Tests", function () {
  this.timeout(30000);

  let client: LspClient;
  let lspBinary: string;

  suiteSetup(function () {
    try {
      lspBinary = findLspBinary();
    } catch {
      this.skip();
    }
  });

  setup(function () {
    client = new LspClient(lspBinary);
  });

  teardown(function () {
    client.kill();
  });

  /** Common initialization sequence */
  async function initServer(): Promise<LspInitializeResult> {
    const result = await client.request("initialize", {
      processId: process.pid,
      capabilities: {},
      rootUri: null,
    });
    client.notify("initialized", {});
    // Safe: the LSP server always returns an object with capabilities
    assert.ok(isRecord(result), "Initialize result must be an object");
    return result as LspInitializeResult;
  }

  // Implements [LQL-HOVER-TEST-REQUEST].
  async function hoverAt(uri: string, position: LspPosition, message: string): Promise<LspHoverResult> {
    const raw = await client.request("textDocument/hover", {
      textDocument: { uri },
      position,
    });
    assert.ok(raw !== null && raw !== undefined, message);
    // Safe: LSP hover response always has this shape when non-null
    return raw as LspHoverResult;
  }

  /** Open a document and wait for diagnostics */
  async function openDocument(uri: string, content: string): Promise<PublishDiagnosticsParams[]> {
    client.notify("textDocument/didOpen", {
      textDocument: { uri, languageId: "lql", version: 1, text: content },
    });
    const raw = await client.waitForNotification("textDocument/publishDiagnostics", 5000);
    return raw.filter(isDiagnosticsParams);
  }

  // Implements [LQL-PROTOCOL-SHARED-SCENARIOS].
  function assertCompletionLabels(
    labels: string[],
    firstLabel: string,
    firstMessage: string,
    secondLabel: string,
    secondMessage: string,
    thirdLabel: string,
    thirdMessage: string
  ): void {
    assert.ok(labels.includes(firstLabel), firstMessage);
    assert.ok(labels.includes(secondLabel), secondMessage);
    assert.ok(labels.includes(thirdLabel), thirdMessage);
  }

  // Implements [LQL-PROTOCOL-SHARED-SCENARIOS].
  async function openCompletionsAndAssertLabel(
    uri: string,
    content: string,
    character: number,
    expectedLabel: string,
    message: string
  ): Promise<string[]> {
    await openDocument(uri, content);

    const result = await client.request("textDocument/completion", {
      textDocument: { uri: uri },
      position: { line: 0, character: character },
    });

    const items = extractCompletionItems(result);
    const labels = items.map((i) => i.label);

    assert.ok(labels.includes(expectedLabel), message);
    return labels;
  }

  // Implements [LQL-PROTOCOL-SHARED-SCENARIOS].
  async function openAndReadCompletions(
    uri: string,
    content: string,
    character: number
  ): Promise<readonly LspCompletionItem[]> {
    await openDocument(uri, content);

    const result = await client.request("textDocument/completion", {
      textDocument: { uri: uri },
      position: { line: 0, character: character },
    });

    const items = extractCompletionItems(result);
    return items;
  }

  // Implements [LQL-PROTOCOL-SHARED-SCENARIOS].
  async function initializeAndRequestAt(
    uri: string,
    content: string,
    method: string,
    character: number
  ): Promise<unknown> {
    await initServer();
    await openDocument(uri, content);

    const result = await client.request(method, {
      textDocument: { uri: uri },
      position: { line: 0, character: character },
    });
    return result;
  }

  // Implements [LQL-PROTOCOL-SHARED-SCENARIOS].
  async function initializeAndHover(
    content: string,
    uri: string,
    line: number,
    character: number,
    message: string
  ): Promise<LspHoverResult> {
    await initServer();
    const content = content;
    await openDocument(uri, content);

    const hover = await hoverAt(uri, { line: line, character: character }, message);
    return hover;
  }

  // Implements [LQL-PROTOCOL-SHARED-SCENARIOS].
  async function assertHoverAndReadNext(
    previousText: string,
    expectedText: string,
    previousMessage: string,
    uri: string,
    line: number,
    character: number,
    hoverMessage: string
  ): Promise<string> {
    assert.ok(previousText.toLowerCase().includes(expectedText), previousMessage);

    const sumHoverTyped = await hoverAt(uri, { line: line, character: character }, hoverMessage);
    const sumText = extractHoverText(sumHoverTyped);
    return sumText;
  }

  // Implements [LQL-PROTOCOL-SHARED-SCENARIOS].
  async function changeAndReadDiagnostics(
    uri: string,
    content: string,
    message: string
  ): Promise<PublishDiagnosticsParams[]> {
    client.notify("textDocument/didChange", {
      textDocument: { uri: uri, version: 2 },
      contentChanges: [{ text: content }],
    });

    const rawNotifications = await client.waitForNotification("textDocument/publishDiagnostics", 5000);
    const notifications = rawNotifications.filter(isDiagnosticsParams);
    assert.ok(notifications.length > 0, message);
    return notifications;
  }

  // Implements [LQL-PROTOCOL-SHARED-SCENARIOS].
  async function formatAndRequireEdit(
    uri: string,
    resultMessage: string,
    arrayMessage: string,
    editMessage: string
  ): Promise<LspTextEdit> {
    const result = await client.request("textDocument/formatting", {
      textDocument: { uri: uri },
      options: { tabSize: 4, insertSpaces: true },
    });

    assert.ok(result !== null && result !== undefined, resultMessage);
    assert.ok(Array.isArray(result), arrayMessage);
    // Safe: LSP formatting returns an array of TextEdit
    const edits = result as LspTextEdit[];
    assert.ok(edits.length > 0, editMessage);

    const edit = edits[0];
    return edit;
  }

  // Implements [LQL-PROTOCOL-SHARED-SCENARIOS].
  async function openInvalidFixture(fixture: string, uri: string): Promise<PublishDiagnosticsParams[]> {
    await initServer();
    const invalidContent = readFixture(fixture);
    const notifications = await openDocument(uri, invalidContent);
    return notifications;
  }

  // ═══════════════════════════════════════════════════════════════
  // INITIALIZATION
  // ═══════════════════════════════════════════════════════════════

  test("should initialize with correct capabilities", async function () {
    const result = await initServer();

    assert.notStrictEqual(result, undefined, "Initialize result should not be null");
    const caps = result.capabilities;
    assert.notStrictEqual(caps, undefined, "Should have capabilities object");
    assert.notStrictEqual(caps?.textDocumentSync, undefined, "Must support text document sync");
    assert.notStrictEqual(caps?.completionProvider, undefined, "Must support completions (IntelliSense)");
    const triggerChars = caps?.completionProvider?.triggerCharacters;
    assert.notStrictEqual(triggerChars, undefined, "Must have trigger characters");
    if (triggerChars === undefined) {
      assert.fail("Trigger characters must be defined");
    }
    assert.strictEqual(triggerChars.includes("|"), true, "Pipe should be a trigger character");
    assert.strictEqual(triggerChars.includes("."), true, "Dot should be a trigger character");
    if (caps === undefined) {
      assert.fail("Capabilities must be defined");
    }
    assert.strictEqual(caps.hoverProvider, true, "Must support hover (IntelliPrompt)");
    assert.strictEqual(caps.documentSymbolProvider, true, "Must support document symbols");
    assert.strictEqual(caps.documentFormattingProvider, true, "Must support document formatting");
  });

  // ═══════════════════════════════════════════════════════════════
  // COMPLETIONS (IntelliSense)
  // ═══════════════════════════════════════════════════════════════

  test("should provide pipeline completions after |>", async function () {
    const result = await initializeAndRequestAt(
      "file:///test/completion.lql",
      "users |> ",
      "textDocument/completion",
      9
    );

    assert.ok(result !== null && result !== undefined, "Completion result should not be null");
    const items = extractCompletionItems(result);
    assert.ok(items.length > 0, "Should return completion items");

    const labels = items.map((i) => i.label);
    assertCompletionLabels(
      labels,
      "select",
      "Must suggest 'select'",
      "filter",
      "Must suggest 'filter'",
      "join",
      "Must suggest 'join'"
    );
    assertCompletionLabels(
      labels,
      "group_by",
      "Must suggest 'group_by'",
      "order_by",
      "Must suggest 'order_by'",
      "having",
      "Must suggest 'having'"
    );
    assert.ok(labels.includes("limit"), "Must suggest 'limit'");

    const selectItem = items.find((i) => i.label === "select");
    assert.ok(selectItem !== undefined, "select completion must exist");
    assert.ok(selectItem.detail !== undefined, "select must have detail text");
    assert.ok(selectItem.documentation !== undefined, "select must have documentation");
    assert.ok(selectItem.kind !== undefined, "select must have a kind");
  });

  test("should provide aggregate function completions", async function () {
    await initServer();
    const items = await openAndReadCompletions("file:///test/agg.lql", "orders |> select(c", 18);
    const labels = items.map((i) => i.label);
    assertCompletionLabels(
      labels,
      "count",
      "Must suggest 'count'",
      "concat",
      "Must suggest 'concat'",
      "case",
      "Must suggest 'case' keyword"
    );
    assert.ok(labels.includes("coalesce"), "Must suggest 'coalesce'");
  });

  test("should provide keyword completions", async function () {
    await initServer();
    const items = await openAndReadCompletions("file:///test/kw.lql", "l", 1);
    const labels = items.map((i) => i.label);
    assertCompletionLabels(
      labels,
      "let",
      "Must suggest 'let' keyword",
      "lower",
      "Must suggest 'lower' function",
      "length",
      "Must suggest 'length' function"
    );
  });

  // ═══════════════════════════════════════════════════════════════
  // HOVER (IntelliPrompt)
  // ═══════════════════════════════════════════════════════════════

  test("should provide hover info for filter keyword", async function () {
    const hover = await initializeAndHover(
      "users |> filter(fn(row) => row.users.age > 18) |> select(users.name) |> limit(10)",
      "file:///test/hover.lql",
      0,
      12,
      "Hover result for 'filter' must not be null"
    );
    assert.ok(hover.contents !== undefined, "Hover must have contents");
    const filterText = extractHoverText(hover);
    assert.ok(filterText.toLowerCase().includes("filter"), "Hover for 'filter' must mention 'filter'");
    assert.ok(filterText.includes("fn("), "Hover for 'filter' should show lambda syntax");
  });

  test("should provide hover info for select keyword", async function () {
    const hover = await initializeAndHover(
      "users |> filter(fn(row) => row.users.age > 18) |> select(users.name) |> limit(10)",
      "file:///test/hover2.lql",
      0,
      52,
      "Hover result for 'select' must not be null"
    );
    const selectText = extractHoverText(hover);
    assert.ok(selectText.toLowerCase().includes("select"), "Hover for 'select' must mention 'select'");
    assert.ok(selectText.toLowerCase().includes("project"), "Hover for 'select' should describe projection");
  });

  test("should provide hover for aggregate functions", async function () {
    await initServer();
    await openDocument("file:///test/agg_hover.lql", "orders |> select(count(*) as cnt, sum(orders.total) as total)");

    const countHoverTyped = await hoverAt(
      "file:///test/agg_hover.lql",
      { line: 0, character: 19 },
      "Hover for 'count' must not be null"
    );
    assert.ok(countHoverTyped.contents !== undefined, "Hover must have contents");
    const countText = extractHoverText(countHoverTyped);
    const sumText = await assertHoverAndReadNext(
      countText,
      "count",
      "Must describe count function",
      "file:///test/agg_hover.lql",
      0,
      35,
      "Hover for 'sum' must not be null"
    );
    assert.ok(sumText.toLowerCase().includes("sum"), "Must describe sum function");
  });

  test("should return null hover for unknown identifiers", async function () {
    const noHover = await initializeAndRequestAt(
      "file:///test/no_hover.lql",
      "users |> select(users.id)",
      "textDocument/hover",
      2
    );

    assert.strictEqual(noHover, null, "Hover for unknown identifier should be null");
  });

  // ═══════════════════════════════════════════════════════════════
  // DIAGNOSTICS
  // ═══════════════════════════════════════════════════════════════

  test("should publish diagnostics for syntax errors", async function () {
    const notifications = await openInvalidFixture("invalid_syntax.lql", "file:///test/invalid.lql");

    assert.ok(notifications.length > 0, "Must publish diagnostics");
    const diags = notifications[0];
    assert.ok(diags.uri !== undefined, "Diagnostics must have a URI");
    assert.ok(diags.diagnostics.length > 0, "Must have at least one diagnostic for invalid syntax");

    const firstDiag = diags.diagnostics[0];
    assert.ok(firstDiag.range !== undefined, "Diagnostic must have a range");
    assert.ok(firstDiag.range.start !== undefined, "Range must have start");
    assert.ok(firstDiag.range.end !== undefined, "Range must have end");
    assert.ok(firstDiag.message !== undefined, "Diagnostic must have a message");
    assert.ok(firstDiag.message.length > 0, "Message must not be empty");
    assert.ok(firstDiag.severity !== undefined, "Diagnostic must have severity");
  });

  test("should publish clean diagnostics for valid files", async function () {
    const notifications = await openInvalidFixture("simple_select.lql", "file:///test/valid.lql");

    assert.ok(notifications.length > 0, "Must publish diagnostics");
    const diags = notifications[0];
    const errors = diags.diagnostics.filter((d) => d.severity === 1);
    assert.strictEqual(errors.length, 0, `Valid file should have no errors, got: ${JSON.stringify(errors)}`);
  });

  test("should update diagnostics on document change", async function () {
    await initServer();
    await openDocument("file:///test/change.lql", "users |> select(users.id)");

    // Change to invalid content
    const notifications = await changeAndReadDiagnostics(
      "file:///test/change.lql",
      "users |> select(",
      "Must publish updated diagnostics"
    );
    const diags = notifications[notifications.length - 1];
    assert.ok(diags.diagnostics.length > 0, "Changed invalid content should produce diagnostics");
  });

  // ═══════════════════════════════════════════════════════════════
  // DOCUMENT SYMBOLS
  // ═══════════════════════════════════════════════════════════════

  test("should return document symbols for let bindings", async function () {
    await initServer();
    const content = readFixture("complex_pipeline.lql");
    await openDocument("file:///test/symbols.lql", content);

    const result = await client.request("textDocument/documentSymbol", {
      textDocument: { uri: "file:///test/symbols.lql" },
    });

    assert.ok(result !== null && result !== undefined, "Document symbols result should not be null");
    assert.ok(Array.isArray(result), "Symbols should be an array");
    // Safe: LSP documentSymbol returns an array of SymbolInformation
    const symbols = result as LspDocumentSymbol[];
    assert.ok(symbols.length >= 2, "Should find at least 2 let bindings (completed_orders, summary)");

    const names = symbols.map((s) => s.name);
    assert.ok(names.includes("completed_orders"), "Must find 'completed_orders' binding");
    assert.ok(names.includes("summary"), "Must find 'summary' binding");

    const sym = symbols[0];
    assert.ok(sym.name !== undefined, "Symbol must have a name");
    assert.ok(sym.kind !== undefined, "Symbol must have a kind");
    assert.ok(sym.location !== undefined, "Symbol must have a location");
    assert.ok(sym.location.range !== undefined, "Symbol location must have a range");
  });

  // ═══════════════════════════════════════════════════════════════
  // FORMATTING
  // ═══════════════════════════════════════════════════════════════

  test("should format LQL documents", async function () {
    await initServer();
    const uglyContent = "  users   |>   select(  users.id  ,  users.name  )  ";
    await openDocument("file:///test/format.lql", uglyContent);

    const edit = await formatAndRequireEdit(
      "file:///test/format.lql",
      "Formatting result should not be null",
      "Formatting should return text edits",
      "Should have at least one edit"
    );
    assert.ok(edit.range !== undefined, "Edit must have a range");
    assert.ok(edit.newText !== undefined, "Edit must have new text");
    assert.ok(edit.newText.trim().length > 0, "Formatted text should not be empty");
  });

  // ═══════════════════════════════════════════════════════════════
  // COMPLEX REAL-WORLD SCENARIOS
  // ═══════════════════════════════════════════════════════════════

  test("should handle complex pipeline with all features", async function () {
    const notifications = await openInvalidFixture("complex_pipeline.lql", "file:///test/complex.lql");

    assert.ok(notifications.length > 0, "Must publish diagnostics");
    const diags = notifications[0];
    const parseErrors = diags.diagnostics.filter((d) => d.severity === 1 && d.source === "lql");
    assert.strictEqual(
      parseErrors.length,
      0,
      `Complex pipeline should parse without errors, got: ${JSON.stringify(parseErrors)}`
    );

    const result = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/complex.lql" },
      position: { line: 17, character: 0 },
    });
    assert.ok(result !== null && result !== undefined, "Should provide completions in complex document");
  });

  test("should handle window function documents cleanly", async function () {
    const notifications = await openInvalidFixture("window_functions.lql", "file:///test/window.lql");

    assert.ok(notifications.length > 0);
    const diags = notifications[0];
    const errors = diags.diagnostics.filter((d) => d.severity === 1);
    assert.strictEqual(errors.length, 0, "Window function file should parse without errors");
  });

  test("should handle case expression documents cleanly", async function () {
    const notifications = await openInvalidFixture("case_expression.lql", "file:///test/case.lql");

    const diags = notifications[0];
    const errors = diags.diagnostics.filter((d) => d.severity === 1);
    assert.strictEqual(errors.length, 0, "Case expression should parse cleanly");
  });

  test("should handle exists subquery documents cleanly", async function () {
    const notifications = await openInvalidFixture("subquery_exists.lql", "file:///test/exists.lql");

    const diags = notifications[0];
    const errors = diags.diagnostics.filter((d) => d.severity === 1);
    assert.strictEqual(errors.length, 0, "Exists subquery should parse cleanly");
  });

  // ═══════════════════════════════════════════════════════════════
  // LIFECYCLE
  // ═══════════════════════════════════════════════════════════════

  test("should handle shutdown gracefully", async function () {
    await initServer();
    const result = await client.request("shutdown");
    assert.strictEqual(result, null, "Shutdown should return null");
    client.notify("exit", null);
  });

  test("should handle multiple documents simultaneously", async function () {
    await initServer();
    await openDocument("file:///test/doc1.lql", "users |> select(users.id)");
    await openDocument("file:///test/doc2.lql", "orders |> select(orders.total)");

    const result1 = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/doc1.lql" },
      position: { line: 0, character: 24 },
    });
    const result2 = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/doc2.lql" },
      position: { line: 0, character: 30 },
    });

    assert.ok(result1 !== null && result1 !== undefined, "Doc1 should return completions");
    assert.ok(result2 !== null && result2 !== undefined, "Doc2 should return completions");
  });

  test("should handle document close and reopen", async function () {
    await initServer();
    await openDocument("file:///test/reopen.lql", "users |> select(users.id)");

    client.notify("textDocument/didClose", {
      textDocument: { uri: "file:///test/reopen.lql" },
    });

    await new Promise<void>((r) => setTimeout(r, 300));

    await openDocument("file:///test/reopen.lql", "orders |> select(orders.total)");

    const result = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/reopen.lql" },
      position: { line: 0, character: 30 },
    });

    assert.ok(result !== null && result !== undefined, "Should provide completions after reopen");
  });

  // ═══════════════════════════════════════════════════════════════
  // INTELLISENSE PROOF — Deep completions testing (ZERO MOCKING)
  // ═══════════════════════════════════════════════════════════════

  test("PROOF: IntelliSense delivers context-aware completions after pipe in real multiline pipeline", async function () {
    await initServer();
    const content = `let active_users = users
|> filter(fn(row) => row.users.status = 'active')
|> `;
    await openDocument("file:///test/intellisense_proof1.lql", content);

    const result = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/intellisense_proof1.lql" },
      position: { line: 2, character: 3 },
    });

    const items = extractCompletionItems(result);
    const labels = items.map((i) => i.label);

    // All pipeline operations MUST be offered after |>
    const requiredOps = [
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
      "insert",
    ];
    for (const op of requiredOps) {
      assert.ok(labels.includes(op), `IntelliSense MUST suggest '${op}' after |>`);
    }

    // Each completion MUST have kind, detail, documentation, and insert_text
    for (const op of requiredOps) {
      const item = items.find((i) => i.label === op);
      assert.ok(item !== undefined, `Completion for '${op}' must exist`);
      assert.ok(item.kind !== undefined, `'${op}' must have a completion kind`);
      assert.ok(item.detail !== undefined && item.detail.length > 0, `'${op}' must have non-empty detail`);
      assert.ok(
        item.documentation !== undefined && item.documentation.length > 0,
        `'${op}' must have non-empty documentation`
      );
    }
  });

  test("PROOF: IntelliSense delivers function completions inside select() arguments", async function () {
    await initServer();
    const content = "orders |> select(";
    await openDocument("file:///test/intellisense_proof2.lql", content);

    const result = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/intellisense_proof2.lql" },
      position: { line: 0, character: 17 },
    });

    const items = extractCompletionItems(result);
    const labels = items.map((i) => i.label);

    // Aggregate functions MUST be available inside select()
    const requiredFunctions = ["count", "sum", "avg", "max", "min"];
    for (const fn of requiredFunctions) {
      assert.ok(labels.includes(fn), `IntelliSense MUST suggest aggregate function '${fn}' inside select()`);
    }

    // String functions MUST be available
    const stringFunctions = ["concat", "substring", "length", "trim", "upper", "lower"];
    for (const fn of stringFunctions) {
      assert.ok(labels.includes(fn), `IntelliSense MUST suggest string function '${fn}' inside select()`);
    }

    // Window functions MUST be available
    assertCompletionLabels(
      labels,
      "row_number",
      "Must suggest 'row_number' window function",
      "rank",
      "Must suggest 'rank' window function",
      "dense_rank",
      "Must suggest 'dense_rank' window function"
    );
  });

  test("PROOF: IntelliSense completions have correct LSP completion item kinds", async function () {
    await initServer();
    const items = await openAndReadCompletions("file:///test/intellisense_kinds.lql", "orders |> ", 10);

    // LSP completion item kind: Function = 3, Keyword = 14, Snippet = 15
    const selectItem = items.find((i) => i.label === "select");
    assert.ok(selectItem !== undefined, "Must have 'select' completion");
    assert.ok(
      selectItem.kind === 3 || selectItem.kind === 14 || selectItem.kind === 15,
      `'select' must have a valid LSP completion kind, got: ${String(selectItem.kind)}`
    );

    // Verify insert text has snippet syntax for complex operations
    const joinItem = items.find((i) => i.label === "join");
    assert.ok(joinItem !== undefined, "Must have 'join' completion");
    assert.ok(joinItem.insertText !== undefined, "'join' must have insertText for snippet expansion");

    // Verify filter has lambda snippet
    const filterItem = items.find((i) => i.label === "filter");
    assert.ok(filterItem !== undefined, "Must have 'filter' completion");
    assert.ok(filterItem.insertText !== undefined, "'filter' must have insertText for lambda snippet");
  });

  test("PROOF: IntelliSense delivers prefix-filtered completions after pipe", async function () {
    await initServer();
    // Place cursor right after "|> s" — the word prefix is "s", after_pipe detected
    const labels = await openCompletionsAndAssertLabel(
      "file:///test/prefix_filter.lql",
      "orders |> s",
      11,
      "select",
      "Must include 'select' matching prefix 's'"
    );
    // Pipeline ops NOT starting with 's' must be filtered out
    assert.ok(!labels.includes("filter"), "Must NOT include 'filter' (doesn't start with 's')");
    assert.ok(!labels.includes("join"), "Must NOT include 'join' (doesn't start with 's')");
    assert.ok(!labels.includes("limit"), "Must NOT include 'limit' (doesn't start with 's')");
    // Functions starting with 's' should still be available
    assert.ok(labels.includes("sum"), "Must include 'sum' matching prefix 's'");
    assert.ok(labels.includes("substring"), "Must include 'substring' matching prefix 's'");
  });

  // ═══════════════════════════════════════════════════════════════
  // INTELLIPROMPT PROOF — Deep hover testing (ZERO MOCKING)
  // ═══════════════════════════════════════════════════════════════

  test("PROOF: IntelliPrompt delivers rich Markdown hover with signature for ALL pipeline ops", async function () {
    const filterHover = await initializeAndHover(
      `users
|> filter(fn(row) => row.users.active = true)
|> join(orders, on = users.id = orders.user_id)
|> select(users.name, count(*) as order_count)
|> group_by(users.name)
|> having(fn(g) => count(*) > 5)
|> order_by(order_count desc)
|> limit(100)
|> offset(10)`,
      "file:///test/intelliprompt_proof1.lql",
      1,
      5,
      "Hover for 'filter' must return data"
    );
    assert.ok(filterHover.contents !== undefined, "Hover must have contents");
    const filterText = extractHoverText(filterHover);
    assert.ok(filterText.includes("filter"), "Hover must mention 'filter'");
    assert.ok(filterText.includes("fn("), "Hover must show lambda syntax for filter");
    assert.ok(filterText.includes("```"), "Hover must contain code block with signature");

    // Hover over 'join' at line 2, char ~5
    const joinHover = await hoverAt(
      "file:///test/intelliprompt_proof1.lql",
      { line: 2, character: 5 },
      "Hover for 'join' must return data"
    );
    const joinText = extractHoverText(joinHover);
    assert.ok(joinText.toLowerCase().includes("join"), "Hover must describe join");
    assert.ok(joinText.includes("```"), "Hover must contain code signature for join");

    // Hover over 'select' at line 3, char ~5
    const selectHover = await hoverAt(
      "file:///test/intelliprompt_proof1.lql",
      { line: 3, character: 5 },
      "Hover for 'select' must return data"
    );
    const selectText = extractHoverText(selectHover);
    assert.ok(selectText.toLowerCase().includes("select"), "Hover must describe select");
    const groupText = await assertHoverAndReadNext(
      selectText,
      "project",
      "Hover must explain select is for projection",
      "file:///test/intelliprompt_proof1.lql",
      4,
      5,
      "Hover for 'group_by' must return data"
    );
    const havingText = await assertHoverAndReadNext(
      groupText,
      "group",
      "Hover must describe grouping",
      "file:///test/intelliprompt_proof1.lql",
      5,
      5,
      "Hover for 'having' must return data"
    );
    assert.ok(havingText.toLowerCase().includes("having"), "Hover must describe having");
    const orderText = await assertHoverAndReadNext(
      havingText,
      "filter",
      "Having hover must mention filtering groups",
      "file:///test/intelliprompt_proof1.lql",
      6,
      5,
      "Hover for 'order_by' must return data"
    );
    const limitText = await assertHoverAndReadNext(
      orderText,
      "order",
      "Hover must describe ordering",
      "file:///test/intelliprompt_proof1.lql",
      7,
      5,
      "Hover for 'limit' must return data"
    );
    const offsetText = await assertHoverAndReadNext(
      limitText,
      "limit",
      "Hover must describe limit",
      "file:///test/intelliprompt_proof1.lql",
      8,
      5,
      "Hover for 'offset' must return data"
    );
    assert.ok(offsetText.toLowerCase().includes("offset"), "Hover must describe offset");
    assert.ok(offsetText.toLowerCase().includes("skip"), "Offset hover must mention skipping rows");
  });

  test("PROOF: IntelliPrompt delivers hover for aggregate functions in real context", async function () {
    const countHover = await initializeAndHover(
      "orders |> select(count(*) as cnt, sum(orders.total) as total_sum, avg(orders.total) as avg_total, max(orders.total) as high, min(orders.total) as low)",
      "file:///test/intelliprompt_agg.lql",
      0,
      19,
      "Must get hover for 'count'"
    );
    assert.ok(typeof countHover.contents !== "string", "Count hover contents must not be a plain string");
    assert.ok(countHover.contents?.kind === "markdown", "Hover contents must be Markdown format");
    assert.ok(countHover.contents.value?.includes("count") === true, "Count hover must describe count function");

    // Hover over 'sum' at position ~37
    const sumHover = await hoverAt(
      "file:///test/intelliprompt_agg.lql",
      { line: 0, character: 35 },
      "Must get hover for 'sum'"
    );
    assert.ok(typeof sumHover.contents !== "string", "Sum hover contents must not be a plain string");
    assert.ok(sumHover.contents?.kind === "markdown", "Sum hover must be Markdown");
    assert.ok(extractHoverText(sumHover).toLowerCase().includes("sum"), "Sum hover must describe sum");

    // Hover over 'avg'
    const avgHover = await hoverAt(
      "file:///test/intelliprompt_agg.lql",
      { line: 0, character: 69 },
      "Must get hover for 'avg'"
    );
    assert.ok(extractHoverText(avgHover).toLowerCase().includes("avg"), "Avg hover must describe avg");

    // Hover over 'max'
    const maxHover = await hoverAt(
      "file:///test/intelliprompt_agg.lql",
      { line: 0, character: 99 },
      "Must get hover for 'max'"
    );
    assert.ok(extractHoverText(maxHover).toLowerCase().includes("max"), "Max hover must describe max");

    // Hover over 'min'
    const minHover = await hoverAt(
      "file:///test/intelliprompt_agg.lql",
      { line: 0, character: 127 },
      "Must get hover for 'min'"
    );
    assert.ok(extractHoverText(minHover).toLowerCase().includes("min"), "Min hover must describe min");
  });

  test("PROOF: IntelliPrompt delivers hover for string functions", async function () {
    const concatHover = await initializeAndHover(
      "users |> select(concat(users.first, users.last) as name, upper(users.email) as email_upper, trim(users.bio) as bio, length(users.bio) as bio_len)",
      "file:///test/intelliprompt_string.lql",
      0,
      18,
      "Must get hover for 'concat'"
    );
    assert.ok(
      extractHoverText(concatHover).toLowerCase().includes("concat"),
      "Concat hover must describe concatenation"
    );
    assert.ok(extractHoverText(concatHover).includes("```"), "Concat hover must include code signature");

    // Hover over 'upper'
    const upperHover = await hoverAt(
      "file:///test/intelliprompt_string.lql",
      { line: 0, character: 60 },
      "Must get hover for 'upper'"
    );
    assert.ok(extractHoverText(upperHover).toLowerCase().includes("upper"), "Upper hover must describe uppercase");

    // Hover over 'trim'
    const trimHover = await hoverAt(
      "file:///test/intelliprompt_string.lql",
      { line: 0, character: 94 },
      "Must get hover for 'trim'"
    );
    assert.ok(extractHoverText(trimHover).toLowerCase().includes("trim"), "Trim hover must describe trimming");

    // Hover over 'length'
    const lengthHover = await hoverAt(
      "file:///test/intelliprompt_string.lql",
      { line: 0, character: 117 },
      "Must get hover for 'length'"
    );
    assert.ok(
      extractHoverText(lengthHover).toLowerCase().includes("length"),
      "Length hover must describe string length"
    );
  });

  test("PROOF: IntelliPrompt returns null for non-LQL identifiers", async function () {
    const tableHover = await initializeAndRequestAt(
      "file:///test/intelliprompt_null.lql",
      "my_custom_table |> select(my_custom_table.some_column)",
      "textDocument/hover",
      5
    );
    assert.strictEqual(tableHover, null, "Custom table names must NOT trigger hover (not a language construct)");

    // Hover over 'some_column' — not a keyword, should return null
    const colHover = await client.request("textDocument/hover", {
      textDocument: { uri: "file:///test/intelliprompt_null.lql" },
      position: { line: 0, character: 45 },
    });
    assert.strictEqual(colHover, null, "Custom column names must NOT trigger hover");
  });

  // ═══════════════════════════════════════════════════════════════
  // DIAGNOSTICS PROOF — Real error detection (ZERO MOCKING)
  // ═══════════════════════════════════════════════════════════════

  test("PROOF: LSP detects real syntax errors and provides actionable range info", async function () {
    await initServer();
    // Unclosed parenthesis
    const notifications = await openDocument("file:///test/diag_proof1.lql", "users |> select(users.id, users.name");

    assert.ok(notifications.length > 0, "Must publish diagnostics");
    const diagsList = notifications[0].diagnostics;
    assert.ok(diagsList.length > 0, "Must detect unclosed parenthesis");

    const error = diagsList.find((d) => d.severity === 1);
    assert.ok(error !== undefined, "Must report at least one error-level diagnostic");
    assert.ok(error.range !== undefined, "Error diagnostic must have a range");
    assert.ok(error.range.start !== undefined, "Range must have start");
    assert.ok(error.range.start.line >= 0, "Range must have valid start line");
    assert.ok(error.range.start.character >= 0, "Range must have valid start character");
    assert.ok(error.message !== undefined, "Error diagnostic must have a message");
    assert.ok(error.message.length > 0, "Error message must not be empty");
    assert.ok(error.source === "lql", "Error source must be 'lql'");
  });

  test("PROOF: LSP reports zero errors on valid complex multiline LQL", async function () {
    await initServer();
    const validContent = `let completed_orders = orders
|> filter(fn(row) => row.orders.status = 'completed')
|> join(users, on = orders.user_id = users.id)
|> select(users.name, orders.total)

let summary = completed_orders
|> group_by(users.name)
|> select(users.name, sum(orders.total) as total_spent, count(*) as order_count)
|> order_by(total_spent desc)
|> limit(50)`;
    const notifications = await openDocument("file:///test/diag_proof2.lql", validContent);

    assert.ok(notifications.length > 0, "Must publish diagnostics");
    const errors = notifications[0].diagnostics.filter((d) => d.severity === 1);
    assert.strictEqual(
      errors.length,
      0,
      `Valid multiline pipeline must produce zero errors, got: ${JSON.stringify(errors)}`
    );
  });

  test("PROOF: LSP detects errors and then clears them when content is fixed", async function () {
    await initServer();
    // Open with invalid content
    const badNotifications = await openDocument("file:///test/diag_fix.lql", "users |> select(");
    const badErrors = badNotifications[0].diagnostics.filter((d) => d.severity === 1);
    assert.ok(badErrors.length > 0, "Must detect syntax error in broken content");

    // Fix the content
    const fixedNotifications = await changeAndReadDiagnostics(
      "file:///test/diag_fix.lql",
      "users |> select(users.id)",
      "Must publish updated diagnostics"
    );
    const fixedErrors = fixedNotifications[fixedNotifications.length - 1].diagnostics.filter((d) => d.severity === 1);
    assert.strictEqual(fixedErrors.length, 0, "After fixing content, all errors must be cleared");
  });

  // ═══════════════════════════════════════════════════════════════
  // DOCUMENT SYMBOLS PROOF (ZERO MOCKING)
  // ═══════════════════════════════════════════════════════════════

  test("PROOF: LSP extracts document symbols from let bindings with correct locations", async function () {
    await initServer();
    const content = `let users_active = users
|> filter(fn(row) => row.users.active = true)

let order_summary = orders
|> group_by(orders.user_id)
|> select(orders.user_id, count(*) as cnt)

let final_report = users_active
|> join(order_summary, on = users.id = orders.user_id)
|> select(users.name, order_summary.cnt)`;
    await openDocument("file:///test/symbols_proof.lql", content);

    const result = await client.request("textDocument/documentSymbol", {
      textDocument: { uri: "file:///test/symbols_proof.lql" },
    });

    assert.ok(result !== null && result !== undefined, "Document symbols must not be null");
    assert.ok(Array.isArray(result), "Symbols must be an array");
    // Safe: LSP documentSymbol returns an array of SymbolInformation
    const symbols = result as LspDocumentSymbol[];
    assert.strictEqual(symbols.length, 3, "Must find exactly 3 let bindings");

    const names = symbols.map((s) => s.name);
    assert.ok(names.includes("users_active"), "Must find 'users_active' binding");
    assert.ok(names.includes("order_summary"), "Must find 'order_summary' binding");
    assert.ok(names.includes("final_report"), "Must find 'final_report' binding");

    // Each symbol must have proper location with range
    for (const sym of symbols) {
      assert.ok(sym.name !== undefined, "Symbol must have a name");
      assert.ok(sym.kind !== undefined, "Symbol must have a kind");
      assert.ok(sym.location !== undefined, "Symbol must have a location");
      assert.ok(sym.location.uri !== undefined, "Symbol location must have a URI");
      assert.ok(sym.location.range !== undefined, "Symbol location must have a range");
      assert.ok(sym.location.range.start !== undefined, "Range must have start");
      assert.ok(sym.location.range.start.line >= 0, "Range start must have valid line");
    }
  });

  // ═══════════════════════════════════════════════════════════════
  // FORMATTING PROOF (ZERO MOCKING)
  // ═══════════════════════════════════════════════════════════════

  test("PROOF: LSP formats messy multiline LQL into properly indented output", async function () {
    await initServer();
    // Multiline with bad indentation — formatter normalizes leading whitespace per line
    const messy = `       users
          |> filter(fn(row) => row.users.active = true)
  |> select(users.name, users.email)`;
    await openDocument("file:///test/format_proof.lql", messy);

    const edit = await formatAndRequireEdit(
      "file:///test/format_proof.lql",
      "Formatting must return edits",
      "Edits must be an array",
      "Must have at least one edit"
    );
    assert.ok(edit.newText !== undefined, "Edit must have newText");
    assert.ok(edit.newText !== messy, "Formatted text must differ from messy input");
    // First line should not have leading whitespace (it's a table name)
    const lines = edit.newText.split("\n").filter((l: string) => l.trim().length > 0);
    assert.ok(lines.length >= 3, "Must have at least 3 non-empty lines");
    assert.ok(lines[0].trimStart() === lines[0].trim(), "First line should be properly trimmed");
    // Pipeline lines should have consistent indentation
    const pipelines = lines.filter((l: string) => l.trim().startsWith("|>"));
    for (const pl of pipelines) {
      assert.ok(pl.startsWith("    "), "Pipeline continuation lines must be indented");
    }
    assert.ok(edit.newText.trim().length > 0, "Formatted text must not be empty");
  });

  // ═══════════════════════════════════════════════════════════════
  // REAL-WORLD COMPLEX SCENARIO PROOF (ZERO MOCKING)
  // ═══════════════════════════════════════════════════════════════

  test("PROOF: Full E2E workflow — open, complete, hover, diagnose, format in sequence", async function () {
    await initServer();

    // Step 1: Open a real document
    const content = `orders
|> filter(fn(row) => row.orders.amount > 100)
|> join(users, on = orders.user_id = users.id)
|> select(users.name, orders.amount, orders.date)
|> order_by(orders.amount desc)
|> limit(50)`;
    const diagNotifs = await openDocument("file:///test/full_workflow.lql", content);

    // Step 2: Assert diagnostics published — zero errors on valid content
    assert.ok(diagNotifs.length > 0, "Must publish diagnostics on open");
    const errors = diagNotifs[0].diagnostics.filter((d) => d.severity === 1);
    assert.strictEqual(errors.length, 0, "Valid document must have zero errors");

    // Step 3: Request completions at a meaningful position
    const completions = await client.request("textDocument/completion", {
      textDocument: { uri: "file:///test/full_workflow.lql" },
      position: { line: 5, character: 0 },
    });
    assert.ok(completions !== null && completions !== undefined, "Must get completions");

    // Step 4: Hover over 'filter' on line 1
    const hover = await hoverAt("file:///test/full_workflow.lql", { line: 1, character: 5 }, "Must get hover info");
    assert.ok(typeof hover.contents !== "string", "Hover contents must not be a plain string");
    assert.ok(hover.contents?.kind === "markdown", "Hover must be Markdown");
    assert.ok(hover.contents.value?.includes("filter") === true, "Hover must describe filter");

    // Step 5: Request document symbols
    const symbols = await client.request("textDocument/documentSymbol", {
      textDocument: { uri: "file:///test/full_workflow.lql" },
    });
    assert.ok(symbols !== undefined, "Must get symbol result (even if empty array)");

    // Step 6: Request formatting
    void (await client.request("textDocument/formatting", {
      textDocument: { uri: "file:///test/full_workflow.lql" },
      options: { tabSize: 4, insertSpaces: true },
    }));
    // Formatting may return null if already formatted
    // Just assert it doesn't error

    // Step 7: Modify document and verify diagnostics update
    const newDiags = await changeAndReadDiagnostics(
      "file:///test/full_workflow.lql",
      "orders |> select(",
      "Must publish updated diagnostics after change"
    );
    const newErrors = newDiags[newDiags.length - 1].diagnostics.filter((d) => d.severity === 1);
    assert.ok(newErrors.length > 0, "Broken content must produce errors");
  });

  test("PROOF: IntelliSense works for LQL keywords in empty doc and lambda context", async function () {
    await initServer();

    // In empty document: keywords like 'let', 'fn', 'case' should be available
    const emptyItems = await openAndReadCompletions("file:///test/keyword_proof.lql", "", 0);
    const emptyLabels = emptyItems.map((i) => i.label);

    assertCompletionLabels(
      emptyLabels,
      "let",
      "Must suggest 'let' keyword",
      "case",
      "Must suggest 'case' keyword",
      "fn",
      "Must suggest 'fn' keyword"
    );
    assert.ok(emptyLabels.includes("distinct"), "Must suggest 'distinct' keyword");
    assert.ok(emptyLabels.includes("coalesce"), "Must suggest 'coalesce' function");

    // Keyword completions must have documentation
    for (const kw of ["let", "case", "fn"]) {
      const item = emptyItems.find((i) => i.label === kw);
      assert.ok(item !== undefined, `Must have '${kw}' completion`);
      assert.ok(item.documentation !== undefined && item.documentation.length > 0, `'${kw}' must have documentation`);
    }

    // In lambda context: 'exists', 'and', 'or', 'not', 'in', 'like' should be offered
    const lambdaItems = await openAndReadCompletions(
      "file:///test/keyword_lambda.lql",
      "users |> filter(fn(row) => row.users.active = true and ",
      56
    );
    const lambdaLabels = lambdaItems.map((i) => i.label);

    assertCompletionLabels(
      lambdaLabels,
      "exists",
      "Must suggest 'exists' in lambda context",
      "and",
      "Must suggest 'and' in lambda context",
      "or",
      "Must suggest 'or' in lambda context"
    );
    assertCompletionLabels(
      lambdaLabels,
      "not",
      "Must suggest 'not' in lambda context",
      "in",
      "Must suggest 'in' in lambda context",
      "like",
      "Must suggest 'like' in lambda context"
    );
  });

  test("PROOF: IntelliPrompt hover for 'let' and 'fn' keywords", async function () {
    const letHover = await initializeAndHover(
      "let result = users |> filter(fn(row) => row.users.id > 0)",
      "file:///test/kw_hover_proof.lql",
      0,
      1,
      "Must get hover for 'let'"
    );
    assert.ok(extractHoverText(letHover).toLowerCase().includes("bind"), "Let hover must describe binding");
    assert.ok(extractHoverText(letHover).includes("```"), "Let hover must include code signature");

    // Hover over 'fn' at position ~29
    const fnHover = await hoverAt(
      "file:///test/kw_hover_proof.lql",
      { line: 0, character: 29 },
      "Must get hover for 'fn'"
    );
    assert.ok(extractHoverText(fnHover).toLowerCase().includes("lambda"), "fn hover must describe lambda expressions");
  });

  test("PROOF: Window function completions available with correct prefix filtering", async function () {
    await initServer();

    // Prefix "ro" — should match row_number, round
    const labels1 = await openCompletionsAndAssertLabel(
      "file:///test/window_complete1.lql",
      "orders |> select(ro",
      19,
      "row_number",
      "Must suggest 'row_number' with prefix 'ro'"
    );
    assert.ok(labels1.includes("round"), "Must suggest 'round' with prefix 'ro'");

    // Prefix "row" — should match row_number only
    const labels2 = await openCompletionsAndAssertLabel(
      "file:///test/window_complete2.lql",
      "orders |> select(row",
      20,
      "row_number",
      "Must suggest 'row_number' with prefix 'row'"
    );
    assert.ok(!labels2.includes("round"), "Must NOT suggest 'round' with prefix 'row' (prefix mismatch)");
    assert.ok(!labels2.includes("rank"), "Must NOT suggest 'rank' with prefix 'row' (prefix mismatch)");

    // Prefix "ra" — should match rank only
    const labels3 = await openCompletionsAndAssertLabel(
      "file:///test/window_complete3.lql",
      "orders |> select(ra",
      19,
      "rank",
      "Must suggest 'rank' with prefix 'ra'"
    );
    assert.ok(!labels3.includes("row_number"), "Must NOT suggest 'row_number' with prefix 'ra'");
  });
});
