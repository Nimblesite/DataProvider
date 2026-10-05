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
}
