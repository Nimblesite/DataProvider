// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.Sync.SQLite.Tests;

public partial class SpecConformanceTests
{
    private static global::System.Collections.Generic.Dictionary<
        string,
        object?
    > Dataset_Spec15_2_CanonicalJson_KeysSortedAlphabetically_1() =>
        new Dictionary<string, object?>
        {
            ["Zebra"] = "z",
            ["Alpha"] = "a",
            ["Middle"] = "m",
        };

    private static global::System.Collections.Generic.List<global::System.Collections.Generic.Dictionary<
        string,
        object?
    >> Dataset_Spec15_3_DatabaseHash_IsConsistent_1() =>
        new List<Dictionary<string, object?>>
        {
            new() { ["Id"] = "1", ["Name"] = "Alice" },
            new() { ["Id"] = "2", ["Name"] = "Bob" },
        };
}
