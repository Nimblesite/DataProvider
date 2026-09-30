using System.Globalization;
using System.Text;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Core.CodeGeneration;

/// <summary>
/// Static methods for generating data access extension methods
/// </summary>
public static partial class DataAccessGenerator
{
    /// <summary>
    /// C# reserved keywords that need to be escaped when used as parameter names
    /// </summary>
    private static readonly HashSet<string> CSharpReservedKeywords = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "abstract",
        "as",
        "base",
        "bool",
        "break",
        "byte",
        "case",
        "catch",
        "char",
        "checked",
        "class",
        "const",
        "continue",
        "decimal",
        "default",
        "delegate",
        "do",
        "double",
        "else",
        "enum",
        "event",
        "explicit",
        "extern",
        "false",
        "finally",
        "fixed",
        "float",
        "for",
        "foreach",
        "goto",
        "if",
        "implicit",
        "in",
        "int",
        "interface",
        "internal",
        "is",
        "lock",
        "long",
        "namespace",
        "new",
        "null",
        "object",
        "operator",
        "out",
        "override",
        "params",
        "private",
        "protected",
        "public",
        "readonly",
        "ref",
        "return",
        "sbyte",
        "sealed",
        "short",
        "sizeof",
        "stackalloc",
        "static",
        "string",
        "struct",
        "switch",
        "this",
        "throw",
        "true",
        "try",
        "typeof",
        "uint",
        "ulong",
        "unchecked",
        "unsafe",
        "ushort",
        "using",
        "virtual",
        "void",
        "volatile",
        "while",
    };

    /// <summary>
    /// Escapes C# reserved keywords by prefixing with @
    /// </summary>
    /// <param name="identifier">The identifier to potentially escape</param>
    /// <returns>The escaped identifier if it's a reserved keyword, otherwise the original</returns>
    private static string EscapeReservedKeyword(string identifier)
    {
        var upperIdentifier = identifier.ToLowerInvariant();
        return CSharpReservedKeywords.Contains(upperIdentifier)
            ? $"@{upperIdentifier}"
            : upperIdentifier;
    }

    /// <summary>
    /// Generates parameter list string for method signatures
    /// </summary>
    /// <param name="parameters">SQL parameters</param>
    /// <returns>Formatted parameter list</returns>
    public static string GenerateParameterList(IReadOnlyList<ParameterInfo> parameters)
    {
        if (parameters == null || parameters.Count == 0)
            return "";

        return string.Join(", ", parameters.Select(p => $"object {p.Name}"));
    }

    /// <summary>
    /// Appends AddWithValue parameter-binding lines for the given columns.
    /// Nullable columns coalesce to DBNull.Value; non-nullable bind directly.
    /// </summary>
    private static void AppendParameterBindings(
        StringBuilder sb,
        IReadOnlyList<DatabaseColumn> columns
    )
    {
        foreach (var column in columns)
        {
            var escaped = EscapeReservedKeyword(column.Name);
            var value = column.IsNullable ? $"{escaped} ?? (object)DBNull.Value" : escaped;
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                command.Parameters.AddWithValue(\"@{column.Name}\", {value});"
            );
        }
    }

    /// <summary>
    /// Generates a data access extension method for querying
    /// </summary>
    /// <param name="className">Extension class name</param>
    /// <param name="methodName">Method name</param>
    /// <param name="returnTypeName">Return type name</param>
    /// <param name="sql">SQL query</param>
    /// <param name="parameters">SQL parameters</param>
    /// <param name="columns">Database columns</param>
    /// <param name="connectionType">Database connection type (e.g., SqliteConnection)</param>
    /// <returns>Generated extension method code</returns>
    public static Result<string, SqlError> GenerateQueryMethod(
        string className,
        string methodName,
        string returnTypeName,
        string sql,
        IReadOnlyList<ParameterInfo> parameters,
        IReadOnlyList<DatabaseColumn> columns,
        string connectionType = "SqliteConnection"
    )
    {
        if (string.IsNullOrWhiteSpace(className))
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("className cannot be null or empty")
            );

        if (string.IsNullOrWhiteSpace(methodName))
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("methodName cannot be null or empty")
            );

        if (string.IsNullOrWhiteSpace(returnTypeName))
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("returnTypeName cannot be null or empty")
            );

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("sql cannot be null or empty")
            );

        if (columns == null || columns.Count == 0)
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("columns cannot be null or empty")
            );

        var parameterList = GenerateParameterList(parameters);
        var sb = new StringBuilder();

        // Generate extension class
        sb.AppendLine("/// <summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"/// Extension methods for '{methodName}'.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"public static partial class {className}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Executes '{methodName}.sql' and maps results."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// <param name=\"connection\">Open {connectionType} connection.</param>"
        );

        if (parameters != null)
        {
            foreach (var p in parameters)
            {
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"    /// <param name=\"{p.Name}\">Query parameter.</param>"
                );
            }
        }

        sb.AppendLine("    /// <returns>Result of records or SQL error.</returns>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public static async Task<Result<ImmutableList<{returnTypeName}>, SqlError>> {methodName}Async(this {connectionType} connection{(string.IsNullOrEmpty(parameterList) ? "" : ", " + parameterList)})"
        );
        sb.AppendLine("    {");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        const string sql = @\"{sql.Replace("\"", "\"\"", StringComparison.Ordinal)}\";"
        );
        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            var results = ImmutableList.CreateBuilder<{returnTypeName}>();"
        );
        sb.AppendLine();

        var commandType = connectionType.Replace("Connection", "Command", StringComparison.Ordinal);
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            using (var command = new {commandType}(sql, connection))"
        );
        sb.AppendLine("            {");

        // Add parameters - for Npgsql, use AddWithValue for non-null (type inferred from value)
        // and typed NpgsqlParameter for null/DBNull (type matched from result columns to avoid 42P08/42883)
        if (parameters != null)
        {
            foreach (var parameter in parameters)
            {
                if (connectionType.Contains("Npgsql", StringComparison.Ordinal))
                {
                    var npgsqlType = ResolveNpgsqlDbTypeFromColumns(parameter.Name, columns);
                    sb.AppendLine(
                        CultureInfo.InvariantCulture,
                        $"                if ({parameter.Name} is not null and not DBNull)"
                    );
                    sb.AppendLine(
                        CultureInfo.InvariantCulture,
                        $"                    command.Parameters.AddWithValue(\"@{parameter.Name}\", {parameter.Name});"
                    );
                    sb.AppendLine("                else");
                    sb.AppendLine(
                        CultureInfo.InvariantCulture,
                        $"                    command.Parameters.Add(new NpgsqlParameter(\"@{parameter.Name}\", NpgsqlTypes.NpgsqlDbType.{npgsqlType}) {{ Value = DBNull.Value }});"
                    );
                }
                else
                {
                    sb.AppendLine(
                        CultureInfo.InvariantCulture,
                        $"                command.Parameters.AddWithValue(\"@{parameter.Name}\", {parameter.Name} ?? (object)DBNull.Value);"
                    );
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine(
            "                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))"
        );
        sb.AppendLine("                {");
        sb.AppendLine("                    while (await reader.ReadAsync().ConfigureAwait(false))");
        sb.AppendLine("                    {");

        // Generate record constructor using column metadata
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"                        var item = new {returnTypeName}("
        );

        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var isLast = i == columns.Count - 1;
            var comma = isLast ? "" : ",";

            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"                            reader.IsDBNull({i}) ? {(column.IsNullable ? "null" : $"default({column.CSharpType})")} : reader.GetFieldValue<{column.CSharpType}>({i}){comma}"
            );
        }

        sb.AppendLine("                        );");
        sb.AppendLine("                        results.Add(item);");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            return new Result<ImmutableList<{returnTypeName}>, SqlError>.Ok<ImmutableList<{returnTypeName}>, SqlError>(results.ToImmutable());"
        );
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            return new Result<ImmutableList<{returnTypeName}>, SqlError>.Error<ImmutableList<{returnTypeName}>, SqlError>(new SqlError(\"Database error\", ex));"
        );
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    /// <summary>
    /// Generates an INSERT method for a database table
    /// </summary>
    /// <param name="table">Database table metadata</param>
    /// <param name="connectionType">Database connection type</param>
    /// <returns>Generated INSERT method code</returns>
    public static Result<string, SqlError> GenerateInsertMethod(
        DatabaseTable table,
        string connectionType = "SqliteConnection"
    )
    {
        if (table == null)
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("table cannot be null")
            );

        var insertableColumns = table.InsertableColumns;
        if (insertableColumns.Count == 0)
            return new Result<string, SqlError>.Ok<string, SqlError>("");

        var sb = new StringBuilder();
        var parameterList = string.Join(
            ", ",
            insertableColumns.Select(c =>
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{c.CSharpType}{(c.IsNullable && c.CSharpType == "string" ? "?" : "")} {EscapeReservedKeyword(c.Name)}"
                )
            )
        );

        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Inserts a new row into the {table.Name} table."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public static async Task<Result<int, SqlError>> Insert{table.Name}Async(this IDbTransaction transaction, {parameterList})"
        );
        sb.AppendLine("    {");

        // Generate INSERT SQL (no last_insert_rowid - all PKs are UUIDs)
        // All identifiers are lowercase - no quoting needed for cross-platform compatibility
        var columnNames = string.Join(", ", insertableColumns.Select(c => c.Name));
        var parameterNames = string.Join(", ", insertableColumns.Select(c => $"@{c.Name}"));

        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        const string sql = \"INSERT INTO {table.Name} ({columnNames}) VALUES ({parameterNames})\";"
        );
        sb.AppendLine();
        sb.AppendLine("        if (transaction.Connection is null)");
        sb.AppendLine(
            "            return new Result<int, SqlError>.Error<int, SqlError>(new SqlError(\"Transaction has no connection\"));"
        );
        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");

        var commandType = connectionType.Replace("Connection", "Command", StringComparison.Ordinal);
        var transactionType = connectionType.Replace(
            "Connection",
            "Transaction",
            StringComparison.Ordinal
        );
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            using (var command = new {commandType}(sql, ({connectionType})transaction.Connection!, ({transactionType})transaction))"
        );
        sb.AppendLine("            {");

        // Add parameters
        AppendParameterBindings(sb, insertableColumns);

        sb.AppendLine();
        sb.AppendLine(
            "                var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);"
        );
        sb.AppendLine(
            "                return new Result<int, SqlError>.Ok<int, SqlError>(rowsAffected);"
        );
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine(
            "            return new Result<int, SqlError>.Error<int, SqlError>(new SqlError(\"Insert failed\", ex));"
        );
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    /// <summary>
    /// Generates a non-query extension method for UPDATE/DELETE/INSERT SQL statements.
    /// Returns the number of rows affected.
    /// </summary>
    /// <param name="className">Extension class name</param>
    /// <param name="methodName">Method name</param>
    /// <param name="sql">SQL statement</param>
    /// <param name="parameters">SQL parameters</param>
    /// <param name="connectionType">Database connection type (e.g., SqliteConnection)</param>
    /// <returns>Generated extension method code</returns>
    public static Result<string, SqlError> GenerateNonQueryMethod(
        string className,
        string methodName,
        string sql,
        IReadOnlyList<ParameterInfo> parameters,
        string connectionType = "SqliteConnection"
    )
    {
        if (string.IsNullOrWhiteSpace(className))
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("className cannot be null or empty")
            );

        if (string.IsNullOrWhiteSpace(methodName))
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("methodName cannot be null or empty")
            );

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("sql cannot be null or empty")
            );

        var parameterList = GenerateParameterList(parameters);
        var sb = new StringBuilder();

        // Generate extension class
        sb.AppendLine("/// <summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"/// Extension methods for '{methodName}'.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"public static partial class {className}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Executes '{methodName}.sql' and returns rows affected."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// <param name=\"connection\">Open {connectionType} connection.</param>"
        );

        if (parameters != null)
        {
            foreach (var p in parameters)
            {
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"    /// <param name=\"{p.Name}\">Query parameter.</param>"
                );
            }
        }

        sb.AppendLine("    /// <returns>Result with rows affected or SQL error.</returns>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public static async Task<Result<int, SqlError>> {methodName}Async(this {connectionType} connection{(string.IsNullOrEmpty(parameterList) ? "" : ", " + parameterList)})"
        );
        sb.AppendLine("    {");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        const string sql = @\"{sql.Replace("\"", "\"\"", StringComparison.Ordinal)}\";"
        );
        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");

        var commandType = connectionType.Replace("Connection", "Command", StringComparison.Ordinal);
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            using (var command = new {commandType}(sql, connection))"
        );
        sb.AppendLine("            {");

        // Add parameters
        if (parameters != null)
        {
            foreach (var parameter in parameters)
            {
                sb.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"                command.Parameters.AddWithValue(\"@{parameter.Name}\", {parameter.Name} ?? (object)DBNull.Value);"
                );
            }
        }

        sb.AppendLine();
        sb.AppendLine(
            "                var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);"
        );
        sb.AppendLine(
            "                return new Result<int, SqlError>.Ok<int, SqlError>(rowsAffected);"
        );
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine(
            "            return new Result<int, SqlError>.Error<int, SqlError>(new SqlError(\"Database error\", ex));"
        );
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    /// <summary>
    /// Generates an UPDATE method for a database table
    /// </summary>
    /// <param name="table">Database table metadata</param>
    /// <param name="connectionType">Database connection type</param>
    /// <returns>Generated UPDATE method code</returns>
    public static Result<string, SqlError> GenerateUpdateMethod(
        DatabaseTable table,
        string connectionType = "SqliteConnection"
    )
    {
        if (table == null)
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("table cannot be null")
            );

        var updateableColumns = table.UpdateableColumns;
        var primaryKeyColumns = table.PrimaryKeyColumns;

        if (updateableColumns.Count == 0 || primaryKeyColumns.Count == 0)
            return new Result<string, SqlError>.Ok<string, SqlError>("");

        var sb = new StringBuilder();
        var allColumns = primaryKeyColumns.Concat(updateableColumns).ToList();
        var parameterList = string.Join(
            ", ",
            allColumns.Select(c =>
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{c.CSharpType} {EscapeReservedKeyword(c.Name)}"
                )
            )
        );

        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Updates a row in the {table.Name} table."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public static async Task<Result<int, SqlError>> Update{table.Name}Async(this IDbTransaction transaction, {parameterList})"
        );
        sb.AppendLine("    {");

        // Generate UPDATE SQL - all identifiers lowercase, no quoting needed
        var setClause = string.Join(", ", updateableColumns.Select(c => $"{c.Name} = @{c.Name}"));
        var whereClause = string.Join(
            " AND ",
            primaryKeyColumns.Select(c => $"{c.Name} = @{c.Name}")
        );

        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        const string sql = \"UPDATE {table.Name} SET {setClause} WHERE {whereClause}\";"
        );
        sb.AppendLine();
        sb.AppendLine("        if (transaction.Connection is null)");
        sb.AppendLine(
            "            return new Result<int, SqlError>.Error<int, SqlError>(new SqlError(\"Transaction has no connection\"));"
        );
        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");

        var commandType = connectionType.Replace("Connection", "Command", StringComparison.Ordinal);
        var transactionType = connectionType.Replace(
            "Connection",
            "Transaction",
            StringComparison.Ordinal
        );
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            using (var command = new {commandType}(sql, ({connectionType})transaction.Connection!, ({transactionType})transaction))"
        );
        sb.AppendLine("            {");

        // Add parameters (nullable types use null-coalescing to DBNull.Value)
        AppendParameterBindings(sb, allColumns);

        sb.AppendLine();
        sb.AppendLine(
            "                var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);"
        );
        sb.AppendLine(
            "                return new Result<int, SqlError>.Ok<int, SqlError>(rowsAffected);"
        );
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine(
            "            return new Result<int, SqlError>.Error<int, SqlError>(new SqlError(\"Update failed\", ex));"
        );
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    /// <summary>
    /// Generates a bulk INSERT method for a database table.
    /// Uses multi-row VALUES syntax for database-independent bulk inserts.
    /// </summary>
    /// <param name="table">Database table metadata</param>
    /// <param name="batchSize">Maximum rows per batch (default 1000)</param>
    /// <param name="connectionType">Database connection type</param>
    /// <returns>Generated bulk INSERT method code</returns>
    public static Result<string, SqlError> GenerateBulkInsertMethod(
        DatabaseTable table,
        int batchSize = 1000,
        string connectionType = "SqliteConnection"
    )
    {
        if (table == null)
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("table cannot be null")
            );

        var insertableColumns = table.InsertableColumns;
        if (insertableColumns.Count == 0)
            return new Result<string, SqlError>.Ok<string, SqlError>("");

        var sb = new StringBuilder();
        var tupleType = string.Join(
            ", ",
            insertableColumns.Select(c =>
                string.Create(CultureInfo.InvariantCulture, $"{c.CSharpType} {c.Name}")
            )
        );

        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Bulk inserts rows into the {table.Name} table using batched multi-row VALUES."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"transaction\">Active database transaction.</param>");
        sb.AppendLine("    /// <param name=\"records\">Records to insert.</param>");
        sb.AppendLine("    /// <returns>Result with total rows inserted or SQL error.</returns>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public static async Task<Result<int, SqlError>> BulkInsert{table.Name}Async(this IDbTransaction transaction, IEnumerable<({tupleType})> records)"
        );
        sb.AppendLine("    {");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        const int batchSize = {batchSize};");
        sb.AppendLine("        var totalInserted = 0;");
        sb.AppendLine("        var batch = new List<(" + tupleType + ")>(batchSize);");
        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            foreach (var record in records)");
        sb.AppendLine("            {");
        sb.AppendLine("                batch.Add(record);");
        sb.AppendLine("                if (batch.Count >= batchSize)");
        sb.AppendLine("                {");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"                    var result = await ExecuteBulkInsert{table.Name}BatchAsync(transaction, batch).ConfigureAwait(false);"
        );
        sb.AppendLine(
            "                    if (result is Result<int, SqlError>.Error<int, SqlError> err)"
        );
        sb.AppendLine("                        return err;");
        sb.AppendLine(
            "                    totalInserted += ((Result<int, SqlError>.Ok<int, SqlError>)result).Value;"
        );
        sb.AppendLine("                    batch.Clear();");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (batch.Count > 0)");
        sb.AppendLine("            {");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"                var finalResult = await ExecuteBulkInsert{table.Name}BatchAsync(transaction, batch).ConfigureAwait(false);"
        );
        sb.AppendLine(
            "                if (finalResult is Result<int, SqlError>.Error<int, SqlError> finalErr)"
        );
        sb.AppendLine("                    return finalErr;");
        sb.AppendLine(
            "                totalInserted += ((Result<int, SqlError>.Ok<int, SqlError>)finalResult).Value;"
        );
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine(
            "            return new Result<int, SqlError>.Ok<int, SqlError>(totalInserted);"
        );
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine(
            "            return new Result<int, SqlError>.Error<int, SqlError>(new SqlError(\"Bulk insert failed\", ex));"
        );
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate the batch execution helper method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Executes a single batch of bulk inserts for {table.Name}."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    private static async Task<Result<int, SqlError>> ExecuteBulkInsert{table.Name}BatchAsync(IDbTransaction transaction, List<({tupleType})> batch)"
        );
        sb.AppendLine("    {");
        sb.AppendLine("        if (batch.Count == 0)");
        sb.AppendLine("            return new Result<int, SqlError>.Ok<int, SqlError>(0);");
        sb.AppendLine();
        sb.AppendLine("        if (transaction.Connection is null)");
        sb.AppendLine(
            "            return new Result<int, SqlError>.Error<int, SqlError>(new SqlError(\"Transaction has no connection\"));"
        );
        sb.AppendLine();

        // Build the SQL with placeholders - all identifiers lowercase, no quoting
        var columnNames = string.Join(", ", insertableColumns.Select(c => c.Name));
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        var sql = new StringBuilder(\"INSERT INTO {table.Name} ({columnNames}) VALUES \");"
        );
        sb.AppendLine("        var parameters = new List<object?>();");
        sb.AppendLine();
        sb.AppendLine("        for (int i = 0; i < batch.Count; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (i > 0) sql.Append(\", \");");

        // Build VALUES clause with parameter placeholders
        var paramPlaceholders = string.Join(
            ", ",
            insertableColumns.Select(
                (c, idx) => $"@p\" + (i * {insertableColumns.Count} + {idx}) + \""
            )
        );
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            sql.Append(\"({paramPlaceholders})\");"
        );
        sb.AppendLine("            var rec = batch[i];");

        // Add parameters from tuple
        for (int i = 0; i < insertableColumns.Count; i++)
        {
            var col = insertableColumns[i];
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"            parameters.Add(rec.{col.Name});"
            );
        }
        sb.AppendLine("        }");
        sb.AppendLine();

        var commandType = connectionType.Replace("Connection", "Command", StringComparison.Ordinal);
        var transactionType = connectionType.Replace(
            "Connection",
            "Transaction",
            StringComparison.Ordinal
        );
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        using (var command = new {commandType}(sql.ToString(), ({connectionType})transaction.Connection!, ({transactionType})transaction))"
        );
        sb.AppendLine("        {");
        sb.AppendLine("            for (int i = 0; i < parameters.Count; i++)");
        sb.AppendLine("            {");
        sb.AppendLine(
            "                command.Parameters.AddWithValue(\"@p\" + i, parameters[i] ?? (object)DBNull.Value);"
        );
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine(
            "            var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);"
        );
        sb.AppendLine(
            "            return new Result<int, SqlError>.Ok<int, SqlError>(rowsAffected);"
        );
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    /// <summary>
    /// Generates a bulk UPSERT method for a database table.
    /// Uses INSERT ... ON CONFLICT DO UPDATE for PostgreSQL, REPLACE INTO for SQLite.
    /// </summary>
    /// <param name="table">Database table metadata</param>
    /// <param name="databaseType">Target database type (Postgres or SQLite)</param>
    /// <param name="batchSize">Maximum rows per batch (default 1000)</param>
    /// <param name="connectionType">Database connection type</param>
    /// <returns>Generated bulk UPSERT method code</returns>
    public static Result<string, SqlError> GenerateBulkUpsertMethod(
        DatabaseTable table,
        string databaseType = "SQLite",
        int batchSize = 1000,
        string connectionType = "SqliteConnection"
    )
    {
        if (table == null)
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("table cannot be null")
            );

        var insertableColumns = table.InsertableColumns;
        var primaryKeyColumns = table.PrimaryKeyColumns;

        if (insertableColumns.Count == 0 || primaryKeyColumns.Count == 0)
            return new Result<string, SqlError>.Ok<string, SqlError>("");

        var sb = new StringBuilder();
        var allColumns = primaryKeyColumns
            .Concat(insertableColumns.Where(c => !primaryKeyColumns.Any(pk => pk.Name == c.Name)))
            .ToList();
        var tupleType = string.Join(
            ", ",
            allColumns.Select(c =>
                string.Create(CultureInfo.InvariantCulture, $"{c.CSharpType} {c.Name}")
            )
        );

        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Bulk upserts rows into the {table.Name} table (insert or update on conflict)."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"transaction\">Active database transaction.</param>");
        sb.AppendLine("    /// <param name=\"records\">Records to upsert.</param>");
        sb.AppendLine("    /// <returns>Result with total rows affected or SQL error.</returns>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public static async Task<Result<int, SqlError>> BulkUpsert{table.Name}Async(this IDbTransaction transaction, IEnumerable<({tupleType})> records)"
        );
        sb.AppendLine("    {");
        sb.AppendLine(CultureInfo.InvariantCulture, $"        const int batchSize = {batchSize};");
        sb.AppendLine("        var totalAffected = 0;");
        sb.AppendLine("        var batch = new List<(" + tupleType + ")>(batchSize);");
        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            foreach (var record in records)");
        sb.AppendLine("            {");
        sb.AppendLine("                batch.Add(record);");
        sb.AppendLine("                if (batch.Count >= batchSize)");
        sb.AppendLine("                {");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"                    var result = await ExecuteBulkUpsert{table.Name}BatchAsync(transaction, batch).ConfigureAwait(false);"
        );
        sb.AppendLine(
            "                    if (result is Result<int, SqlError>.Error<int, SqlError> err)"
        );
        sb.AppendLine("                        return err;");
        sb.AppendLine(
            "                    totalAffected += ((Result<int, SqlError>.Ok<int, SqlError>)result).Value;"
        );
        sb.AppendLine("                    batch.Clear();");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (batch.Count > 0)");
        sb.AppendLine("            {");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"                var finalResult = await ExecuteBulkUpsert{table.Name}BatchAsync(transaction, batch).ConfigureAwait(false);"
        );
        sb.AppendLine(
            "                if (finalResult is Result<int, SqlError>.Error<int, SqlError> finalErr)"
        );
        sb.AppendLine("                    return finalErr;");
        sb.AppendLine(
            "                totalAffected += ((Result<int, SqlError>.Ok<int, SqlError>)finalResult).Value;"
        );
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine(
            "            return new Result<int, SqlError>.Ok<int, SqlError>(totalAffected);"
        );
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine(
            "            return new Result<int, SqlError>.Error<int, SqlError>(new SqlError(\"Bulk upsert failed\", ex));"
        );
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate the batch execution helper method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Executes a single batch of bulk upserts for {table.Name}."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    private static async Task<Result<int, SqlError>> ExecuteBulkUpsert{table.Name}BatchAsync(IDbTransaction transaction, List<({tupleType})> batch)"
        );
        sb.AppendLine("    {");
        sb.AppendLine("        if (batch.Count == 0)");
        sb.AppendLine("            return new Result<int, SqlError>.Ok<int, SqlError>(0);");
        sb.AppendLine();
        sb.AppendLine("        if (transaction.Connection is null)");
        sb.AppendLine(
            "            return new Result<int, SqlError>.Error<int, SqlError>(new SqlError(\"Transaction has no connection\"));"
        );
        sb.AppendLine();

        // Build the SQL with placeholders - database-specific upsert syntax
        // All identifiers lowercase, no quoting needed for cross-platform compatibility
        var columnNames = string.Join(", ", allColumns.Select(c => c.Name));
        var pkColumnNames = string.Join(", ", primaryKeyColumns.Select(c => c.Name));
        var updateColumns = allColumns
            .Where(c => !primaryKeyColumns.Any(pk => pk.Name == c.Name))
            .ToList();
        var updateSet = string.Join(
            ", ",
            updateColumns.Select(c => $"{c.Name} = EXCLUDED.{c.Name}")
        );

        if (databaseType.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"        var sql = new StringBuilder(\"INSERT INTO {table.Name} ({columnNames}) VALUES \");"
            );
        }
        else
        {
            // SQLite uses INSERT OR REPLACE
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"        var sql = new StringBuilder(\"INSERT OR REPLACE INTO {table.Name} ({columnNames}) VALUES \");"
            );
        }

        sb.AppendLine("        var parameters = new List<object?>();");
        sb.AppendLine();
        sb.AppendLine("        for (int i = 0; i < batch.Count; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (i > 0) sql.Append(\", \");");

        // Build VALUES clause with parameter placeholders
        var paramPlaceholders = string.Join(
            ", ",
            allColumns.Select((c, idx) => $"@p\" + (i * {allColumns.Count} + {idx}) + \"")
        );
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            sql.Append(\"({paramPlaceholders})\");"
        );
        sb.AppendLine("            var rec = batch[i];");

        // Add parameters from tuple
        foreach (var col in allColumns)
        {
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"            parameters.Add(rec.{col.Name});"
            );
        }
        sb.AppendLine("        }");

        // Add ON CONFLICT clause for Postgres
        if (
            databaseType.Equals("Postgres", StringComparison.OrdinalIgnoreCase)
            && updateColumns.Count > 0
        )
        {
            sb.AppendLine();
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"        sql.Append(\" ON CONFLICT ({pkColumnNames}) DO UPDATE SET {updateSet}\");"
            );
        }

        sb.AppendLine();

        var commandType = connectionType.Replace("Connection", "Command", StringComparison.Ordinal);
        var transactionType = connectionType.Replace(
            "Connection",
            "Transaction",
            StringComparison.Ordinal
        );
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        using (var command = new {commandType}(sql.ToString(), ({connectionType})transaction.Connection!, ({transactionType})transaction))"
        );
        sb.AppendLine("        {");
        sb.AppendLine("            for (int i = 0; i < parameters.Count; i++)");
        sb.AppendLine("            {");
        sb.AppendLine(
            "                command.Parameters.AddWithValue(\"@p\" + i, parameters[i] ?? (object)DBNull.Value);"
        );
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine(
            "            var rowsAffected = await command.ExecuteNonQueryAsync().ConfigureAwait(false);"
        );
        sb.AppendLine(
            "            return new Result<int, SqlError>.Ok<int, SqlError>(rowsAffected);"
        );
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    /// <summary>
    /// Resolves NpgsqlDbType for a parameter by matching its name to result columns.
    /// Uses the column's C# type to determine the correct NpgsqlDbType. Falls back to Text.
    /// </summary>
    private static string ResolveNpgsqlDbTypeFromColumns(
        string parameterName,
        IReadOnlyList<DatabaseColumn> columns
    )
    {
        var matchingColumn = columns?.FirstOrDefault(c =>
            string.Equals(c.Name, parameterName, StringComparison.OrdinalIgnoreCase)
        );

        return matchingColumn?.CSharpType switch
        {
            "int" or "int?" => "Integer",
            "long" or "long?" => "Bigint",
            "short" or "short?" => "Smallint",
            "bool" or "bool?" => "Boolean",
            "float" or "float?" => "Real",
            "double" or "double?" => "Double",
            "decimal" or "decimal?" => "Numeric",
            "DateTime" or "DateTime?" => "Timestamp",
            "Guid" or "Guid?" => "Uuid",
            "byte[]" => "Bytea",
            _ => "Text",
        };
    }
}
