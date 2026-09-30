#!/usr/bin/env python3
"""
Generate embeddings for ICD-10-CM codes using MedEmbed model.

This script populates the icd10cm_code_embedding table with vector embeddings
for semantic RAG search. MUST be run after import_icd10cm.py.

Usage:
    python generate_embeddings.py --db-path ../icd10cm.db
    python generate_embeddings.py --db-path ../icd10cm.db --batch-size 500
"""

import json
import sqlite3
import uuid
from datetime import datetime

import click

# Model configuration
EMBEDDING_MODEL = "abhinand/MedEmbed-small-v0.1"
EMBEDDING_DIMENSIONS = 384


def get_codes_without_embeddings(conn: sqlite3.Connection, limit: int = 0) -> list:
    """Get all codes that don't have embeddings yet."""
    cursor = conn.cursor()
    query = """
        SELECT c.Id, c.Code, c.ShortDescription, c.LongDescription,
               c.InclusionTerms, c.ExclusionTerms, c.CodeAlso, c.CodeFirst, c.Synonyms
        FROM icd10cm_code c
        LEFT JOIN icd10cm_code_embedding e ON c.Id = e.CodeId
        WHERE e.Id IS NULL
    """
    if limit > 0:
        query += f" LIMIT {limit}"

    cursor.execute(query)
    return cursor.fetchall()


def create_embedding_text(
    code: str,
    short_desc: str,
    long_desc: str,
    inclusion_terms: str,
    exclusion_terms: str,
    code_also: str,
    code_first: str,
    synonyms: str,
) -> str:
    """Create the text to embed from ALL code fields including synonyms."""
    parts = [f"{code} {short_desc}"]

    if long_desc and long_desc != short_desc:
        parts.append(long_desc)

    if synonyms:
        parts.append(f"Also known as: {synonyms}")

    if inclusion_terms:
        parts.append(f"Includes: {inclusion_terms}")

    if exclusion_terms:
        parts.append(f"Excludes: {exclusion_terms}")

    if code_also:
        parts.append(f"Code also: {code_also}")

    if code_first:
        parts.append(f"Code first: {code_first}")

    return " | ".join(parts)


def insert_embedding(
    conn: sqlite3.Connection,
    code_id: str,
    embedding: list[float],
    model_name: str
) -> None:
    """Insert embedding into database."""
    cursor = conn.cursor()
    embedding_json = json.dumps(embedding)
    embedding_id = str(uuid.uuid4())
    timestamp = datetime.utcnow().isoformat()

    cursor.execute(
        """
        INSERT INTO icd10cm_code_embedding (Id, CodeId, Embedding, EmbeddingModel, LastUpdated)
        VALUES (?, ?, ?, ?, ?)
        """,
        (embedding_id, code_id, embedding_json, model_name, timestamp)
    )


@click.command()
@click.option("--db-path", required=True, help="Path to SQLite database")
@click.option("--batch-size", default=100, help="Batch size for processing")
@click.option("--limit", default=0, help="Limit number of codes to process (0 = all)")
def main(db_path: str, batch_size: int, limit: int):
    """Generate embeddings for ICD-10-CM codes."""

    print(f"Loading MedEmbed model: {EMBEDDING_MODEL}")
    print("This may take a minute on first run (downloads ~100MB model)...")

    from sentence_transformers import SentenceTransformer
    model = SentenceTransformer(EMBEDDING_MODEL)
    print(f"Model loaded! Embedding dimensions: {EMBEDDING_DIMENSIONS}")

    conn = sqlite3.connect(db_path)

    # Get codes needing embeddings
    codes = get_codes_without_embeddings(conn, limit)
    total = len(codes)

    if total == 0:
        print("All codes already have embeddings!")
        return

    print(f"Generating embeddings for {total} codes...")
    print(f"Batch size: {batch_size}")
    print("-" * 60)

    processed = 0
    for i in range(0, total, batch_size):
        batch = codes[i:i + batch_size]

        # Create texts for batch - include ALL fields for better semantic search
        texts = [
            create_embedding_text(code, short_desc, long_desc, incl, excl, code_also, code_first, synonyms)
            for _, code, short_desc, long_desc, incl, excl, code_also, code_first, synonyms in batch
        ]

        # Generate embeddings in batch
        embeddings = model.encode(texts, show_progress_bar=False)

        # Insert into database
        for j, (code_id, code, *_) in enumerate(batch):
            embedding_list = embeddings[j].tolist()
            insert_embedding(conn, code_id, embedding_list, EMBEDDING_MODEL)

        conn.commit()
        processed += len(batch)

        pct = (processed / total) * 100
        print(f"Progress: {processed}/{total} ({pct:.1f}%) - Last code: {batch[-1][1]}")

    print("-" * 60)
    print(f"DONE! Generated {processed} embeddings.")

    # Verify
    cursor = conn.cursor()
    cursor.execute("SELECT COUNT(*) FROM icd10cm_code_embedding")
    total_embeddings = cursor.fetchone()[0]
    print(f"Total embeddings in database: {total_embeddings}")

    conn.close()


if __name__ == "__main__":
    main()
