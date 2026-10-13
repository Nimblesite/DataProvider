using Nimblesite.Sql.Model;

namespace Nimblesite.Lql.Core;

// Implements [LQL-CONTEXT-ORDERING-SHARED].
internal static class StatementBuilderOperations
{
    internal static void AddOrderBy(
        SelectStatementBuilder builder,
        IEnumerable<(string Column, string Direction)> orderItems
    )
    {
        foreach (var (column, direction) in orderItems)
        {
            builder.AddOrderBy(column: column, direction: direction);
        }
    }

    // Implements [LQL-CONTEXT-WHERE-SHARED].
    internal static string FormatWhereCondition(
        WhereCondition condition,
        Func<ColumnInfo, string> formatColumn
    ) =>
        condition switch
        {
            ComparisonCondition comparison =>
                $"{formatColumn(arg: comparison.Left)} {comparison.Operator.ToSql()} {comparison.Right}",
            LogicalOperator logical => logical.ToSql(),
            Parenthesis parenthesis => parenthesis.IsOpening ? "(" : ")",
            ExpressionCondition expression => expression.Expression,
            _ => "/*UNKNOWN_WHERE*/",
        };
}
