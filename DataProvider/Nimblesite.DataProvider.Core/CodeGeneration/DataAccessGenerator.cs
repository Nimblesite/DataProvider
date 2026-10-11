using System.Globalization;
using System.Text;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Core.CodeGeneration;

/// <summary>
/// Static methods for generating data access extension methods
/// </summary>
// Implements [DP-CODEGEN-SHARED-BULK-LOOP].
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

        return string.Join(", ", parameters.Select(p => $"object? {p.Name}"));
    }

    /// <summary>
    /// Appends portable AddParameter binding lines for the given columns.
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
                $"                AddParameter(command, \"@{column.Name}\", {value});"
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
        if (
            CodeGenerationGuards.ValidateQueryGeneration(
                className: className,
                methodName: methodName,
                returnTypeName: returnTypeName,
                sql: sql,
                columns: columns
            ) is
            { } error
        )
            return error;

        var sb = new StringBuilder();
        AppendSqlHeader(
            builder: sb,
            className: className,
            methodName: methodName,
            connectionType: connectionType,
            parameters: parameters,
            resultType: $"ImmutableList<{returnTypeName}>",
            query: true
        );
        AppendSqlStart(builder: sb, sql: sql);
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            var results = ImmutableList.CreateBuilder<{returnTypeName}>();"
        );
        sb.AppendLine();

        AppendNativeCommand(builder: sb, connectionType: connectionType);

        // Add parameters - for Npgsql, use AddWithValue for non-null (type inferred from value)
        // and typed NpgsqlParameter for null/DBNull (type matched from result columns to avoid 42P08/42883)
        AppendSqlParameters(
            builder: sb,
            connectionType: connectionType,
            parameters: parameters,
            columns: columns
        );

        // Generate record constructor using column metadata
        ModelGenerator.AppendBlockAndLine(
            builder: sb,
            block: EmissionBlockLines1,
            line: $"                        var item = new {returnTypeName}("
        );

        ModelGenerator.AppendIndexedLines(
            builder: sb,
            count: columns.Count,
            render: (i, comma) =>
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"                            reader.IsDBNull({i}) ? {(columns[i].IsNullable ? "null" : $"default({columns[i].CSharpType})")} : reader.GetFieldValue<{columns[i].CSharpType}>({i}){comma}"
                )
        );

        global::Nimblesite.DataProvider.Core.CodeGeneration.ModelGenerator.AppendSourceLines(
            builder: sb,
            lines: EmissionBlockLines2
        );
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            return new Result<ImmutableList<{returnTypeName}>, SqlError>.Ok<ImmutableList<{returnTypeName}>, SqlError>(results.ToImmutable());"
        );
        global::Nimblesite.DataProvider.Core.CodeGeneration.ModelGenerator.AppendSourceLines(
            builder: sb,
            lines: EmissionBlockLines3
        );
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"            return new Result<ImmutableList<{returnTypeName}>, SqlError>.Error<ImmutableList<{returnTypeName}>, SqlError>(new SqlError(\"Database error\", ex));"
        );
        return ModelGenerator.CompleteResult(builder: sb, tail: EmissionBlockLines4);
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
        if (CodeGenerationGuards.RequirePresent(value: table, name: nameof(table)) is { } error)
            return error;

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

        // Generate INSERT SQL (no last_insert_rowid - all PKs are UUIDs). Identifiers are
        // double-quoted so the statement matches case-preserved names on every platform.
        var columnNames = string.Join(
            ", ",
            insertableColumns.Select(c => QuotedIdentifier(c.Name))
        );
        var parameterNames = string.Join(", ", insertableColumns.Select(c => $"@{c.Name}"));

        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        const string sql = \"INSERT INTO {QualifiedTable(table)} ({columnNames}) VALUES ({parameterNames})\";"
        );
        AppendTransactionConnectionGuard(sb);
        AppendTransactionCommandOpen(sb);

        // Add parameters
        AppendParameterBindings(sb, insertableColumns);
        AppendNonQueryTail(sb, failure: "Insert failed", connectionType: connectionType);

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
        if (
            CodeGenerationGuards.ValidateNonQueryGeneration(
                className: className,
                methodName: methodName,
                sql: sql
            ) is
            { } error
        )
            return error;

        var sb = new StringBuilder();
        AppendSqlHeader(
            builder: sb,
            className: className,
            methodName: methodName,
            connectionType: connectionType,
            parameters: parameters,
            resultType: "int",
            query: false
        );
        AppendSqlStart(builder: sb, sql: sql);

        AppendNativeCommand(builder: sb, connectionType: connectionType);

        // Add parameters
        AppendSqlParameters(builder: sb, connectionType: connectionType, parameters: parameters);

        return ModelGenerator.CompleteResult(builder: sb, tail: EmissionBlockLines5);
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
        return GenerateBulkMethod(
            table: table,
            databaseType: "SQLite",
            batchSize: batchSize,
            connectionType: connectionType,
            upsert: false
        );
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
        return GenerateBulkMethod(
            table: table,
            databaseType: databaseType,
            batchSize: batchSize,
            connectionType: connectionType,
            upsert: true
        );
    }

    /// <summary>
    /// Emits the generated guard that rejects transactions without a live connection.
    /// </summary>
    /// <param name="sb">Builder receiving the generated code.</param>
    private static void AppendTransactionConnectionGuard(StringBuilder sb) =>
        ModelGenerator.AppendSourceLines(builder: sb, lines: EmissionBlockLines6);

    /// <summary>
    /// Emits the generated try-block opening and a transaction-scoped command created by
    /// the transaction's own connection, so the code runs on any ADO.NET provider.
    /// </summary>
    /// <param name="sb">Builder receiving the generated code.</param>
    private static void AppendTransactionCommandOpen(StringBuilder sb) =>
        ModelGenerator.AppendSourceLines(builder: sb, lines: EmissionBlockLines7);

    /// <summary>
    /// Emits the non-query execution, the Result mapping, and the method's closing, including
    /// the portable AddParameter local function the bindings call.
    /// </summary>
    /// <param name="sb">Builder receiving the generated code.</param>
    /// <param name="failure">Error message wrapped around a thrown exception.</param>
    /// <param name="connectionType">Connection type the schema was read with (documented only).</param>
    private static void AppendNonQueryTail(StringBuilder sb, string failure, string connectionType)
    {
        sb.AppendLine();
        AppendNonQueryExecute(sb, indent: "                ");
        ModelGenerator.AppendBlockAndLine(
            builder: sb,
            block: EmissionBlockLines8,
            line: $"            return new Result<int, SqlError>.Error<int, SqlError>(new SqlError(\"{failure}\", ex));"
        );
        sb.AppendLine("        }");
        AppendAddParameterFunction(sb, connectionType: connectionType);
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Emits the rows-affected execution, async when the provider's command supports it.
    /// </summary>
    private static void AppendNonQueryExecute(StringBuilder sb, string indent)
    {
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"{indent}var rowsAffected = command is System.Data.Common.DbCommand dbCommand"
        );
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"{indent}    ? await dbCommand.ExecuteNonQueryAsync().ConfigureAwait(false)"
        );
        sb.AppendLine(CultureInfo.InvariantCulture, $"{indent}    : command.ExecuteNonQuery();");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"{indent}return new Result<int, SqlError>.Ok<int, SqlError>(rowsAffected);"
        );
    }

    /// <summary>
    /// Emits the provider-neutral parameter binder used by generated table operations.
    /// </summary>
    private static void AppendAddParameterFunction(StringBuilder sb, string connectionType)
    {
        sb.AppendLine();
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        // Portable ADO.NET binding; the schema was read through {connectionType}."
        );
        global::Nimblesite.DataProvider.Core.CodeGeneration.ModelGenerator.AppendSourceLines(
            builder: sb,
            lines: EmissionBlockLines9
        );
    }

    /// <summary>
    /// Double-quotes an identifier (SQL standard) for a generated C# string literal.
    /// </summary>
    private static string QualifiedTable(DatabaseTable table) =>
        table.Schema is "" or "main" or "public" or "dbo"
            ? QuotedIdentifier(table.Name)
            : $"{QuotedIdentifier(table.Schema)}.{QuotedIdentifier(table.Name)}";

    private static string QuotedIdentifier(string name) =>
        $"\\\"{name.Replace("\"", "\\\"\\\"", StringComparison.Ordinal)}\\\"";

    /// <summary>
    /// Derives the platform command and transaction type names from a connection type.
    /// </summary>
    /// <param name="connectionType">Database connection type (e.g., SqliteConnection).</param>
    /// <returns>The matching command and transaction type names.</returns>
    private static (string CommandType, string TransactionType) DeriveTransactionTypes(
        string connectionType
    ) =>
        (
            connectionType.Replace("Connection", "Command", StringComparison.Ordinal),
            connectionType.Replace("Connection", "Transaction", StringComparison.Ordinal)
        );

    /// <summary>
    /// Emits the generated batch execution tail: transaction-scoped command creation,
    /// positional parameter binding, and non-query execution.
    /// </summary>
    /// <param name="sb">Builder receiving the generated code.</param>
    /// <param name="connectionType">Database connection type (e.g., SqliteConnection).</param>
    private static void AppendBatchCommandExecute(StringBuilder sb, string connectionType)
    {
        sb.AppendLine();
        var (commandType, transactionType) = DeriveTransactionTypes(connectionType);
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        using (var command = new {commandType}(sql.ToString(), ({connectionType})transaction.Connection!, ({transactionType})transaction))"
        );
        global::Nimblesite.DataProvider.Core.CodeGeneration.ModelGenerator.AppendSourceLines(
            builder: sb,
            lines: EmissionBlockLines10
        );
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
