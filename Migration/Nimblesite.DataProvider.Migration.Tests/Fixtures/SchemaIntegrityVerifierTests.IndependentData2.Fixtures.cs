// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Migration.Tests;

public partial class SchemaIntegrityVerifierTests
{
    private static global::Nimblesite.DataProvider.Migration.Core.SchemaDefinition Dataset_Verify_WhenRlsAndSupportObjectsAreExcluded_IgnoresThoseMismatches_1() =>
        SchemaTestData.Schema(
            name: "live",
            tables: [SchemaTestData.Table(schema: "public", name: "documents")]
        );
}
