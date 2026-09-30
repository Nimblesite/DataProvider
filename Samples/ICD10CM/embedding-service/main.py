"""
MedEmbed Embedding Service

FastAPI service that generates medical embeddings using MedEmbed-Small-v0.1.
Designed for ICD-10-AM RAG search functionality.

Model: abhinand5/MedEmbed-small-v0.1 (384 dimensions)
For higher accuracy, use MedEmbed-large-v0.1 (1024 dimensions)
"""

import logging
from contextlib import asynccontextmanager
from typing import Any

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from sentence_transformers import SentenceTransformer

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

MODEL_NAME = "abhinand/MedEmbed-small-v0.1"
MODEL_DIMENSIONS = 384

model: SentenceTransformer | None = None


class EmbedRequest(BaseModel):
    """Request to generate embedding for text."""

    text: str


class EmbedBatchRequest(BaseModel):
    """Request to generate embeddings for multiple texts."""

    texts: list[str]


class EmbedResponse(BaseModel):
    """Response containing the embedding vector."""

    embedding: list[float]
    model: str
    dimensions: int


class EmbedBatchResponse(BaseModel):
    """Response containing multiple embedding vectors."""

    embeddings: list[list[float]]
    model: str
    dimensions: int
    count: int


class HealthResponse(BaseModel):
    """Health check response."""

    status: str
    model: str
    dimensions: int


@asynccontextmanager
async def lifespan(app: FastAPI) -> Any:
    """Load model on startup."""
    global model
    logger.info(f"Loading model: {MODEL_NAME}")
    model = SentenceTransformer(MODEL_NAME)
    logger.info(f"Model loaded successfully. Dimensions: {MODEL_DIMENSIONS}")
    yield
    logger.info("Shutting down embedding service")


app = FastAPI(
    title="MedEmbed Embedding Service",
    description="Medical embedding service for ICD-10-AM RAG search",
    version="1.0.0",
    lifespan=lifespan,
)


@app.get("/health", response_model=HealthResponse)
async def health_check() -> HealthResponse:
    """Health check endpoint."""
    if model is None:
        raise HTTPException(status_code=503, detail="Model not loaded")
    return HealthResponse(status="healthy", model=MODEL_NAME, dimensions=MODEL_DIMENSIONS)


@app.post("/embed", response_model=EmbedResponse)
async def generate_embedding(request: EmbedRequest) -> EmbedResponse:
    """Generate embedding for a single text."""
    if model is None:
        raise HTTPException(status_code=503, detail="Model not loaded")

    if not request.text.strip():
        raise HTTPException(status_code=400, detail="Text cannot be empty")

    embedding = model.encode(request.text, normalize_embeddings=True)
    return EmbedResponse(
        embedding=embedding.tolist(),
        model=MODEL_NAME,
        dimensions=MODEL_DIMENSIONS,
    )


@app.post("/embed/batch", response_model=EmbedBatchResponse)
async def generate_embeddings_batch(request: EmbedBatchRequest) -> EmbedBatchResponse:
    """Generate embeddings for multiple texts."""
    if model is None:
        raise HTTPException(status_code=503, detail="Model not loaded")

    if not request.texts:
        raise HTTPException(status_code=400, detail="Texts list cannot be empty")

    if len(request.texts) > 100:
        raise HTTPException(status_code=400, detail="Maximum 100 texts per batch")

    embeddings = model.encode(request.texts, normalize_embeddings=True)
    return EmbedBatchResponse(
        embeddings=[e.tolist() for e in embeddings],
        model=MODEL_NAME,
        dimensions=MODEL_DIMENSIONS,
        count=len(request.texts),
    )


@app.get("/")
async def root() -> dict[str, str]:
    """Root endpoint with service info."""
    return {
        "service": "MedEmbed Embedding Service",
        "model": MODEL_NAME,
        "dimensions": str(MODEL_DIMENSIONS),
        "endpoints": "/embed, /embed/batch, /health",
    }
