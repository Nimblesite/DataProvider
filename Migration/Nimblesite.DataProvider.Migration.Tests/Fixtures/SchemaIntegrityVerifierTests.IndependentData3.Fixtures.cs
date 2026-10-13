// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaIntegrityVerifierTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Data_Verify_WhenPrimaryKeyIsMissing_ReturnsMissingPrimaryKey_1() =>
        Schema
            .Define("live")
            .Table("public", "accounts", table => table.Column("id", PortableTypes.Uuid))
            .Build();
}
