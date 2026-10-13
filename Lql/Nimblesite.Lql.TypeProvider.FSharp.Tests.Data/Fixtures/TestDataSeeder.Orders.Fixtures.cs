using System.Collections.Immutable;

namespace Nimblesite.Lql.TypeProvider.FSharp.Tests.Data;

// Implements [LQL-FSHARP-SHARED-SEED-ORDERS]. Independent seed inputs only.
public static partial class TestDataSeeder
{
    private static ImmutableArray<(
        string Id,
        string User,
        string Product,
        double Total,
        double Subtotal,
        double Tax,
        string Status
    )> OrderInputs() =>
        [
            ("o1", "u1", "p1", 100.00, 90.00, 10.00, "completed"),
            ("o2", "u1", "p2", 50.00, 45.00, 5.00, "completed"),
            ("o3", "u1", "p1", 75.00, 68.00, 7.00, "pending"),
            ("o4", "u1", "p3", 25.00, 22.50, 2.50, "completed"),
            ("o5", "u1", "p2", 125.00, 112.50, 12.50, "completed"),
            ("o6", "u1", "p1", 200.00, 180.00, 20.00, "pending"),
            ("o7", "u2", "p3", 30.00, 27.00, 3.00, "completed"),
        ];
}
