global using Microsoft.Data.Sqlite;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Logging.Abstractions;
global using Nimblesite.DataProvider.Migration.Core;
global using Nimblesite.DataProvider.Migration.Postgres;
global using Nimblesite.DataProvider.Migration.SQLite;
global using Nimblesite.TestSupport;
global using Npgsql;
global using Xunit;
global using MigrationApplyResult = Outcome.Result<
    bool,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;
global using MigrationApplyResultError = Outcome.Result<
    bool,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Error<bool, Nimblesite.DataProvider.Migration.Core.MigrationError>;
global using MigrationApplyResultOk = Outcome.Result<
    bool,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<bool, Nimblesite.DataProvider.Migration.Core.MigrationError>;
global using OperationsResultOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Migration.Core.SchemaOperation>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Migration.Core.SchemaOperation>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;
// Type aliases for Result types per CLAUDE.md
global using SchemaResultOk = Outcome.Result<
    Nimblesite.DataProvider.Migration.Core.SchemaDefinition,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>.Ok<
    Nimblesite.DataProvider.Migration.Core.SchemaDefinition,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;
