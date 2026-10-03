#!/usr/bin/env bash
set -euo pipefail

echo "==> Setting up development environment..."

# ---- .NET ----
if command -v dotnet &>/dev/null; then
  dotnet restore
  dotnet tool restore
fi

# ---- Rust (for LQL LSP) ----
if command -v cargo &>/dev/null; then
  cd Lql/lql-lsp-rust && cargo build && cd ../..
fi

# ---- Node (for LQL Extension) ----
if command -v npm &>/dev/null && [[ -f Lql/LqlExtension/package.json ]]; then
  cd Lql/LqlExtension && npm install --no-audit --no-fund && cd ../..
fi

echo "==> Setup complete. Run 'make ci' to validate."
