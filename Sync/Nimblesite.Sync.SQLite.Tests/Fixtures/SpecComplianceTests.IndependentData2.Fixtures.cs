// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.Sync.SQLite.Tests;

public partial class SpecComplianceTests
{
    private static global::System.Collections.Generic.Dictionary<
        string,
        object?
    > Dataset_Spec_S15_CanonicalJson_SortedKeys_1() =>
        new Dictionary<string, object?>
        {
            ["Zebra"] = "last",
            ["Alpha"] = "first",
            ["Middle"] = "middle",
        };
}
