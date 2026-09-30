using System.CommandLine;
using System.Text;
using System.Text.Json;
using DataProvider.CodeGeneration;
using Npgsql;
using Outcome;
using Selecta;
using SqlParser;
using SqlParser.Ast;

#pragma warning disable CA1849 // Call async methods when in an async method
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities

namespace DataProvider.Postgres.Cli;

/// <summary>
/// PostgreSQL code generation CLI for DataProvider.
/// </summary>
internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Entry point.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        var projectDir = new Option<DirectoryInfo>(
            "--project-dir",
            description: "Project directory containing sql files and DataProvider.json"
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
        var root = new RootCommand("DataProvider.Postgres codegen CLI")
        {
            projectDir,
            config,
            outDir,
        };
        root.SetHandler(
            async (DirectoryInfo proj, FileInfo cfg, DirectoryInfo output) =>
            {
                var exit = await RunAsync(proj, cfg, output).ConfigureAwait(false);
                Environment.Exit(exit);
            },
            projectDir,
            config,
            outDir
        );

        return await root.InvokeAsync(args).ConfigureAwait(false);
    }

    private static async Task<int> RunAsync(
        DirectoryInfo projectDir,
        FileInfo configFile,
        DirectoryInfo outDir
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
            var cfg = JsonSerializer.Deserialize<PostgresDataProviderConfig>(cfgText, JsonOptions);
            if (cfg is null || string.IsNullOrWhiteSpace(cfg.ConnectionString))
            {
                Console.WriteLine("❌ DataProvider.json ConnectionString is required");
                return 1;
            }

            // Verify DB connection
            try
            {
                await using var conn = new NpgsqlConnection(cfg.ConnectionString);
                await conn.OpenAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to connect to database: {ex.Message}");
                return 1;
            }

            // Gather SQL files
            var sqlFiles = Directory.GetFiles(
                projectDir.FullName,
                "*.sql",
                SearchOption.AllDirectories
            );

            var hadErrors = false;

            foreach (var sqlPath in sqlFiles)
            {
                try
                {
                    var sql = await File.ReadAllTextAsync(sqlPath).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(sql))
                        continue;

                    var baseName = Path.GetFileNameWithoutExtension(sqlPath);
                    if (baseName.EndsWith(".generated", StringComparison.OrdinalIgnoreCase))
                    {
                        baseName = baseName[..^".generated".Length];
                    }
                    if (
                        string.Equals(baseName, "schema", StringComparison.OrdinalIgnoreCase)
                        || baseName.EndsWith("_schema", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        continue;
                    }

                    // Parse SQL to extract parameters
                    var parameters = ExtractParameters(sql);

                    // Get column metadata from PostgreSQL
                    var colsResult = await GetColumnMetadataAsync(
                            cfg.ConnectionString,
                            sql,
                            parameters
                        )
                        .ConfigureAwait(false);
                    if (
                        colsResult
                        is Result<IReadOnlyList<DatabaseColumn>, SqlError>.Error<
                            IReadOnlyList<DatabaseColumn>,
                            SqlError
                        > colsError
                    )
                    {
                        Console.WriteLine($"❌ {colsError.Value.Message}");
                        Console.Error.WriteLine(
                            $"{sqlPath}(1,1): error DP0001: {colsError.Value.Message}"
                        );
                        var errorFile = Path.Combine(outDir.FullName, baseName + ".g.cs");
                        var content =
                            $"// Auto-generated due to SQL error in {sqlPath}\n#error {EscapeForPreprocessor(colsError.Value.Message)}\n";
                        await File.WriteAllTextAsync(errorFile, content).ConfigureAwait(false);
                        hadErrors = true;
                        continue;
                    }

                    var cols = (
                        (Result<IReadOnlyList<DatabaseColumn>, SqlError>.Ok<
                            IReadOnlyList<DatabaseColumn>,
                            SqlError
                        >)colsResult
                    ).Value;

                    // Generate code
                    var genResult = GenerateCode(baseName, sql, cols, parameters);
                    if (genResult is Result<string, SqlError>.Ok<string, SqlError> success)
                    {
                        var target = Path.Combine(outDir.FullName, baseName + ".g.cs");
                        await File.WriteAllTextAsync(target, success.Value).ConfigureAwait(false);
                        Console.WriteLine($"✅ Generated {target}");
                    }
                    else if (genResult is Result<string, SqlError>.Error<string, SqlError> failure)
                    {
                        Console.WriteLine($"❌ {failure.Value.Message}");
                        Console.Error.WriteLine(
                            $"{sqlPath}(1,1): error DP0002: {failure.Value.Message}"
                        );
                        var errorFile = Path.Combine(outDir.FullName, baseName + ".g.cs");
                        var content =
                            $"// Auto-generated due to generation error in {sqlPath}\n#error {EscapeForPreprocessor(failure.Value.Message)}\n";
                        await File.WriteAllTextAsync(errorFile, content).ConfigureAwait(false);
                        hadErrors = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error processing {sqlPath}: {ex.Message}");
                    var baseName = Path.GetFileNameWithoutExtension(sqlPath);
                    if (baseName.EndsWith(".generated", StringComparison.OrdinalIgnoreCase))
                    {
                        baseName = baseName[..^".generated".Length];
                    }
                    var errorFile = Path.Combine(outDir.FullName, baseName + ".g.cs");
                    var content =
                        $"// Auto-generated due to unexpected error in {sqlPath}\n#error {EscapeForPreprocessor(ex.Message)}\n";
                    Console.Error.WriteLine($"{sqlPath}(1,1): error DP0003: {ex.Message}");
                    await File.WriteAllTextAsync(errorFile, content).ConfigureAwait(false);
                    hadErrors = true;
                }
            }

            // Process table configurations for INSERT/UPDATE generation
            if (cfg.Tables is { Count: > 0 })
            {
                foreach (var table in cfg.Tables)
                {
                    try
                    {
                        var tableCode = await GenerateTableOperationsAsync(
                            cfg.ConnectionString,
                            table,
                            outDir.FullName
                        ).ConfigureAwait(false);

                        if (tableCode is Result<string, SqlError>.Error<string, SqlError> err)
                        {
                            Console.WriteLine($"❌ Table {table.Name}: {err.Value.Message}");
                            hadErrors = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error processing table {table.Name}: {ex.Message}");
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

    private static async Task<Result<string, SqlError>> GenerateTableOperationsAsync(
        string connectionString,
        TableConfigItem table,
        string outDir)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync().ConfigureAwait(false);

        // Get column metadata from information_schema
        var columns = new List<DatabaseColumn>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT c.column_name, c.data_type, c.is_nullable, c.column_default,
                       COALESCE(c.is_identity, 'NO') as is_identity,
                       CASE WHEN pk.column_name IS NOT NULL THEN 'YES' ELSE 'NO' END as is_pk
                FROM information_schema.columns c
                LEFT JOIN (
                    SELECT kcu.column_name
                    FROM information_schema.table_constraints tc
                    JOIN information_schema.key_column_usage kcu
                        ON tc.constraint_name = kcu.constraint_name
                    WHERE tc.table_schema = @schema
                        AND tc.table_name = @table
                        AND tc.constraint_type = 'PRIMARY KEY'
                ) pk ON pk.column_name = c.column_name
                WHERE c.table_schema = @schema AND c.table_name = @table
                ORDER BY c.ordinal_position
                """;
            cmd.Parameters.AddWithValue("schema", table.Schema);
            cmd.Parameters.AddWithValue("table", table.Name);

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var colName = reader.GetString(0);
                var dataType = reader.GetString(1);
                var isNullable = reader.GetString(2) == "YES";
                var colDefault = reader.IsDBNull(3) ? null : reader.GetString(3);
                var isIdentity = reader.GetString(4) == "YES";
                var isPk = reader.GetString(5) == "YES";

                // Skip excluded columns
                if (table.ExcludeColumns.Contains(colName, StringComparer.OrdinalIgnoreCase))
                    continue;

                columns.Add(new DatabaseColumn
                {
                    Name = colName,
                    SqlType = dataType,
                    CSharpType = MapPostgresTypeToCSharp(dataType, isNullable),
                    IsNullable = isNullable,
                    IsPrimaryKey = isPk,
                    IsIdentity = isIdentity,
                    IsComputed = colDefault?.StartsWith("nextval", StringComparison.OrdinalIgnoreCase) == true,
                });
            }
        }

        if (columns.Count == 0)
        {
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError($"No columns found for table {table.Schema}.{table.Name}"));
        }

        var sb = new StringBuilder();
        var pascalName = ToPascalCase(table.Name);

        // Header
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Npgsql;");
        sb.AppendLine("using Outcome;");
        sb.AppendLine("using Selecta;");
        sb.AppendLine();

        // Extension class
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Generated CRUD operations for {table.Name} table.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public static class {pascalName}Extensions");
        sb.AppendLine("{");

        // Generate INSERT method
        if (table.GenerateInsert)
        {
            GenerateInsertMethod(sb, table, columns, pascalName);
        }

        // Generate UPDATE method
        if (table.GenerateUpdate)
        {
            GenerateUpdateMethod(sb, table, columns, pascalName);
        }

        // Generate DELETE method
        if (table.GenerateDelete)
        {
            GenerateDeleteMethod(sb, table, columns, pascalName);
        }

        // Generate bulk INSERT method
        if (table.GenerateBulkInsert)
        {
            GenerateBulkInsertMethod(sb, table, columns, pascalName);
        }

        // Generate bulk UPSERT method
        if (table.GenerateBulkUpsert)
        {
            GenerateBulkUpsertMethod(sb, table, columns, pascalName);
        }

        sb.AppendLine("}");

        var target = Path.Combine(outDir, $"{pascalName}Operations.g.cs");
        await File.WriteAllTextAsync(target, sb.ToString()).ConfigureAwait(false);
        Console.WriteLine($"✅ Generated {target}");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    private static void GenerateInsertMethod(
        StringBuilder sb,
        TableConfigItem table,
        List<DatabaseColumn> columns,
        string pascalName)
    {
        // Get insertable columns (exclude auto-generated ones)
        var insertable = columns.Where(c => !c.IsIdentity && !c.IsComputed).ToList();
        var parameters = string.Join(", ", insertable.Select(c =>
            $"{c.CSharpType} {ToCamelCase(c.Name)}"));

        sb.AppendLine();
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Inserts a row into {table.Name}. Returns inserted id or null on conflict.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public static async Task<Result<Guid?, SqlError>> Insert{pascalName}Async(");
        sb.AppendLine($"        this NpgsqlConnection conn,");
        sb.AppendLine($"        {parameters})");
        sb.AppendLine("    {");

        var colNames = string.Join(", ", insertable.Select(c => c.Name));
        var paramNames = string.Join(", ", insertable.Select(c => $"@{ToCamelCase(c.Name)}"));

        sb.AppendLine($"        const string sql = @\"");
        sb.AppendLine($"            INSERT INTO {table.Schema}.{table.Name} ({colNames})");
        sb.AppendLine($"            VALUES ({paramNames})");
        sb.AppendLine($"            ON CONFLICT DO NOTHING");
        sb.AppendLine($"            RETURNING id\";");
        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            await using var cmd = new NpgsqlCommand(sql, conn);");

        foreach (var col in insertable)
        {
            var paramName = ToCamelCase(col.Name);
            if (col.IsNullable)
            {
                sb.AppendLine($"            cmd.Parameters.AddWithValue(\"{paramName}\", {paramName} ?? (object)DBNull.Value);");
            }
            else
            {
                sb.AppendLine($"            cmd.Parameters.AddWithValue(\"{paramName}\", {paramName});");
            }
        }

        sb.AppendLine();
        sb.AppendLine("            var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);");
        sb.AppendLine("            return new Result<Guid?, SqlError>.Ok<Guid?, SqlError>(result is Guid g ? g : null);");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine("            return new Result<Guid?, SqlError>.Error<Guid?, SqlError>(SqlError.FromException(ex));");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    private static void GenerateUpdateMethod(
        StringBuilder sb,
        TableConfigItem table,
        List<DatabaseColumn> columns,
        string pascalName)
    {
        var pkCols = columns.Where(c => c.IsPrimaryKey).ToList();
        var updateable = columns.Where(c => !c.IsPrimaryKey && !c.IsIdentity && !c.IsComputed).ToList();

        if (pkCols.Count == 0 || updateable.Count == 0) return;

        var allParams = pkCols.Concat(updateable).ToList();
        var parameters = string.Join(", ", allParams.Select(c =>
            $"{c.CSharpType} {ToCamelCase(c.Name)}"));

        sb.AppendLine();
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Updates a row in {table.Name} by primary key.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public static async Task<Result<int, SqlError>> Update{pascalName}Async(");
        sb.AppendLine($"        this NpgsqlConnection conn,");
        sb.AppendLine($"        {parameters})");
        sb.AppendLine("    {");

        var setClauses = string.Join(", ", updateable.Select(c => $"{c.Name} = @{ToCamelCase(c.Name)}"));
        var whereClauses = string.Join(" AND ", pkCols.Select(c => $"{c.Name} = @{ToCamelCase(c.Name)}"));

        sb.AppendLine($"        const string sql = @\"");
        sb.AppendLine($"            UPDATE {table.Schema}.{table.Name}");
        sb.AppendLine($"            SET {setClauses}");
        sb.AppendLine($"            WHERE {whereClauses}\";");
        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            await using var cmd = new NpgsqlCommand(sql, conn);");

        foreach (var col in allParams)
        {
            var paramName = ToCamelCase(col.Name);
            if (col.IsNullable)
            {
                sb.AppendLine($"            cmd.Parameters.AddWithValue(\"{paramName}\", {paramName} ?? (object)DBNull.Value);");
            }
            else
            {
                sb.AppendLine($"            cmd.Parameters.AddWithValue(\"{paramName}\", {paramName});");
            }
        }

        sb.AppendLine();
        sb.AppendLine("            var rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);");
        sb.AppendLine("            return new Result<int, SqlError>.Ok<int, SqlError>(rows);");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine("            return new Result<int, SqlError>.Error<int, SqlError>(SqlError.FromException(ex));");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    private static void GenerateDeleteMethod(
        StringBuilder sb,
        TableConfigItem table,
        List<DatabaseColumn> columns,
        string pascalName)
    {
        var pkCols = columns.Where(c => c.IsPrimaryKey).ToList();
        if (pkCols.Count == 0) return;

        var parameters = string.Join(", ", pkCols.Select(c =>
            $"{c.CSharpType} {ToCamelCase(c.Name)}"));

        sb.AppendLine();
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Deletes a row from {table.Name} by primary key.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public static async Task<Result<int, SqlError>> Delete{pascalName}Async(");
        sb.AppendLine($"        this NpgsqlConnection conn,");
        sb.AppendLine($"        {parameters})");
        sb.AppendLine("    {");

        var whereClauses = string.Join(" AND ", pkCols.Select(c => $"{c.Name} = @{ToCamelCase(c.Name)}"));

        sb.AppendLine($"        const string sql = @\"DELETE FROM {table.Schema}.{table.Name} WHERE {whereClauses}\";");
        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            await using var cmd = new NpgsqlCommand(sql, conn);");

        foreach (var col in pkCols)
        {
            sb.AppendLine($"            cmd.Parameters.AddWithValue(\"{ToCamelCase(col.Name)}\", {ToCamelCase(col.Name)});");
        }

        sb.AppendLine();
        sb.AppendLine("            var rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);");
        sb.AppendLine("            return new Result<int, SqlError>.Ok<int, SqlError>(rows);");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine("            return new Result<int, SqlError>.Error<int, SqlError>(SqlError.FromException(ex));");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    private static void GenerateBulkInsertMethod(
        StringBuilder sb,
        TableConfigItem table,
        List<DatabaseColumn> columns,
        string pascalName)
    {
        // Get insertable columns (exclude auto-generated ones)
        var insertable = columns.Where(c => !c.IsIdentity && !c.IsComputed).ToList();
        if (insertable.Count == 0) return;

        // Build tuple type for the IEnumerable parameter
        var tupleType = string.Join(", ", insertable.Select(c =>
            $"{c.CSharpType} {ToPascalCase(c.Name)}"));

        sb.AppendLine();
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Bulk inserts rows into {table.Name} using batched multi-row VALUES.");
        sb.AppendLine($"    /// Uses ON CONFLICT DO NOTHING to skip duplicates.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"conn\">Open database connection.</param>");
        sb.AppendLine($"    /// <param name=\"records\">Records to insert as tuples.</param>");
        sb.AppendLine($"    /// <param name=\"batchSize\">Max rows per batch (default 1000).</param>");
        sb.AppendLine($"    /// <returns>Total rows inserted.</returns>");
        sb.AppendLine($"    public static async Task<Result<int, SqlError>> BulkInsert{pascalName}Async(");
        sb.AppendLine($"        this NpgsqlConnection conn,");
        sb.AppendLine($"        IEnumerable<({tupleType})> records,");
        sb.AppendLine($"        int batchSize = 1000)");
        sb.AppendLine("    {");
        sb.AppendLine("        var totalInserted = 0;");
        sb.AppendLine($"        var batch = new List<({tupleType})>(batchSize);");
        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            foreach (var record in records)");
        sb.AppendLine("            {");
        sb.AppendLine("                batch.Add(record);");
        sb.AppendLine("                if (batch.Count >= batchSize)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    var result = await ExecuteBulkInsert{pascalName}BatchAsync(conn, batch).ConfigureAwait(false);");
        sb.AppendLine("                    if (result is Result<int, SqlError>.Error<int, SqlError> err)");
        sb.AppendLine("                        return err;");
        sb.AppendLine("                    totalInserted += ((Result<int, SqlError>.Ok<int, SqlError>)result).Value;");
        sb.AppendLine("                    batch.Clear();");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (batch.Count > 0)");
        sb.AppendLine("            {");
        sb.AppendLine($"                var finalResult = await ExecuteBulkInsert{pascalName}BatchAsync(conn, batch).ConfigureAwait(false);");
        sb.AppendLine("                if (finalResult is Result<int, SqlError>.Error<int, SqlError> finalErr)");
        sb.AppendLine("                    return finalErr;");
        sb.AppendLine("                totalInserted += ((Result<int, SqlError>.Ok<int, SqlError>)finalResult).Value;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            return new Result<int, SqlError>.Ok<int, SqlError>(totalInserted);");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine("            return new Result<int, SqlError>.Error<int, SqlError>(SqlError.FromException(ex));");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate batch execution helper
        sb.AppendLine($"    private static async Task<Result<int, SqlError>> ExecuteBulkInsert{pascalName}BatchAsync(");
        sb.AppendLine($"        NpgsqlConnection conn,");
        sb.AppendLine($"        List<({tupleType})> batch)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (batch.Count == 0)");
        sb.AppendLine("            return new Result<int, SqlError>.Ok<int, SqlError>(0);");
        sb.AppendLine();

        var colNames = string.Join(", ", insertable.Select(c => c.Name));
        sb.AppendLine($"        var sql = new System.Text.StringBuilder(\"INSERT INTO {table.Schema}.{table.Name} ({colNames}) VALUES \");");
        sb.AppendLine();
        sb.AppendLine("        for (int i = 0; i < batch.Count; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (i > 0) sql.Append(\", \");");

        // Build VALUES placeholders
        var placeholders = string.Join(", ", insertable.Select((c, idx) => $"@p\" + (i * {insertable.Count} + {idx}) + \""));
        sb.AppendLine($"            sql.Append(\"({placeholders})\");");
        sb.AppendLine("        }");
        sb.AppendLine("        sql.Append(\" ON CONFLICT DO NOTHING\");");
        sb.AppendLine();
        sb.AppendLine("        await using var cmd = new NpgsqlCommand(sql.ToString(), conn);");
        sb.AppendLine();
        sb.AppendLine("        for (int i = 0; i < batch.Count; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            var rec = batch[i];");

        for (int i = 0; i < insertable.Count; i++)
        {
            var col = insertable[i];
            var propName = ToPascalCase(col.Name);
            if (col.IsNullable)
            {
                sb.AppendLine($"            cmd.Parameters.AddWithValue(\"p\" + (i * {insertable.Count} + {i}), rec.{propName} ?? (object)DBNull.Value);");
            }
            else
            {
                sb.AppendLine($"            cmd.Parameters.AddWithValue(\"p\" + (i * {insertable.Count} + {i}), rec.{propName});");
            }
        }

        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);");
        sb.AppendLine("        return new Result<int, SqlError>.Ok<int, SqlError>(rows);");
        sb.AppendLine("    }");
    }

    private static void GenerateBulkUpsertMethod(
        StringBuilder sb,
        TableConfigItem table,
        List<DatabaseColumn> columns,
        string pascalName)
    {
        // Get insertable columns (exclude auto-generated ones)
        var insertable = columns.Where(c => !c.IsIdentity && !c.IsComputed).ToList();
        var pkCols = columns.Where(c => c.IsPrimaryKey).ToList();

        if (insertable.Count == 0 || pkCols.Count == 0) return;

        // Build tuple type for the IEnumerable parameter
        var tupleType = string.Join(", ", insertable.Select(c =>
            $"{c.CSharpType} {ToPascalCase(c.Name)}"));

        sb.AppendLine();
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Bulk upserts rows into {table.Name} using batched multi-row VALUES.");
        sb.AppendLine($"    /// Uses ON CONFLICT DO UPDATE to insert or update existing rows.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    /// <param name=\"conn\">Open database connection.</param>");
        sb.AppendLine($"    /// <param name=\"records\">Records to upsert as tuples.</param>");
        sb.AppendLine($"    /// <param name=\"batchSize\">Max rows per batch (default 1000).</param>");
        sb.AppendLine($"    /// <returns>Total rows affected.</returns>");
        sb.AppendLine($"    public static async Task<Result<int, SqlError>> BulkUpsert{pascalName}Async(");
        sb.AppendLine($"        this NpgsqlConnection conn,");
        sb.AppendLine($"        IEnumerable<({tupleType})> records,");
        sb.AppendLine($"        int batchSize = 1000)");
        sb.AppendLine("    {");
        sb.AppendLine("        var totalAffected = 0;");
        sb.AppendLine($"        var batch = new List<({tupleType})>(batchSize);");
        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            foreach (var record in records)");
        sb.AppendLine("            {");
        sb.AppendLine("                batch.Add(record);");
        sb.AppendLine("                if (batch.Count >= batchSize)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    var result = await ExecuteBulkUpsert{pascalName}BatchAsync(conn, batch).ConfigureAwait(false);");
        sb.AppendLine("                    if (result is Result<int, SqlError>.Error<int, SqlError> err)");
        sb.AppendLine("                        return err;");
        sb.AppendLine("                    totalAffected += ((Result<int, SqlError>.Ok<int, SqlError>)result).Value;");
        sb.AppendLine("                    batch.Clear();");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (batch.Count > 0)");
        sb.AppendLine("            {");
        sb.AppendLine($"                var finalResult = await ExecuteBulkUpsert{pascalName}BatchAsync(conn, batch).ConfigureAwait(false);");
        sb.AppendLine("                if (finalResult is Result<int, SqlError>.Error<int, SqlError> finalErr)");
        sb.AppendLine("                    return finalErr;");
        sb.AppendLine("                totalAffected += ((Result<int, SqlError>.Ok<int, SqlError>)finalResult).Value;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            return new Result<int, SqlError>.Ok<int, SqlError>(totalAffected);");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine("            return new Result<int, SqlError>.Error<int, SqlError>(SqlError.FromException(ex));");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate batch execution helper
        sb.AppendLine($"    private static async Task<Result<int, SqlError>> ExecuteBulkUpsert{pascalName}BatchAsync(");
        sb.AppendLine($"        NpgsqlConnection conn,");
        sb.AppendLine($"        List<({tupleType})> batch)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (batch.Count == 0)");
        sb.AppendLine("            return new Result<int, SqlError>.Ok<int, SqlError>(0);");
        sb.AppendLine();

        var colNames = string.Join(", ", insertable.Select(c => c.Name));
        var pkColNames = string.Join(", ", pkCols.Select(c => c.Name));
        var updateCols = insertable.Where(c => !c.IsPrimaryKey).ToList();
        var updateSet = string.Join(", ", updateCols.Select(c => $"{c.Name} = EXCLUDED.{c.Name}"));

        sb.AppendLine($"        var sql = new System.Text.StringBuilder(\"INSERT INTO {table.Schema}.{table.Name} ({colNames}) VALUES \");");
        sb.AppendLine();
        sb.AppendLine("        for (int i = 0; i < batch.Count; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (i > 0) sql.Append(\", \");");

        // Build VALUES placeholders
        var placeholders = string.Join(", ", insertable.Select((c, idx) => $"@p\" + (i * {insertable.Count} + {idx}) + \""));
        sb.AppendLine($"            sql.Append(\"({placeholders})\");");
        sb.AppendLine("        }");

        // Add ON CONFLICT DO UPDATE clause
        if (updateCols.Count > 0)
        {
            sb.AppendLine($"        sql.Append(\" ON CONFLICT ({pkColNames}) DO UPDATE SET {updateSet}\");");
        }
        else
        {
            // If all columns are PKs, just do nothing on conflict
            sb.AppendLine($"        sql.Append(\" ON CONFLICT ({pkColNames}) DO NOTHING\");");
        }

        sb.AppendLine();
        sb.AppendLine("        await using var cmd = new NpgsqlCommand(sql.ToString(), conn);");
        sb.AppendLine();
        sb.AppendLine("        for (int i = 0; i < batch.Count; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            var rec = batch[i];");

        for (int i = 0; i < insertable.Count; i++)
        {
            var col = insertable[i];
            var propName = ToPascalCase(col.Name);
            if (col.IsNullable)
            {
                sb.AppendLine($"            cmd.Parameters.AddWithValue(\"p\" + (i * {insertable.Count} + {i}), rec.{propName} ?? (object)DBNull.Value);");
            }
            else
            {
                sb.AppendLine($"            cmd.Parameters.AddWithValue(\"p\" + (i * {insertable.Count} + {i}), rec.{propName});");
            }
        }

        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);");
        sb.AppendLine("        return new Result<int, SqlError>.Ok<int, SqlError>(rows);");
        sb.AppendLine("    }");
    }

    private static List<string> ExtractParameters(string sql)
    {
        var parameters = new List<string>();
        var i = 0;
        while (i < sql.Length)
        {
            if (sql[i] == '@')
            {
                var start = i + 1;
                while (
                    start < sql.Length && (char.IsLetterOrDigit(sql[start]) || sql[start] == '_')
                )
                {
                    start++;
                }
                if (start > i + 1)
                {
                    var paramName = sql[(i + 1)..start];
                    if (!parameters.Contains(paramName, StringComparer.OrdinalIgnoreCase))
                    {
                        parameters.Add(paramName);
                    }
                }
                i = start;
            }
            else
            {
                i++;
            }
        }
        return parameters;
    }

    private static async Task<
        Result<IReadOnlyList<DatabaseColumn>, SqlError>
    > GetColumnMetadataAsync(string connectionString, string sql, List<string> parameters)
    {
        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync().ConfigureAwait(false);

            // Replace @params with NULL for metadata query
            var metaSql = sql;
            foreach (var param in parameters)
            {
                metaSql = metaSql.Replace($"@{param}", "NULL", StringComparison.OrdinalIgnoreCase);
            }

            // Wrap in a CTE to get metadata without executing
            var wrappedSql = $"SELECT * FROM ({metaSql}) AS _meta WHERE 1=0";

            await using var cmd = new NpgsqlCommand(wrappedSql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(
                    System.Data.CommandBehavior.SchemaOnly
                )
                .ConfigureAwait(false);

            var schema = reader.GetColumnSchema();
            var columns = new List<DatabaseColumn>();

            foreach (var col in schema)
            {
                var dbType = col.DataTypeName ?? "text";
                var csharpType = MapPostgresTypeToCSharp(dbType, col.AllowDBNull ?? true);

                columns.Add(
                    new DatabaseColumn
                    {
                        Name = col.ColumnName,
                        SqlType = dbType,
                        CSharpType = csharpType,
                        IsNullable = col.AllowDBNull ?? true,
                        IsPrimaryKey = col.IsKey ?? false,
                        IsIdentity = col.IsIdentity ?? false,
                        IsComputed = col.IsReadOnly ?? false,
                    }
                );
            }

            return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Ok<
                IReadOnlyList<DatabaseColumn>,
                SqlError
            >(columns.AsReadOnly());
        }
        catch (Exception ex)
        {
            return new Result<IReadOnlyList<DatabaseColumn>, SqlError>.Error<
                IReadOnlyList<DatabaseColumn>,
                SqlError
            >(SqlError.FromException(ex));
        }
    }

    private static string MapPostgresTypeToCSharp(string pgType, bool isNullable)
    {
        var baseType = pgType.ToLowerInvariant() switch
        {
            "uuid" => "Guid",
            "boolean" or "bool" => "bool",
            "smallint" or "int2" => "short",
            "integer" or "int4" or "int" => "int",
            "bigint" or "int8" => "long",
            "real" or "float4" => "float",
            "double precision" or "float8" => "double",
            "numeric" or "decimal" or "money" => "decimal",
            "date" => "DateOnly",
            "time" or "time without time zone" => "TimeOnly",
            "time with time zone" or "timetz" => "TimeOnly",
            "timestamp" or "timestamp without time zone" => "DateTime",
            "timestamp with time zone" or "timestamptz" => "DateTimeOffset",
            "interval" => "TimeSpan",
            "bytea" => "byte[]",
            "text" or "varchar" or "character varying" or "char" or "character" or "name" =>
                "string",
            "json" or "jsonb" => "string",
            var t when t.EndsWith("[]", StringComparison.Ordinal) => "string[]",
            _ => "string",
        };

        // Add nullable suffix for nullable types (including strings but not arrays)
        if (isNullable && !baseType.EndsWith("[]", StringComparison.Ordinal))
        {
            return baseType + "?";
        }

        return baseType;
    }

    private static Result<string, SqlError> GenerateCode(
        string fileName,
        string sql,
        IReadOnlyList<DatabaseColumn> columns,
        List<string> parameters
    )
    {
        var sb = new StringBuilder();
        var recordName = fileName;

        // Header with all using statements (including type aliases) at the top
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Immutable;");
        sb.AppendLine("using Npgsql;");
        sb.AppendLine("using Outcome;");
        sb.AppendLine("using Selecta;");
        sb.AppendLine();
        // Result type aliases must come after standard usings but before any type definitions
        // Use fully qualified names since type aliases don't use namespace context
        sb.AppendLine(
            $"using {fileName}Result = Outcome.Result<System.Collections.Immutable.ImmutableList<{recordName}>, Selecta.SqlError>;"
        );
        sb.AppendLine(
            $"using {fileName}Ok = Outcome.Result<System.Collections.Immutable.ImmutableList<{recordName}>, Selecta.SqlError>.Ok<System.Collections.Immutable.ImmutableList<{recordName}>, Selecta.SqlError>;"
        );
        sb.AppendLine(
            $"using {fileName}Error = Outcome.Result<System.Collections.Immutable.ImmutableList<{recordName}>, Selecta.SqlError>.Error<System.Collections.Immutable.ImmutableList<{recordName}>, Selecta.SqlError>;"
        );
        sb.AppendLine();

        // Generate record type
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Generated record for {fileName} query.");
        sb.AppendLine($"/// </summary>");
        sb.Append($"public sealed record {recordName}(");

        var first = true;
        foreach (var col in columns)
        {
            if (!first)
                sb.Append(", ");
            first = false;

            var propName = ToPascalCase(col.Name);
            sb.Append($"{col.CSharpType} {propName}");
        }
        sb.AppendLine(");");
        sb.AppendLine();

        // Generate extension method
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Extension methods for {fileName} query.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public static class {fileName}Extensions");
        sb.AppendLine("{");

        // SQL constant
        sb.AppendLine($"    private const string Sql = @\"");
        sb.AppendLine(sql.Replace("\"", "\"\""));
        sb.AppendLine("\";");
        sb.AppendLine();

        // Async method
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Executes the {fileName} query.");
        sb.AppendLine($"    /// </summary>");
        sb.Append(
            $"    public static async Task<{fileName}Result> {fileName}Async(this NpgsqlConnection conn"
        );

        foreach (var param in parameters)
        {
            var paramType = InferParameterType(param);
            sb.Append($", {paramType} {ToCamelCase(param)}");
        }
        sb.AppendLine(")");
        sb.AppendLine("    {");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine($"            var results = ImmutableList.CreateBuilder<{recordName}>();");
        sb.AppendLine("            await using var cmd = new NpgsqlCommand(Sql, conn);");

        foreach (var param in parameters)
        {
            sb.AppendLine(
                $"            cmd.Parameters.AddWithValue(\"{param}\", {ToCamelCase(param)});"
            );
        }

        sb.AppendLine();
        sb.AppendLine(
            "            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);"
        );
        sb.AppendLine("            while (await reader.ReadAsync().ConfigureAwait(false))");
        sb.AppendLine("            {");
        sb.AppendLine($"                results.Add(Read{recordName}(reader));");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            return new {fileName}Ok(results.ToImmutable());");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine($"            return new {fileName}Error(SqlError.FromException(ex));");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Reader method
        sb.AppendLine(
            $"    private static {recordName} Read{recordName}(NpgsqlDataReader reader) =>"
        );
        sb.Append($"        new(");

        first = true;
        var ordinal = 0;
        foreach (var col in columns)
        {
            if (!first)
                sb.Append(", ");
            first = false;

            var propName = ToPascalCase(col.Name);
            var readExpr = GetReaderExpression(col, ordinal);
            sb.Append($"{propName}: {readExpr}");
            ordinal++;
        }
        sb.AppendLine(");");

        sb.AppendLine("}");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    private static string GetReaderExpression(DatabaseColumn col, int ordinal)
    {
        var nullCheck = col.IsNullable ? $"reader.IsDBNull({ordinal}) ? null : " : "";

        return col.CSharpType.TrimEnd('?') switch
        {
            "Guid" => $"{nullCheck}reader.GetGuid({ordinal})",
            "bool" => $"{nullCheck}reader.GetBoolean({ordinal})",
            "short" => $"{nullCheck}reader.GetInt16({ordinal})",
            "int" => $"{nullCheck}reader.GetInt32({ordinal})",
            "long" => $"{nullCheck}reader.GetInt64({ordinal})",
            "float" => $"{nullCheck}reader.GetFloat({ordinal})",
            "double" => $"{nullCheck}reader.GetDouble({ordinal})",
            "decimal" => $"{nullCheck}reader.GetDecimal({ordinal})",
            "DateOnly" => $"{nullCheck}DateOnly.FromDateTime(reader.GetDateTime({ordinal}))",
            "TimeOnly" => $"{nullCheck}TimeOnly.FromTimeSpan(reader.GetTimeSpan({ordinal}))",
            "DateTime" => $"{nullCheck}reader.GetDateTime({ordinal})",
            "DateTimeOffset" => $"{nullCheck}reader.GetFieldValue<DateTimeOffset>({ordinal})",
            "TimeSpan" => $"{nullCheck}reader.GetTimeSpan({ordinal})",
            "byte[]" => $"{nullCheck}reader.GetFieldValue<byte[]>({ordinal})",
            "string[]" => $"reader.GetFieldValue<string[]>({ordinal})",
            _ => $"{nullCheck}reader.GetString({ordinal})",
        };
    }

    private static string InferParameterType(string paramName)
    {
        var lower = paramName.ToLowerInvariant();
        if (lower.EndsWith("id", StringComparison.Ordinal))
            return "Guid";
        if (lower.Contains("limit") || lower.Contains("offset") || lower.Contains("count"))
            return "int";
        return "object";
    }

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var parts = name.Split('_');
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.Length > 0)
            {
                sb.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1)
                    sb.Append(part[1..].ToLowerInvariant());
            }
        }
        return sb.ToString();
    }

    private static string ToCamelCase(string name)
    {
        var pascal = ToPascalCase(name);
        if (string.IsNullOrEmpty(pascal))
            return pascal;
        return char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }

    private static string EscapeForPreprocessor(string message)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;
        var oneLine = message.Replace('\r', ' ').Replace('\n', ' ');
        return oneLine.Replace('"', '\'');
    }
}

/// <summary>
/// Configuration for PostgreSQL DataProvider code generation.
/// </summary>
internal sealed record PostgresDataProviderConfig
{
    /// <summary>
    /// The connection string to the PostgreSQL database.
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// List of query configurations.
    /// </summary>
    public List<QueryConfig>? Queries { get; init; }

    /// <summary>
    /// List of table configurations for CRUD generation.
    /// </summary>
    public List<TableConfigItem>? Tables { get; init; }
}

/// <summary>
/// Query configuration.
/// </summary>
internal sealed record QueryConfig
{
    /// <summary>
    /// Query name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Path to SQL file.
    /// </summary>
    public string SqlFile { get; init; } = string.Empty;
}

/// <summary>
/// Table configuration for CRUD generation.
/// </summary>
internal sealed record TableConfigItem
{
    /// <summary>
    /// Schema name.
    /// </summary>
    public string Schema { get; init; } = "public";

    /// <summary>
    /// Table name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Generate INSERT method.
    /// </summary>
    public bool GenerateInsert { get; init; }

    /// <summary>
    /// Generate UPDATE method.
    /// </summary>
    public bool GenerateUpdate { get; init; }

    /// <summary>
    /// Generate DELETE method.
    /// </summary>
    public bool GenerateDelete { get; init; }

    /// <summary>
    /// Generate bulk INSERT method for batch operations.
    /// </summary>
    public bool GenerateBulkInsert { get; init; }

    /// <summary>
    /// Generate bulk UPSERT method for batch insert-or-update operations.
    /// </summary>
    public bool GenerateBulkUpsert { get; init; }

    /// <summary>
    /// Columns to exclude from generation.
    /// </summary>
    public IReadOnlyList<string> ExcludeColumns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Primary key columns.
    /// </summary>
    public IReadOnlyList<string> PrimaryKeyColumns { get; init; } = Array.Empty<string>();
}
