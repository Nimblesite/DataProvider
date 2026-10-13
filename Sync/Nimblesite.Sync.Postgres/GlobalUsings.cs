global using Microsoft.Extensions.Logging;
global using Nimblesite.Sync.Core;
global using Npgsql;
// Type aliases for Result types - matching Nimblesite.Sync.SQLite patterns using Outcome package
global using BoolSyncError = Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>.Error<
    bool,
    Nimblesite.Sync.Core.SyncError
>;
global using BoolSyncOk = Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>.Ok<
    bool,
    Nimblesite.Sync.Core.SyncError
>;
global using BoolSyncResult = Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>;
global using ColumnInfoListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Postgres.TriggerColumnInfo>,
    Nimblesite.Sync.Core.SyncError
>.Error<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Postgres.TriggerColumnInfo>,
    Nimblesite.Sync.Core.SyncError
>;
global using ColumnInfoListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Postgres.TriggerColumnInfo>,
    Nimblesite.Sync.Core.SyncError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Postgres.TriggerColumnInfo>,
    Nimblesite.Sync.Core.SyncError
>;
global using ColumnInfoListResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Postgres.TriggerColumnInfo>,
    Nimblesite.Sync.Core.SyncError
>;
global using LongSyncError = Outcome.Result<long, Nimblesite.Sync.Core.SyncError>.Error<
    long,
    Nimblesite.Sync.Core.SyncError
>;
global using LongSyncOk = Outcome.Result<long, Nimblesite.Sync.Core.SyncError>.Ok<
    long,
    Nimblesite.Sync.Core.SyncError
>;
global using LongSyncResult = Outcome.Result<long, Nimblesite.Sync.Core.SyncError>;
global using StringSyncError = Outcome.Result<string, Nimblesite.Sync.Core.SyncError>.Error<
    string,
    Nimblesite.Sync.Core.SyncError
>;
global using StringSyncOk = Outcome.Result<string, Nimblesite.Sync.Core.SyncError>.Ok<
    string,
    Nimblesite.Sync.Core.SyncError
>;
global using StringSyncResult = Outcome.Result<string, Nimblesite.Sync.Core.SyncError>;
global using SyncClientListResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncClient>,
    Nimblesite.Sync.Core.SyncError
>;
global using SyncClientResult = Outcome.Result<
    Nimblesite.Sync.Core.SyncClient?,
    Nimblesite.Sync.Core.SyncError
>;
global using SyncLogListResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncLogEntry>,
    Nimblesite.Sync.Core.SyncError
>;
