using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Nimblesite.DataProvider.Core.CodeGeneration;
using Nimblesite.DataProvider.SQLite.CodeGeneration;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.SQLite;

/// <summary>
/// SQLite specific code generator implementation and incremental source generator entrypoint
/// </summary>
[Generator]
public sealed class SqliteCodeGenerator : IIncrementalGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Generates C# source for a SQL file using real database metadata.
    /// Delegates to <see cref="SqlAntlrCodeGenerator.GenerateCodeWithMetadata"/>
    /// per [CON-SHARED-CORE]; this method is now a thin SQLite-specific
    /// adapter that supplies the default SQLite <see cref="CodeGenerationConfig"/>.
    /// </summary>
    /// <param name="fileName">The SQL file name.</param>
    /// <param name="sql">The SQL content.</param>
    /// <param name="statement">The parsed SQL statement metadata.</param>
    /// <param name="connectionString">The database connection string (unused; callers must have already resolved metadata).</param>
    /// <param name="columnMetadata">Real column metadata from the database.</param>
    /// <param name="hasCustomImplementation">Whether a custom implementation exists.</param>
    /// <param name="groupingConfig">Optional grouping configuration for parent-child relationships.</param>
    /// <param name="config">Optional code generation configuration with custom functions.</param>
    /// <returns>Result with generated source or an error.</returns>
    public static Result<string, SqlError> GenerateCodeWithMetadata(
        string fileName,
        string sql,
        SelectStatement statement,
        string connectionString,
        IReadOnlyList<DatabaseColumn> columnMetadata,
        bool hasCustomImplementation,
        GroupingConfig? groupingConfig = null,
        CodeGenerationConfig? config = null
    )
    {
        _ = connectionString; // Metadata has already been resolved by the caller
        _ = hasCustomImplementation; // Suppress unused parameter warning

        return SqlAntlrCodeGenerator.GenerateCodeWithMetadata(
            fileName,
            sql,
            statement,
            columnMetadata,
            config ?? CreateDefaultSqliteConfig(),
            groupingConfig
        );
    }

    /// <summary>
    /// Creates default SQLite configuration for code generation
    /// </summary>
    private static CodeGenerationConfig CreateDefaultSqliteConfig()
    {
        var databaseEffects = SqliteDatabaseEffects.Create();
        var tableOperationGenerator = new DefaultTableOperationGenerator("SqliteConnection");

        return new CodeGenerationConfig(
            databaseEffects.GetColumnMetadataFromSqlAsync,
            tableOperationGenerator
        )
        {
            ConnectionType = "SqliteConnection",
            TargetNamespace = "Generated",
        };
    }

    /// <summary>
    /// Generate C# source code for Insert/Update operations based on table configuration
    /// </summary>
    /// <param name="tableOperationGenerator">The table generator</param>
    /// <param name="table">The database table metadata</param>
    /// <param name="config">The table configuration</param>
    /// <returns>The generated C# source code</returns>
    public static Result<string, SqlError> GenerateTableOperations(
        ITableOperationGenerator tableOperationGenerator,
        DatabaseTable table,
        TableConfig config
    ) => tableOperationGenerator.GenerateTableOperations(table, config);

    /// <summary>
    /// Gets column metadata by executing the SQL query against the database.
    /// This is the proper way to get column types - by executing the query and checking metadata.
    /// </summary>
    /// <param name="connectionString">Database connection string</param>
    /// <param name="sql">SQL query to execute</param>
    /// <param name="parameters">Query parameters</param>
    /// <returns>List of database columns with their metadata</returns>
    public static async Task<
        Result<IReadOnlyList<DatabaseColumn>, SqlError>
    > GetColumnMetadataFromSqlAsync(
        string connectionString,
        string sql,
        IEnumerable<ParameterInfo> parameters
    )
    {
        var databaseEffects = SqliteDatabaseEffects.Create();
        return await databaseEffects
            .GetColumnMetadataFromSqlAsync(connectionString, sql, parameters)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets table metadata using SQLite's native PRAGMA table_info command
    /// </summary>
    private static Result<DatabaseTable, SqlError> GetTableMetadataFromDatabase(
        string connectionString,
        string tableName
    )
    {
        try
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
            connection.Open();

            // Get column information from SQLite's PRAGMA table_info
            var columns = new List<DatabaseColumn>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA table_info({tableName})";
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var columnName = reader.GetString(1); // name column
                    var sqliteType = reader.GetString(2); // type column
                    var notNull = reader.GetInt32(3) == 1; // notnull column
                    var isPrimaryKey = reader.GetInt32(5) > 0; // pk column

                    var csharpType = SqliteTypeMapper.MapSqliteTypeToCSharpType(
                        sqliteType,
                        !notNull
                    );

                    columns.Add(
                        new DatabaseColumn
                        {
                            Name = columnName,
                            SqlType = sqliteType,
                            CSharpType = csharpType,
                            IsNullable = !notNull,
                            IsPrimaryKey = isPrimaryKey,
                            IsIdentity =
                                isPrimaryKey
                                && sqliteType.Contains(
                                    "INTEGER",
                                    StringComparison.OrdinalIgnoreCase
                                ),
                            IsComputed = false,
                        }
                    );
                }
            }

            if (columns.Count == 0)
            {
                return new Result<DatabaseTable, SqlError>.Error<DatabaseTable, SqlError>(
                    new SqlError($"Table {tableName} not found or has no columns")
                );
            }

            var table = new DatabaseTable
            {
                Schema = "main",
                Name = tableName,
                Columns = columns.AsReadOnly(),
            };

            return new Result<DatabaseTable, SqlError>.Ok<DatabaseTable, SqlError>(table);
        }
        catch (Exception ex)
        {
            return new Result<DatabaseTable, SqlError>.Error<DatabaseTable, SqlError>(
                new SqlError($"Failed to get table metadata for {tableName}", ex)
            );
        }
    }

    // =============================
    // Incremental generator wiring
    // =============================

    /// <summary>
    /// Initializes the incremental generator for SQLite. Collects .sql files, grouping config, and Nimblesite.DataProvider.Core.json
    /// and registers the output step.
    /// </summary>
    /// <param name="context">The initialization context</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var additional = context.AdditionalTextsProvider;

        var sqlFiles = additional.Where(at =>
            at.Path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
            && !Path.GetFileName(at.Path).Contains("schema", StringComparison.OrdinalIgnoreCase)
        );
        var schemaFiles = additional.Where(at =>
            Path.GetFileName(at.Path).Contains("schema", StringComparison.OrdinalIgnoreCase)
            && at.Path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
        );
        var configFiles = additional.Where(at =>
            at.Path.EndsWith(
                "Nimblesite.DataProvider.Core.json",
                StringComparison.OrdinalIgnoreCase
            )
        );
        var groupingFiles = additional.Where(at =>
            at.Path.EndsWith(".grouping.json", StringComparison.OrdinalIgnoreCase)
        );

        var sqlCollected = sqlFiles.Collect();
        var schemaCollected = schemaFiles.Collect();
        var configCollected = configFiles.Collect();
        var groupingCollected = groupingFiles.Collect();

        var left = sqlCollected.Combine(schemaCollected);
        var middle = left.Combine(configCollected);
        var all = middle.Combine(groupingCollected);

        context.RegisterSourceOutput(all, GenerateCodeForAllFiles);
    }

    private static void GenerateCodeForAllFiles(
        SourceProductionContext context,
        (
            (
                (
                    ImmutableArray<AdditionalText> SqlFiles,
                    ImmutableArray<AdditionalText> SchemaFiles
                ) Left,
                ImmutableArray<AdditionalText> ConfigFiles
            ) Middle,
            ImmutableArray<AdditionalText> GroupingFiles
        ) data
    )
    {
        var (middle, groupingFiles) = data;
        var (left, configFiles) = middle;
        var (sqlFiles, schemaFiles) = left;

        // Read configuration
        SourceGeneratorDataProviderConfiguration? config = null;
        if (configFiles.Length > 0)
        {
            var configContent = configFiles[0].GetText()?.ToString();
            if (!string.IsNullOrEmpty(configContent))
            {
                try
                {
                    config = JsonSerializer.Deserialize<SourceGeneratorDataProviderConfiguration>(
                        configContent!,
                        JsonOptions
                    );
                }
                catch (JsonException ex)
                {
                    var diag = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "DataProvider002",
                            "Configuration parsing failed",
                            "Failed to parse Nimblesite.DataProvider.Core.json: {0}",
                            "DataProvider",
                            DiagnosticSeverity.Error,
                            true
                        ),
                        Location.None,
                        ex.Message
                    );
                    context.ReportDiagnostic(diag);
                    return;
                }
            }
        }

        if (config == null || string.IsNullOrWhiteSpace(config.ConnectionString))
        {
            var diag = Diagnostic.Create(
                new DiagnosticDescriptor(
                    "DataProvider003",
                    "Configuration missing",
                    "Nimblesite.DataProvider.Core.json with ConnectionString is required for code generation",
                    "DataProvider",
                    DiagnosticSeverity.Error,
                    true
                ),
                Location.None
            );
            context.ReportDiagnostic(diag);
            return;
        }

        // Build lookup for grouping configs by base filename (strip ".grouping.json")
        var groupingByBase = groupingFiles
            .Select(g =>
            {
                var fileName = Path.GetFileName(g.Path);
                var baseName = fileName.EndsWith(
                    ".grouping.json",
                    StringComparison.OrdinalIgnoreCase
                )
                    ? fileName[..^".grouping.json".Length]
                    : Path.GetFileNameWithoutExtension(fileName);
                return new { Text = g, Base = baseName };
            })
            .ToLookup(x => x.Base, x => x.Text);

        var parser = new Parsing.SqliteAntlrParser();

        // Helper to detect non-query statements
        static bool IsNonQueryStatement(string sql)
        {
            var trimmed = sql.TrimStart();
            return trimmed.StartsWith("UPDATE ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("DELETE ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("INSERT ", StringComparison.OrdinalIgnoreCase);
        }

        // Helper to extract @parameters from SQL
        static IReadOnlyList<ParameterInfo> ExtractParameters(string sql)
        {
            var parameters = new List<ParameterInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var i = 0;
            while (i < sql.Length)
            {
                if (sql[i] == '@')
                {
                    var start = i + 1;
                    var end = start;
                    while (end < sql.Length && (char.IsLetterOrDigit(sql[end]) || sql[end] == '_'))
                    {
                        end++;
                    }
                    if (end > start)
                    {
                        var name = sql[start..end];
                        if (seen.Add(name))
                        {
                            parameters.Add(new ParameterInfo(name, "TEXT"));
                        }
                    }
                    i = end;
                }
                else
                {
                    i++;
                }
            }
            return parameters.AsReadOnly();
        }

        foreach (var sqlFile in sqlFiles)
        {
            try
            {
                var sqlText =
                    sqlFile.GetText(context.CancellationToken)?.ToString() ?? string.Empty;
                var baseName = Path.GetFileNameWithoutExtension(sqlFile.Path);
                // Normalize base name to ignore optional ".generated" suffix from intermediate files
                if (baseName.EndsWith(".generated", StringComparison.OrdinalIgnoreCase))
                {
                    baseName = baseName[..^".generated".Length];
                }

                if (string.IsNullOrWhiteSpace(sqlText))
                {
                    continue;
                }

                // Handle non-query statements (UPDATE/DELETE/INSERT) separately
                if (IsNonQueryStatement(sqlText))
                {
                    var parameters = ExtractParameters(sqlText);
                    var className = $"{baseName}Extensions";
                    var nonQueryResult = DataAccessGenerator.GenerateNonQueryMethod(
                        className,
                        baseName,
                        sqlText,
                        parameters,
                        "SqliteConnection"
                    );

                    if (nonQueryResult is Result<string, SqlError>.Ok<string, SqlError> nqSuccess)
                    {
                        // Wrap in source file with usings
                        var sb = new StringBuilder();
                        sb.AppendLine("using System;");
                        sb.AppendLine("using System.Threading.Tasks;");
                        sb.AppendLine("using Microsoft.Data.Sqlite;");
                        sb.AppendLine("using Outcome;");
                        sb.AppendLine("using Nimblesite.Sql.Model;");
                        sb.AppendLine();
                        sb.AppendLine("namespace Generated;");
                        sb.AppendLine();
                        sb.Append(nqSuccess.Value);

                        context.AddSource(
                            baseName + ".g.cs",
                            SourceText.From(sb.ToString(), Encoding.UTF8)
                        );
                    }
                    else if (
                        nonQueryResult is Result<string, SqlError>.Error<string, SqlError> nqFailure
                    )
                    {
                        var nqDiag = Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "DP0003",
                                "Non-query generation failed",
                                $"Failed to generate non-query for '{baseName}': {nqFailure.Value.Message}",
                                "Nimblesite.DataProvider.SQLite",
                                DiagnosticSeverity.Error,
                                true
                            ),
                            Location.None
                        );
                        context.ReportDiagnostic(nqDiag);
                    }
                    continue;
                }

                // Parse SQL (attach any unexpected parser errors to the SQL file)
                var parseResult = parser.ParseSql(sqlText);
                if (
                    parseResult
                    is Result<SelectStatement, string>.Error<SelectStatement, string> parseFailure
                )
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "DP0002",
                                "SQL Parse Error",
                                $"Failed to parse SQL file '{baseName}': {parseFailure.Value}",
                                "Nimblesite.DataProvider.SQLite",
                                DiagnosticSeverity.Error,
                                true
                            ),
                            Location.None
                        )
                    );
                    continue;
                }
                var statement = (
                    (Result<SelectStatement, string>.Ok<SelectStatement, string>)parseResult
                ).Value;

                // Discover real column metadata by executing the SQL against the DB
                var columnsResult = GetColumnMetadataFromSqlAsync(
                        config.ConnectionString,
                        sqlText,
                        statement.Parameters
                    )
                    .GetAwaiter()
                    .GetResult();

                if (
                    columnsResult
                    is not Result<IReadOnlyList<DatabaseColumn>, SqlError>.Ok<
                        IReadOnlyList<DatabaseColumn>,
                        SqlError
                    > colSuccess
                )
                {
                    var err = (
                        columnsResult
                        as Result<IReadOnlyList<DatabaseColumn>, SqlError>.Error<
                            IReadOnlyList<DatabaseColumn>,
                            SqlError
                        >
                    )!.Value;

                    // Attach the diagnostic to the start of the SQL file so IDEs show it inline
                    var text =
                        sqlFile.GetText(context.CancellationToken)
                        ?? SourceText.From(sqlText, Encoding.UTF8);
                    var location = CreateSqlFileLocation(sqlFile, text);

                    var diagCol = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "DL0002",
                            "Database metadata error",
                            "Failed to get column metadata for {0}: {1}",
                            "DataProvider",
                            DiagnosticSeverity.Error,
                            true
                        ),
                        location,
                        baseName,
                        err.DetailedMessage
                    );
                    context.ReportDiagnostic(diagCol);
                    continue;
                }

                // Optional grouping config for this SQL file
                GroupingConfig? groupingConfig = null;
                if (groupingByBase.Contains(baseName))
                {
                    var groupingText = groupingByBase[baseName].First().GetText()?.ToString();
                    if (!string.IsNullOrWhiteSpace(groupingText))
                    {
                        try
                        {
                            groupingConfig = JsonSerializer.Deserialize<GroupingConfig>(
                                groupingText!,
                                JsonOptions
                            );
                        }
                        catch (JsonException ex)
                        {
                            var diagGrp = Diagnostic.Create(
                                new DiagnosticDescriptor(
                                    "DataProvider004",
                                    "Grouping parsing failed",
                                    "Failed to parse {0}.grouping.json: {1}",
                                    "DataProvider",
                                    DiagnosticSeverity.Error,
                                    true
                                ),
                                Location.None,
                                baseName,
                                ex.Message
                            );
                            context.ReportDiagnostic(diagGrp);
                        }
                    }
                }

                var sourceResult = GenerateCodeWithMetadata(
                    baseName,
                    sqlText,
                    statement,
                    config.ConnectionString,
                    colSuccess.Value,
                    hasCustomImplementation: false,
                    groupingConfig
                );

                if (sourceResult is Result<string, SqlError>.Ok<string, SqlError> success)
                {
                    context.AddSource(
                        baseName + ".g.cs",
                        SourceText.From(success.Value, Encoding.UTF8)
                    );
                }
                else if (sourceResult is Result<string, SqlError>.Error<string, SqlError> failure)
                {
                    // Attach the diagnostic to the SQL file as well
                    var text =
                        sqlFile.GetText(context.CancellationToken)
                        ?? SourceText.From(sqlText, Encoding.UTF8);
                    var location = CreateSqlFileLocation(sqlFile, text);

                    var diagGen = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "DataProvider005",
                            "Code generation failed",
                            "Failed to generate code for {0}: {1}",
                            "DataProvider",
                            DiagnosticSeverity.Error,
                            true
                        ),
                        location,
                        baseName,
                        failure.Value.DetailedMessage
                    );
                    context.ReportDiagnostic(diagGen);
                }
            }
            catch (Exception ex)
            {
                // Attach unexpected errors to the SQL file so they are visible inline
                var text =
                    sqlFile.GetText(context.CancellationToken)
                    ?? SourceText.From(string.Empty, Encoding.UTF8);
                var location = CreateSqlFileLocation(sqlFile, text);

                var diag = Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "DataProvider006",
                        "Unexpected error",
                        "Unexpected error while generating for file '{0}': {1}",
                        "DataProvider",
                        DiagnosticSeverity.Error,
                        true
                    ),
                    location,
                    sqlFile.Path,
                    ex.Message
                );
                context.ReportDiagnostic(diag);
            }
        }

        // Generate table operations if configured
        var debugDiag = Diagnostic.Create(
            new DiagnosticDescriptor(
                "DataProvider010",
                "Debug info",
                "Processing {0} table configurations",
                "DataProvider",
                DiagnosticSeverity.Info,
                true
            ),
            Location.None,
            config.Tables.Count
        );
        context.ReportDiagnostic(debugDiag);

        if (config.Tables.Count > 0)
        {
            var tableOperationGenerator = new DefaultTableOperationGenerator("SqliteConnection");

            foreach (var tableConfigItem in config.Tables)
            {
                try
                {
                    // Use SQLite's native schema inspection
                    var tableMetadataResult = GetTableMetadataFromDatabase(
                        config.ConnectionString,
                        tableConfigItem.Name
                    );
                    if (
                        tableMetadataResult
                        is not Result<DatabaseTable, SqlError>.Ok<DatabaseTable, SqlError> tableOk
                    )
                    {
                        var err = (
                            tableMetadataResult
                            as Result<DatabaseTable, SqlError>.Error<DatabaseTable, SqlError>
                        )!.Value;
                        var tableDiag = Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "DataProvider007",
                                "Table metadata error",
                                "Failed to get metadata for table {0}: {1}",
                                "DataProvider",
                                DiagnosticSeverity.Warning,
                                true
                            ),
                            Location.None,
                            tableConfigItem.Name,
                            err.Message
                        );
                        context.ReportDiagnostic(tableDiag);
                        continue;
                    }

                    // Convert TableConfigItem to TableConfig
                    var tableConfig = new TableConfig
                    {
                        Schema = tableConfigItem.Schema,
                        Name = tableConfigItem.Name,
                        GenerateInsert = tableConfigItem.GenerateInsert,
                        GenerateUpdate = tableConfigItem.GenerateUpdate,
                        GenerateDelete = tableConfigItem.GenerateDelete,
                        ExcludeColumns = tableConfigItem.ExcludeColumns.ToList().AsReadOnly(),
                        PrimaryKeyColumns = tableConfigItem.PrimaryKeyColumns.ToList().AsReadOnly(),
                    };

                    // Generate table operations
                    var operationsResult = tableOperationGenerator.GenerateTableOperations(
                        tableOk.Value,
                        tableConfig
                    );
                    if (
                        operationsResult
                        is Result<string, SqlError>.Ok<string, SqlError> operationsSuccess
                    )
                    {
                        context.AddSource(
                            tableConfigItem.Name + "Operations.g.cs",
                            SourceText.From(operationsSuccess.Value, Encoding.UTF8)
                        );
                    }
                    else if (
                        operationsResult
                        is Result<string, SqlError>.Error<string, SqlError> operationsFailure
                    )
                    {
                        var opsDiag = Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "DataProvider008",
                                "Table operations generation failed",
                                "Failed to generate table operations for {0}: {1}",
                                "DataProvider",
                                DiagnosticSeverity.Error,
                                true
                            ),
                            Location.None,
                            tableConfigItem.Name,
                            operationsFailure.Value.Message
                        );
                        context.ReportDiagnostic(opsDiag);
                    }
                }
                catch (Exception ex)
                {
                    var exceptionDiag = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "DataProvider009",
                            "Table operations error",
                            "Error generating table operations for {0}: {1}",
                            "DataProvider",
                            DiagnosticSeverity.Error,
                            true
                        ),
                        Location.None,
                        tableConfigItem.Name,
                        ex.Message
                    );
                    context.ReportDiagnostic(exceptionDiag);
                }
            }
        }
    }

    /// <summary>
    /// Builds a diagnostic <see cref="Location"/> pointing at the start of a .sql additional file.
    /// </summary>
    private static Location CreateSqlFileLocation(AdditionalText sqlFile, SourceText text)
    {
        var span = new TextSpan(0, Math.Min(1, text.Length));
        var lineSpan = new LinePositionSpan(
            new LinePosition(0, 0),
            new LinePosition(0, Math.Min(1, text.Length))
        );
        return Location.Create(sqlFile.Path, span, lineSpan);
    }
}
