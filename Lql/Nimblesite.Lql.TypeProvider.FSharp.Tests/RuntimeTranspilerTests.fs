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

// Implements [LQL-FSHARP-SHARED-SQL-ASSERTIONS].
let private verifyContains (lql: string) (expected: string list) : unit =
    let sql = transpile lql
    for needle in expected do
        assertContainsCi needle sql

// Implements [LQL-FSHARP-SHARED-NAMED-CASE].
let internal verifyNamedCase (scenario: string) (verify: unit -> unit) : unit =
    Assert.False(String.IsNullOrEmpty(scenario))
    verify()

[<Collection("TypeProvider")>]
type RuntimeSelectTests() =

    // Implements [LQL-FSHARP-SHARED-SQL-ASSERTIONS].
    [<Theory>]
    [<MemberData("RuntimeCases", MemberType = typeof<Nimblesite.Lql.TypeProvider.FSharp.Tests.Data.TestDataSeeder>)>]
    member _.``Runtime: named SQL fragment scenarios``(scenario: string, lql: string, expected: string array) =
        verifyNamedCase scenario (fun () -> verifyContains lql (Array.toList expected))

[<Collection("TypeProvider")>]
type RuntimeJoinTests() =

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

// Implements [LQL-FSHARP-SHARED-PARSE-ERROR].
let private verifyParseError (input: string) (expectedFailure: string) : unit =
    let result = LqlStatementConverter.ToStatement(input)
    match result with
    | :? Result<LqlStatement, SqlError>.Error<LqlStatement, SqlError> -> ()
    | :? Result<LqlStatement, SqlError>.Ok<LqlStatement, SqlError> ->
        failwith expectedFailure
    | _ -> failwith "Unexpected result type"

[<Collection("TypeProvider")>]
type RuntimeParseErrorTests() =

    [<Fact>]
    member _.``Runtime: invalid LQL returns parse error``() =
        verifyParseError "this is not valid lql @@@" "Expected parse error for invalid LQL"

    [<Fact>]
    member _.``Runtime: empty input returns parse error``() =
        verifyParseError "" "Expected parse error for empty input"
