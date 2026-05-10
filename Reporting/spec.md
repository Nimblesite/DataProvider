# Reporting Platform Spec

## Overview

Embeddable reporting platform. Define data sources (SQL or LQL) with parameters, serve data through format adapters (JSON default, protobuf etc.), render reports via a JSON-driven UI config that any toolkit can consume (React first, Flutter/AvaloniaUI later).

## Architecture

```
Reporting/
├── Reporting.Engine/           # Core: config parsing, data source execution, format adapters
├── Reporting.Api/              # ASP.NET Core API serving report data + report configs
├── Reporting.React/            # React renderer (H5 transpiled C# -> JS, like Dashboard.Web)
├── Reporting.Tests/            # Integration tests
└── spec.md                     # This file
```

## Report Definition (JSON Config)

A report is a JSON document that describes data sources and visual layout. Any renderer (React, Flutter, Avalonia) reads this config and renders appropriately.

```json
{
  "id": "monthly-patient-summary",
  "title": "Monthly Patient Summary",
  "parameters": [
    { "name": "startDate", "type": "date", "label": "Start Date", "required": true },
    { "name": "endDate", "type": "date", "label": "End Date", "required": true },
    { "name": "department", "type": "string", "label": "Department", "default": "all" }
  ],
  "dataSources": [
    {
      "id": "patientCounts",
      "type": "sql",
      "connectionRef": "clinical-db",
      "query": "SELECT date_trunc('month', created_at) as month, COUNT(*) as count FROM fhir_patient WHERE created_at BETWEEN @startDate AND @endDate GROUP BY month ORDER BY month",
      "parameters": ["startDate", "endDate"]
    },
    {
      "id": "departmentBreakdown",
      "type": "lql",
      "connectionRef": "clinical-db",
      "query": "fhir_encounter |> filter(fn(row) => row.period_start >= @startDate AND row.period_start <= @endDate) |> group_by(service_type) |> select(service_type, count() as total) |> order_by(total desc)",
      "parameters": ["startDate", "endDate"]
    },
    {
      "id": "topConditions",
      "type": "api",
      "url": "{clinicalApiUrl}/fhir/Condition",
      "method": "GET",
      "headers": { "Authorization": "Bearer {authToken}" }
    }
  ],
  "layout": {
    "type": "grid",
    "columns": 12,
    "rows": [
      {
        "cells": [
          {
            "colSpan": 4,
            "component": {
              "type": "metric",
              "dataSource": "patientCounts",
              "title": "Total Patients",
              "value": "sum(count)",
              "format": "number"
            }
          },
          {
            "colSpan": 8,
            "component": {
              "type": "chart",
              "chartType": "line",
              "dataSource": "patientCounts",
              "title": "Patient Admissions Over Time",
              "xAxis": { "field": "month", "format": "MMM yyyy" },
              "yAxis": { "field": "count", "label": "Patients" }
            }
          }
        ]
      },
      {
        "cells": [
          {
            "colSpan": 6,
            "component": {
              "type": "chart",
              "chartType": "bar",
              "dataSource": "departmentBreakdown",
              "title": "Encounters by Department",
              "xAxis": { "field": "service_type" },
              "yAxis": { "field": "total" }
            }
          },
          {
            "colSpan": 6,
            "component": {
              "type": "table",
              "dataSource": "topConditions",
              "title": "Conditions",
              "columns": [
                { "field": "code", "header": "Code" },
                { "field": "display", "header": "Condition" },
                { "field": "status", "header": "Status" }
              ],
              "pageSize": 10
            }
          }
        ]
      },
      {
        "cells": [
          {
            "colSpan": 12,
            "component": {
              "type": "text",
              "content": "Report generated for period {startDate} to {endDate}. Department filter: {department}.",
              "style": "caption"
            }
          }
        ]
      }
    ]
  }
}
```

## Component Types

| Type | Description | Props |
|------|-------------|-------|
| `metric` | Single KPI value with optional trend | `dataSource`, `title`, `value` (expression), `format`, `trend` |
| `chart` | Visualization (line, bar, pie, area, donut) | `dataSource`, `chartType`, `title`, `xAxis`, `yAxis`, `series` |
| `table` | Paginated data table with sort/filter | `dataSource`, `title`, `columns`, `pageSize`, `sortable` |
| `text` | Static or templated text block | `content`, `style` (heading/body/caption) |

## Data Source Types

| Type | Description | Execution |
|------|-------------|-----------|
| `sql` | Raw SQL query | Executed via DataProvider against configured connection |
| `lql` | LQL expression | Transpiled to platform SQL via Lql engine, then executed |
| `api` | REST API call | HTTP request to external endpoint, returns JSON |

## Connection Registry (Server-Side Config)

Connections are defined server-side only (never sent to client). Referenced by `connectionRef` in report definitions.

```json
{
  "connections": {
    "clinical-db": {
      "provider": "postgres",
      "connectionString": "Host=localhost;Database=clinical;..."
    },
    "scheduling-db": {
      "provider": "postgres",
      "connectionString": "Host=localhost;Database=scheduling;..."
    }
  },
  "apiEndpoints": {
    "clinicalApiUrl": "http://localhost:5080",
    "authToken": "..."
  }
}
```

## Format Adapters

The engine returns data in pluggable formats:

| Format | Content-Type | Use Case |
|--------|-------------|----------|
| `json` (default) | `application/json` | Web renderers, REST clients |
| `protobuf` | `application/protobuf` | High-performance mobile/embedded |
| `csv` | `text/csv` | Export/download |

## API Endpoints

```
GET  /api/reports                          # List available reports
GET  /api/reports/{id}                     # Get report definition (layout + parameter metadata, no connection strings)
POST /api/reports/{id}/execute             # Execute all data sources, return data
POST /api/reports/{id}/datasources/{dsId}  # Execute single data source
GET  /api/reports/{id}/export?format=csv   # Export report data
```

### Execute Request

```json
{
  "parameters": {
    "startDate": "2025-01-01",
    "endDate": "2025-12-31",
    "department": "cardiology"
  },
  "format": "json"
}
```

### Execute Response

```json
{
  "reportId": "monthly-patient-summary",
  "executedAt": "2025-03-03T10:00:00Z",
  "dataSources": {
    "patientCounts": {
      "columns": ["month", "count"],
      "rows": [
        ["2025-01-01T00:00:00Z", 42],
        ["2025-02-01T00:00:00Z", 38]
      ],
      "totalRows": 12
    },
    "departmentBreakdown": {
      "columns": ["service_type", "total"],
      "rows": [
        ["Cardiology", 156],
        ["Neurology", 89]
      ],
      "totalRows": 8
    }
  }
}
```

## React Renderer

Built with H5 transpiler (C# -> JavaScript), same as Dashboard.Web. Renders report JSON config into interactive React components.

### Rendering Pipeline

1. Fetch report definition from API (`GET /api/reports/{id}`)
2. Show parameter form (auto-generated from parameter metadata)
3. User fills parameters, clicks "Run"
4. `POST /api/reports/{id}/execute` with parameters
5. Renderer walks `layout.rows[].cells[].component` tree
6. Each component type maps to a React component that receives its data source result
7. Charts rendered with Canvas 2D (no external chart library - simple SVG/Canvas)

### Embeddability

The renderer is a standalone JS bundle. Embed in any page:

```html
<div id="report-container"></div>
<script src="reporting-renderer.js"></script>
<script>
  ReportRenderer.render({
    container: 'report-container',
    apiBaseUrl: 'http://localhost:5100',
    reportId: 'monthly-patient-summary',
    parameters: { startDate: '2025-01-01', endDate: '2025-12-31' }
  });
</script>
```

## Database Schema

**All database schemas MUST be created using the Migration library with YAML definitions.** Raw SQL for creating database schemas is strictly prohibited. Use `DataProviderMigrate` with YAML schema files as the single source of truth.

If the reporting platform requires its own persistence (e.g., for saved reports, scheduled executions), the schema MUST be defined in a `reporting-schema.yaml` file and created via:

```bash
dotnet run --project Migration/DataProviderMigrate/DataProviderMigrate.csproj -- migrate --schema reporting-schema.yaml --output reporting.db --provider sqlite
```

For the MVP, report definitions are loaded from JSON files on disk (no database persistence needed). Future phases will add YAML-migrated schema for saved reports.

## Security

- Connection strings / secrets are NEVER exposed to the client
- Report definitions sent to client contain layout + parameter metadata only
- API endpoints require authentication (Bearer token via Gatekeeper)
- SQL parameters are always parameterized (no string concatenation)
- LQL is transpiled server-side, client never sees raw SQL

## MVP Scope

### Phase 1 (This PR)
- [x] Spec
- [ ] Reporting.Engine: Report config model, data source execution (SQL + LQL), JSON format adapter
- [ ] Reporting.Api: Serve report definitions, execute reports, return data
- [ ] Reporting.React: Render metric, table, text, bar chart components from JSON config
- [ ] Reporting.Tests: Integration tests with real SQLite database
- [ ] Sample report definition for healthcare data

### Phase 2 (Future)
- Line/pie/area/donut charts
- Protobuf + CSV format adapters
- Report parameter validation
- Caching layer
- Report scheduling (cron-based execution)
- Dashboard composer (drag-and-drop report builder)
- Flutter and AvaloniaUI renderers
