using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nimblesite.DataProvider.Core;
using Nimblesite.DataProvider.SqlServer;
using CodeOk = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Ok<
    string,
    Nimblesite.Sql.Model.SqlError
>;
using TableOk = Outcome.Result<
    Nimblesite.DataProvider.Core.DatabaseTable,
    Nimblesite.Sql.Model.SqlError
>.Ok<Nimblesite.DataProvider.Core.DatabaseTable, Nimblesite.Sql.Model.SqlError>;

namespace DataProvider;

// Implements [CON-SHARED-CORE] and [CON-PARSER-ONLY].
internal static class SqlServerCli
{
    internal static Command BuildCommand()
    {
        var project = new Option<DirectoryInfo>("--project-dir") { IsRequired = true };
        var config = new Option<FileInfo>("--config") { IsRequired = true };
        var output = new Option<DirectoryInfo>("--out") { IsRequired = true };
        var command = new Command("sqlserver", "Generate type-safe SQL Server data access code")
        {
            project,
            config,
            output,
        };
        command.AddAlias(alias: "mssql");
        command.SetHandler(
            async (context) =>
                context.ExitCode = await RunAsync(
                        projectDirectory: context.ParseResult.GetValueForOption(project),
                        configFile: context.ParseResult.GetValueForOption(config),
                        outputDirectory: context.ParseResult.GetValueForOption(output)
                    )
                    .ConfigureAwait(false)
        );
        return command;
    }

    internal static async Task<int> RunAsync(
        DirectoryInfo? projectDirectory,
        FileInfo? configFile,
        DirectoryInfo? outputDirectory
    )
    {
        using var logging = LoggerFactory.Create(configure: builder => builder.AddConsole());
        var logger = logging.CreateLogger("DataProvider.SqlServer");
        try
        {
            if (projectDirectory is null || configFile is null || outputDirectory is null)
                return 1;
            var config = JsonSerializer.Deserialize<DataProviderConfig>(
                await File.ReadAllTextAsync(configFile.FullName).ConfigureAwait(false),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
            if (config?.ConnectionString is not { Length: > 0 } connectionString)
                return 1;
            outputDirectory.Create();
            return await GenerateAsync(
                    project: projectDirectory,
                    output: outputDirectory,
                    config: config,
                    connectionString: connectionString,
                    logger: logger
                )
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "SQL Server code generation failed");
            return 1;
        }
    }

    private static async Task<int> GenerateAsync(
        DirectoryInfo project,
        DirectoryInfo output,
        DataProviderConfig config,
        string connectionString,
        ILogger logger
    )
    {
        var errors = 0;
        foreach (
            var path in Directory.EnumerateFiles(
                project.FullName,
                "*.sql",
                SearchOption.AllDirectories
            )
        )
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var sql = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(sql))
                continue;
            var result = await SqlServerCodeGenerator
                .GenerateCodeAsync(fileName: name, sql: sql, connectionString: connectionString)
                .ConfigureAwait(false);
            errors += await WriteAsync(output: output, name: name, result: result, logger: logger)
                .ConfigureAwait(false);
        }
        foreach (var table in config.Tables)
            errors += await GenerateTableAsync(
                    table: table,
                    connectionString: connectionString,
                    output: output,
                    logger: logger
                )
                .ConfigureAwait(false);
        return errors == 0 ? 0 : 1;
    }

    private static async Task<int> GenerateTableAsync(
        TableConfig table,
        string connectionString,
        DirectoryInfo output,
        ILogger logger
    )
    {
        var metadata = await SqlServerTableMetadata
            .ReadAsync(connectionString: connectionString, table: table)
            .ConfigureAwait(false);
        var result = metadata is TableOk success
            ? SqlServerCodeGenerator
                .CreateConfig()
                .TableOperationGenerator.GenerateTableOperations(
                    table: success.Value,
                    config: table
                )
            : metadata.Match<Outcome.Result<string, Nimblesite.Sql.Model.SqlError>>(
                _ => new CodeOk(string.Empty),
                error => new Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Error<
                    string,
                    Nimblesite.Sql.Model.SqlError
                >(error)
            );
        return await WriteAsync(
                output: output,
                name: $"{table.Name}Operations",
                result: result,
                logger: logger
            )
            .ConfigureAwait(false);
    }

    private static async Task<int> WriteAsync(
        DirectoryInfo output,
        string name,
        Outcome.Result<string, Nimblesite.Sql.Model.SqlError> result,
        ILogger logger
    )
    {
        var path = Path.Combine(output.FullName, $"{name}.g.cs");
        if (result is CodeOk success)
        {
            await File.WriteAllTextAsync(path, success.Value).ConfigureAwait(false);
            logger.LogInformation("Generated {File}", Path.GetFileName(path));
            return 0;
        }
        logger.LogError("Failed to generate {Query}", name);
        await File.WriteAllTextAsync(path, "#error SQL Server code generation failed\n")
            .ConfigureAwait(false);
        return 1;
    }
}
