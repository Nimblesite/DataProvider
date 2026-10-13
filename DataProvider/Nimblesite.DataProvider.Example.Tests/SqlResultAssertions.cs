using System.Collections.Immutable;
using Nimblesite.Lql.SQLite;
using Nimblesite.Sql.Model;
using StringSqlResult = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>;

namespace Nimblesite.DataProvider.Example.Tests;

// Implements [DP-SQL-MODEL-TEST-FRAGMENTS].
internal static class SqlResultAssertions
{
    internal static string Contains(
        StringSqlResult result,
        ImmutableArray<string> fragments,
        string? message = null
    )
    {
        Assert.True(condition: result is StringSqlOk, userMessage: message);
        var sql = global::Tests.Shared.ResultAssertions.ExactOk(actual: result).Value;
        foreach (var fragment in fragments)
        {
            Assert.Contains(expectedSubstring: fragment, actualString: sql);
        }
        return sql;
    }

    internal static string ContainsQuery(
        SelectStatement query,
        ImmutableArray<string> fragments,
        string? message = null
    ) => Contains(result: query.ToSQLite(), fragments: fragments, message: message);
}
