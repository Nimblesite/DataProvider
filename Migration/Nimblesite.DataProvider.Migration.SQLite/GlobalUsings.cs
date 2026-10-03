global using System.Data;
global using System.Text;
global using Microsoft.Data.Sqlite;
global using Microsoft.Extensions.Logging;
global using Nimblesite.DataProvider.Migration.Core;
global using MigrationApplyResult = Outcome.Result<
    bool,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;
// Type aliases
global using SchemaResult = Outcome.Result<
    Nimblesite.DataProvider.Migration.Core.SchemaDefinition,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;
global using TableResult = Outcome.Result<
    Nimblesite.DataProvider.Migration.Core.TableDefinition,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;
