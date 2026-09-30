#!/bin/bash
# Stop Docker dependencies
# Usage: ./stop.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$(dirname "$SCRIPT_DIR")")"
EMBEDDING_SERVICE_DIR="${PROJECT_DIR}/embedding-service"

echo "=== Stopping Dependencies ==="

cd "$EMBEDDING_SERVICE_DIR"
docker compose down

echo "=== Dependencies stopped ==="
