using MappingStateListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.MappingStateEntry>,
    Nimblesite.Sync.Core.SyncError
>.Ok<
    System.Collections.Generic.IReadOnlyList<Nimblesite.Sync.Core.MappingStateEntry>,
    Nimblesite.Sync.Core.SyncError
>;

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
        global::Tests.Shared.ResultAssertions.ExactOk(actual: SyncSchema.CreateSchema(_db));
    }

    [Fact]
    public void MappingState_UpsertGetListAndDelete_RoundTripsExactValues()
    {
        Assert.Null(
            global::Tests
                .Shared.ResultAssertions.ExactOk(
                    actual: MappingRepository.GetMappingState(_db, "b")
                )
                .Value
        );

        global::Tests.Shared.ResultAssertions.ExactOk(
            actual: MappingRepository.UpsertMappingState(
                _db,
                new MappingStateEntry("b", 3, Timestamp, 7)
            )
        );
        global::Tests.Shared.ResultAssertions.ExactOk(
            actual: MappingRepository.UpsertMappingState(
                _db,
                MappingStateEntry.Initial("a", Timestamp)
            )
        );
        global::Tests.Shared.ResultAssertions.ExactOk(
            actual: MappingRepository.UpsertMappingState(
                _db,
                new MappingStateEntry("b", 9, Timestamp, 12)
            )
        );

        global::Tests.Shared.ResultAssertions.EqualValue(
            expected: new MappingStateEntry("b", 9, Timestamp, 12),
            actual: MappingRepository.GetMappingState(_db, "b")
        );
        Assert.Equal(
            [
                MappingStateEntry.Initial("a", Timestamp),
                new MappingStateEntry("b", 9, Timestamp, 12),
            ],
            Assert.IsType<MappingStateListOk>(MappingRepository.GetAllMappingStates(_db)).Value
        );

        global::Tests.Shared.ResultAssertions.ExactOk(
            actual: MappingRepository.DeleteMappingState(_db, "a")
        );
        Assert.Equal(
            [new MappingStateEntry("b", 9, Timestamp, 12)],
            Assert.IsType<MappingStateListOk>(MappingRepository.GetAllMappingStates(_db)).Value
        );
    }

    [Fact]
    public void RecordHashes_UpsertCountAndDelete_TrackOnlyTheirMapping()
    {
        Assert.Null(
            global::Tests
                .Shared.ResultAssertions.ExactOk(
                    actual: MappingRepository.GetRecordHash(_db, "m1", "pk-1")
                )
                .Value
        );
        VerifyReusableWorkflow2(
            input0: _db,
            value1: "pk-1",
            value2: "h1",
            value4: "m1",
            value6: "h2"
        );
        VerifyReusableWorkflow2(
            input0: _db,
            value1: "pk-2",
            value2: "h3",
            value4: "m2",
            value6: "h4"
        );

        global::Tests.Shared.ResultAssertions.EqualValue(
            expected: new RecordHashEntry("m1", "pk-1", "h2", Timestamp),
            actual: MappingRepository.GetRecordHash(_db, "m1", "pk-1")
        );
        global::Tests.Shared.ResultAssertions.EqualValue(
            expected: 2L,
            actual: MappingRepository.CountRecordHashes(_db, "m1")
        );

        global::Tests.Shared.ResultAssertions.ExactOk(
            actual: MappingRepository.DeleteRecordHash(_db, "m1", "pk-2")
        );
        global::Tests.Shared.ResultAssertions.EqualValue(
            expected: 1L,
            actual: MappingRepository.CountRecordHashes(_db, "m1")
        );

        global::Tests.Shared.ResultAssertions.EqualValue(
            expected: 1,
            actual: MappingRepository.DeleteRecordHashesByMapping(_db, "m1")
        );
        global::Tests.Shared.ResultAssertions.EqualValue(
            expected: 0L,
            actual: MappingRepository.CountRecordHashes(_db, "m1")
        );
        global::Tests.Shared.ResultAssertions.EqualValue(
            expected: 1L,
            actual: MappingRepository.CountRecordHashes(_db, "m2")
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

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow2(
        SqliteConnection input0,
        string value1,
        string value2,
        string value4,
        string value6
    )
    {
        global::Tests.Shared.ResultAssertions.ExactOk(
            actual: MappingRepository.UpsertRecordHash(
                input0,
                new RecordHashEntry("m1", value1, value2, Timestamp)
            )
        );
        global::Tests.Shared.ResultAssertions.ExactOk(
            actual: MappingRepository.UpsertRecordHash(
                input0,
                new RecordHashEntry(value4, "pk-1", value6, Timestamp)
            )
        );
    }
}
