using System.CommandLine;
using Nimblesite.Lql.Core;
using Nimblesite.Sql.Model;
using LqlStatementError = Outcome.Result<
    Nimblesite.Lql.Core.LqlStatement,
    Nimblesite.Sql.Model.SqlError
>.Error<Nimblesite.Lql.Core.LqlStatement, Nimblesite.Sql.Model.SqlError>;
using LqlStatementOk = Outcome.Result<
    Nimblesite.Lql.Core.LqlStatement,
    Nimblesite.Sql.Model.SqlError
>.Ok<Nimblesite.Lql.Core.LqlStatement, Nimblesite.Sql.Model.SqlError>;
using StringSqlError = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Error<
    string,
    Nimblesite.Sql.Model.SqlError
>;
using StringSqlOk = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Ok<
    string,
    Nimblesite.Sql.Model.SqlError
>;

namespace Lql;

/// <summary>
/// Shared transpile pipeline used by every platform subcommand. Keeps
/// parse/validate/output logic in one place so each subcommand only
/// needs to supply a platform label and the LQL-to-SQL conversion
/// function.
/// </summary>
internal static class TranspileRunner
{
    // Implements [LQL-CLI-SHARED-COMMAND].
    internal static Command BuildCommand(
        string name,
        string platformLabel,
        Func<LqlStatement, Outcome.Result<string, SqlError>> transpile
    )
    {
        var inputOption = new Option<FileInfo?>(
            name: "--input",
            description: "Input LQL file to transpile"
        )
        {
            IsRequired = true,
        };
        inputOption.AddAlias(alias: "-i");
        var outputOption = new Option<FileInfo?>(
            name: "--output",
            description: $"Output {platformLabel} SQL file (optional - prints to console if not specified)"
        )
        {
            IsRequired = false,
        };
        outputOption.AddAlias(alias: "-o");
        var validateOption = new Option<bool>(
            name: "--validate",
            description: "Validate the LQL syntax without generating output",
            getDefaultValue: () => false
        );
        validateOption.AddAlias(alias: "-v");
        var command = new Command(name: name, description: $"LQL to {platformLabel} SQL transpiler")
        {
            inputOption,
            outputOption,
            validateOption,
        };
        command.SetHandler(
            async (inputFile, outputFile, validate) =>
            {
                Environment.ExitCode = inputFile is null
                    ? 1
                    : await RunAsync(
                            platformLabel: platformLabel,
                            inputFile: inputFile,
                            outputFile: outputFile,
                            validate: validate,
                            transpile: transpile
                        )
                        .ConfigureAwait(false);
            },
            inputOption,
            outputOption,
            validateOption
        );
        return command;
    }

    /// <summary>
    /// Reads the input file, parses the LQL, and either validates it
    /// or runs the supplied <paramref name="transpile"/> function and
    /// writes the resulting SQL.
    /// </summary>
    /// <param name="platformLabel">Display name (e.g. "PostgreSQL").</param>
    /// <param name="inputFile">Input LQL file.</param>
    /// <param name="outputFile">Optional SQL output file.</param>
    /// <param name="validate">If true, only parses and reports.</param>
    /// <param name="transpile">Statement-to-SQL conversion function.</param>
    /// <returns>Exit code (0 = success, 1 = error).</returns>
    public static async Task<int> RunAsync(
        string platformLabel,
        FileInfo inputFile,
        FileInfo? outputFile,
        bool validate,
        Func<LqlStatement, Outcome.Result<string, SqlError>> transpile
    )
    {
        try
        {
            if (!inputFile.Exists)
            {
                Console.WriteLine($"❌ Error: Input file '{inputFile.FullName}' does not exist.");
                return 1;
            }

            var lqlContent = await File.ReadAllTextAsync(inputFile.FullName).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(lqlContent))
            {
                Console.WriteLine($"❌ Error: Input file '{inputFile.FullName}' is empty.");
                return 1;
            }

            Console.WriteLine($"📖 Reading LQL from: {inputFile.FullName}");

            var parseResult = LqlStatementConverter.ToStatement(lqlContent);

            return parseResult switch
            {
                LqlStatementOk success => await ProcessParsedAsync(
                        platformLabel,
                        success.Value,
                        outputFile,
                        validate,
                        inputFile.FullName,
                        transpile
                    )
                    .ConfigureAwait(false),
                LqlStatementError failure => HandleParseError(failure.Value),
            };
        }
#pragma warning disable CA1031 // Top-level CLI error boundary
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Console.WriteLine($"❌ Unexpected error: {ex}");
            return 1;
        }
    }

    private static async Task<int> ProcessParsedAsync(
        string platformLabel,
        LqlStatement statement,
        FileInfo? outputFile,
        bool validate,
        string inputFileName,
        Func<LqlStatement, Outcome.Result<string, SqlError>> transpile
    )
    {
        if (validate)
        {
            Console.WriteLine($"✅ LQL syntax is valid in: {inputFileName}");
            return 0;
        }

        var result = transpile(statement);

        return result switch
        {
            StringSqlOk success => await OutputSqlAsync(platformLabel, success.Value, outputFile)
                .ConfigureAwait(false),
            StringSqlError failure => HandleTranspilationError(platformLabel, failure.Value),
        };
    }

    private static async Task<int> OutputSqlAsync(
        string platformLabel,
        string sql,
        FileInfo? outputFile
    )
    {
        if (outputFile != null)
        {
            var directory = outputFile.Directory;
            if (directory != null && !directory.Exists)
            {
                directory.Create();
            }

            await File.WriteAllTextAsync(outputFile.FullName, sql).ConfigureAwait(false);
            Console.WriteLine($"✅ {platformLabel} SQL written to: {outputFile.FullName}");
        }
        else
        {
            Console.WriteLine($"\n🔄 Generated {platformLabel} SQL:");
            Console.WriteLine("─".PadRight(50, '─'));
            Console.WriteLine(sql);
            Console.WriteLine("─".PadRight(50, '─'));
        }

        return 0;
    }

    private static int HandleParseError(SqlError error)
    {
        Console.WriteLine($"❌ LQL Parse Error: {error.FormattedMessage}");
        if (
            !string.IsNullOrEmpty(error.DetailedMessage)
            && error.DetailedMessage != error.FormattedMessage
        )
        {
            Console.WriteLine($"   Details: {error.DetailedMessage}");
        }
        return 1;
    }

    private static int HandleTranspilationError(string platformLabel, SqlError error)
    {
        Console.WriteLine($"❌ {platformLabel} Transpilation Error: {error.FormattedMessage}");
        if (
            !string.IsNullOrEmpty(error.DetailedMessage)
            && error.DetailedMessage != error.FormattedMessage
        )
        {
            Console.WriteLine($"   Details: {error.DetailedMessage}");
        }
        return 1;
    }
}
