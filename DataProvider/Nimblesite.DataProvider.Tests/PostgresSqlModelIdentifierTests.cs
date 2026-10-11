using Nimblesite.Lql.Postgres;
using Nimblesite.Sql.Model;
using static global::Tests.Shared.StringAssertions;

namespace Nimblesite.DataProvider.Tests;

public sealed class PostgresSqlModelIdentifierTests
{
    [Fact]
    public void RenderSelectStatement_QuotesMixedCaseIdentifiers()
    {
        var statement = new SelectStatementBuilder()
            .AddSelectColumn("Id")
            .AddTable("Users")
            .Build();

        var sql = Assert.IsType<StringOk>(statement.ToPostgreSql()).Value;

        ContainsAll(
            actual: sql,
            expected: ["SELECT \"Id\"", "FROM \"Users\""],
            comparison: StringComparison.Ordinal
        );
    }
}
