module Nimblesite.Lql.Core.TypeProvider.TestQueries

open Lql

// Implements [LQL-FSHARP-SHARED-COMPILE-TIME-CASES].
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

