global using Nimblesite.DataProvider.Core;
global using Nimblesite.DataProvider.Core.CodeGeneration;
global using Xunit;
// Global usings for Nimblesite.DataProvider.Tests
global using IntError = Outcome.Result<int, Nimblesite.Sql.Model.SqlError>.Error<
    int,
    Nimblesite.Sql.Model.SqlError
>;
// Result type aliases for tests

global using NullableStringOk = Outcome.Result<string?, Nimblesite.Sql.Model.SqlError>.Ok<
    string?,
    Nimblesite.Sql.Model.SqlError
>;
global using SqlError = Nimblesite.Sql.Model.SqlError;
global using StringError = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Error<
    string,
    Nimblesite.Sql.Model.SqlError
>;
global using StringOk = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Ok<
    string,
    Nimblesite.Sql.Model.SqlError
>;
