using System.Globalization;

namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-CLI-COMMANDS]: every in-process CLI invocation shares the
// process-wide Console writers, including invocations from different collections.
internal static class MigrationCliConsole
{
    private static readonly object Gate = new();

    internal static (int ExitCode, string Output) Migrate(
        string schemaPath,
        string provider,
        string output,
        bool allowDestructive = false,
        string? phase = null
    ) =>
        Invoke(run: () =>
            DataProviderMigrate.Program.Main(
                args: Arguments(
                    schemaPath: schemaPath,
                    provider: provider,
                    output: output,
                    allowDestructive: allowDestructive,
                    phase: phase
                )
            )
        );

    private static string[] Arguments(
        string schemaPath,
        string provider,
        string output,
        bool allowDestructive,
        string? phase
    ) =>
        [
            "migrate",
            "--schema",
            schemaPath,
            "--provider",
            provider,
            "--output",
            output,
            .. (allowDestructive ? ["--allow-destructive"] : Array.Empty<string>()),
            .. (phase is null ? Array.Empty<string>() : ["--phase", phase]),
        ];

    private static (int ExitCode, string Output) Invoke(Func<int> run)
    {
        lock (Gate)
        {
            return Capture(run: run);
        }
    }

    private static (int ExitCode, string Output) Capture(Func<int> run)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter(formatProvider: CultureInfo.InvariantCulture);
        Console.SetOut(newOut: output);
        Console.SetError(newError: output);
        try
        {
            return (run(), output.ToString());
        }
        finally
        {
            Console.SetOut(newOut: originalOut);
            Console.SetError(newError: originalError);
        }
    }
}
