import * as fs from "fs";
import * as os from "os";
import * as path from "path";
import { runTests } from "@vscode/test-electron";

async function main() {
  try {
    const extensionDevelopmentPath = path.resolve(__dirname, "../../");
    const extensionTestsPath = path.resolve(__dirname, "./suite/index");
    // The default user-data-dir lives under the repo; on deep checkouts its
    // IPC socket path exceeds the macOS 103-char AF_UNIX limit (EINVAL).
    const userDataDir = path.join(os.tmpdir(), "lql-ext-test");
    fs.mkdirSync(userDataDir, { recursive: true });

    await runTests({
      extensionDevelopmentPath,
      extensionTestsPath,
      launchArgs: ["--disable-extensions", `--user-data-dir=${userDataDir}`],
    });
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    process.stderr.write(`Failed to run tests: ${message}\n`);
    process.exit(1);
  }
}

void main();
