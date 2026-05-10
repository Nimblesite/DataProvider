global using System.Collections.Immutable;
global using System.Data;
global using Microsoft.Extensions.Logging;
global using MigrationApplyResult = Outcome.Result<
    bool,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;
global using OperationsResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.DataProvider.Migration.Core.SchemaOperation>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;
global using SchemaIntegrityResult = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<string>,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;
// Type aliases for Result types per CLAUDE.md
