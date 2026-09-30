# ICD-10 Microservice Specification

## Overview

The ICD-10 microservice provides clinical coders with RAG (Retrieval-Augmented Generation) search capabilities and standard lookup functionality for ICD-10-CM diagnosis codes.

### What is ICD-10-CM?

**ICD-10-CM** (International Statistical Classification of Diseases and Related Health Problems, Tenth Revision, Clinical Modification) is used to classify diseases, injuries, and related health problems in healthcare settings.

The classification system comprises:
- **ICD-10-CM**: Diagnosis classification (FREE from CMS.gov)
- **ICD-10-PCS**: Procedure Coding System (FREE from CMS.gov)

**Current Edition**: FY 2025 (effective October 1, 2024)

### Data Source - FREE & OPEN SOURCE

**Primary Source**: [CMS.gov ICD-10 Codes](https://www.cms.gov/medicare/coding-billing/icd-10-codes) (US Government - FREE!)

**Alternative Sources**:
- [CDC ICD-10-CM Files](https://www.cdc.gov/nchs/icd/icd-10-cm/files.html)
- [GitHub Mirror (JSON)](https://gist.github.com/cryocaustik/b86de96e66489ada97c25fc25f755de0)

**NO LICENSE REQUIRED** - CMS.gov data is public domain.

To import the full 71,000+ diagnosis codes:
```bash
python scripts/import_icd10cm.py --db-path icd10.db
```

This downloads directly from CMS.gov and creates a complete database.

## Features

### 1. RAG Search (Primary Feature)
Semantic search using medical embeddings to find relevant ICD-10-AM codes from natural language clinical descriptions.

**Use Case**: A clinical coder enters "patient presented with chest pain and shortness of breath" and receives ranked ICD-10-AM code suggestions with confidence scores.

### 2. Standard Lookup
Direct code lookup and hierarchical browsing with both simple JSON and FHIR-compliant response formats.

## Embedding Model Selection

### Recommended: MedEmbed-Large-v1

**Repository**: [abhinand5/MedEmbed](https://github.com/abhinand5/MedEmbed)

**Why MedEmbed?**
- Purpose-built for medical/clinical information retrieval
- Fine-tuned on clinical notes and PubMed Central literature
- Outperforms general-purpose models on medical benchmarks
- Open source with permissive licensing
- Available in multiple sizes (Small/Base/Large)

**Model Variants**:
| Model | Dimensions | Use Case |
|-------|------------|----------|
| MedEmbed-Small-v1 | 384 | Edge devices, resource-constrained |
| MedEmbed-Base-v1 | 768 | Balanced performance |
| MedEmbed-Large-v1 | 1024 | Maximum accuracy (recommended) |

**Alternatives Considered**:
- PubMedBERT Embeddings - Good but less specialized for retrieval
- MedEIR - Newer, supports 8192 tokens but less mature
- BGE/E5 fine-tuned - Requires custom fine-tuning effort

## Architecture

### System Context

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│  Dashboard.Web  │────▶│  ICD10AM.Api     │────▶│  PostgreSQL     │
│  (Clinical UI)  │     │  (This Service)  │     │  (Vector DB)    │
└─────────────────┘     └──────────────────┘     └─────────────────┘
                               │
                               ▼
                        ┌──────────────────┐
                        │  Gatekeeper.Api  │
                        │  (Auth + RLS)    │
                        └──────────────────┘
```

### Request Flow

1. User authenticates via Gatekeeper (WebAuthn/JWT)
2. ICD10AM.Api receives request with JWT token
3. API impersonates user for PostgreSQL connection (RLS enforcement)
4. Query executes with row-level security
5. Results returned (JSON or FHIR format)

## Database Schema

### Entity Relationship Diagram

```mermaid
erDiagram
    icd10am_chapter {
        uuid Id PK
        text ChapterNumber
        text Title
        text CodeRangeStart
        text CodeRangeEnd
        text LastUpdated
        int VersionId
    }

    icd10am_block {
        uuid Id PK
        uuid ChapterId FK
        text BlockCode
        text Title
        text CodeRangeStart
        text CodeRangeEnd
        text LastUpdated
        int VersionId
    }

    icd10am_category {
        uuid Id PK
        uuid BlockId FK
        text CategoryCode
        text Title
        text LastUpdated
        int VersionId
    }

    icd10am_code {
        uuid Id PK
        uuid CategoryId FK
        text Code
        text ShortDescription
        text LongDescription
        text InclusionTerms
        text ExclusionTerms
        text CodeAlso
        text CodeFirst
        boolean Billable
        text EffectiveFrom
        text EffectiveTo
        text LastUpdated
        int VersionId
    }

    icd10am_code_embedding {
        uuid Id PK
        uuid CodeId FK
        vector Embedding
        text EmbeddingModel
        text LastUpdated
    }

    achi_block {
        uuid Id PK
        text BlockNumber
        text Title
        text CodeRangeStart
        text CodeRangeEnd
        text LastUpdated
        int VersionId
    }

    achi_code {
        uuid Id PK
        uuid BlockId FK
        text Code
        text ShortDescription
        text LongDescription
        boolean Billable
        text EffectiveFrom
        text EffectiveTo
        text LastUpdated
        int VersionId
    }

    achi_code_embedding {
        uuid Id PK
        uuid CodeId FK
        vector Embedding
        text EmbeddingModel
        text LastUpdated
    }

    coding_standard {
        uuid Id PK
        text StandardNumber
        text Title
        text Content
        text ApplicableCodes
        text Edition
        text LastUpdated
        int VersionId
    }

    user_search_history {
        uuid Id PK
        uuid UserId FK
        text Query
        text SelectedCode
        text Timestamp
    }

    icd10am_chapter ||--o{ icd10am_block : contains
    icd10am_block ||--o{ icd10am_category : contains
    icd10am_category ||--o{ icd10am_code : contains
    icd10am_code ||--|| icd10am_code_embedding : has
    achi_block ||--o{ achi_code : contains
    achi_code ||--|| achi_code_embedding : has
```

### Row Level Security (RLS)

The API impersonates the authenticated user when connecting to PostgreSQL. RLS policies ensure:

- Users can only access codes relevant to their organization
- Search history is private per user
- Audit trails are maintained

```sql
-- Example RLS policy (conceptual - actual implementation via DataProvider)
-- Users see their own search history only
CREATE POLICY user_search_history_policy ON user_search_history
    USING (UserId = current_setting('app.current_user_id')::uuid);
```

## API Endpoints

### RAG Search

```
POST /api/search
Content-Type: application/json
Authorization: Bearer <jwt>

{
  "query": "chest pain with shortness of breath",
  "limit": 10,
  "includeAchi": true,
  "format": "json" | "fhir"
}
```

**Response (JSON format)**:
```json
{
  "results": [
    {
      "code": "R07.4",
      "description": "Chest pain, unspecified",
      "confidence": 0.92,
      "category": "Symptoms and signs involving the circulatory and respiratory systems",
      "chapter": "XVIII"
    },
    {
      "code": "R06.0",
      "description": "Dyspnoea",
      "confidence": 0.87,
      "category": "Symptoms and signs involving the circulatory and respiratory systems",
      "chapter": "XVIII"
    }
  ],
  "query": "chest pain with shortness of breath",
  "model": "MedEmbed-Large-v1"
}
```

**Response (FHIR format)**:
```json
{
  "resourceType": "Bundle",
  "type": "searchset",
  "total": 2,
  "entry": [
    {
      "resource": {
        "resourceType": "CodeSystem",
        "url": "http://hl7.org/fhir/sid/icd-10-am",
        "concept": {
          "code": "R07.4",
          "display": "Chest pain, unspecified"
        }
      },
      "search": {
        "score": 0.92
      }
    }
  ]
}
```

### Direct Lookup

```
GET /api/codes/{code}
Authorization: Bearer <jwt>
Accept: application/json | application/fhir+json
```

### Hierarchical Browse

```
GET /api/chapters
GET /api/chapters/{chapterId}/blocks
GET /api/blocks/{blockId}/categories
GET /api/categories/{categoryId}/codes
```

### ACHI Procedures

```
GET /api/achi/search?q={query}
GET /api/achi/codes/{code}
```

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `DATABASE_URL` | PostgreSQL connection string | Required |
| `GATEKEEPER_URL` | Gatekeeper API base URL | `http://localhost:5002` |
| `EMBEDDING_MODEL` | MedEmbed model variant | `MedEmbed-Large-v1` |
| `VECTOR_DIMENSIONS` | Embedding dimensions | `1024` |
| `JWT_SIGNING_KEY` | Base64 JWT signing key | Required in production |

### PostgreSQL Extensions

Required extensions:
- `pgvector` - Vector similarity search
- `pg_trgm` - Trigram text search (fallback)

## Import Pipeline

### Step 1: Import ICD-10-CM Codes

Location: `scripts/import_icd10cm.py`

Downloads FREE ICD-10-CM data from CMS.gov and populates the database.

```bash
cd Samples/ICD10CM
pip install click requests
python scripts/import_icd10cm.py --db-path icd10cm.db
```

**Result**: 74,260 ICD-10-CM codes imported into SQLite.

### Step 2: Generate Embeddings (REQUIRED FOR RAG SEARCH)

Location: `scripts/generate_embeddings.py`

**CRITICAL**: RAG semantic search will NOT work until embeddings are generated!

```bash
cd Samples/ICD10CM
pip install sentence-transformers torch click

# Generate embeddings for all codes (takes ~30-60 minutes)
python scripts/generate_embeddings.py --db-path icd10cm.db

# Or generate in batches for large datasets
python scripts/generate_embeddings.py --db-path icd10cm.db --batch-size 500
```

**What it does**:
1. Loads MedEmbed-Small-v0.1 model (384 dimensions)
2. For each ICD-10 code, generates embedding from: `{Code} {ShortDescription} {LongDescription}`
3. Stores embedding as JSON array in `icd10cm_code_embedding` table
4. Links embedding to code via `CodeId` foreign key

**Embedding Text Format**:
```
{Code} | {ShortDescription} | {LongDescription}
```
Example: `R07.4 | Chest pain, unspecified | Pain in chest, unspecified cause`

**Storage Format**: JSON array of 384 floats in `Embedding` column.

### Runtime Architecture

```mermaid
flowchart LR
    Client([User]) --> |POST /api/search| API["ICD10AM.Api<br/>(C# / .NET)"]
    API --> |POST /embed| Container
    subgraph Container["Docker Container"]
        PyAPI["FastAPI<br/>(Python)"]
        PyAPI --> ST["sentence-transformers"]
        ST --> Model["MedEmbed-small"]
    end
    Container --> |vector| API
    API --> |cosine similarity| DB[(PostgreSQL)]
    API --> |ranked results| Client
```

### Setup (One-Time)

**Python scripts for data preparation:**
- `import_icd10cm.py` - Import 74,260 codes from CMS.gov
- `generate_embeddings.py` - Generate embeddings for all codes

### Runtime

**C# API** calls **Docker Embedding Service** for query encoding:
- ICD10AM.Api receives search request
- Calls `POST /embed` on Docker container (port 8000)
- Container runs FastAPI + sentence-transformers + MedEmbed
- Returns embedding vector
- C# computes cosine similarity against stored embeddings
- Returns ranked results

## Testing Strategy

- **Integration tests**: Full API tests against real PostgreSQL with pgvector
- **No mocking**: Real database, real embeddings
- **Coverage target**: 100% coverage, Stryker score 70%+

## Security Considerations

1. **Authentication**: All endpoints require valid JWT from Gatekeeper
2. **Authorization**: RLS policies enforce data access at database level
3. **User Impersonation**: API sets PostgreSQL session variables for RLS
4. **Audit Logging**: All searches logged with user context
5. **No PII in codes**: ICD codes themselves are not patient data

## Future Enhancements

- [ ] Code suggestion based on clinical notes (NER integration)
- [ ] Coding standards (ACS) search and retrieval
- [ ] Multi-edition support (11th, 12th, 13th editions)
- [ ] Offline-first sync for mobile coders
- [ ] Integration with Clinical.Api for condition coding

## References

- [IHACPA ICD-10-AM/ACHI/ACS](https://www.ihacpa.gov.au/health-care/classification/icd-10-amachiacs)
- [ICD-10-AM Thirteenth Edition](https://www.ihacpa.gov.au/resources/icd-10-amachiacs-thirteenth-edition)
- [MedEmbed GitHub](https://github.com/abhinand5/MedEmbed)
- [pgvector Documentation](https://github.com/pgvector/pgvector)
- [FHIR CodeSystem Resource](https://build.fhir.org/codesystem.html)
