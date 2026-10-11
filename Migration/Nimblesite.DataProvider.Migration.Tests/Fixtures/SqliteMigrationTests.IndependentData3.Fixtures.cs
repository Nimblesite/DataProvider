// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SqliteMigrationTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Data_UpgradeExistingDatabase_AddTable_Success_1() =>
        Schema
            .Define("Test")
            .Table("Users", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Data_Destructive_DropTable_BlockedByDefault_1() =>
        Schema
            .Define("Test")
            .Table("Users", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Table("Products", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Data_Destructive_DropTable_BlockedByDefault_2() =>
        Schema
            .Define("Test")
            .Table("Users", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Data_Destructive_DropTable_AllowedWithOption_1() =>
        Schema
            .Define("Test")
            .Table("Users", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Table("Products", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();

    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Data_Destructive_DropTable_AllowedWithOption_2() =>
        Schema
            .Define("Test")
            .Table("Users", t => t.Column("Id", PortableTypes.Uuid, c => c.PrimaryKey()))
            .Build();
}
