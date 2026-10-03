using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Nimblesite.DataProvider.Core.CodeGeneration;
using Nimblesite.DataProvider.SQLite;
using Nimblesite.DataProvider.SQLite.Parsing;
using Nimblesite.Sql.Model;
using Outcome;

#pragma warning disable CA1849 // Call async methods when in an async method

namespace DataProvider;

internal static class SqliteCli
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static Command BuildCommand()
    {
        var projectDir = new Option<DirectoryInfo>(
            "--project-dir",
            description: "Project directory containing sql, grouping, and DataProvider.json"
        )
        {
            IsRequired = true,
        };
        var config = new Option<FileInfo>("--config", description: "Path to DataProvider.json")
        {
            IsRequired = true,
        };
        var outDir = new Option<DirectoryInfo>(
            "--out",
            description: "Output directory for generated .g.cs files"
        )
        {
            IsRequired = true,
        };
        var connectionTypeOpt = new Option<string>(
            "--connection-type",
            getDefaultValue: () => "SqliteConnection",
            description: "Database connection type for generated code (e.g., SqliteConnection, NpgsqlConnection)"
        );
        var cmd = new Command("sqlite", "Generate type-safe SQLite data access code")
        {
            projectDir,
            config,
            outDir,
            connectionTypeOpt,
        };
        cmd.SetHandler(
            async (DirectoryInfo proj, FileInfo cfg, DirectoryInfo output, string connType) =>
            {
                var exit = await RunAsync(proj, cfg, output, connectionType: connType)
                    .ConfigureAwait(false);
                Environment.Exit(exit);
            },
            projectDir,
            config,
            outDir,
            connectionTypeOpt
        );
        return cmd;
    }

    public static async Task<int> RunAsync(
        DirectoryInfo projectDir,
        FileInfo configFile,
        DirectoryInfo outDir,
        string connectionType = "SqliteConnection"
    )
    {
        try
        {
            if (!configFile.Exists)
            {
                Console.WriteLine($"❌ Config not found: {configFile.FullName}");
                return 1;
            }

            if (!outDir.Exists)
                outDir.Create();

            var cfgText = await File.ReadAllTextAsync(configFile.FullName).ConfigureAwait(false);
            var cfg = JsonSerializer.Deserialize<SourceGeneratorDataProviderConfiguration>(
                cfgText,
                JsonOptions
            );
            if (cfg is null || string.IsNullOrWhiteSpace(cfg.ConnectionString))
            {
                Console.WriteLine(
                    "❌ Nimblesite.DataProvider.Core.json ConnectionString is required"
                );
                return 1;
            }

            // Make the connection string path absolute relative to project directory
            var absoluteConnectionString = MakeConnectionStringAbsolute(
                cfg.ConnectionString,
                projectDir.FullName
            );

            // Verify DB exists and is accessible; if empty, run schema file
            try
            {
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection(
                    absoluteConnectionString
                );
                await conn.OpenAsync().ConfigureAwait(false);

                // Check if any tables exist
                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText =
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
                var tableCount = Convert.ToInt64(
                    await checkCmd.ExecuteScalarAsync().ConfigureAwait(false),
                    CultureInfo.InvariantCulture
                );

                if (tableCount == 0)
                {
                    // Look for build-schema.sql in project directory
                    var schemaFile = Path.Combine(projectDir.FullName, "build-schema.sql");
                    if (File.Exists(schemaFile))
                    {
                        Console.WriteLine($"🔧 Creating database schema from {schemaFile}");
                        var schemaSql = await File.ReadAllTextAsync(schemaFile)
                            .ConfigureAwait(false);
                        using var schemaCmd = conn.CreateCommand();
                        schemaCmd.CommandText = schemaSql;
                        await schemaCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        Console.WriteLine("✅ Database schema created");
                    }
                    else
                    {
                        Console.WriteLine(
                            $"⚠️ No tables in database and no build-schema.sql found"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to open database: {ex.Message}");
                return 1;
            }

            // Gather files
            var sqlFiles = Directory.GetFiles(
                projectDir.FullName,
                "*.sql",
                SearchOption.AllDirectories
            );
            var groupingFiles = Directory
                .GetFiles(projectDir.FullName, "*.grouping.json", SearchOption.AllDirectories)
                .Select(p =>
                {
                    var fileName = Path.GetFileName(p);
                    var baseName = fileName.EndsWith(
                        ".grouping.json",
                        StringComparison.OrdinalIgnoreCase
                    )
                        ? fileName[..^".grouping.json".Length]
                        : Path.GetFileNameWithoutExtension(fileName);
                    return new { Path = p, Base = baseName };
                })
                .ToLookup(x => x.Base, x => x.Path);

            var parser = new SqliteAntlrParser();

            // Build custom config when targeting a non-SQLite connection type
            CodeGenerationConfig? codeGenConfig = null;
            if (!string.Equals(connectionType, "SqliteConnection", StringComparison.Ordinal))
            {
                var tableOpGen = new DefaultTableOperationGenerator(connectionType);
                codeGenConfig = new CodeGenerationConfig(
                    SqliteCodeGenerator.GetColumnMetadataFromSqlAsync,
                    tableOpGen
                )
                {
                    ConnectionType = connectionType,
                    TargetNamespace = "Generated",
                    GenerateSourceFile = (ns, model, dataAccess) =>
                        GenerateSourceFileForConnectionType(ns, model, dataAccess, connectionType),
                };
            }

            var hadErrors = false;

            foreach (var sqlPath in sqlFiles)
            {
                try
                {
                    var sql = await File.ReadAllTextAsync(sqlPath).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(sql))
                        continue;

                    var baseName = Path.GetFileNameWithoutExtension(sqlPath);
                    // Normalize base name to ignore optional ".generated" suffix from intermediate files
                    if (baseName.EndsWith(".generated", StringComparison.OrdinalIgnoreCase))
                    {
                        baseName = baseName[..^".generated".Length];
                    }
                    if (
                        string.Equals(baseName, "schema", StringComparison.OrdinalIgnoreCase)
                        || baseName.EndsWith("_schema", StringComparison.OrdinalIgnoreCase)
                        || baseName.EndsWith("-schema", StringComparison.OrdinalIgnoreCase)
                        || baseName.StartsWith("build-", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        // Skip schema files; they're only for DB initialization
                        continue;
                    }
                    // Pre-process SQL for SQLite compatibility: ILIKE is Postgres-only,
                    // replace with LIKE for parsing/metadata (original SQL kept for generated code)
                    var sqliteSql = sql.Replace(
                        " ILIKE ",
                        " LIKE ",
                        StringComparison.OrdinalIgnoreCase
                    );
                    var parseResult = parser.ParseSql(sqliteSql);
                    if (
                        parseResult
                        is Result<SelectStatement, string>.Error<
                            SelectStatement,
                            string
                        > parseFailure
                    )
                    {
                        Console.WriteLine(
                            $"Error parsing SQL file '{sqlPath}': {parseFailure.Value}"
                        );
                        continue;
                    }
                    var stmt = (
                        (Result<SelectStatement, string>.Ok<SelectStatement, string>)parseResult
                    ).Value;

                    var colsResult = await SqliteCodeGenerator
                        .GetColumnMetadataFromSqlAsync(
                            absoluteConnectionString,
                            sqliteSql,
                            stmt.Parameters
                        )
                        .ConfigureAwait(false);
                    if (
                        colsResult
                        is not Result<IReadOnlyList<DatabaseColumn>, SqlError>.Ok<
                            IReadOnlyList<DatabaseColumn>,
                            SqlError
                        > cols
                    )
                    {
                        var err = (
                            colsResult
                            as Result<IReadOnlyList<DatabaseColumn>, SqlError>.Error<
                                IReadOnlyList<DatabaseColumn>,
                                SqlError
                            >
                        )!.Value;
                        var prettyMeta = FormatSqliteMetadataMessage(err.DetailedMessage);
                        Console.WriteLine($"❌ {prettyMeta}");
                        // Also emit an MSBuild-formatted error so IDE Problem Matchers pick it up
                        Console.Error.WriteLine($"{sqlPath}(1,1): error DP0001: {prettyMeta}");
                        // Emit a compile-time error file so MSBuild surfaces the exact problem
                        await WriteCompileTimeErrorFileAsync(
                                outDir,
                                baseName,
                                sqlPath,
                                reason: "SQL error",
                                message: prettyMeta
                            )
                            .ConfigureAwait(false);
                        hadErrors = true;
                        continue;
                    }

                    GroupingConfig? grouping = null;
                    if (groupingFiles.Contains(baseName))
                    {
                        var gpath = groupingFiles[baseName].First();
                        var gtext = await File.ReadAllTextAsync(gpath).ConfigureAwait(false);
                        grouping = JsonSerializer.Deserialize<GroupingConfig>(gtext, JsonOptions);
                    }

                    var gen = SqliteCodeGenerator.GenerateCodeWithMetadata(
                        baseName,
                        sql,
                        stmt,
                        absoluteConnectionString,
                        cols.Value,
                        hasCustomImplementation: false,
                        grouping,
                        codeGenConfig
                    );
                    if (gen is Result<string, SqlError>.Ok<string, SqlError> success)
                    {
                        var target = Path.Combine(outDir.FullName, baseName + ".g.cs");
                        await File.WriteAllTextAsync(target, success.Value).ConfigureAwait(false);
                        Console.WriteLine($"✅ Generated {target}");
                    }
                    else if (gen is Result<string, SqlError>.Error<string, SqlError> failure)
                    {
                        var prettyGen = FormatSqliteMetadataMessage(failure.Value.DetailedMessage);
                        Console.WriteLine($"❌ {prettyGen}");
                        // Also emit an MSBuild-formatted error so IDE Problem Matchers pick it up
                        Console.Error.WriteLine($"{sqlPath}(1,1): error DL0002: {prettyGen}");
                        // Emit a compile-time error file so MSBuild surfaces the exact problem
                        await WriteCompileTimeErrorFileAsync(
                                outDir,
                                baseName,
                                sqlPath,
                                reason: "SQL generation error",
                                message: prettyGen
                            )
                            .ConfigureAwait(false);
                        hadErrors = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error processing {sqlPath}: {ex.Message}");
                    // Emit a compile-time error file so MSBuild surfaces the exact problem
                    var baseName = Path.GetFileNameWithoutExtension(sqlPath);
                    if (baseName.EndsWith(".generated", StringComparison.OrdinalIgnoreCase))
                    {
                        baseName = baseName[..^".generated".Length];
                    }
                    Console.Error.WriteLine($"{sqlPath}(1,1): error DL0003: {ex.Message}");
                    await WriteCompileTimeErrorFileAsync(
                            outDir,
                            baseName,
                            sqlPath,
                            reason: "unexpected error",
                            message: ex.Message
                        )
                        .ConfigureAwait(false);
                    hadErrors = true;
                }
            }

            // Generate table operations if configured
            if (cfg.Tables?.Count > 0)
            {
                Console.WriteLine($"🔧 Processing {cfg.Tables.Count} table configurations...");

                foreach (var tableConfigItem in cfg.Tables)
                {
                    try
                    {
                        Console.WriteLine(
                            $"🔧 Generating operations for table {tableConfigItem.Name}..."
                        );

                        // Use SQLite's native schema inspection to get table metadata
                        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(
                            absoluteConnectionString
                        );
                        await conn.OpenAsync().ConfigureAwait(false);

                        var columns = new List<DatabaseColumn>();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = $"PRAGMA table_info({tableConfigItem.Name})";
                            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

                            while (await reader.ReadAsync().ConfigureAwait(false))
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
                            Console.WriteLine(
                                $"❌ Table {tableConfigItem.Name} not found or has no columns"
                            );
                            hadErrors = true;
                            continue;
                        }

                        var table = new DatabaseTable
                        {
                            Schema = "main",
                            Name = tableConfigItem.Name,
                            Columns = columns.AsReadOnly(),
                        };

                        // Convert TableConfigItem to TableConfig
                        var tableConfig = new TableConfig
                        {
                            Schema = tableConfigItem.Schema,
                            Name = tableConfigItem.Name,
                            GenerateInsert = tableConfigItem.GenerateInsert,
                            GenerateUpdate = tableConfigItem.GenerateUpdate,
                            GenerateDelete = tableConfigItem.GenerateDelete,
                            ExcludeColumns = tableConfigItem.ExcludeColumns.ToList().AsReadOnly(),
                            PrimaryKeyColumns = tableConfigItem
                                .PrimaryKeyColumns.ToList()
                                .AsReadOnly(),
                        };

                        // Generate table operations
                        var tableOperationGenerator = new DefaultTableOperationGenerator(
                            connectionType
                        );
                        var operationsResult = tableOperationGenerator.GenerateTableOperations(
                            table,
                            tableConfig
                        );
                        if (
                            operationsResult
                            is Result<string, SqlError>.Ok<string, SqlError> operationsSuccess
                        )
                        {
                            var target = Path.Combine(
                                outDir.FullName,
                                tableConfigItem.Name + "Operations.g.cs"
                            );
                            await File.WriteAllTextAsync(target, operationsSuccess.Value)
                                .ConfigureAwait(false);
                            Console.WriteLine($"✅ Generated {target}");
                        }
                        else if (
                            operationsResult
                            is Result<string, SqlError>.Error<string, SqlError> operationsFailure
                        )
                        {
                            Console.WriteLine(
                                $"❌ Failed to generate table operations for {tableConfigItem.Name}: {operationsFailure.Value.Message}"
                            );
                            hadErrors = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"❌ Error generating table operations for {tableConfigItem.Name}: {ex.Message}"
                        );
                        hadErrors = true;
                    }
                }
            }

            return hadErrors ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Unexpected error: {ex}");
            return 1;
        }
    }

    private static async Task WriteCompileTimeErrorFileAsync(
        DirectoryInfo outDir,
        string baseName,
        string sqlPath,
        string reason,
        string message
    )
    {
        var errorFile = Path.Combine(outDir.FullName, baseName + ".g.cs");
        var content =
            $"// Auto-generated due to {reason} in {sqlPath}\n#error {EscapeForPreprocessor(message)}\n";
        await File.WriteAllTextAsync(errorFile, content).ConfigureAwait(false);
    }

    /// <summary>
    /// Generates a source file with the correct using statement for the specified connection type.
    /// </summary>
    private static Result<string, SqlError> GenerateSourceFileForConnectionType(
        string namespaceName,
        string modelCode,
        string dataAccessCode,
        string connectionType
    )
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("namespaceName cannot be null or empty")
            );

        if (string.IsNullOrWhiteSpace(modelCode) && string.IsNullOrWhiteSpace(dataAccessCode))
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("At least one of modelCode or dataAccessCode must be provided")
            );

        var connectionNamespace = connectionType switch
        {
            "NpgsqlConnection" => "Npgsql",
            "SqlConnection" => "Microsoft.Data.SqlClient",
            _ => "Microsoft.Data.Sqlite",
        };

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Collections.Immutable;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine(CultureInfo.InvariantCulture, $"using {connectionNamespace};");
        sb.AppendLine("using Outcome;");
        sb.AppendLine("using Nimblesite.Sql.Model;");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"namespace {namespaceName};");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(dataAccessCode))
        {
            sb.Append(dataAccessCode);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(modelCode))
        {
            sb.Append(modelCode);
        }

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    private static string EscapeForPreprocessor(string message)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;
        // Preprocessor directives cannot span multiple lines; replace newlines and quotes
        var oneLine = message.Replace('\r', ' ').Replace('\n', ' ');
        return oneLine.Replace('"', '\'');
    }

    private static string FormatSqliteMetadataMessage(string detailed)
    {
        if (string.IsNullOrWhiteSpace(detailed))
            return detailed;

        var msg = detailed.Trim();

        const string sqlitePrefix = "SQLite Error ";
        var idx = msg.IndexOf(sqlitePrefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return msg; // Not a known SQLite pattern; return as-is

        // Extract code
        var codeStart = idx + sqlitePrefix.Length;
        var colonAfterCode = msg.IndexOf(':', codeStart);
        if (colonAfterCode < 0)
            return msg;
        var codeText = msg[codeStart..colonAfterCode].Trim();

        // Extract inner quoted detail after colon, between single quotes if present
        var firstQuote = msg.IndexOf('\'', colonAfterCode + 1);
        var lastQuote = firstQuote >= 0 ? msg.IndexOf('\'', firstQuote + 1) : -1;
        string detail =
            lastQuote > firstQuote && firstQuote >= 0
                ? msg.Substring(firstQuote + 1, lastQuote - firstQuote - 1)
                : msg[(colonAfterCode + 1)..].Trim().Trim('.');

        // If detail starts with "no such column: ", rephrase
        const string noSuch = "no such column: ";
        string head;
        if (detail.StartsWith(noSuch, StringComparison.OrdinalIgnoreCase))
        {
            var col = detail[noSuch.Length..].Trim();
            head = $"No such column {col}.";
        }
        else
        {
            // Capitalize first letter and ensure trailing period
            if (detail.Length > 0)
            {
                var first = char.ToUpperInvariant(detail[0]);
                var rest = detail.Length > 1 ? detail[1..] : string.Empty;
                detail = first + rest;
            }
            head = detail.EndsWith('.') ? detail : detail + ".";
        }

        // Keep the leading context up to the sqlite prefix (e.g., "Failed to get column metadata: ")
        var contextPart = msg[..idx].Trim();
        if (contextPart.EndsWith(':'))
            contextPart = contextPart[..^1].TrimEnd();

        var tail = string.IsNullOrEmpty(contextPart) ? string.Empty : $" {contextPart}";
        var final = string.IsNullOrEmpty(tail)
            ? head
            : $"{head}{tail} (SQLite generation Error {codeText})";
        return final;
    }

    /// <summary>
    /// Makes a SQLite connection string's Data Source path absolute relative to a project directory.
    /// </summary>
    private static string MakeConnectionStringAbsolute(string connectionString, string projectDir)
    {
        // Parse "Data Source=path" from connection string
        const string dataSourcePrefix = "Data Source=";
        var idx = connectionString.IndexOf(dataSourcePrefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return connectionString;

        var pathStart = idx + dataSourcePrefix.Length;
        var semicolonIdx = connectionString.IndexOf(';', pathStart);
        var dbPath =
            semicolonIdx >= 0
                ? connectionString[pathStart..semicolonIdx]
                : connectionString[pathStart..];

        // If already absolute or special (like :memory:), return as-is
        if (Path.IsPathRooted(dbPath) || dbPath.StartsWith(':'))
            return connectionString;

        // Make path absolute relative to project directory
        var absolutePath = Path.GetFullPath(Path.Combine(projectDir, dbPath));

        // Reconstruct connection string
        var prefix = connectionString[..idx];
        var suffix = semicolonIdx >= 0 ? connectionString[semicolonIdx..] : string.Empty;
        return $"{prefix}{dataSourcePrefix}{absolutePath}{suffix}";
    }
}
