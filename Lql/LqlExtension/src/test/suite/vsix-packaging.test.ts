import * as assert from "assert";
import * as path from "path";
import * as fs from "fs";
import { execSync } from "child_process";

/**
 * Tests that verify the packaged VSIX contains everything the extension
 * needs to actually run. These exist because .vscodeignore once excluded
 * node_modules/**, which meant vscode-languageclient was missing and
 * the extension silently failed to activate with zero logs.
 */

const EXT_DIR = path.resolve(__dirname, "..", "..", "..");

function findVsix(): string {
  const files = fs
    .readdirSync(EXT_DIR)
    .filter((f) => f.endsWith(".vsix"))
    .map((f) => path.join(EXT_DIR, f));

  if (files.length === 0) {
    throw new Error(
      `No .vsix file found in ${EXT_DIR}. Run 'npx vsce package' first.`,
    );
  }

  // Most recently modified
  files.sort(
    (a, b) => fs.statSync(b).mtimeMs - fs.statSync(a).mtimeMs,
  );
  return files[0];
}

function listVsixContents(vsixPath: string): string[] {
  const output = execSync(`unzip -l "${vsixPath}"`, {
    encoding: "utf-8",
  });
  return output.split("\n").map((line) => line.trim());
}

/** True when any line of the VSIX listing contains the given text. */
function containsLine(contents: readonly string[], needle: string): boolean {
  return contents.some((line) => line.includes(needle));
}

suite("VSIX Packaging", () => {
  let vsixPath: string;
  let contents: string[];

  suiteSetup(() => {
    vsixPath = findVsix();
    contents = listVsixContents(vsixPath);
  });

  test("VSIX contains compiled extension entry point", () => {
    const hasEntryPoint = containsLine(contents, "extension/out/extension.js");
    assert.strictEqual(
      hasEntryPoint,
      true,
      "VSIX must contain out/extension.js",
    );
  });

  test("VSIX contains vscode-languageclient", () => {
    const hasLangClient = containsLine(contents, "node_modules/vscode-languageclient/");
    assert.strictEqual(
      hasLangClient,
      true,
      "VSIX must contain node_modules/vscode-languageclient/ — " +
        "check .vscodeignore is not excluding node_modules/**",
    );
  });

  test("VSIX contains vscode-languageserver-protocol", () => {
    const hasPkg = containsLine(contents, "node_modules/vscode-languageserver-protocol/");
    assert.strictEqual(
      hasPkg,
      true,
      "VSIX must contain vscode-languageserver-protocol (dependency of vscode-languageclient)",
    );
  });

  test("VSIX contains vscode-jsonrpc", () => {
    const hasPkg = containsLine(contents, "node_modules/vscode-jsonrpc/");
    assert.strictEqual(
      hasPkg,
      true,
      "VSIX must contain vscode-jsonrpc (dependency of vscode-languageclient)",
    );
  });

  test("VSIX contains package.json", () => {
    const hasPkgJson = contents.some(
      (line) =>
        line.includes("extension/package.json") &&
        !line.includes("node_modules"),
    );
    assert.strictEqual(
      hasPkgJson,
      true,
      "VSIX must contain package.json at the extension root",
    );
  });

  test("VSIX contains language-configuration.json", () => {
    const hasLangConfig = containsLine(contents, "extension/language-configuration.json");
    assert.strictEqual(
      hasLangConfig,
      true,
      "VSIX must contain language-configuration.json",
    );
  });

  test("VSIX contains TextMate grammar", () => {
    const hasGrammar = containsLine(contents, "extension/syntaxes/lql.tmLanguage.json");
    assert.strictEqual(
      hasGrammar,
      true,
      "VSIX must contain syntaxes/lql.tmLanguage.json",
    );
  });

  test("VSIX does NOT contain TypeScript source files", () => {
    const hasTsSrc = contents.some(
      (line) =>
        line.includes("extension/src/") && line.endsWith(".ts"),
    );
    assert.strictEqual(
      hasTsSrc,
      false,
      "VSIX should not contain .ts source files (only compiled .js)",
    );
  });

  test("VSIX does NOT contain LSP binary in bin/", () => {
    const hasBin = containsLine(contents, "extension/bin/lql-lsp");
    assert.strictEqual(
      hasBin,
      false,
      "VSIX should not bundle the LSP binary — it is downloaded at runtime",
    );
  });
});
