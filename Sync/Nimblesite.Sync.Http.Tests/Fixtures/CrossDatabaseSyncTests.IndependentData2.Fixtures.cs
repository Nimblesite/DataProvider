// Implements [CI-DESLOP-INDEPENDENT-DATA].

namespace Nimblesite.Sync.Http.Tests;

public partial class CrossDatabaseSyncTests
{
    private static global::System.InvalidOperationException Dataset_CreatePostgresDb_1(
        global::Outcome.Result<
            global::System.Boolean,
            global::Nimblesite.Sync.Core.SyncError
        > schemaResult
    ) => new InvalidOperationException($"Failed to create Postgres schema: {schemaResult}");
}
