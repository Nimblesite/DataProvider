---
layout: layouts/base.njk
title: "About DataProvider - .NET Database Access Toolkit"
description: "DataProvider is an open-source .NET toolkit for type-safe database access with source-generated SQL, LQL query language, offline-first sync, and YAML migrations."
---

<section style="padding: var(--space-12) 0;">
  <div class="container" style="max-width: 800px;">
    <h1>About DataProvider</h1>
    
    <p>DataProvider is a comprehensive .NET toolkit for database connectivity, designed to simplify data access while maintaining type safety and performance.</p>

    <h2>Our Mission</h2>
    <p>We believe database access in .NET should be simple, type-safe, and performant. DataProvider eliminates boilerplate code while giving developers full control over their SQL.</p>

    <h2>Core Principles</h2>
    <ul>
      <li><strong>Type Safety First</strong> - Source-generated code ensures compile-time checking</li>
      <li><strong>No ORM Overhead</strong> - Direct SQL execution without mapping layers</li>
      <li><strong>Result Types</strong> - Error handling without exceptions</li>
      <li><strong>Developer Experience</strong> - IntelliSense, refactoring, and clean APIs</li>
    </ul>

    <h2>The Stack</h2>
    <ul>
      <li><strong><a href="/docs/dataprovider/">DataProvider</a></strong> - CLI source generator: SQL and LQL files become type-safe extension methods</li>
      <li><strong><a href="/docs/lql/">LQL</a></strong> - Lambda Query Language: write once, transpile to any SQL dialect. Ships with a native Rust LSP and a VS Code extension</li>
      <li><strong><a href="/docs/migrations/">Migrations</a></strong> - YAML schemas applied via the <code>DataProviderMigrate</code> CLI tool</li>
      <li><strong><a href="/docs/sync/">Sync</a></strong> - Offline-first bidirectional synchronisation with conflict resolution</li>
      <li><strong><a href="https://github.com/Nimblesite/ClinicalCoding">Clinical Coding Platform</a></strong> - Reference implementation: FHIR R5 services, bidirectional sync, and a React dashboard (<a href="https://github.com/Nimblesite/ClinicalCoding">GitHub</a>)</li>
    </ul>

    <h2>Get Involved</h2>
    <p>DataProvider is open source. Contribute on <a href="https://github.com/Nimblesite/DataProvider">GitHub</a> or check out our <a href="/docs/getting-started/">documentation</a> to get started.</p>
  </div>
</section>
