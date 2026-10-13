// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaDiffTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Data_Calculate_CaseInsensitiveTableMatching_1() =>
        Schema
            .Define("Current")
            .Table("public", "USERS", t => t.Column("id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();
}
