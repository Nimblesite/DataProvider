module Nimblesite.Lql.Core.TypeProvider.Tests

open System
open System.IO
open Microsoft.Data.Sqlite
open Xunit
open Lql
open Nimblesite.Lql.Core.TypeProvider
open Nimblesite.Lql.TypeProvider.FSharp.Tests.Data

// =============================================================================
// COMPILE-TIME VALIDATED LQL QUERIES
// These are validated at COMPILE TIME by the F# type provider
// Invalid LQL will cause a COMPILATION ERROR, not a runtime error
// =============================================================================

// Basic select queries
type SelectAll = LqlCommand<"Customer |> select(*)">
type SelectColumns = LqlCommand<"Users |> select(Users.Id, Users.Name, Users.Email)">
type SelectWithAlias = LqlCommand<"Users |> select(Users.Id, Users.Name as username)">

// Filter queries
type FilterSimple = LqlCommand<"Users |> filter(fn(row) => row.Users.Age > 18) |> select(Users.Name)">
type FilterComplex = LqlCommand<"Users |> filter(fn(row) => row.Users.Age > 18 and row.Users.Status = 'active') |> select(*)">
type FilterOr = LqlCommand<"Users |> filter(fn(row) => row.Users.Age < 18 or row.Users.Role = 'admin') |> select(*)">

// Join queries
type JoinSimple = LqlCommand<"Users |> join(Orders, on = Users.Id = Orders.UserId) |> select(Users.Name, Orders.Total)">
type JoinLeft = LqlCommand<"Users |> left_join(Orders, on = Users.Id = Orders.UserId) |> select(Users.Name, Orders.Total)">
type JoinMultiple = LqlCommand<"Users |> join(Orders, on = Users.Id = Orders.UserId) |> join(Products, on = Orders.ProductId = Products.Id) |> select(Users.Name, Products.Name)">

// Aggregation queries
type GroupBy = LqlCommand<"Orders |> group_by(Orders.UserId) |> select(Orders.UserId, count(*) as order_count)">
type Aggregates = LqlCommand<"Orders |> group_by(Orders.Status) |> select(Orders.Status, sum(Orders.Total) as total_sum, avg(Orders.Total) as avg_total)">
type Having = LqlCommand<"Orders |> group_by(Orders.UserId) |> having(fn(g) => count(*) > 5) |> select(Orders.UserId, count(*) as cnt)">

// Order and limit
type OrderBy = LqlCommand<"Users |> order_by(Users.Name asc) |> select(*)">
type OrderByDesc = LqlCommand<"Users |> order_by(Users.CreatedAt desc) |> select(*)">
type Limit = LqlCommand<"Users |> order_by(Users.Id) |> limit(10) |> select(*)">
type Offset = LqlCommand<"Users |> order_by(Users.Id) |> limit(10) |> offset(20) |> select(*)">

// Arithmetic expressions
type ArithmeticBasic = LqlCommand<"Products |> select(Products.Price * Products.Quantity as total)">
type ArithmeticComplex = LqlCommand<"Orders |> select(Orders.Subtotal + Orders.Tax - Orders.Discount as final_total)">

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

// =============================================================================
// E2E TESTS - Comprehensive tests for the F# Type Provider
// =============================================================================

[<Collection("TypeProvider")>]
type TypeProviderCompileTimeValidationTests() =

    [<Fact>]
    member _.``Type provider generates Query property for simple select``() =
        Assert.Equal("Customer |> select(*)", SelectAll.Query)

    [<Fact>]
    member _.``Type provider generates Sql property for simple select``() =
        Assert.NotNull(SelectAll.Sql)
        Assert.Contains("SELECT", SelectAll.Sql.ToUpperInvariant())

    [<Fact>]
    member _.``Type provider generates correct SQL for column selection``() =
        let sql = SelectColumns.Sql.ToUpperInvariant()
        Assert.Contains("SELECT", sql)
        Assert.Contains("USERS", sql)

    [<Fact>]
    member _.``Type provider generates SQL with alias``() =
        let sql = SelectWithAlias.Sql
        Assert.Contains("AS", sql.ToUpperInvariant())

[<Collection("TypeProvider")>]
type TypeProviderFilterTests() =

    [<Fact>]
    member _.``Filter query generates WHERE clause``() =
        let sql = FilterSimple.Sql.ToUpperInvariant()
        Assert.Contains("WHERE", sql)

    [<Fact>]
    member _.``Complex filter with AND generates correct SQL``() =
        let sql = FilterComplex.Sql.ToUpperInvariant()
        Assert.Contains("WHERE", sql)
        Assert.Contains("AND", sql)

    [<Fact>]
    member _.``Filter with OR generates correct SQL``() =
        let sql = FilterOr.Sql.ToUpperInvariant()
        Assert.Contains("WHERE", sql)
        Assert.Contains("OR", sql)

[<Collection("TypeProvider")>]
type TypeProviderJoinTests() =

    [<Fact>]
    member _.``Simple join generates JOIN clause``() =
        let sql = JoinSimple.Sql.ToUpperInvariant()
        Assert.Contains("JOIN", sql)
        Assert.Contains("ON", sql)

    [<Fact>]
    member _.``Left join generates LEFT JOIN clause``() =
        let sql = JoinLeft.Sql.ToUpperInvariant()
        Assert.Contains("LEFT", sql)
        Assert.Contains("JOIN", sql)

    [<Fact>]
    member _.``Multiple joins are chained correctly``() =
        let sql = JoinMultiple.Sql.ToUpperInvariant()
        // Should have at least 2 JOINs
        let joinCount = sql.Split([|"JOIN"|], StringSplitOptions.None).Length - 1
        Assert.True(joinCount >= 2, sprintf "Expected at least 2 JOINs but got %d" joinCount)

[<Collection("TypeProvider")>]
type TypeProviderAggregationTests() =

    [<Fact>]
    member _.``Group by generates GROUP BY clause``() =
        let sql = GroupBy.Sql.ToUpperInvariant()
        Assert.Contains("GROUP BY", sql)
        Assert.Contains("COUNT", sql)

    [<Fact>]
    member _.``Multiple aggregates work correctly``() =
        let sql = Aggregates.Sql.ToUpperInvariant()
        Assert.Contains("SUM", sql)
        Assert.Contains("AVG", sql)

    [<Fact>]
    member _.``Having clause generates HAVING``() =
        let sql = Having.Sql.ToUpperInvariant()
        Assert.Contains("HAVING", sql)

[<Collection("TypeProvider")>]
type TypeProviderOrderingTests() =

    [<Fact>]
    member _.``Order by generates ORDER BY clause``() =
        let sql = OrderBy.Sql.ToUpperInvariant()
        Assert.Contains("ORDER BY", sql)

    [<Fact>]
    member _.``Order by desc includes DESC``() =
        let sql = OrderByDesc.Sql.ToUpperInvariant()
        Assert.Contains("DESC", sql)

    [<Fact>]
    member _.``Limit generates LIMIT clause``() =
        let sql = Limit.Sql.ToUpperInvariant()
        Assert.Contains("LIMIT", sql)

    [<Fact>]
    member _.``Offset generates OFFSET clause``() =
        let sql = Offset.Sql.ToUpperInvariant()
        Assert.Contains("OFFSET", sql)

[<Collection("TypeProvider")>]
type TypeProviderArithmeticTests() =

    [<Fact>]
    member _.``Basic arithmetic in select``() =
        let sql = ArithmeticBasic.Sql
        Assert.Contains("*", sql) // multiplication

    [<Fact>]
    member _.``Complex arithmetic with multiple operators``() =
        let sql = ArithmeticComplex.Sql
        Assert.Contains("+", sql)
        Assert.Contains("-", sql)

[<Collection("TypeProvider")>]
type TypeProviderE2EExecutionTests() =

    [<Fact>]
    member _.``Execute simple select against real SQLite database``() =
        use conn = TestFixtures.createTestDatabase()
        let results = TestFixtures.executeQuery conn SelectAll.Sql
        Assert.Equal(3, results.Length)

    [<Fact>]
    member _.``Execute filter query and verify results``() =
        use conn = TestFixtures.createTestDatabase()
        let results = TestFixtures.executeQuery conn FilterSimple.Sql
        // Should return users with age > 18 (Alice=30, Charlie=25)
        Assert.Equal(2, results.Length)

    [<Fact>]
    member _.``Execute join query and verify results``() =
        use conn = TestFixtures.createTestDatabase()
        let results = TestFixtures.executeQuery conn JoinSimple.Sql
        // Should return joined user-order records
        Assert.True(results.Length > 0)

    [<Fact>]
    member _.``Execute group by query and verify aggregation``() =
        use conn = TestFixtures.createTestDatabase()
        let results = TestFixtures.executeQuery conn GroupBy.Sql
        // Should have aggregated results
        Assert.True(results.Length > 0)
        for row in results do
            Assert.True(row.ContainsKey("order_count") || row.ContainsKey("COUNT(*)"))

    [<Fact>]
    member _.``Execute having query and verify filtering on aggregates``() =
        use conn = TestFixtures.createTestDatabase()
        let results = TestFixtures.executeQuery conn Having.Sql
        // User 1 has 6 orders, which is > 5
        Assert.True(results.Length > 0)

    [<Fact>]
    member _.``Execute order by with limit``() =
        use conn = TestFixtures.createTestDatabase()
        let results = TestFixtures.executeQuery conn Limit.Sql
        Assert.True(results.Length <= 10)

    [<Fact>]
    member _.``Execute arithmetic expression query``() =
        use conn = TestFixtures.createTestDatabase()
        let results = TestFixtures.executeQuery conn ArithmeticBasic.Sql
        Assert.True(results.Length > 0)
        // Verify the computed column exists
        for row in results do
            Assert.True(row.ContainsKey("total"))

[<Collection("TypeProvider")>]
type TypeProviderRealWorldScenarioTests() =

    [<Fact>]
    member _.``E2E: Query customers and execute against database``() =
        use conn = TestFixtures.createTestDatabase()

        // Use the type provider validated query
        let sql = SelectAll.Sql
        let results = TestFixtures.executeQuery conn sql

        // Verify we got all customers
        Assert.Equal(3, results.Length)

        // Verify customer data
        let names = results |> List.map (fun r -> r.["Name"] :?> string) |> Set.ofList
        Assert.Contains("Acme Corp", names)
        Assert.Contains("Tech Corp", names)

    [<Fact>]
    member _.``E2E: Filter active adult users``() =
        use conn = TestFixtures.createTestDatabase()

        // The type provider validates this at compile time
        let sql = FilterComplex.Sql
        let results = TestFixtures.executeQuery conn sql

        // Should only get Alice (age 30, active)
        // Charlie is inactive, Bob and Diana are under 18
        Assert.Equal(1, results.Length)

    [<Fact>]
    member _.``E2E: Join users with orders and calculate totals``() =
        use conn = TestFixtures.createTestDatabase()

        let sql = JoinSimple.Sql
        let results = TestFixtures.executeQuery conn sql

        // Alice has 6 orders, Bob has 1
        Assert.Equal(7, results.Length)

    [<Fact>]
    member _.``E2E: Aggregate order totals by user``() =
        use conn = TestFixtures.createTestDatabase()

        let sql = GroupBy.Sql
        let results = TestFixtures.executeQuery conn sql

        // Should have 2 users with orders (user 1 and user 2)
        Assert.Equal(2, results.Length)

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
