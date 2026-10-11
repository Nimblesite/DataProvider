using System.Globalization;
using System.Text;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Core.CodeGeneration;

// Implements [CON-SHARED-CORE]. Shared keyed transaction operations.
public static partial class DataAccessGenerator
{
    /// <summary>
    /// Generates an UPDATE method for a database table
    /// </summary>
    /// <param name="table">Database table metadata</param>
    /// <param name="connectionType">Database connection type</param>
    /// <returns>Generated UPDATE method code</returns>
    public static Result<string, SqlError> GenerateUpdateMethod(
        DatabaseTable table,
        string connectionType = "SqliteConnection"
    ) => GenerateKeyWriteMethod(table: table, connectionType: connectionType, delete: false);

    /// <summary>Generates a parameterized DELETE extension for a table's primary key.</summary>
    /// <param name="table">Database table metadata.</param>
    /// <param name="connectionType">Database connection type.</param>
    /// <returns>Generated transaction extension source.</returns>
    public static Result<string, SqlError> GenerateDeleteMethod(
        DatabaseTable table,
        string connectionType = "SqliteConnection"
    ) => GenerateKeyWriteMethod(table: table, connectionType: connectionType, delete: true);

    private static Result<string, SqlError> GenerateKeyWriteMethod(
        DatabaseTable table,
        string connectionType,
        bool delete
    )
    {
        if (CodeGenerationGuards.RequirePresent(value: table, name: nameof(table)) is { } error)
            return error;

        IReadOnlyList<DatabaseColumn> updateableColumns = delete ? [] : table.UpdateableColumns;
        var primaryKeyColumns = table.PrimaryKeyColumns;

        if ((!delete && updateableColumns.Count == 0) || primaryKeyColumns.Count == 0)
            return new Result<string, SqlError>.Ok<string, SqlError>("");

        var operation = delete ? "Delete" : "Update";
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
            $"    /// {operation}s a row in the {table.Name} table."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public static async Task<Result<int, SqlError>> {operation}{table.Name}Async(this IDbTransaction transaction, {parameterList})"
        );
        sb.AppendLine("    {");

        // Generate UPDATE SQL with double-quoted identifiers (see the INSERT generator)
        var setClause = BuildQualifiedAssignmentList(columns: updateableColumns, separator: ", ");
        var whereClause = BuildQualifiedAssignmentList(
            columns: primaryKeyColumns,
            separator: " AND "
        );

        var sql = delete
            ? $"DELETE FROM {QualifiedTable(table)} WHERE {whereClause}"
            : $"UPDATE {QualifiedTable(table)} SET {setClause} WHERE {whereClause}";
        sb.AppendLine(CultureInfo.InvariantCulture, $"        const string sql = \"{sql}\";");
        AppendTransactionConnectionGuard(sb);
        AppendTransactionCommandOpen(sb);

        // Add parameters (nullable types use null-coalescing to DBNull.Value)
        AppendParameterBindings(sb, allColumns);
        AppendNonQueryTail(sb, failure: $"{operation} failed", connectionType: connectionType);

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    private static string BuildQualifiedAssignmentList(
        IEnumerable<DatabaseColumn> columns,
        string separator
    ) => string.Join(separator, columns.Select(c => $"{QuotedIdentifier(c.Name)} = @{c.Name}"));
}
