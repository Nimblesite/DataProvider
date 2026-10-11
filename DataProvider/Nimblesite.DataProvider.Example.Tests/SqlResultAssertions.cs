using System.Collections.Immutable;
using Xunit;
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
        var sql = Assert.IsType<StringSqlOk>(@object: result).Value;
        foreach (var fragment in fragments)
        {
            Assert.Contains(expectedSubstring: fragment, actualString: sql);
        }
        return sql;
    }
}
