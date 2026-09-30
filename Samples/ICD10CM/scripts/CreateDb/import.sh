#!/bin/bash
# Create and populate the ICD-10-CM database
# Usage: ./import.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$(dirname "$SCRIPT_DIR")")"
REPO_ROOT="$(dirname "$(dirname "$PROJECT_DIR")")"
API_DIR="${PROJECT_DIR}/ICD10AM.Api"
DB_PATH="${PROJECT_DIR}/icd10cm.db"
SCHEMA_PATH="${API_DIR}/icd10am-schema.yaml"
MIGRATION_CLI="${REPO_ROOT}/Migration/Migration.Cli"
VENV_DIR="${PROJECT_DIR}/.venv"

echo "=== ICD-10-CM Database Setup ==="
echo "Database: $DB_PATH"
echo ""

# 1. Delete existing database
echo "=== Step 1: Removing existing database ==="
if [ -f "$DB_PATH" ]; then
    rm "$DB_PATH"
    echo "Deleted: $DB_PATH"
else
    echo "No existing database found"
fi

# 2. Migrate database schema
echo "=== Step 2: Migrating database schema ==="
dotnet run --project "$MIGRATION_CLI" -- \
    --schema "$SCHEMA_PATH" \
    --output "$DB_PATH" \
    --provider sqlite

# 3. Set up Python virtual environment and install dependencies
echo ""
echo "=== Step 3: Setting up Python environment ==="
if [ ! -d "$VENV_DIR" ]; then
    echo "Creating virtual environment..."
    python3 -m venv "$VENV_DIR"
fi
source "$VENV_DIR/bin/activate"
pip install -r "$SCRIPT_DIR/requirements.txt"

# 4. Import ICD-10-CM codes
echo ""
echo "=== Step 4: Importing ICD-10-CM codes ==="
python "$SCRIPT_DIR/import_icd10cm.py" --db-path "$DB_PATH"

# 5. Generate embeddings
echo ""
echo "=== Step 5: Generating embeddings ==="
echo "This takes 30-60 minutes for all 74,260 codes"
python "$SCRIPT_DIR/generate_embeddings.py" --db-path "$DB_PATH"

deactivate

echo ""
echo "=== Database setup complete ==="
echo "Database: $DB_PATH"
