#!/bin/bash
# Start Docker dependencies (embedding service)
# Usage: ./start.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$(dirname "$SCRIPT_DIR")")"
EMBEDDING_SERVICE_DIR="${PROJECT_DIR}/embedding-service"

echo "=== Starting Dependencies ==="

# Start embedding service (used for generating embeddings)
echo "Starting embedding service..."
cd "$EMBEDDING_SERVICE_DIR"
docker compose up -d

echo ""
echo "Waiting for embedding service to be healthy..."
sleep 5

# Check health
if curl -s http://localhost:8000/health > /dev/null 2>&1; then
    echo "Embedding service is running at http://localhost:8000"
else
    echo "Embedding service starting... (may take a minute to load model)"
    echo "Check status with: docker compose -f $EMBEDDING_SERVICE_DIR/docker-compose.yml logs -f"
fi

echo ""
echo "=== Dependencies started ==="
