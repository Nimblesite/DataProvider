module Nimblesite.Lql.Core.TypeProvider.RuntimeTranspilerTests

open System
open System.IO
open Xunit
open Nimblesite.Lql.Core
open Nimblesite.Lql.SQLite
open Nimblesite.Sql.Model
open Outcome

// =============================================================================
// RUNTIME PARSER + TRANSPILER COVERAGE TESTS
//
// The compile-time tests in TypeProviderE2ETests.fs only read precomputed Sql
// strings baked by the F# Type Provider at compile time, so coverlet sees 0%
// coverage of the Lql.Core / Lql.SQLite / Sql.Model assemblies. These tests
// invoke LqlStatementConverter.ToStatement(...) and SqlStatementExtensionsSQLite
// .ToSQLite(...) directly at runtime so coverlet captures them.
// =============================================================================

let private transpile (lql: string) : string =
    let stmtResult = LqlStatementConverter.ToStatement(lql)
    match stmtResult with
    | :? Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError> as ok ->
        let sqlResult = ok.Value.ToSQLite()
        match sqlResult with
        | :? Result<string, SqlError>.Ok<string, SqlError> as sqlOk -> sqlOk.Value
        | :? Result<string, SqlError>.Error<string, SqlError> as sqlErr ->
            failwithf "ToSQLite failed for %s: %s" lql (sqlErr.Value.ToString())
        | _ -> failwithf "Unexpected ToSQLite result for %s" lql
    | :? Result<LqlStatement, SqlError>.Error<LqlStatement, SqlError> as err ->
        failwithf "Parse failed for %s: %s" lql (err.Value.ToString())
    | _ -> failwithf "Unexpected ToStatement result for %s" lql

let private assertContainsCi (needle: string) (haystack: string) =
    Assert.Contains(needle.ToUpperInvariant(), haystack.ToUpperInvariant())

[<Collection("TypeProvider")>]
type RuntimeSelectTests() =

    [<Fact>]
    member _.``Runtime: select * generates SELECT *``() =
        let sql = transpile "Customer |> select(*)"
        assertContainsCi "SELECT" sql
        assertContainsCi "CUSTOMER" sql

    [<Fact>]
    member _.``Runtime: select named columns``() =
        let sql = transpile "Users |> select(Users.Id, Users.Name, Users.Email)"
        assertContainsCi "SELECT" sql
        assertContainsCi "ID" sql
        assertContainsCi "NAME" sql
        assertContainsCi "EMAIL" sql

    [<Fact>]
    member _.``Runtime: select with column alias``() =
        let sql = transpile "Users |> select(Users.Id, Users.Name as username)"
        assertContainsCi "AS" sql

    [<Fact>]
    member _.``Runtime: select multiple tables produces qualified columns``() =
        let sql = transpile "Users |> select(Users.Id, Users.Name)"
        assertContainsCi "USERS" sql

[<Collection("TypeProvider")>]
type RuntimeFilterTests() =

    [<Fact>]
    member _.``Runtime: simple filter generates WHERE``() =
        let sql = transpile "Users |> filter(fn(row) => row.Users.Age > 18) |> select(Users.Name)"
        assertContainsCi "WHERE" sql
        assertContainsCi ">" sql

    [<Fact>]
    member _.``Runtime: filter with AND``() =
        let sql =
            transpile
                "Users |> filter(fn(row) => row.Users.Age > 18 and row.Users.Status = 'active') |> select(*)"
        assertContainsCi "WHERE" sql
        assertContainsCi "AND" sql

    [<Fact>]
    member _.``Runtime: filter with OR``() =
        let sql =
            transpile
                "Users |> filter(fn(row) => row.Users.Age < 18 or row.Users.Role = 'admin') |> select(*)"
        assertContainsCi "WHERE" sql
        assertContainsCi "OR" sql

    [<Fact>]
    member _.``Runtime: filter with equality on string literal``() =
        let sql = transpile "Users |> filter(fn(row) => row.Users.Status = 'active') |> select(*)"
        assertContainsCi "WHERE" sql
        assertContainsCi "ACTIVE" sql

    [<Fact>]
    member _.``Runtime: filter with less-than-or-equal``() =
        let sql = transpile "Users |> filter(fn(row) => row.Users.Age <= 65) |> select(*)"
        assertContainsCi "WHERE" sql

    [<Fact>]
    member _.``Runtime: filter with greater-than-or-equal``() =
        let sql = transpile "Users |> filter(fn(row) => row.Users.Age >= 21) |> select(*)"
        assertContainsCi "WHERE" sql

[<Collection("TypeProvider")>]
type RuntimeJoinTests() =

    [<Fact>]
    member _.``Runtime: inner join``() =
        let sql =
            transpile
                "Users |> join(Orders, on = Users.Id = Orders.UserId) |> select(Users.Name, Orders.Total)"
        assertContainsCi "JOIN" sql
        assertContainsCi "ON" sql

    [<Fact>]
    member _.``Runtime: left join``() =
        let sql =
            transpile
                "Users |> left_join(Orders, on = Users.Id = Orders.UserId) |> select(Users.Name, Orders.Total)"
        assertContainsCi "LEFT" sql
        assertContainsCi "JOIN" sql

    [<Fact>]
    member _.``Runtime: multiple joins chained``() =
        let sql =
            transpile
                "Users |> join(Orders, on = Users.Id = Orders.UserId) |> join(Products, on = Orders.ProductId = Products.Id) |> select(Users.Name, Products.Name)"
        let upper = sql.ToUpperInvariant()
        let joinCount =
            upper.Split([| "JOIN" |], System.StringSplitOptions.None).Length - 1
        Assert.True(joinCount >= 2, sprintf "Expected at least 2 JOINs, got SQL: %s" sql)

[<Collection("TypeProvider")>]
type RuntimeAggregationTests() =

    [<Fact>]
    member _.``Runtime: group by``() =
        let sql =
            transpile "Orders |> group_by(Orders.UserId) |> select(Orders.UserId, count(*) as order_count)"
        assertContainsCi "GROUP BY" sql
        assertContainsCi "COUNT" sql

    [<Fact>]
    member _.``Runtime: group by with sum and avg``() =
        let sql =
            transpile
                "Orders |> group_by(Orders.Status) |> select(Orders.Status, sum(Orders.Total) as total_sum, avg(Orders.Total) as avg_total)"
        assertContainsCi "GROUP BY" sql
        assertContainsCi "SUM" sql
        assertContainsCi "AVG" sql

    [<Fact>]
    member _.``Runtime: having clause``() =
        let sql =
            transpile
                "Orders |> group_by(Orders.UserId) |> having(fn(g) => count(*) > 5) |> select(Orders.UserId, count(*) as cnt)"
        assertContainsCi "HAVING" sql

    [<Fact>]
    member _.``Runtime: count star``() =
        let sql = transpile "Users |> select(count(*) as total)"
        assertContainsCi "COUNT" sql

[<Collection("TypeProvider")>]
type RuntimeOrderingTests() =

    [<Fact>]
    member _.``Runtime: order by ascending``() =
        let sql = transpile "Users |> order_by(Users.Name asc) |> select(*)"
        assertContainsCi "ORDER BY" sql

    [<Fact>]
    member _.``Runtime: order by descending``() =
        let sql = transpile "Users |> order_by(Users.CreatedAt desc) |> select(*)"
        assertContainsCi "ORDER BY" sql
        assertContainsCi "DESC" sql

    [<Fact>]
    member _.``Runtime: limit``() =
        let sql = transpile "Users |> order_by(Users.Id) |> limit(10) |> select(*)"
        assertContainsCi "LIMIT" sql

    [<Fact>]
    member _.``Runtime: limit with offset``() =
        let sql = transpile "Users |> order_by(Users.Id) |> limit(10) |> offset(20) |> select(*)"
        assertContainsCi "LIMIT" sql
        assertContainsCi "OFFSET" sql

[<Collection("TypeProvider")>]
type RuntimeArithmeticTests() =

    [<Fact>]
    member _.``Runtime: multiplication in select``() =
        let sql = transpile "Products |> select(Products.Price * Products.Quantity as total)"
        Assert.Contains("*", sql)

    [<Fact>]
    member _.``Runtime: addition and subtraction in select``() =
        let sql =
            transpile "Orders |> select(Orders.Subtotal + Orders.Tax - Orders.Discount as final_total)"
        Assert.Contains("+", sql)
        Assert.Contains("-", sql)

    [<Fact>]
    member _.``Runtime: division``() =
        let sql = transpile "Orders |> select(Orders.Total / Orders.Quantity as unit_price)"
        Assert.Contains("/", sql)

[<Collection("TypeProvider")>]
type RuntimeComplexPipelineTests() =

    [<Fact>]
    member _.``Runtime: filter then select then order then limit``() =
        let sql =
            transpile
                "Users |> filter(fn(row) => row.Users.Age > 18) |> order_by(Users.Name asc) |> limit(50) |> select(Users.Id, Users.Name)"
        assertContainsCi "WHERE" sql
        assertContainsCi "ORDER BY" sql
        assertContainsCi "LIMIT" sql

    [<Fact>]
    member _.``Runtime: join + filter + group + having + order``() =
        let sql =
            transpile
                "Users |> join(Orders, on = Users.Id = Orders.UserId) |> filter(fn(row) => row.Orders.Status = 'paid') |> group_by(Users.Id) |> having(fn(g) => sum(Orders.Total) > 100) |> order_by(Users.Id asc) |> select(Users.Id, sum(Orders.Total) as revenue)"
        assertContainsCi "JOIN" sql
        assertContainsCi "WHERE" sql
        assertContainsCi "GROUP BY" sql
        assertContainsCi "HAVING" sql
        assertContainsCi "ORDER BY" sql

    [<Fact>]
    member _.``Runtime: group by with multiple aggregates``() =
        let sql =
            transpile
                "Orders |> group_by(Orders.UserId) |> select(Orders.UserId, count(*) as orders, sum(Orders.Total) as revenue, avg(Orders.Total) as avg_order)"
        assertContainsCi "GROUP BY" sql
        assertContainsCi "COUNT" sql
        assertContainsCi "SUM" sql
        assertContainsCi "AVG" sql

[<Collection("TypeProvider")>]
type RuntimeDistinctAndPredicateTests() =

    [<Fact>]
    member _.``Runtime: select_distinct renders SELECT DISTINCT``() =
        Assert.Equal(
            "SELECT DISTINCT users.country FROM users",
            transpile "users |> select_distinct(users.country)"
        )

    [<Fact>]
    member _.``Runtime: null check and comparison are joined with AND``() =
        Assert.Equal(
            "SELECT users.id FROM users WHERE users.email IS NOT NULL AND users.age >= 18",
            transpile
                "users |> filter(fn(row) => row.users.email is not null and row.users.age >= 18) |> select(users.id)"
        )

    [<Fact>]
    member _.``Runtime: parenthesized OR keeps its grouping inside AND``() =
        Assert.Equal(
            "SELECT users.id FROM users WHERE (users.age < 18 OR users.age > 65) AND users.active = 1",
            transpile
                "users |> filter(fn(row) => (row.users.age < 18 or row.users.age > 65) and row.users.active = 1) |> select(users.id)"
        )

// Implements [LQL-CTE], [LQL-DERIVED-TABLE], and [LQL-SUBQUERY-LAYOUT]
// through the F# runtime path, sharing the canonical query/SQL fixtures.
[<Collection("TypeProvider")>]
type RuntimeSubqueryTests() =

    [<Theory>]
    [<InlineData("cte_with")>]
    [<InlineData("exists_subquery")>]
    [<InlineData("in_subquery")>]
    [<InlineData("subquery_nested")>]
    member _.``Runtime: subqueries match the shared SQLite golden SQL``(name: string) =
        let path extension =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", name + extension)
        let expected = File.ReadAllText(path ".sql").Trim()
        Assert.Equal(expected, File.ReadAllText(path ".lql") |> transpile)

[<Collection("TypeProvider")>]
type RuntimeParseErrorTests() =

    [<Fact>]
    member _.``Runtime: invalid LQL returns parse error``() =
        let result = LqlStatementConverter.ToStatement("this is not valid lql @@@")
        match result with
        | :? Result<LqlStatement, SqlError>.Error<LqlStatement, SqlError> -> ()
        | :? Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError> ->
            failwith "Expected parse error for invalid LQL"
        | _ -> failwith "Unexpected result type"

    [<Fact>]
    member _.``Runtime: empty input returns parse error``() =
        let result = LqlStatementConverter.ToStatement("")
        match result with
        | :? Result<LqlStatement, SqlError>.Error<LqlStatement, SqlError> -> ()
        | :? Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError> ->
            failwith "Expected parse error for empty input"
        | _ -> failwith "Unexpected result type"
