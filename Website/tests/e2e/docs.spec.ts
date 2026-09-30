import { test, expect, type Page } from "@playwright/test";
import * as fs from "node:fs";
import * as path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const FORBIDDEN_STRINGS = [
  "MelbourneDev.",
  "DataProvider.MySql",
  "dotnet add package DataProvider.Sqlite",
  "dotnet add package DataProvider.SqlServer",
  "dotnet add package DataProvider.Postgres",
  "dotnet add package DataProvider\n",
  "dotnet add package Lql.SQLite",
  "dotnet add package Lql.Postgres",
  "dotnet add package Lql.SqlServer",
  "dotnet add package Lql.TypeProvider.FSharp",
  "Healthcare Samples",
  "--version 0.4.0",
  "version\": \"0.4.0",
];

const DOC_PAGES = [
  "/",
  "/about/",
  "/docs/",
  "/docs/installation/",
  "/docs/getting-started/",
  "/docs/quick-start/",
  "/docs/dataprovider/",
  "/docs/lql/",
  "/docs/sync/",
  "/docs/migrations/",
  "/apidocs/",
  "/apidocs/Nimblesite/DataProvider/Core/",
  "/apidocs/Nimblesite/DataProvider/Core/CodeGeneration/",
  "/apidocs/Nimblesite/DataProvider/SQLite/",
  "/apidocs/Nimblesite/DataProvider/Postgres/",
  "/apidocs/Nimblesite/DataProvider/SqlServer/",
  "/apidocs/Nimblesite/DataProvider/Migration/Core/",
  "/apidocs/Nimblesite/DataProvider/Migration/SQLite/",
  "/apidocs/Nimblesite/DataProvider/Migration/Postgres/",
  "/apidocs/DataProviderMigrate/",
  "/apidocs/Nimblesite/Lql/Core/",
  "/apidocs/Nimblesite/Lql/Postgres/",
  "/apidocs/Nimblesite/Lql/SQLite/",
  "/apidocs/Nimblesite/Lql/SqlServer/",
  "/apidocs/Nimblesite/Sync/Core/",
  "/apidocs/Nimblesite/Sync/Http/",
  "/apidocs/Nimblesite/Sync/Postgres/",
  "/apidocs/Nimblesite/Sync/SQLite/",
  "/apidocs/Nimblesite/Reporting/Engine/",
  "/apidocs/Nimblesite/Sql/Model/",
  "/blog/",
  "/blog/getting-started-dataprovider/",
  "/blog/connecting-sql-server/",
  "/blog/lql-simplifies-development/",
];

async function getBodyText(page: Page): Promise<string> {
  return (await page.textContent("body")) ?? "";
}

test.describe("Homepage", () => {
  test("renders hero and links to key docs", async ({ page }) => {
    await page.goto("/");

    await expect(page.locator("h1")).toContainText("Effortless .NET Data Access");

    const referenceLink = page.locator('a[href="https://github.com/Nimblesite/ClinicalCoding"]').first();
    await expect(referenceLink).toBeVisible();

    const body = await getBodyText(page);
    for (const forbidden of FORBIDDEN_STRINGS) {
      expect(body, `forbidden string "${forbidden}" found on homepage`).not.toContain(forbidden);
    }
  });

  test("nav contains Clinical Coding Platform and no stale entries", async ({ page }) => {
    await page.goto("/");
    const nav = (await page.locator("nav, header").first().textContent()) ?? "";
    expect(nav).not.toContain("Healthcare Samples");
    expect(nav).not.toContain("F# Type Provider");
  });
});

test.describe("Installation page", () => {
  test("advertises the three real CLI tools and Nimblesite runtime", async ({ page }) => {
    await page.goto("/docs/installation/");

    const body = await getBodyText(page);

    expect(body).toContain("dotnet tool install DataProvider");
    expect(body).toContain("dotnet tool install DataProviderMigrate");
    expect(body).toContain("dotnet tool install Lql");
    expect(body).toContain("Nimblesite.DataProvider.SQLite");
    expect(body).toContain("0.9.6-beta");
    expect(body).not.toContain("0.4.0");

    for (const forbidden of FORBIDDEN_STRINGS) {
      expect(body, `forbidden string "${forbidden}" on installation page`).not.toContain(forbidden);
    }
  });
});

test.describe("Getting Started page", () => {
  test("walks through all three CLI tools and Result<T,E>", async ({ page }) => {
    await page.goto("/docs/getting-started/");
    const body = await getBodyText(page);

    expect(body).toContain("dotnet tool install DataProvider");
    expect(body).toContain("DataProviderMigrate migrate");
    expect(body).toContain("Lql sqlite");
    expect(body).toContain("DataProvider sqlite");
    expect(body).toContain("Result<");
    expect(body).toContain("SqlError");
    expect(body).toContain("net10.0");
  });
});

test.describe("Quick Start page", () => {
  test("uses the generated API, not Dapper", async ({ page }) => {
    await page.goto("/docs/quick-start/");
    const body = await getBodyText(page);

    expect(body).not.toContain("connection.Query<");
    expect(body).not.toContain("connection.Execute(");
    expect(body).toContain("Result<");
    expect(body).toContain("GetActiveOrdersAsync");
    expect(body).toContain("Nimblesite.Lql.Postgres");
  });
});

test.describe("Clinical Coding external repository", () => {
  const repository = "https://github.com/Nimblesite/ClinicalCoding";

  for (const route of ["/", "/about/", "/docs/getting-started/", "/docs/quick-start/", "/docs/installation/"]) {
    test(`${route} links directly to the separate repository`, async ({ page }) => {
      const response = await page.goto(route);
      expect(response?.status()).toBe(200);
      const links = page.locator(`a[href="${repository}"]`);
      expect(await links.count()).toBeGreaterThan(0);
      await expect(links.first()).toBeVisible();
      expect(await links.first().getAttribute("href")).toBe(repository);
    });
  }

  test("the local sample files are absent", async () => {
    const repoRoot = path.resolve(__dirname, "../../..");
    expect(fs.existsSync(path.join(repoRoot, "Samples"))).toBe(false);
    expect(fs.existsSync(path.join(repoRoot, "Website/src/docs/samples.md"))).toBe(false);
    expect(fs.existsSync(path.join(repoRoot, "Website/src/assets/images/clinical-coding/login.png"))).toBe(false);
    expect(fs.existsSync(path.join(repoRoot, "Website/src/assets/images/cc.mp4"))).toBe(false);
  });
});

test.describe("No ASCII box-drawing diagrams remain", () => {
  const ASCII_DIAGRAM_CHARS = ["┌", "└", "├", "┤", "─", "│", "┼", "┐", "┘"];
  const ASCII_ARROWS = ["+--->", "<----", "+---", "--->"];

  const pagesToScan = [
    "/",
    "/about/",
    "/docs/installation/",
    "/docs/getting-started/",
    "/docs/quick-start/",
    "/docs/dataprovider/",
    "/docs/lql/",
    "/docs/sync/",
    "/docs/migrations/",
  ];

  for (const path of pagesToScan) {
    test(`${path} has no ASCII diagram characters`, async ({ page }) => {
      await page.goto(path);
      const body = await getBodyText(page);

      for (const ch of ASCII_DIAGRAM_CHARS) {
        expect(body, `box-drawing char "${ch}" found on ${path}`).not.toContain(ch);
      }
      for (const arrow of ASCII_ARROWS) {
        expect(body, `ASCII arrow "${arrow}" found on ${path}`).not.toContain(arrow);
      }
    });
  }
});

test.describe("Version is centralised, not hardcoded", () => {
  test("installation page renders the current version from the variable", async ({ page }) => {
    await page.goto("/docs/installation/");
    const body = await getBodyText(page);
    expect(body).toContain("0.9.6-beta");
    expect(body).toContain("dotnet tool install DataProvider --version 0.9.6-beta");
  });

  test("no source file hardcodes 0.9.6-beta (must use a variable/token)", async () => {
    const REPO_ROOT = path.resolve(__dirname, "../../..");
    const SOURCE_PATHS = [
      "Website/src/docs/installation.md",
      "Website/src/docs/getting-started.md",
      "Website/src/docs/quick-start.md",
      "Website/src/index.njk",
      "Website/src/blog/getting-started-dataprovider.md",
      "Website/src/blog/connecting-sql-server.md",
      "Website/src/blog/lql-simplifies-development.md",
      "README.md",
      "DataProvider/README.md",
      "Lql/README.md",
      "Sync/README.md",
      "Migration/README.md",
    ];

    const offenders: string[] = [];
    for (const rel of SOURCE_PATHS) {
      const abs = path.join(REPO_ROOT, rel);
      if (!fs.existsSync(abs)) continue;
      const src = fs.readFileSync(abs, "utf8");
      if (/0\.9\.6-beta/.test(src)) {
        offenders.push(rel);
      }
    }

    expect(
      offenders,
      `These files hardcode "0.9.6-beta" instead of using {{ versions.xxx }} (Eleventy) or \${NIMBLESITE_VERSION}/\${DATAPROVIDER_VERSION}/\${LQL_VERSION}/\${DATAPROVIDERMIGRATE_VERSION} (READMEs): \n  ${offenders.join("\n  ")}`,
    ).toEqual([]);
  });
});

test.describe("DataProvider templating is documented", () => {
  test("/docs/dataprovider/ explains how to customise generated code", async ({ page }) => {
    const response = await page.goto("/docs/dataprovider/");
    expect(response?.status()).toBe(200);

    const body = await getBodyText(page);
    expect(body).toContain("Customising generated code");
    expect(body).toContain("CodeGenerationConfig");
    expect(body).toContain("GenerateDataAccessMethod");
  });
});

test.describe("No Result<T,E> marketing lead-ins", () => {
  // Marketing/intro prose should not LEAD with Result<T,E>; code examples and
  // customisation docs may still reference it as the default shape.
  const FORBIDDEN_LEADS = [
    "Every DataProvider operation returns `Result",
    "DataProvider never throws",
    "DataProvider **never throws**",
  ];

  const pagesToScan = [
    "/",
    "/docs/",
    "/docs/installation/",
    "/docs/getting-started/",
    "/docs/quick-start/",
    "/blog/getting-started-dataprovider/",
    "/blog/connecting-sql-server/",
    "/blog/lql-simplifies-development/",
  ];

  for (const path of pagesToScan) {
    test(`${path} has no Result<T,E> marketing lead`, async ({ page }) => {
      await page.goto(path);
      const body = await getBodyText(page);

      for (const lead of FORBIDDEN_LEADS) {
        expect(body, `marketing lead "${lead}" on ${path}`).not.toContain(lead);
      }
    });
  }
});

test.describe("API reference (DocFX-generated)", () => {
  test("/apidocs/ landing page lists every shipping namespace family", async ({ page }) => {
    const response = await page.goto("/apidocs/");
    expect(response?.status()).toBe(200);

    const body = await getBodyText(page);

    // Group headings — confirm the index is grouped by family
    expect(body).toContain("DataProvider");
    expect(body).toContain("Migrations");
    expect(body).toContain("LQL");
    expect(body).toContain("Sync");
    expect(body).toContain("Reporting");
    expect(body).toContain("SQL Model");

    // Every shipping namespace must be listed
    const expectedNamespaces = [
      "Nimblesite.DataProvider.Core",
      "Nimblesite.DataProvider.Core.CodeGeneration",
      "Nimblesite.DataProvider.SQLite",
      "Nimblesite.DataProvider.Postgres",
      "Nimblesite.DataProvider.SqlServer",
      "Nimblesite.DataProvider.Migration.Core",
      "Nimblesite.DataProvider.Migration.SQLite",
      "Nimblesite.DataProvider.Migration.Postgres",
      "DataProviderMigrate",
      "Nimblesite.Lql.Core",
      "Nimblesite.Lql.Postgres",
      "Nimblesite.Lql.SQLite",
      "Nimblesite.Lql.SqlServer",
      "Nimblesite.Sync.Core",
      "Nimblesite.Sync.Http",
      "Nimblesite.Sync.Postgres",
      "Nimblesite.Sync.SQLite",
      "Nimblesite.Reporting.Engine",
      "Nimblesite.Sql.Model",
    ];
    for (const ns of expectedNamespaces) {
      expect(body, `namespace "${ns}" missing from /apidocs/ index`).toContain(ns);
    }

    // The H5 React project must be explicitly noted as excluded
    expect(body).toContain("Nimblesite.Reporting.React");
    expect(body).toContain("H5");
  });

  test("every namespace landing page returns 200 with content", async ({ page }) => {
    const namespaces = [
      "/apidocs/Nimblesite/DataProvider/Core/",
      "/apidocs/Nimblesite/DataProvider/Core/CodeGeneration/",
      "/apidocs/Nimblesite/DataProvider/SQLite/",
      "/apidocs/Nimblesite/DataProvider/Postgres/",
      "/apidocs/Nimblesite/DataProvider/SqlServer/",
      "/apidocs/Nimblesite/DataProvider/Migration/Core/",
      "/apidocs/Nimblesite/DataProvider/Migration/SQLite/",
      "/apidocs/Nimblesite/DataProvider/Migration/Postgres/",
      "/apidocs/DataProviderMigrate/",
      "/apidocs/Nimblesite/Lql/Core/",
      "/apidocs/Nimblesite/Lql/Postgres/",
      "/apidocs/Nimblesite/Lql/SQLite/",
      "/apidocs/Nimblesite/Lql/SqlServer/",
      "/apidocs/Nimblesite/Sync/Core/",
      "/apidocs/Nimblesite/Sync/Http/",
      "/apidocs/Nimblesite/Sync/Postgres/",
      "/apidocs/Nimblesite/Sync/SQLite/",
      "/apidocs/Nimblesite/Reporting/Engine/",
      "/apidocs/Nimblesite/Sql/Model/",
    ];

    for (const ns of namespaces) {
      const response = await page.goto(ns);
      expect(response?.status(), `${ns} should return 200`).toBe(200);
      const body = await getBodyText(page);
      expect(body.length, `${ns} should have content`).toBeGreaterThan(150);
    }
  });
});

test.describe("Code blocks render without inline-chip leakage", () => {
  test("XML in getting-started has no per-token chip background", async ({ page }) => {
    await page.goto("/docs/getting-started/");

    const xmlBlock = page.locator("pre.language-xml").first();
    await expect(xmlBlock).toBeVisible();

    const tokenStyles = await xmlBlock.locator("span.token").first().evaluate((el) => {
      const style = window.getComputedStyle(el);
      return {
        background: style.backgroundColor,
        border: style.borderTopWidth,
        padding: style.paddingTop,
      };
    });

    expect(tokenStyles.background).toBe("rgba(0, 0, 0, 0)");
    expect(tokenStyles.border).toBe("0px");
    expect(tokenStyles.padding).toBe("0px");
  });
});

test.describe("Migration docs", () => {
  test("/docs/migrations/ renders with DataProviderMigrate content", async ({ page }) => {
    const response = await page.goto("/docs/migrations/");
    expect(response?.status()).toBe(200);

    const body = await getBodyText(page);
    expect(body).toContain("DataProviderMigrate");
    expect(body).toContain("YAML");
    expect(body).toContain("migrate");
    expect(body).toContain("0.9.6-beta");
  });
});

test.describe("Doc pages return 200 and are free of stale strings", () => {
  for (const path of DOC_PAGES) {
    test(`GET ${path}`, async ({ page }) => {
      const response = await page.goto(path);
      expect(response?.status(), `${path} should return 200`).toBe(200);

      const body = await getBodyText(page);
      for (const forbidden of FORBIDDEN_STRINGS) {
        expect(body, `forbidden string "${forbidden}" on ${path}`).not.toContain(forbidden);
      }
    });
  }
});
