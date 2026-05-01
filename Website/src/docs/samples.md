---
layout: layouts/docs.njk
title: Nimblesite Clinical Coding Platform — Reference Implementation
description: A full-stack healthcare reference implementation for the DataProvider toolkit. Four .NET 10 microservices, FHIR R5, ICD-10 semantic search with pgvector, bidirectional sync, and WebAuthn auth.
---

The **Nimblesite Clinical Coding Platform** is the official reference implementation for the DataProvider toolkit — a realistic multi-service healthcare application that exercises every package and CLI tool working together.

> **Technology demonstration only — not for production use.** No warranty, no clinical certification.

<video class="demo-video" controls preload="metadata" poster="/assets/images/clinical-coding/login.png">
  <source src="/assets/images/cc.mp4" type="video/mp4">
</video>

- **Repository**: [github.com/Nimblesite/ClinicalCoding](https://github.com/Nimblesite/ClinicalCoding)
- **License**: MIT © 2026 Nimblesite Pty Ltd
- **Stack**: .NET 10, PostgreSQL + pgvector, TypeScript + React, Docker Compose

## What it demonstrates

- FHIR R5 Patient, Encounter, Condition, MedicationRequest, Practitioner, Appointment
- Agentic ICD-10 coding via pgvector semantic search over 16,000+ codes
- Bidirectional sync between Clinical and Scheduling domains
- WebAuthn passkeys + record-level RBAC via Gatekeeper
- Build-time code generation from all three CLI tools (`DataProviderMigrate`, `Lql`, `DataProvider`)
- 100% `Result<T, SqlError>` — no thrown exceptions on the query path

## Get started

Full setup instructions, architecture diagrams, API reference, and contribution guidelines are in the repository.

```bash
git clone https://github.com/Nimblesite/ClinicalCoding.git
cd ClinicalCoding
make start-docker
```

Open **http://localhost:5173** — prerequisites: Docker, .NET 10 SDK, GNU Make.

## Next Steps

- [Installation](/docs/installation/) — install the CLI tools the platform uses
- [Getting Started](/docs/getting-started/) — build a smaller version of this stack
- [DataProvider](/docs/dataprovider/) — the source generator in detail
- [LQL](/docs/lql/) — the query language
- [Sync](/docs/sync/) — the bidirectional sync framework
- [Gatekeeper](/docs/gatekeeper/) — WebAuthn + RBAC
