// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.Sync.Tests;

public partial class HashVerifierTests
{
    private static global::System.Collections.Generic.Dictionary<
        string,
        object?
    > Dataset_ToCanonicalJson_SortsKeysAlphabetically_1() =>
        new Dictionary<string, object?>
        {
            ["Zebra"] = "last",
            ["Apple"] = "first",
            ["Mango"] = "middle",
        };

    private static global::System.Collections.Generic.Dictionary<
        string,
        global::System.Collections.Generic.List<global::System.Collections.Generic.Dictionary<
            string,
            object?
        >>
    > Dataset_ComputeDatabaseHash_SameTables_SameHash_1() =>
        new Dictionary<string, List<Dictionary<string, object?>>>
        {
            ["Order"] = [new() { ["Id"] = "1", ["Total"] = 100 }],
            ["Person"] = [new() { ["Id"] = "1", ["Name"] = "Alice" }],
        };

    private static global::System.Collections.Generic.Dictionary<
        string,
        global::System.Collections.Generic.List<global::System.Collections.Generic.Dictionary<
            string,
            object?
        >>
    > Dataset_ComputeDatabaseHash_TablesProcessedAlphabetically_1() =>
        new Dictionary<string, List<Dictionary<string, object?>>>
        {
            ["Zebra"] = [new() { ["Id"] = "z1" }],
            ["Apple"] = [new() { ["Id"] = "a1" }],
        };
}
