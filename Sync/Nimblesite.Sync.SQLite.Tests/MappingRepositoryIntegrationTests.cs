using Microsoft.Data.Sqlite;
using Xunit;
using MappingStateListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.MappingStateEntry>,
    Nimblesite.Sync.Core.SyncError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.MappingStateEntry>,
    Nimblesite.Sync.Core.SyncError
>;
using MappingStateOk = Outcome.Result<
    Nimblesite.Sync.Core.MappingStateEntry?,
    Nimblesite.Sync.Core.SyncError
>.Ok<Nimblesite.Sync.Core.MappingStateEntry?, Nimblesite.Sync.Core.SyncError>;
using RecordHashOk = Outcome.Result<
    Nimblesite.Sync.Core.RecordHashEntry?,
    Nimblesite.Sync.Core.SyncError
>.Ok<Nimblesite.Sync.Core.RecordHashEntry?, Nimblesite.Sync.Core.SyncError>;

namespace Nimblesite.Sync.SQLite.Tests;

/// <summary>
/// Mapping state and record hash persistence (spec Section 7.5.2) on a real SQLite file.
/// </summary>
public sealed class MappingRepositoryIntegrationTests : IDisposable
{
    private const string Timestamp = "2025-01-01T00:00:00.000Z";
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"mappingrepository_{Guid.NewGuid():N}.db"
    );
    private readonly SqliteConnection _db;

    public MappingRepositoryIntegrationTests()
    {
        _db = new SqliteConnection($"Data Source={_dbPath}");
        _db.Open();
        Assert.IsType<BoolSyncOk>(SyncSchema.CreateSchema(_db));
    }

    [Fact]
    public void MappingState_UpsertGetListAndDelete_RoundTripsExactValues()
    {
        Assert.Null(
            Assert.IsType<MappingStateOk>(MappingRepository.GetMappingState(_db, "b")).Value
        );

        Assert.IsType<BoolSyncOk>(
            MappingRepository.UpsertMappingState(_db, new MappingStateEntry("b", 3, Timestamp, 7))
        );
        Assert.IsType<BoolSyncOk>(
            MappingRepository.UpsertMappingState(_db, MappingStateEntry.Initial("a", Timestamp))
        );
        Assert.IsType<BoolSyncOk>(
            MappingRepository.UpsertMappingState(_db, new MappingStateEntry("b", 9, Timestamp, 12))
        );

        Assert.Equal(
            new MappingStateEntry("b", 9, Timestamp, 12),
            Assert.IsType<MappingStateOk>(MappingRepository.GetMappingState(_db, "b")).Value
        );
        Assert.Equal(
            [
                MappingStateEntry.Initial("a", Timestamp),
                new MappingStateEntry("b", 9, Timestamp, 12),
            ],
            Assert.IsType<MappingStateListOk>(MappingRepository.GetAllMappingStates(_db)).Value
        );

        Assert.IsType<BoolSyncOk>(MappingRepository.DeleteMappingState(_db, "a"));
        Assert.Equal(
            [new MappingStateEntry("b", 9, Timestamp, 12)],
            Assert.IsType<MappingStateListOk>(MappingRepository.GetAllMappingStates(_db)).Value
        );
    }

    [Fact]
    public void RecordHashes_UpsertCountAndDelete_TrackOnlyTheirMapping()
    {
        Assert.Null(
            Assert.IsType<RecordHashOk>(MappingRepository.GetRecordHash(_db, "m1", "pk-1")).Value
        );
        Assert.IsType<BoolSyncOk>(
            MappingRepository.UpsertRecordHash(
                _db,
                new RecordHashEntry("m1", "pk-1", "h1", Timestamp)
            )
        );
        Assert.IsType<BoolSyncOk>(
            MappingRepository.UpsertRecordHash(
                _db,
                new RecordHashEntry("m1", "pk-1", "h2", Timestamp)
            )
        );
        Assert.IsType<BoolSyncOk>(
            MappingRepository.UpsertRecordHash(
                _db,
                new RecordHashEntry("m1", "pk-2", "h3", Timestamp)
            )
        );
        Assert.IsType<BoolSyncOk>(
            MappingRepository.UpsertRecordHash(
                _db,
                new RecordHashEntry("m2", "pk-1", "h4", Timestamp)
            )
        );

        Assert.Equal(
            new RecordHashEntry("m1", "pk-1", "h2", Timestamp),
            Assert.IsType<RecordHashOk>(MappingRepository.GetRecordHash(_db, "m1", "pk-1")).Value
        );
        Assert.Equal(
            2L,
            Assert.IsType<LongSyncOk>(MappingRepository.CountRecordHashes(_db, "m1")).Value
        );

        Assert.IsType<BoolSyncOk>(MappingRepository.DeleteRecordHash(_db, "m1", "pk-2"));
        Assert.Equal(
            1L,
            Assert.IsType<LongSyncOk>(MappingRepository.CountRecordHashes(_db, "m1")).Value
        );

        Assert.Equal(
            1,
            Assert.IsType<IntSyncOk>(MappingRepository.DeleteRecordHashesByMapping(_db, "m1")).Value
        );
        Assert.Equal(
            0L,
            Assert.IsType<LongSyncOk>(MappingRepository.CountRecordHashes(_db, "m1")).Value
        );
        Assert.Equal(
            1L,
            Assert.IsType<LongSyncOk>(MappingRepository.CountRecordHashes(_db, "m2")).Value
        );
    }

    [Fact]
    public void Repository_WithoutSyncSchema_ReturnsDatabaseErrors()
    {
        var barePath = Path.Combine(
            Path.GetTempPath(),
            $"mappingrepository_bare_{Guid.NewGuid():N}.db"
        );
        using var cleanup = new BareDatabase(barePath);
        using var bare = new SqliteConnection($"Data Source={barePath}");
        bare.Open();
        Assert.True(MappingRepository.GetMappingState(bare, "a").IsError);
        Assert.True(MappingRepository.GetAllMappingStates(bare).IsError);
        Assert.True(
            MappingRepository
                .UpsertMappingState(bare, MappingStateEntry.Initial("a", Timestamp))
                .IsError
        );
        Assert.True(MappingRepository.DeleteMappingState(bare, "a").IsError);
        Assert.True(MappingRepository.GetRecordHash(bare, "a", "pk").IsError);
        Assert.True(
            MappingRepository
                .UpsertRecordHash(bare, new RecordHashEntry("a", "pk", "h", Timestamp))
                .IsError
        );
        Assert.True(MappingRepository.DeleteRecordHash(bare, "a", "pk").IsError);
        Assert.True(MappingRepository.DeleteRecordHashesByMapping(bare, "a").IsError);
        Assert.True(MappingRepository.CountRecordHashes(bare, "a").IsError);
    }

    private sealed record BareDatabase(string Path) : IDisposable
    {
        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            File.Delete(Path);
        }
    }

    public void Dispose()
    {
        _db.Dispose();
        SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }
}
