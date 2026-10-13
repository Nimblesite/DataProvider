module Nimblesite.Lql.Core.TypeProvider.Tests

open System
open System.IO
open Microsoft.Data.Sqlite
open Xunit
open Nimblesite.Lql.Core.TypeProvider.RuntimeTranspilerTests
open Nimblesite.Lql.Core.TypeProvider.TestQueries
open Nimblesite.Lql.Core.TypeProvider
open Nimblesite.Lql.TypeProvider.FSharp.Tests.Data

// =============================================================================
// COMPILE-TIME VALIDATED LQL QUERIES
// These are validated at COMPILE TIME by the F# type provider
// Invalid LQL will cause a COMPILATION ERROR, not a runtime error
// =============================================================================

// =============================================================================
// E2E TEST FIXTURES - Test the type provider with REAL SQLite database file
// Schema is created by Nimblesite.DataProvider.Migration.Core.CLI from YAML - NO raw SQL for schema!
// =============================================================================

module TestFixtures =
    /// Get the path to the test database file (created by Nimblesite.DataProvider.Migration.Core.CLI from YAML)
    let getTestDbPath() =
        let baseDir = AppDomain.CurrentDomain.BaseDirectory
        // The database is created in the project directory by MSBuild target
        // bin/Debug/net9.0 -> go up 3 levels to project dir
        let projectDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."))
        Path.Combine(projectDir, "typeprovider-test.db")

    /// Open connection to the REAL SQLite database file
    let openTestDatabase() =
        let dbPath = getTestDbPath()
        if not (File.Exists(dbPath)) then
            failwithf "Test database not found at %s. Run 'dotnet build' first to create it via Nimblesite.DataProvider.Migration.Core.CLI." dbPath
        let conn = new SqliteConnection($"Data Source={dbPath}")
        conn.Open()
        conn

    /// Create test database connection with fresh test data using C# seeder with generated extensions
    let createTestDatabase() =
        let conn = openTestDatabase()
        use transaction = conn.BeginTransaction()
        let result = TestDataSeeder.SeedDataAsync(transaction).GetAwaiter().GetResult()
        match result with
        | :? Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Ok<string, Nimblesite.Sql.Model.SqlError> ->
            transaction.Commit()
        | :? Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Error<string, Nimblesite.Sql.Model.SqlError> as err ->
            transaction.Rollback()
            failwithf "Failed to seed test data: %s" (err.Value.ToString())
        | _ ->
            transaction.Rollback()
            failwith "Unknown result type from SeedDataAsync"
        conn

    let executeQuery (conn: SqliteConnection) (sql: string) =
        use cmd = new SqliteCommand(sql, conn)
        use reader = cmd.ExecuteReader()
        let results = ResizeArray<Map<string, obj>>()
        while reader.Read() do
            let row : Map<string, obj> =
                [| for i in 0 .. reader.FieldCount - 1 ->
                    let name : string = reader.GetName(i) |> string
                    let value : obj =
                        if reader.IsDBNull(i) then
                            DBNull.Value :> obj
                        else
                            reader.GetValue(i) |> Unchecked.nonNull
                    (name, value) |]
                |> Map.ofArray
            results.Add(row)
        results |> List.ofSeq

// Implements [LQL-FSHARP-SHARED-ROW-COUNT].
let private verifyDatabaseRowCount (sql: string) (expected: int) (verifyRows: Map<string, obj> list -> unit) : unit =
    use conn = TestFixtures.createTestDatabase()
    let results = TestFixtures.executeQuery conn sql
    Assert.Equal(expected, results.Length)
    verifyRows results

// =============================================================================
// E2E TESTS - Comprehensive tests for the F# Type Provider
// =============================================================================

// Implements [LQL-FSHARP-SHARED-SQL-ASSERTIONS].
let private verifySqlContains (sql: string) (expected: string list) : unit =
    let upper = sql.ToUpperInvariant()
    for needle in expected do
        Assert.Contains(needle, upper)

[<Collection("TypeProvider")>]
type TypeProviderCompileTimeValidationTests() =

    [<Fact>]
    member _.``Type provider generates Query property for simple select``() =
        Assert.Equal("Customer |> select(*)", SelectAll.Query)

    [<Fact>]
    member _.``Type provider generates Sql property for simple select``() =
        Assert.NotNull(SelectAll.Sql)
        Assert.Contains("SELECT", SelectAll.Sql.ToUpperInvariant())

    // Implements [LQL-FSHARP-SHARED-COMPILE-TIME-CASES].
    static member FragmentCases : seq<objnull array> =
        Nimblesite.Lql.Core.TypeProvider.TestInputs.fragmentCases

    [<Theory>]
    [<MemberData("FragmentCases")>]
    member _.``Named compile-time SQL fragment scenarios``(scenario: string, sql: string, expected: string array) =
        verifyNamedCase scenario (fun () -> verifySqlContains sql (Array.toList expected))

[<Collection("TypeProvider")>]
type TypeProviderJoinTests() =

    [<Fact>]
    member _.``Multiple joins are chained correctly``() =
        let sql = JoinMultiple.Sql.ToUpperInvariant()
        // Should have at least 2 JOINs
        let joinCount = sql.Split([|"JOIN"|], StringSplitOptions.None).Length - 1
        Assert.True(joinCount >= 2, sprintf "Expected at least 2 JOINs but got %d" joinCount)

[<Collection("TypeProvider")>]
type TypeProviderE2EExecutionTests() =

    // Implements [LQL-FUNCTION-MAPPING-SHARED].
    [<Theory>]
    [<InlineData("upper", "'alice'", "ALICE")>]
    [<InlineData("lower", "'ALICE'", "alice")>]
    member _.``Shared function mappings execute correctly in SQLite``
        (functionName: string, argument: string, expected: string) =
        use connection = TestFixtures.openTestDatabase()
        let expression =
            Nimblesite.Lql.SQLite.SQLiteFunctionMappingLocal.Instance.TranspileFunction(
                functionName = functionName, arguments = [| argument |])
        let rows = TestFixtures.executeQuery connection $"SELECT {expression} AS value"
        let row = Assert.Single(rows)
        Assert.Equal(expected, Assert.IsType<string>(row["value"]))

    // Implements [LQL-FSHARP-SHARED-EXECUTION-CASES].
    static member CountCases : seq<objnull array> =
        Nimblesite.Lql.Core.TypeProvider.TestInputs.executionCountCases

    [<Theory>]
    [<MemberData("CountCases")>]
    member _.``Named execution row counts``(scenario: string, sql: string, expected: int) =
        verifyNamedCase scenario (fun () -> verifyDatabaseRowCount sql expected ignore)

    static member NonEmptyCases : seq<objnull array> =
        Nimblesite.Lql.Core.TypeProvider.TestInputs.executionNonEmptyCases

    [<Theory>]
    [<MemberData("NonEmptyCases")>]
    member _.``Named nonempty execution results``(scenario: string, sql: string, keys: string array) =
        verifyNamedCase scenario (fun () ->
            use conn = TestFixtures.createTestDatabase()
            let results = TestFixtures.executeQuery conn sql
            Assert.True(results.Length > 0)
            for row in results do
                if keys.Length > 0 then
                    Assert.True(keys |> Array.exists row.ContainsKey))

    [<Fact>]
    member _.``Execute order by with limit``() =
        use conn = TestFixtures.createTestDatabase()
        let results = TestFixtures.executeQuery conn Limit.Sql
        Assert.True(results.Length <= 10)

[<Collection("TypeProvider")>]
type TypeProviderRealWorldScenarioTests() =

    [<Fact>]
    member _.``E2E: Query customers and execute against database``() =
        verifyDatabaseRowCount SelectAll.Sql 3 (fun results ->
            let names = results |> List.map (fun r -> r.["Name"] :?> string) |> Set.ofList
            Assert.Contains("Acme Corp", names)
            Assert.Contains("Tech Corp", names))

    [<Fact>]
    member _.``E2E: Filter active adult users``() =
        // Charlie is inactive; Bob and Diana are under 18.
        verifyDatabaseRowCount FilterComplex.Sql 1 ignore

    [<Fact>]
    member _.``E2E: Join users with orders and calculate totals``() =
        // Alice has 6 orders, Bob has 1.
        verifyDatabaseRowCount JoinSimple.Sql 7 ignore

    [<Fact>]
    member _.``E2E: Aggregate order totals by user``() =
        verifyDatabaseRowCount GroupBy.Sql 2 ignore

[<Collection("TypeProvider")>]
type TypeProviderQueryPropertyTests() =

    [<Fact>]
    member _.``Query property returns original LQL for all query types``() =
        // Verify each type provider generated type has correct Query property
        Assert.Equal("Customer |> select(*)", SelectAll.Query)
        Assert.Equal("Users |> select(Users.Id, Users.Name, Users.Email)", SelectColumns.Query)
        Assert.Equal("Users |> filter(fn(row) => row.Users.Age > 18) |> select(Users.Name)", FilterSimple.Query)
        Assert.Equal("Users |> join(Orders, on = Users.Id = Orders.UserId) |> select(Users.Name, Orders.Total)", JoinSimple.Query)
        Assert.Equal("Orders |> group_by(Orders.UserId) |> select(Orders.UserId, count(*) as order_count)", GroupBy.Query)

    [<Fact>]
    member _.``Sql property is never null or empty``() =
        Assert.False(String.IsNullOrWhiteSpace(SelectAll.Sql))
        Assert.False(String.IsNullOrWhiteSpace(SelectColumns.Sql))
        Assert.False(String.IsNullOrWhiteSpace(FilterSimple.Sql))
        Assert.False(String.IsNullOrWhiteSpace(JoinSimple.Sql))
        Assert.False(String.IsNullOrWhiteSpace(GroupBy.Sql))
        Assert.False(String.IsNullOrWhiteSpace(OrderBy.Sql))
        Assert.False(String.IsNullOrWhiteSpace(Limit.Sql))
