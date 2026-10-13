module Nimblesite.Lql.Core.TypeProvider.TestInputs

open Nimblesite.Lql.Core.TypeProvider.TestQueries

// Implements [LQL-FSHARP-SHARED-COMPILE-TIME-CASES]. Independent named inputs only.
let fragmentCases : seq<objnull array> =
    [
        [| box "Type provider generates correct SQL for column selection"; box SelectColumns.Sql; box [| "SELECT"; "USERS" |] |]
        [| box "Type provider generates SQL with alias"; box SelectWithAlias.Sql; box [| "AS" |] |]
        [| box "Filter query generates WHERE clause"; box FilterSimple.Sql; box [| "WHERE" |] |]
        [| box "Complex filter with AND generates correct SQL"; box FilterComplex.Sql; box [| "WHERE"; "AND" |] |]
        [| box "Filter with OR generates correct SQL"; box FilterOr.Sql; box [| "WHERE"; "OR" |] |]
        [| box "Simple join generates JOIN clause"; box JoinSimple.Sql; box [| "JOIN"; "ON" |] |]
        [| box "Left join generates LEFT JOIN clause"; box JoinLeft.Sql; box [| "LEFT"; "JOIN" |] |]
        [| box "Group by generates GROUP BY clause"; box GroupBy.Sql; box [| "GROUP BY"; "COUNT" |] |]
        [| box "Multiple aggregates work correctly"; box Aggregates.Sql; box [| "SUM"; "AVG" |] |]
        [| box "Having clause generates HAVING"; box Having.Sql; box [| "HAVING" |] |]
        [| box "Order by generates ORDER BY clause"; box OrderBy.Sql; box [| "ORDER BY" |] |]
        [| box "Order by desc includes DESC"; box OrderByDesc.Sql; box [| "DESC" |] |]
        [| box "Limit generates LIMIT clause"; box Limit.Sql; box [| "LIMIT" |] |]
        [| box "Offset generates OFFSET clause"; box Offset.Sql; box [| "OFFSET" |] |]
        [| box "Basic arithmetic in select"; box ArithmeticBasic.Sql; box [| "*" |] |]
        [| box "Complex arithmetic with multiple operators"; box ArithmeticComplex.Sql; box [| "+"; "-" |] |]
    ]

// Implements [LQL-FSHARP-SHARED-EXECUTION-CASES]. Original independent expectations.
let executionCountCases : seq<objnull array> =
    [
        [| box "Execute simple select against real SQLite database"; box SelectAll.Sql; box 3 |]
        [| box "Execute filter query and verify results"; box FilterSimple.Sql; box 2 |]
    ]

let executionNonEmptyCases : seq<objnull array> =
    [
        [| box "Execute join query and verify results"; box JoinSimple.Sql; box ([||] : string array) |]
        [| box "Execute group by query and verify aggregation"; box GroupBy.Sql; box [| "order_count"; "COUNT(*)" |] |]
        [| box "Execute having query and verify filtering on aggregates"; box Having.Sql; box ([||] : string array) |]
        [| box "Execute arithmetic expression query"; box ArithmeticBasic.Sql; box [| "total" |] |]
    ]
