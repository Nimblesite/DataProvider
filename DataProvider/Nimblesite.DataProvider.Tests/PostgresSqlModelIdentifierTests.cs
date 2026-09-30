using Nimblesite.Lql.Postgres;
using Nimblesite.Sql.Model;

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

        Assert.Contains("SELECT \"Id\"", sql, StringComparison.Ordinal);
        Assert.Contains("FROM \"Users\"", sql, StringComparison.Ordinal);
    }
}
