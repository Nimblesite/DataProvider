global using Microsoft.Data.Sqlite;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Logging.Abstractions;
global using Nimblesite.Sync.Core;
global using Nimblesite.Sync.SQLite;
global using Nimblesite.TestSupport;
global using Npgsql;
global using Xunit;
// Type aliases for Result types - matching Sync patterns using Outcome package

global using BoolSyncOk = Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>.Ok<
    bool,
    Nimblesite.Sync.Core.SyncError
>;
global using SyncClientListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncClient>,
    Nimblesite.Sync.Core.SyncError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncClient>,
    Nimblesite.Sync.Core.SyncError
>;
global using SyncLogListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncLogEntry>,
    Nimblesite.Sync.Core.SyncError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncLogEntry>,
    Nimblesite.Sync.Core.SyncError
>;
