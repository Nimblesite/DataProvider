#!/usr/bin/env bash
# ============================================================================
# LQL LSP + Ollama AI Setup
#
# Installs Ollama, pulls a lightweight code completion model, and builds the
# Rust LSP server. After running this, the LSP will use a REAL local model
# for AI-powered completions.
#
# Usage:
#   ./setup-ai.sh                  # Default: qwen2.5-coder:1.5b
#   ./setup-ai.sh codellama:7b     # Use a specific model
#   LQL_AI_MODEL=deepseek-coder:1.3b ./setup-ai.sh  # Via env var
# ============================================================================

set -euo pipefail

MODEL="${1:-${LQL_AI_MODEL:-qwen2.5-coder:1.5b}}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
OLLAMA_ENDPOINT="${OLLAMA_ENDPOINT:-http://localhost:11434}"

echo "=== LQL LSP AI Setup ==="
echo "Model:    $MODEL"
echo "Endpoint: $OLLAMA_ENDPOINT"
echo ""

# ── 1. Install Ollama if not present ────────────────────────────────────────
if ! command -v ollama &>/dev/null; then
    echo ">>> Installing Ollama..."
    curl -fsSL https://ollama.com/install.sh | sh
    echo ">>> Ollama installed."
else
    echo ">>> Ollama already installed: $(ollama --version 2>/dev/null || echo 'unknown version')"
fi

# ── 2. Start Ollama server if not running ───────────────────────────────────
if ! curl -sf "$OLLAMA_ENDPOINT/api/tags" &>/dev/null; then
    echo ">>> Starting Ollama server..."
    ollama serve &>/dev/null &
    OLLAMA_PID=$!
    echo ">>> Waiting for Ollama to be ready..."
    for i in $(seq 1 30); do
        if curl -sf "$OLLAMA_ENDPOINT/api/tags" &>/dev/null; then
            echo ">>> Ollama server ready (PID $OLLAMA_PID)."
            break
        fi
        if [ "$i" -eq 30 ]; then
            echo "ERROR: Ollama server failed to start after 30s"
            exit 1
        fi
        sleep 1
    done
else
    echo ">>> Ollama server already running."
fi

# ── 3. Pull the model ──────────────────────────────────────────────────────
echo ">>> Pulling model: $MODEL"
ollama pull "$MODEL"
echo ">>> Model ready."

# ── 4. Verify model responds ───────────────────────────────────────────────
echo ">>> Smoke test: generating a completion..."
RESPONSE=$(curl -sf "$OLLAMA_ENDPOINT/api/generate" \
    -d "{\"model\":\"$MODEL\",\"prompt\":\"Complete this LQL: users |> filter(fn(row) => row.age > 18) |> \",\"stream\":false,\"options\":{\"num_predict\":32}}" \
    2>/dev/null || echo '{"error":"failed"}')

if echo "$RESPONSE" | grep -q '"response"'; then
    PREVIEW=$(echo "$RESPONSE" | python3 -c "import sys,json; print(json.load(sys.stdin).get('response','')[:100])" 2>/dev/null || echo "(parse failed)")
    echo ">>> Model responded: $PREVIEW"
else
    echo "WARNING: Model smoke test failed. Response: $RESPONSE"
    echo ">>> The model may still work — continuing setup."
fi

# ── 5. Build the LSP server ────────────────────────────────────────────────
echo ""
echo ">>> Building LQL LSP server (release)..."
cd "$SCRIPT_DIR"
cargo build --release -p lql-lsp 2>&1
echo ">>> LSP binary: $SCRIPT_DIR/target/release/lql-lsp"

# ── 6. Print config ────────────────────────────────────────────────────────
echo ""
echo "=== Setup Complete ==="
echo ""
echo "VS Code settings (settings.json):"
echo ""
cat <<JSONEOF
{
  "lql.languageServer.enabled": true,
  "lql.languageServer.initializationOptions": {
    "connectionString": "host=localhost dbname=YOUR_DB user=postgres password=YOUR_PASSWORD",
    "aiProvider": {
      "provider": "ollama",
      "endpoint": "$OLLAMA_ENDPOINT/api/generate",
      "model": "$MODEL",
      "timeoutMs": 3000,
      "enabled": true
    }
  }
}
JSONEOF
echo ""
echo "To disable AI completions, set \"enabled\": false"
echo "To switch models: ollama pull MODEL_NAME, then update settings"
