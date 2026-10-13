// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.DataProvider.Example.Tests;

public partial class DataProviderIntegrationTests
{
    private static global::System.InvalidOperationException Dataset_GetInvoicesAsync_WithValidData_ReturnsCorrectTypes_1(
        global::Outcome.Result<
            global::System.Collections.Immutable.ImmutableList<global::Generated.Invoice>,
            global::Nimblesite.Sql.Model.SqlError
        >.Error<
            global::System.Collections.Immutable.ImmutableList<global::Generated.Invoice>,
            global::Nimblesite.Sql.Model.SqlError
        > failure
    ) => new InvalidOperationException($"GetInvoicesAsync failed: {failure.Value.Message}");
}
