// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.Sync.Integration.Tests;

public partial class HttpMappingSyncTests
{
    private static global::System.InvalidOperationException Dataset_CreateTargetDb_1(
        global::Outcome.Result<
            global::System.Boolean,
            global::Nimblesite.Sync.Core.SyncError
        > schemaResult
    ) => new InvalidOperationException($"Failed to create Postgres schema: {schemaResult}");
}
