global using Generated;
global using Nimblesite.DataProvider.Core;
// Type aliases for Result types to reduce verbosity in Nimblesite.DataProvider.Example.Tests
global using CustomerListError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.Customer>,
    Nimblesite.Sql.Model.SqlError
>.Error<
    System.Collections.Immutable.ImmutableList<Generated.Customer>,
    Nimblesite.Sql.Model.SqlError
>;
global using CustomerListOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.Customer>,
    Nimblesite.Sql.Model.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.Customer>, Nimblesite.Sql.Model.SqlError>;
global using InvoiceListError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.Invoice>,
    Nimblesite.Sql.Model.SqlError
>.Error<
    System.Collections.Immutable.ImmutableList<Generated.Invoice>,
    Nimblesite.Sql.Model.SqlError
>;
global using InvoiceListOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.Invoice>,
    Nimblesite.Sql.Model.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.Invoice>, Nimblesite.Sql.Model.SqlError>;
global using OrderListOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.Order>,
    Nimblesite.Sql.Model.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.Order>, Nimblesite.Sql.Model.SqlError>;
global using OrderReadOnlyListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Generated.Order>,
    Nimblesite.Sql.Model.SqlError
>.Ok<System.Collections.Generic.IReadOnlyList<Generated.Order>, Nimblesite.Sql.Model.SqlError>;
global using StringSqlError = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Error<
    string,
    Nimblesite.Sql.Model.SqlError
>;
global using StringSqlOk = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Ok<
    string,
    Nimblesite.Sql.Model.SqlError
>;
