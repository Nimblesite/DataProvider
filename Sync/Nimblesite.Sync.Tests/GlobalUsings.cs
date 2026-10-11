global using Nimblesite.Sync.Core;
global using Xunit;
// Type aliases for Outcome Result types to simplify test assertions
global using BatchApplyResultError = Outcome.Result<
    Nimblesite.Sync.Core.BatchApplyResult,
    Nimblesite.Sync.Core.SyncError
>.Error<Nimblesite.Sync.Core.BatchApplyResult, Nimblesite.Sync.Core.SyncError>;
global using BatchApplyResultOk = Outcome.Result<
    Nimblesite.Sync.Core.BatchApplyResult,
    Nimblesite.Sync.Core.SyncError
>.Ok<Nimblesite.Sync.Core.BatchApplyResult, Nimblesite.Sync.Core.SyncError>;
global using BoolSyncError = Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>.Error<
    bool,
    Nimblesite.Sync.Core.SyncError
>;
global using BoolSyncOk = Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>.Ok<
    bool,
    Nimblesite.Sync.Core.SyncError
>;
global using BoolSyncResult = Outcome.Result<bool, Nimblesite.Sync.Core.SyncError>;
global using ConflictResolutionError = Outcome.Result<
    Nimblesite.Sync.Core.ConflictResolution,
    Nimblesite.Sync.Core.SyncError
>.Error<Nimblesite.Sync.Core.ConflictResolution, Nimblesite.Sync.Core.SyncError>;
global using IntSyncOk = Outcome.Result<int, Nimblesite.Sync.Core.SyncError>.Ok<
    int,
    Nimblesite.Sync.Core.SyncError
>;
global using PullResultError = Outcome.Result<
    Nimblesite.Sync.Core.PullResult,
    Nimblesite.Sync.Core.SyncError
>.Error<Nimblesite.Sync.Core.PullResult, Nimblesite.Sync.Core.SyncError>;
// SyncCoordinator result types

global using PushResultError = Outcome.Result<
    Nimblesite.Sync.Core.PushResult,
    Nimblesite.Sync.Core.SyncError
>.Error<Nimblesite.Sync.Core.PushResult, Nimblesite.Sync.Core.SyncError>;
global using SyncLogEntryError = Outcome.Result<
    Nimblesite.Sync.Core.SyncLogEntry,
    Nimblesite.Sync.Core.SyncError
>.Error<Nimblesite.Sync.Core.SyncLogEntry, Nimblesite.Sync.Core.SyncError>;
global using SyncLogEntryOk = Outcome.Result<
    Nimblesite.Sync.Core.SyncLogEntry,
    Nimblesite.Sync.Core.SyncError
>.Ok<Nimblesite.Sync.Core.SyncLogEntry, Nimblesite.Sync.Core.SyncError>;
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
global using SyncResultError = Outcome.Result<
    Nimblesite.Sync.Core.SyncResult,
    Nimblesite.Sync.Core.SyncError
>.Error<Nimblesite.Sync.Core.SyncResult, Nimblesite.Sync.Core.SyncError>;
