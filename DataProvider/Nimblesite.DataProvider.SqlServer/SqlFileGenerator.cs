using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Nimblesite.DataProvider.Core;
using Nimblesite.DataProvider.Core.CodeGeneration;
using Nimblesite.DataProvider.SqlServer.SqlParsing;
// IsNonQuery lives on SqlParserCsImplementation (parent-owned parser).
using Nimblesite.Sql.Model;
using Outcome;
using SqlError = Nimblesite.Sql.Model.SqlError;

namespace Nimblesite.DataProvider.SqlServer;

// Implements [CON-SHARED-CORE]. SQL Server incremental generation entry point.
// Scans AdditionalFiles for .sql files plus DataProvider.json /
// Nimblesite.DataProvider.Core.json / *.grouping.json, parses with the official
// ScriptDom parser, resolves metadata through SqlServerCodeGenerator, and emits
// through the shared Core generators.
/// <summary>
/// SQL Server incremental generator core: scans AdditionalFiles for SQL and
/// configuration and produces strongly-typed data access extension methods.
/// Registered for execution by <see cref="DataProviderIncrementalSourceGenerator"/>.
/// </summary>
public sealed partial class SqlFileGenerator : IIncrementalGenerator
{
    private const string NullableConfigName = "DataProvider.json";
    private const string CoreConfigName = "Nimblesite.DataProvider.Core.json";

    private const string ConfigMissingMessage =
        "DataProvider.json (or Nimblesite.DataProvider.Core.json) with ConnectionString is required for code generation";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ISqlParser _parser;

    /// <summary>
    /// Creates an error diagnostic descriptor inline (avoids release-tracking requirements).
    /// </summary>
    private static Diagnostic ErrorDiag(string message) =>
        Diagnostic.Create(
            new DiagnosticDescriptor(
                "DataProvider009",
                "DataProvider SQL Server generation error",
                message,
                "DataProvider",
                DiagnosticSeverity.Error,
                true
            ),
            Location.None
        );

    /// <summary>
    /// Initializes a new instance using the official SQL Server ScriptDom parser.
    /// </summary>
    public SqlFileGenerator()
        : this(new SqlServerParser()) { }

    /// <summary>
    /// Initializes a new instance with a specific SQL parser.
    /// </summary>
    /// <param name="sqlParser">The SQL parser to use.</param>
    public SqlFileGenerator(ISqlParser sqlParser) => _parser = sqlParser;

    /// <summary>
    /// Initializes the incremental pipeline: collects SQL, config, and grouping
    /// AdditionalFiles and registers the single output step.
    /// </summary>
    /// <param name="context">The initialization context provided by Roslyn.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var additional = context.AdditionalTextsProvider;
        var sqlFiles = additional
            .Where(at => at.Path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .Collect();
        var configFiles = additional.Where(at => IsConfigFileName(at.Path)).Collect();
        var groupingFiles = additional
            .Where(at => at.Path.EndsWith(".grouping.json", StringComparison.OrdinalIgnoreCase))
            .Collect();

        var combined = sqlFiles.Combine(configFiles).Combine(groupingFiles);
        context.RegisterSourceOutput(combined, GenerateCodeForAllFiles);
    }

    /// <summary>
    /// Determines whether an AdditionalFile path names a supported generator config file.
    /// </summary>
    private static bool IsConfigFileName(string path) =>
        path.EndsWith(NullableConfigName, StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(CoreConfigName, StringComparison.OrdinalIgnoreCase);

    private void GenerateCodeForAllFiles(
        SourceProductionContext context,
        (
            (
                ImmutableArray<AdditionalText> SqlFiles,
                ImmutableArray<AdditionalText> ConfigFiles
            ) Left,
            ImmutableArray<AdditionalText> GroupingFiles
        ) data
    )
    {
        var (left, groupingFiles) = data;
        var (sqlFiles, configFiles) = left;

        var config = LoadConfig(context, configFiles);
        if (config is null)
        {
            return;
        }

        var groupingByBase = BuildGroupingLookup(groupingFiles);
        var codeGenConfig = SqlServerCodeGenerator.CreateSqlServerConfig();

        // [GEN-QUERIES-CONFIG] When config.Queries is populated, generate exactly
        // those entries (Name / GroupingFile overrides honored); otherwise scan all .sql files.
        if (config.Queries.Count > 0)
        {
            foreach (var query in config.Queries)
            {
                GenerateForQueryItem(
                    context,
                    query,
                    sqlFiles,
                    config,
                    groupingByBase,
                    codeGenConfig
                );
            }
        }
        else
        {
            foreach (var sqlFile in sqlFiles)
            {
                GenerateForSqlFile(
                    context,
                    sqlFile,
                    name: null,
                    groupingOverride: null,
                    config,
                    groupingByBase,
                    codeGenConfig
                );
            }
        }

        GenerateForTables(context, config, codeGenConfig);
    }

    /// <summary>
    /// Resolves a config Queries entry to its SQL AdditionalFile and generates
    /// code using the entry's Name (type/method name) and GroupingFile overrides.
    /// Reports an error when the referenced SQL file cannot be found.
    /// </summary>
    private void GenerateForQueryItem(
        SourceProductionContext context,
        QueryConfigItem query,
        ImmutableArray<AdditionalText> sqlFiles,
        SourceGeneratorDataProviderConfiguration config,
        ILookup<string, AdditionalText> groupingByBase,
        CodeGenerationConfig codeGenConfig
    )
    {
        var fileName = Path.GetFileName(query.SqlFile);
        var sqlFile = sqlFiles.FirstOrDefault(f =>
            string.Equals(Path.GetFileName(f.Path), fileName, StringComparison.OrdinalIgnoreCase)
        );
        if (sqlFile is null)
        {
            context.ReportDiagnostic(
                ErrorDiag($"Query '{query.Name}' references missing SQL file '{query.SqlFile}'")
            );
            return;
        }

        AdditionalText? groupingFile = null;
        if (!string.IsNullOrWhiteSpace(query.GroupingFile))
        {
            var groupingName = Path.GetFileName(query.GroupingFile);
            groupingFile = groupingByBase
                .SelectMany(x => x)
                .FirstOrDefault(g =>
                    string.Equals(
                        Path.GetFileName(g.Path),
                        groupingName,
                        StringComparison.OrdinalIgnoreCase
                    )
                );
            if (groupingFile is null)
            {
                context.ReportDiagnostic(
                    ErrorDiag(
                        $"Query '{query.Name}' references missing grouping file '{query.GroupingFile}'"
                    )
                );
                return;
            }
        }

        GenerateForSqlFile(
            context,
            sqlFile,
            string.IsNullOrWhiteSpace(query.Name) ? null : query.Name,
            groupingFile,
            config,
            groupingByBase,
            codeGenConfig
        );
    }

    /// <summary>
    /// Deserializes the generator configuration from the first config AdditionalFile.
    /// Reports an error diagnostic and returns null on failure.
    /// </summary>
    private static SourceGeneratorDataProviderConfiguration? LoadConfig(
        SourceProductionContext context,
        ImmutableArray<AdditionalText> configFiles
    )
    {
        if (configFiles.Length == 0)
        {
            context.ReportDiagnostic(ErrorDiag(ConfigMissingMessage));
            return null;
        }

        var content = configFiles[0].GetText()?.ToString();
        if (string.IsNullOrWhiteSpace(content))
        {
            context.ReportDiagnostic(ErrorDiag(ConfigMissingMessage));
            return null;
        }

        try
        {
            var config = JsonSerializer.Deserialize<SourceGeneratorDataProviderConfiguration>(
                content,
                JsonOptions
            );
            if (config is null || string.IsNullOrWhiteSpace(config.ConnectionString))
            {
                context.ReportDiagnostic(ErrorDiag(ConfigMissingMessage));
                return null;
            }
            return config;
        }
        catch (JsonException ex)
        {
            context.ReportDiagnostic(
                ErrorDiag($"Failed to parse {configFiles[0].Path}: {ex.Message}")
            );
            return null;
        }
    }

    /// <summary>
    /// Builds a lookup from base file name (stripped of ".grouping.json") to grouping AdditionalFiles.
    /// </summary>
    private static ILookup<string, AdditionalText> BuildGroupingLookup(
        ImmutableArray<AdditionalText> groupingFiles
    ) =>
        groupingFiles
            .Select(g =>
            {
                var name = Path.GetFileName(g.Path);
                var baseName = name.EndsWith(".grouping.json", StringComparison.OrdinalIgnoreCase)
                    ? name[..^".grouping.json".Length]
                    : Path.GetFileNameWithoutExtension(name);
                return (Text: g, Base: baseName);
            })
            .ToLookup(x => x.Base, x => x.Text);

    /// <summary>
    /// Generates data access extensions for a single SQL AdditionalFile: parses
    /// with ScriptDom, resolves column metadata against the database, and emits
    /// via the shared Core generators. <paramref name="name"/> overrides the
    /// emitted type/method name; <paramref name="groupingOverride"/> overrides
    /// the base-name grouping lookup.
    /// </summary>
    private void GenerateForSqlFile(
        SourceProductionContext context,
        AdditionalText sqlFile,
        string? name,
        AdditionalText? groupingOverride,
        SourceGeneratorDataProviderConfiguration config,
        ILookup<string, AdditionalText> groupingByBase,
        CodeGenerationConfig codeGenConfig
    )
    {
        var sqlText = sqlFile.GetText(context.CancellationToken)?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sqlText))
        {
            return;
        }

        var baseName = name ?? Path.GetFileNameWithoutExtension(sqlFile.Path);

        // [GEN-NONQUERY] DML statements (INSERT/UPDATE/DELETE without a result
        // clause) must never execute against the DB at compile time — emit via
        // the shared non-query generator instead of metadata discovery.
        if (SqlParserCsImplementation.IsNonQuery(sqlText))
        {
            GenerateNonQuery(context, baseName, sqlText, codeGenConfig);
            return;
        }

        var parseResult = _parser.ParseSql(sqlText);
        if (parseResult is not Result<SelectStatement, string>.Ok<SelectStatement, string> parsed)
        {
            var message = parseResult
                is Result<SelectStatement, string>.Error<SelectStatement, string> failure
                ? failure.Value
                : "Unknown parse failure";
            context.ReportDiagnostic(
                ErrorDiag($"Failed to parse SQL file '{baseName}': {message}")
            );
            return;
        }

        var columnsResult = SqlServerCodeGenerator
            .GetColumnMetadataFromSqlAsync(
                config.ConnectionString,
                sqlText,
                parsed.Value.Parameters
            )
            .GetAwaiter()
            .GetResult();
        if (
            columnsResult
            is not Result<IReadOnlyList<DatabaseColumn>, SqlError>.Ok<
                IReadOnlyList<DatabaseColumn>,
                SqlError
            > columns
        )
        {
            var error = columnsResult
                is Result<IReadOnlyList<DatabaseColumn>, SqlError>.Error<
                    IReadOnlyList<DatabaseColumn>,
                    SqlError
                > colFailure
                ? colFailure.Value.Message
                : "Unknown metadata failure";
            context.ReportDiagnostic(
                ErrorDiag($"Failed to get column metadata for {baseName}: {error}")
            );
            return;
        }

        var grouping = groupingOverride is null
            ? LoadGroupingFromLookup(context, baseName, groupingByBase)
            : LoadGrouping(context, baseName, groupingOverride);
        var sourceResult = SqlServerCodeGenerator.GenerateCodeWithMetadata(
            baseName,
            sqlText,
            parsed.Value,
            config.ConnectionString,
            columns.Value,
            hasCustomImplementation: false,
            grouping,
            codeGenConfig
        );
        EmitResult(context, baseName, sourceResult);
    }
}
