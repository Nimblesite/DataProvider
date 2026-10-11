global using Nimblesite.Sync.Core;
// Type aliases for Result types to reduce verbosity in Nimblesite.Sync.SQLite
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
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.SQLite.TriggerColumnInfo>,
    Nimblesite.Sync.Core.SyncError
>.Error<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.SQLite.TriggerColumnInfo>,
    Nimblesite.Sync.Core.SyncError
>;
global using ColumnInfoListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.SQLite.TriggerColumnInfo>,
    Nimblesite.Sync.Core.SyncError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.SQLite.TriggerColumnInfo>,
    Nimblesite.Sync.Core.SyncError
>;
global using ColumnInfoListResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.SQLite.TriggerColumnInfo>,
    Nimblesite.Sync.Core.SyncError
>;
global using IntSyncResult = Outcome.Result<int, Nimblesite.Sync.Core.SyncError>;
global using LongSyncError = Outcome.Result<long, Nimblesite.Sync.Core.SyncError>.Error<
    long,
    Nimblesite.Sync.Core.SyncError
>;
global using LongSyncOk = Outcome.Result<long, Nimblesite.Sync.Core.SyncError>.Ok<
    long,
    Nimblesite.Sync.Core.SyncError
>;
global using LongSyncResult = Outcome.Result<long, Nimblesite.Sync.Core.SyncError>;
global using MappingStateError = Outcome.Result<
    Nimblesite.Sync.Core.MappingStateEntry?,
    Nimblesite.Sync.Core.SyncError
>.Error<Nimblesite.Sync.Core.MappingStateEntry?, Nimblesite.Sync.Core.SyncError>;
global using MappingStateListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.MappingStateEntry>,
    Nimblesite.Sync.Core.SyncError
>.Error<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.MappingStateEntry>,
    Nimblesite.Sync.Core.SyncError
>;
global using MappingStateListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.MappingStateEntry>,
    Nimblesite.Sync.Core.SyncError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.MappingStateEntry>,
    Nimblesite.Sync.Core.SyncError
>;
global using MappingStateListResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.MappingStateEntry>,
    Nimblesite.Sync.Core.SyncError
>;
global using MappingStateOk = Outcome.Result<
    Nimblesite.Sync.Core.MappingStateEntry?,
    Nimblesite.Sync.Core.SyncError
>.Ok<Nimblesite.Sync.Core.MappingStateEntry?, Nimblesite.Sync.Core.SyncError>;
global using MappingStateResult = Outcome.Result<
    Nimblesite.Sync.Core.MappingStateEntry?,
    Nimblesite.Sync.Core.SyncError
>;
global using RecordHashError = Outcome.Result<
    Nimblesite.Sync.Core.RecordHashEntry?,
    Nimblesite.Sync.Core.SyncError
>.Error<Nimblesite.Sync.Core.RecordHashEntry?, Nimblesite.Sync.Core.SyncError>;
global using RecordHashOk = Outcome.Result<
    Nimblesite.Sync.Core.RecordHashEntry?,
    Nimblesite.Sync.Core.SyncError
>.Ok<Nimblesite.Sync.Core.RecordHashEntry?, Nimblesite.Sync.Core.SyncError>;
global using RecordHashResult = Outcome.Result<
    Nimblesite.Sync.Core.RecordHashEntry?,
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
global using StringSyncResult = Outcome.Result<string, Nimblesite.Sync.Core.SyncError>;
global using SubscriptionError = Outcome.Result<
    Nimblesite.Sync.Core.SyncSubscription?,
    Nimblesite.Sync.Core.SyncError
>.Error<Nimblesite.Sync.Core.SyncSubscription?, Nimblesite.Sync.Core.SyncError>;
global using SubscriptionListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncSubscription>,
    Nimblesite.Sync.Core.SyncError
>.Error<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncSubscription>,
    Nimblesite.Sync.Core.SyncError
>;
global using SubscriptionListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncSubscription>,
    Nimblesite.Sync.Core.SyncError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncSubscription>,
    Nimblesite.Sync.Core.SyncError
>;
global using SubscriptionListResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncSubscription>,
    Nimblesite.Sync.Core.SyncError
>;
global using SubscriptionOk = Outcome.Result<
    Nimblesite.Sync.Core.SyncSubscription?,
    Nimblesite.Sync.Core.SyncError
>.Ok<Nimblesite.Sync.Core.SyncSubscription?, Nimblesite.Sync.Core.SyncError>;
global using SubscriptionResult = Outcome.Result<
    Nimblesite.Sync.Core.SyncSubscription?,
    Nimblesite.Sync.Core.SyncError
>;
global using SyncClientListResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncClient>,
    Nimblesite.Sync.Core.SyncError
>;
global using SyncClientResult = Outcome.Result<
    Nimblesite.Sync.Core.SyncClient?,
    Nimblesite.Sync.Core.SyncError
>;
global using SyncLogListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncLogEntry>,
    Nimblesite.Sync.Core.SyncError
>.Error<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncLogEntry>,
    Nimblesite.Sync.Core.SyncError
>;
global using SyncLogListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncLogEntry>,
    Nimblesite.Sync.Core.SyncError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncLogEntry>,
    Nimblesite.Sync.Core.SyncError
>;
global using SyncLogListResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.SyncLogEntry>,
    Nimblesite.Sync.Core.SyncError
>;
