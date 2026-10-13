// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class MigrationCornerCaseTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Data_TableWithOnlyPrimaryKey_Success_1() =>
        Schema
            .Define("Test")
            .Table("Simple", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Data_UpgradeFrom_EmptyTable_ToFullSchema_Success_1() =>
        Schema
            .Define("Test")
            .Table("Products", t => t.Column("Id", PortableTypes.Int, c => c.PrimaryKey()))
            .Build();
}
