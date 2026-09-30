using System.Diagnostics;

namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Implements [MIG-AOT-TEST]. Drives the PUBLISHED Native AOT binary as a
/// subprocess — the only thing that proves the native executable actually runs
/// (in-process <c>Program.Main</c> tests exercise the managed assembly, not the
/// AOT image). The binary path comes from the <c>DATAPROVIDERMIGRATE_AOT_BIN</c>
/// environment variable, which the AOT publish + CI set. When it is absent the
/// facts skip, so <c>make test</c> stays fast and self-contained while the CI
/// native job runs the real thing.
/// </summary>
public sealed class NativeAotMigrateSmokeTests
{
    private const string BinEnvVar = "DATAPROVIDERMIGRATE_AOT_BIN";

    private static string? NativeBinaryPath()
    {
        var path = Environment.GetEnvironmentVariable(BinEnvVar);
        return string.IsNullOrWhiteSpace(path) || !File.Exists(path) ? null : path;
    }

    // Uses only SQLite-affinity-stable types (Text). VARCHAR/INT collapse to
    // SQLite's TEXT/INTEGER affinity on inspection, which the integrity verifier
    // would (correctly) flag as drift — that is a SQLite behaviour, not an AOT
    // concern, so the smoke schema avoids it to keep the test focused on the
    // native binary running the full pipeline.
    private const string SmokeSchema = """
        name: aot_smoke
        tables:
          - name: Widget
            columns:
              - name: Id
                type: Text
                isNullable: false
              - name: Label
                type: Text
                isNullable: false
              - name: Quantity
                type: Text
                isNullable: false
            primaryKey:
              columns:
                - Id
            indexes:
              - name: idx_widget_label
                columns:
                  - Label
                isUnique: true
        """;

    [SkippableFact]
    public void NativeBinary_MigratesSqliteSchema_ExitZero()
    {
        var bin = NativeBinaryPath();
        Skip.If(bin is null, $"{BinEnvVar} not set — native AOT binary not published.");

        var (schemaPath, dbPath) = WriteFixtures();
        try
        {
            var result = Run(bin!, schemaPath, dbPath);

            Assert.True(result.ExitCode == 0, userMessage: result.Output);
            Assert.True(File.Exists(dbPath), userMessage: "SQLite database file was not created.");
            Assert.Contains(
                "Schema integrity check passed",
                result.Output,
                StringComparison.Ordinal
            );
        }
        finally
        {
            Cleanup(schemaPath, dbPath);
        }
    }

    [SkippableFact]
    public void NativeBinary_RerunIsIdempotent_NoOperations()
    {
        var bin = NativeBinaryPath();
        Skip.If(bin is null, $"{BinEnvVar} not set — native AOT binary not published.");

        var (schemaPath, dbPath) = WriteFixtures();
        try
        {
            var first = Run(bin!, schemaPath, dbPath);
            Assert.True(first.ExitCode == 0, userMessage: first.Output);

            var second = Run(bin!, schemaPath, dbPath);
            Assert.True(second.ExitCode == 0, userMessage: second.Output);
            Assert.Contains("Schema is up to date", second.Output, StringComparison.Ordinal);
        }
        finally
        {
            Cleanup(schemaPath, dbPath);
        }
    }

    [SkippableFact]
    public void NativeBinary_ExportCommand_ReportsUnsupported()
    {
        var bin = NativeBinaryPath();
        Skip.If(bin is null, $"{BinEnvVar} not set — native AOT binary not published.");

        // Implements [MIG-AOT-EXPORT]: export is compiled out of the native build.
        var result = RunRaw(
            bin!,
            ["export", "--assembly", "x.dll", "--type", "T", "--output", "o.yaml"]
        );

        Assert.True(result.ExitCode != 0, userMessage: result.Output);
        Assert.Contains("not available in the native", result.Output, StringComparison.Ordinal);
    }

    private static (string SchemaPath, string DbPath) WriteFixtures()
    {
        var id = Guid.NewGuid().ToString("N");
        var schemaPath = Path.Combine(Path.GetTempPath(), $"aot-smoke-{id}.yaml");
        var dbPath = Path.Combine(Path.GetTempPath(), $"aot-smoke-{id}.db");
        File.WriteAllText(schemaPath, SmokeSchema);
        return (schemaPath, dbPath);
    }

    private static (int ExitCode, string Output) Run(
        string bin,
        string schemaPath,
        string dbPath
    ) =>
        RunRaw(
            bin,
            ["migrate", "--schema", schemaPath, "--output", dbPath, "--provider", "sqlite"]
        );

    private static (int ExitCode, string Output) RunRaw(string bin, string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = bin,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process =
            Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {bin}");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();

        // 60s ceiling keeps a hung native process from stalling the suite; a
        // timeout is a failure per the project testing rules.
        Assert.True(
            process.WaitForExit(milliseconds: 60_000),
            userMessage: "Native binary did not exit within 60s."
        );

        return (process.ExitCode, string.Concat(stdout, stderr));
    }

    private static void Cleanup(string schemaPath, string dbPath)
    {
        TryDelete(schemaPath);
        TryDelete(dbPath);
    }

    private static void TryDelete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
