global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Logging.Abstractions;
global using Nimblesite.Sync.Core;
// Type aliases for Result types in tests

global using BoolSyncError = Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>.Error<
    bool,
    Nimblesite.Sync.Core.SyncError
>;
global using BoolSyncOk = Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>.Ok<
    bool,
    Nimblesite.Sync.Core.SyncError
>;
global using BoolSyncResult = Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>;
global using ColumnInfoListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.SQLite.TriggerColumnInfo>,
    Nimblesite.Sync.Core.SyncError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.SQLite.TriggerColumnInfo>,
    Nimblesite.Sync.Core.SyncError
>;
global using IntSyncOk = Outcome.Result<int, Nimblesite.Sync.Core.SyncError>.Ok<
    int,
    Nimblesite.Sync.Core.SyncError
>;
global using LongSyncOk = Outcome.Result<long, Nimblesite.Sync.Core.SyncError>.Ok<
    long,
    Nimblesite.Sync.Core.SyncError
>;
global using StringSyncError = Outcome.Result<string, Nimblesite.Sync.Core.SyncError>.Error<
    string,
    Nimblesite.Sync.Core.SyncError
>;
global using StringSyncOk = Outcome.Result<string, Nimblesite.Sync.Core.SyncError>.Ok<
    string,
    Nimblesite.Sync.Core.SyncError
>;
global using SubscriptionListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncSubscription>,
    Nimblesite.Sync.Core.SyncError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncSubscription>,
    Nimblesite.Sync.Core.SyncError
>;
global using SubscriptionOk = Outcome.Result<
    Nimblesite.Sync.Core.SyncSubscription?,
    Nimblesite.Sync.Core.SyncError
>.Ok<Nimblesite.Sync.Core.SyncSubscription?, Nimblesite.Sync.Core.SyncError>;
global using SyncClientListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncClient>,
    Nimblesite.Sync.Core.SyncError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncClient>,
    Nimblesite.Sync.Core.SyncError
>;
global using SyncClientOk = Outcome.Result<
    Nimblesite.Sync.Core.SyncClient?,
    Nimblesite.Sync.Core.SyncError
>.Ok<Nimblesite.Sync.Core.SyncClient?, Nimblesite.Sync.Core.SyncError>;
global using SyncLogListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncLogEntry>,
    Nimblesite.Sync.Core.SyncError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncLogEntry>,
    Nimblesite.Sync.Core.SyncError
>;
