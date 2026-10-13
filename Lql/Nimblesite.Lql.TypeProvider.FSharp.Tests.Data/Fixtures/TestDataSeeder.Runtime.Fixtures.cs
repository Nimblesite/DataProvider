namespace Nimblesite.Lql.TypeProvider.FSharp.Tests.Data;

// Implements [LQL-FSHARP-SHARED-SQL-ASSERTIONS]. Named inputs and expected fragments only.
public static partial class TestDataSeeder
{
    /// <summary>Original runtime query cases for the shared F# assertion workflow.</summary>
    public static IEnumerable<object[]> RuntimeCases() =>
        [
            new object[]
            {
                "Runtime: select * generates SELECT *",
                "Customer |> select(*)",
                new string[] { "SELECT", "CUSTOMER" },
            },
            new object[]
            {
                "Runtime: select named columns",
                "Users |> select(Users.Id, Users.Name, Users.Email)",
                new string[] { "SELECT", "ID", "NAME", "EMAIL" },
            },
            new object[]
            {
                "Runtime: select with column alias",
                "Users |> select(Users.Id, Users.Name as username)",
                new string[] { "AS" },
            },
            new object[]
            {
                "Runtime: select multiple tables produces qualified columns",
                "Users |> select(Users.Id, Users.Name)",
                new string[] { "USERS" },
            },
            new object[]
            {
                "Runtime: simple filter generates WHERE",
                "Users |> filter(fn(row) => row.Users.Age > 18) |> select(Users.Name)",
                new string[] { "WHERE", ">" },
            },
            new object[]
            {
                "Runtime: filter with AND",
                "Users |> filter(fn(row) => row.Users.Age > 18 and row.Users.Status = 'active') |> select(*)",
                new string[] { "WHERE", "AND" },
            },
            new object[]
            {
                "Runtime: filter with OR",
                "Users |> filter(fn(row) => row.Users.Age < 18 or row.Users.Role = 'admin') |> select(*)",
                new string[] { "WHERE", "OR" },
            },
            new object[]
            {
                "Runtime: filter with equality on string literal",
                "Users |> filter(fn(row) => row.Users.Status = 'active') |> select(*)",
                new string[] { "WHERE", "ACTIVE" },
            },
            new object[]
            {
                "Runtime: filter with less-than-or-equal",
                "Users |> filter(fn(row) => row.Users.Age <= 65) |> select(*)",
                new string[] { "WHERE" },
            },
            new object[]
            {
                "Runtime: filter with greater-than-or-equal",
                "Users |> filter(fn(row) => row.Users.Age >= 21) |> select(*)",
                new string[] { "WHERE" },
            },
            new object[]
            {
                "Runtime: inner join",
                "Users |> join(Orders, on = Users.Id = Orders.UserId) |> select(Users.Name, Orders.Total)",
                new string[] { "JOIN", "ON" },
            },
            new object[]
            {
                "Runtime: left join",
                "Users |> left_join(Orders, on = Users.Id = Orders.UserId) |> select(Users.Name, Orders.Total)",
                new string[] { "LEFT", "JOIN" },
            },
            new object[]
            {
                "Runtime: group by",
                "Orders |> group_by(Orders.UserId) |> select(Orders.UserId, count(*) as order_count)",
                new string[] { "GROUP BY", "COUNT" },
            },
            new object[]
            {
                "Runtime: group by with sum and avg",
                "Orders |> group_by(Orders.Status) |> select(Orders.Status, sum(Orders.Total) as total_sum, avg(Orders.Total) as avg_total)",
                new string[] { "GROUP BY", "SUM", "AVG" },
            },
            new object[]
            {
                "Runtime: having clause",
                "Orders |> group_by(Orders.UserId) |> having(fn(g) => count(*) > 5) |> select(Orders.UserId, count(*) as cnt)",
                new string[] { "HAVING" },
            },
            new object[]
            {
                "Runtime: count star",
                "Users |> select(count(*) as total)",
                new string[] { "COUNT" },
            },
            new object[]
            {
                "Runtime: order by ascending",
                "Users |> order_by(Users.Name asc) |> select(*)",
                new string[] { "ORDER BY" },
            },
            new object[]
            {
                "Runtime: order by descending",
                "Users |> order_by(Users.CreatedAt desc) |> select(*)",
                new string[] { "ORDER BY", "DESC" },
            },
            new object[]
            {
                "Runtime: limit",
                "Users |> order_by(Users.Id) |> limit(10) |> select(*)",
                new string[] { "LIMIT" },
            },
            new object[]
            {
                "Runtime: limit with offset",
                "Users |> order_by(Users.Id) |> limit(10) |> offset(20) |> select(*)",
                new string[] { "LIMIT", "OFFSET" },
            },
            new object[]
            {
                "Runtime: filter then select then order then limit",
                "Users |> filter(fn(row) => row.Users.Age > 18) |> order_by(Users.Name asc) |> limit(50) |> select(Users.Id, Users.Name)",
                new string[] { "WHERE", "ORDER BY", "LIMIT" },
            },
            new object[]
            {
                "Runtime: join + filter + group + having + order",
                "Users |> join(Orders, on = Users.Id = Orders.UserId) |> filter(fn(row) => row.Orders.Status = 'paid') |> group_by(Users.Id) |> having(fn(g) => sum(Orders.Total) > 100) |> order_by(Users.Id asc) |> select(Users.Id, sum(Orders.Total) as revenue)",
                new string[] { "JOIN", "WHERE", "GROUP BY", "HAVING", "ORDER BY" },
            },
            new object[]
            {
                "Runtime: group by with multiple aggregates",
                "Orders |> group_by(Orders.UserId) |> select(Orders.UserId, count(*) as orders, sum(Orders.Total) as revenue, avg(Orders.Total) as avg_order)",
                new string[] { "GROUP BY", "COUNT", "SUM", "AVG" },
            },
        ];
}
