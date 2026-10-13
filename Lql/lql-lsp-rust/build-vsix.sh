#!/usr/bin/env bash
# ============================================================================
# Build the Rust LSP server (release), package the VS Code extension (.vsix),
# install the extension, and place the LSP binary where the extension expects it.
#
# Usage:
#   ./build-vsix.sh
# ============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
EXT_DIR="$SCRIPT_DIR/../LqlExtension"
LSP_BINARY="$SCRIPT_DIR/target/release/lql-lsp"
GLOBAL_STORAGE="$HOME/Library/Application Support/Code/User/globalStorage/lql-team.lql-language-support/bin"

# ── 1. Build Rust LSP (release) ─────────────────────────────────────────────
echo ">>> Building LQL LSP server (release)..."
cd "$SCRIPT_DIR"
cargo build --release -p lql-lsp 2>&1
echo ">>> LSP binary: $LSP_BINARY"

# ── 2. Build the VS Code extension ──────────────────────────────────────────
echo ">>> Installing extension npm dependencies..."
cd "$EXT_DIR"
npm install --no-audit --no-fund

echo ">>> Compiling TypeScript..."
npm run compile

echo ">>> Packaging VSIX..."
npx vsce package --no-git-tag-version --no-update-package-json
VSIX_FILE=$(ls -t "$EXT_DIR"/*.vsix 2>/dev/null | head -1)

if [ -z "$VSIX_FILE" ]; then
    echo "ERROR: VSIX file not found after packaging"
    exit 1
fi

echo ">>> VSIX: $VSIX_FILE"

# ── 3. Install extension into VS Code ───────────────────────────────────────
echo ">>> Installing extension into VS Code..."
code --install-extension "$VSIX_FILE" --force

# ── 4. Place LSP binary where the extension downloads it to ─────────────────
echo ">>> Placing LSP binary in extension global storage..."
mkdir -p "$GLOBAL_STORAGE"
cp "$LSP_BINARY" "$GLOBAL_STORAGE/lql-lsp"
chmod +x "$GLOBAL_STORAGE/lql-lsp"

echo ""
echo "=== Build complete ==="
echo "  LSP binary:    $LSP_BINARY"
echo "  VSIX:          $VSIX_FILE"
echo "  Installed to:  $GLOBAL_STORAGE/lql-lsp"
echo ""
echo ">>> Reload VS Code to activate."
