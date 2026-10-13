using System.Runtime.CompilerServices;
using Nimblesite.Sql.Model;
using StringSqlError = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Error<
    string,
    Nimblesite.Sql.Model.SqlError
>;
using StringSqlResult = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>;

namespace Nimblesite.DataProvider.Core.CodeGeneration;

// Implements [DP-CODEGEN-SHARED-GUARDS].
internal static class CodeGenerationGuards
{
    internal static StringSqlResult? ValidateQueryGeneration(
        string? className,
        string? methodName,
        string? returnTypeName,
        string? sql,
        IReadOnlyList<DatabaseColumn>? columns
    ) =>
        RequireText(className)
        ?? RequireText(methodName)
        ?? RequireText(returnTypeName)
        ?? RequireText(sql)
        ?? RequireItems(columns);

    internal static StringSqlResult? ValidateNonQueryGeneration(
        string? className,
        string? methodName,
        string? sql
    ) => RequireText(className) ?? RequireText(methodName) ?? RequireText(sql);

    internal static StringSqlResult? ValidateGroupingMethod(
        string? fileName,
        object? groupingConfig
    ) => RequireText(fileName) ?? RequirePresent(groupingConfig);

    internal static StringSqlResult? ValidateGroupedQueryGeneration(
        string? className,
        string? methodName,
        string? sql,
        IReadOnlyList<DatabaseColumn>? columns,
        object? groupingConfig
    ) =>
        RequireText(className)
        ?? RequireText(methodName)
        ?? RequireText(sql)
        ?? RequireItems(columns)
        ?? RequirePresent(groupingConfig);

    internal static StringSqlResult? ValidateGroupedRecordTypes(
        string? parentName,
        string? childName,
        IReadOnlyList<string>? parentColumns,
        IReadOnlyList<string>? childColumns,
        IReadOnlyList<DatabaseColumn>? allColumns
    ) =>
        RequireText(parentName)
        ?? RequireText(childName)
        ?? RequireItems(parentColumns)
        ?? RequireItems(childColumns)
        ?? RequireItems(allColumns);

    internal static StringSqlResult? RequireText(
        string? value,
        [CallerArgumentExpression(nameof(value))] string name = ""
    ) =>
        string.IsNullOrWhiteSpace(value)
            ? new StringSqlError(new SqlError($"{name} cannot be null or empty"))
            : null;

    internal static StringSqlResult? RequirePresent(
        object? value,
        [CallerArgumentExpression(nameof(value))] string name = ""
    ) => value is null ? new StringSqlError(new SqlError($"{name} cannot be null")) : null;

    internal static StringSqlResult? RequireItems<T>(
        IReadOnlyList<T>? items,
        [CallerArgumentExpression(nameof(items))] string name = ""
    ) =>
        items is null || items.Count == 0
            ? new StringSqlError(new SqlError($"{name} cannot be null or empty"))
            : null;
}
