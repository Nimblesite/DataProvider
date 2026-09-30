# MedEmbed Embedding Service
# Uses MedEmbed-Small-v0.1 for reasonable container size (~500MB model)
# For production, consider MedEmbed-Large-v0.1 (1024 dims, ~1.3GB)

FROM python:3.11-slim

WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

# Install Python dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Download model at build time for faster startup
RUN python -c "from sentence_transformers import SentenceTransformer; SentenceTransformer('abhinand/MedEmbed-small-v0.1')"

# Copy application code
COPY main.py .

# Expose port
EXPOSE 8000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8000/health || exit 1

# Run the service
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
