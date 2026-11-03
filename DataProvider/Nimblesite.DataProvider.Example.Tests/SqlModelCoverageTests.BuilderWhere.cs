// Implements [DP-SQL-MODEL-TEST-FRAGMENTS].
using System.Linq.Expressions;
using Nimblesite.Lql.SQLite;
using Nimblesite.Sql.Model;
using static global::Tests.Shared.ResultAssertions;
using static Nimblesite.DataProvider.Example.MapFunctions;
using OrderColumns = Nimblesite.DataProvider.Example.Model.Order;

namespace Nimblesite.DataProvider.Example.Tests;

/// <summary>
/// Expression predicates applied through <see cref="SelectStatementBuilder"/> run against SQLite.
/// </summary>
public sealed partial class SqlModelCoverageTests
{
    /// <summary>AND joins an equality and a greater-than comparison.</summary>
    [Fact]
    public async Task BuilderWhere_AndComparisons_ReturnsMatchingOrder() =>
        Assert.Equal(
            ["ORD-002"],
            await QueryOrderNumbers(predicate: o =>
                    o.Status == "Processing" && o.TotalAmount > 600.0
                )
                .ConfigureAwait(false)
        );

    /// <summary>OR wraps each comparison in parentheses.</summary>
    [Fact]
    public async Task BuilderWhere_OrComparisons_ReturnsMatchingOrder() =>
        Assert.Equal(
            ["ORD-001"],
            await QueryOrderNumbers(predicate: o =>
                    o.OrderNumber == "ORD-001" || o.TotalAmount < 100.0
                )
                .ConfigureAwait(false)
        );

    /// <summary>Not-equal comparisons exclude the matching row.</summary>
    [Fact]
    public async Task BuilderWhere_NotEqual_ExcludesMatchingOrder() =>
        Assert.Equal(
            ["ORD-002"],
            await QueryOrderNumbers(predicate: o => o.Status != "Completed").ConfigureAwait(false)
        );

    private async Task<string[]> QueryOrderNumbers(Expression<Func<OrderColumns, bool>> predicate)
    {
        await SetupDatabase().ConfigureAwait(false);
        var statement = "Orders".From().SelectAll().Where(predicate: predicate).ToSqlStatement();
        var orders = Ok(read: () =>
            _connection.GetRecords(statement, stmt => stmt.ToSQLite(), MapOrder)
        ).Value;
        return [.. orders.Select(o => o.OrderNumber ?? string.Empty).Order(StringComparer.Ordinal)];
    }
}
