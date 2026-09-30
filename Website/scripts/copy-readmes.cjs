/**
 * Copy README files from source directories to docs with Eleventy frontmatter
 * READMEs are the source of truth - this script copies them to the website
 */

const fs = require('fs');
const path = require('path');

const REPO_ROOT = path.join(__dirname, '../..');
const DOCS_DIR = path.join(__dirname, '../src/docs');
const ASSETS_DIR = path.join(__dirname, '../src/assets/images');

// Single source of truth for published NuGet versions. Override in CI via env vars.
const DEFAULT_VERSION = '0.9.6-beta';
const VERSIONS = {
  DATAPROVIDER_VERSION: process.env.DATAPROVIDER_VERSION || DEFAULT_VERSION,
  DATAPROVIDERMIGRATE_VERSION: process.env.DATAPROVIDERMIGRATE_VERSION || DEFAULT_VERSION,
  LQL_VERSION: process.env.LQL_VERSION || DEFAULT_VERSION,
  NIMBLESITE_VERSION: process.env.NIMBLESITE_VERSION || DEFAULT_VERSION,
};

function injectVersions(content) {
  // Replace ${VAR_NAME} tokens with the resolved version.
  return content.replace(/\$\{(DATAPROVIDER_VERSION|DATAPROVIDERMIGRATE_VERSION|LQL_VERSION|NIMBLESITE_VERSION)\}/g, (_, key) => VERSIONS[key]);
}

// Map of README source paths to docs output and frontmatter
const README_MAPPINGS = [
  {
    source: path.join(REPO_ROOT, 'README.md'),
    output: path.join(DOCS_DIR, 'index.md'),
    frontmatter: {
      layout: 'layouts/docs.njk',
      title: 'Introduction',
      description: 'DataProvider - A comprehensive .NET toolkit for compile-time safe database access.'
    }
  },
  {
    source: path.join(REPO_ROOT, 'DataProvider/README.md'),
    output: path.join(DOCS_DIR, 'dataprovider.md'),
    frontmatter: {
      layout: 'layouts/docs.njk',
      title: 'DataProvider',
      description: 'Source generator that creates compile-time safe extension methods from SQL files.'
    }
  },
  {
    source: path.join(REPO_ROOT, 'Lql/README.md'),
    output: path.join(DOCS_DIR, 'lql.md'),
    frontmatter: {
      layout: 'layouts/docs.njk',
      title: 'Lambda Query Language (LQL)',
      description: 'A functional pipeline-style DSL that transpiles to SQL.'
    }
  },
  {
    source: path.join(REPO_ROOT, 'Sync/README.md'),
    output: path.join(DOCS_DIR, 'sync.md'),
    frontmatter: {
      layout: 'layouts/docs.njk',
      title: 'Sync Framework',
      description: 'Offline-first bidirectional synchronization framework for .NET.'
    }
  },
  {
    source: path.join(REPO_ROOT, 'Migration/README.md'),
    output: path.join(DOCS_DIR, 'migrations.md'),
    frontmatter: {
      layout: 'layouts/docs.njk',
      title: 'Migrations',
      description: 'Database-agnostic YAML schema migrations via the DataProviderMigrate CLI tool.'
    }
  }
];

function generateFrontmatter(fm) {
  let yaml = '---\n';
  for (const [key, value] of Object.entries(fm)) {
    yaml += `${key}: "${value}"\n`;
  }
  yaml += '---\n\n';
  return yaml;
}

function processReadme(mapping) {
  if (!fs.existsSync(mapping.source)) {
    console.error(`ERROR: ${mapping.source} not found — every mapped README must exist`);
    process.exitCode = 1;
    return false;
  }

  let content = fs.readFileSync(mapping.source, 'utf8');

  // Remove any existing frontmatter from README
  content = content.replace(/^---[\s\S]*?---\n*/, '');

  // Remove the first H1 heading (will be rendered from frontmatter title)
  content = content.replace(/^#\s+[^\n]+\n+/, '');

  // Fix relative links to point to correct locations
  // Convert ./Component/README.md links to /docs/component/
  content = content.replace(/\[([^\]]+)\]\(\.\/([^/]+)\/README\.md\)/g, '[$1](/docs/$2/)');
  content = content.replace(/\[([^\]]+)\]\(\.\/([^)]+)\.md\)/g, '[$1](/docs/$2/)');

  // Convert relative image paths
  content = content.replace(/!\[([^\]]*)\]\((?!http)([^)]+)\)/g, '![$1](/assets/images/$2)');

  // Inject versions (${DATAPROVIDER_VERSION} etc.) so READMEs stay version-agnostic.
  content = injectVersions(content);

  const output = generateFrontmatter(mapping.frontmatter) + content;

  fs.writeFileSync(mapping.output, output);
  console.log(`Generated: ${path.relative(DOCS_DIR, mapping.output)}`);
  return true;
}

function copyImages() {
  console.log('Copying images from READMEs...\n');

  // Ensure assets directory exists
  if (!fs.existsSync(ASSETS_DIR)) {
    fs.mkdirSync(ASSETS_DIR, { recursive: true });
  }

  // Copy images referenced in READMEs from repo root
  const repoRootImages = ['lqldbbrowser.png'];
  for (const img of repoRootImages) {
    const src = path.join(REPO_ROOT, img);
    const dest = path.join(ASSETS_DIR, img);
    if (fs.existsSync(src)) {
      fs.copyFileSync(src, dest);
      console.log(`Copied image: ${img}`);
    } else {
      console.log(`SKIP: Image ${img} not found`);
    }
  }

}

function main() {
  console.log('Copying README files to docs...\n');

  // Ensure docs directory exists
  if (!fs.existsSync(DOCS_DIR)) {
    fs.mkdirSync(DOCS_DIR, { recursive: true });
  }

  let count = 0;
  const missing = [];
  for (const mapping of README_MAPPINGS) {
    if (processReadme(mapping)) {
      count++;
    } else {
      missing.push(mapping.source);
    }
  }

  console.log(`\nCopied ${count} README files to docs.`);

  if (missing.length > 0) {
    console.error(`\nFATAL: ${missing.length} README(s) missing — build cannot continue.`);
    process.exit(1);
  }

  // Copy images referenced in READMEs
  copyImages();
}

main();
