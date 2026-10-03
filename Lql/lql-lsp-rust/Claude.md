# lql-lsp-rust — AI guidance

## Migration to `lspkit`

The cross-cutting LSP scaffolding in this repo (tower-lsp boilerplate, document store, diagnostics pipeline, init-options config) is being distilled into the generic `lspkit-*` workspace at `/Users/christianfindlay/Documents/Code/lsp_toolkit`.

**For new LSP infrastructure work:** prefer `lspkit-*` crates over reinventing it here.
**For changes to existing scaffolding in this repo:** flag in the PR description if the patch duplicates `lspkit` functionality, and reference the upstream crate.

Mapping (current → toolkit crate):

| Current file | Toolkit crate |
|---|---|
| `crates/lql-lsp/src/main.rs` document `HashMap<Url, String>` (lines 19–38, 310–337) | `lspkit-vfs` (`Vfs`, `DocumentUri`, incremental edits) |
| `crates/lql-lsp/src/main.rs` `tower-lsp` setup (lines 633–649, 206–245) | `lspkit-server` (hand-rolled JSON-RPC + `Dispatcher` + `Capabilities`) — **note:** toolkit does not depend on `tower-lsp` (unmaintained) |
| `crates/lql-lsp/src/main.rs` diagnostics collection (lines 45–92) | `lspkit-server::diagnostics::DiagnosticsBus` |
| `crates/lql-lsp/src/main.rs` `initializationOptions` parsing (lines 207–220) | `lspkit-config::load_from_ancestor` (file-backed) or consumer code reading init options |
| `crates/lql-lsp/tests/lsp_protocol.rs` LSP harness | (not yet in toolkit; harness crate is a v0.1 follow-up) |

Code in this repo is **not** being removed — it stays canonical until the toolkit matures. This note exists so future agents reuse `lspkit` for new servers and avoid widening this repo's scaffolding.
