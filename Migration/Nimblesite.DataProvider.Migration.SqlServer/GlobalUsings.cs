global using System.Globalization;
global using System.Text;
global using Microsoft.Data.SqlClient;
global using Microsoft.Extensions.Logging;
global using Nimblesite.DataProvider.Migration.Core;
// Type aliases
global using SchemaResult = Outcome.Result<
    Nimblesite.DataProvider.Migration.Core.SchemaDefinition,
    Nimblesite.DataProvider.Migration.Core.MigrationError
>;
