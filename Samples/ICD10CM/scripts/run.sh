#!/bin/bash
# Run the ICD-10-CM API and dependencies
# Usage: ./run.sh [port]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
API_DIR="${PROJECT_DIR}/ICD10AM.Api"
DB_PATH="${PROJECT_DIR}/icd10cm.db"
PORT="${1:-5558}"

echo "=== Starting ICD-10-CM Service ==="

# Check database exists
if [ ! -f "$DB_PATH" ]; then
    echo "ERROR: Database not found at $DB_PATH"
    echo "Run CreateDb/import.sh first"
    exit 1
fi

# Start embedding service (required for RAG search)
echo "Starting embedding service..."
"$SCRIPT_DIR/Dependencies/start.sh"

echo ""
echo "=== Starting API ==="
echo "URL: http://localhost:$PORT"
echo ""

cd "$API_DIR"
DbPath="$DB_PATH" dotnet run --urls "http://localhost:$PORT"
