namespace Nimblesite.Sync.Tests;

public sealed partial class BatchManagerTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly NullLogger _logger = NullLogger.Instance;

    [Fact]
    public void FetchBatch_EmptyLog_ReturnsEmptyBatch()
    {
        var batch = VerifySharedChecks3(input0: _db, input1: _logger, value0: 0, value1: 100);
        Assert.Empty(batch.Changes);
        Assert.Equal(0, batch.FromVersion);
        Assert.Equal(0, batch.ToVersion);
        Assert.False(batch.HasMore);
    }

    [Fact]
    public void FetchBatch_WithChanges_ReturnsBatch()
    {
        _db.InsertSyncLogEntry(
            "Person",
            "{\"Id\":\"1\"}",
            "insert",
            "{\"Id\":\"1\",\"Name\":\"Alice\"}",
            "origin-1",
            "2025-01-01T00:00:00.000Z"
        );
        _db.InsertSyncLogEntry(
            "Person",
            "{\"Id\":\"2\"}",
            "insert",
            "{\"Id\":\"2\",\"Name\":\"Bob\"}",
            "origin-1",
            "2025-01-01T00:00:01.000Z"
        );

        var batch = VerifySharedChecks3(input0: _db, input1: _logger, value0: 0, value1: 100);
        Assert.Equal(2, batch.Changes.Count);
        Assert.Equal(0, batch.FromVersion);
        Assert.Equal(2, batch.ToVersion);
        Assert.False(batch.HasMore);
    }

    [Fact]
    public void FetchBatch_ExceedsBatchSize_HasMoreTrue()
    {
        for (var i = 1; i <= 5; i++)
        {
            _db.InsertSyncLogEntry(
                "Person",
                $"{{\"Id\":\"{i}\"}}",
                "insert",
                $"{{\"Id\":\"{i}\",\"Name\":\"Person{i}\"}}",
                "origin-1",
                $"2025-01-01T00:00:0{i}.000Z"
            );
        }

        var batch = VerifySharedChecks3(input0: _db, input1: _logger, value0: 0, value1: 3);
        Assert.Equal(3, batch.Changes.Count);
        Assert.True(batch.HasMore);
        Assert.Equal(3, batch.ToVersion);
    }

    [Fact]
    public void FetchBatch_FromVersion_SkipsOlderEntries()
    {
        VerifySyncLoopWorkflow1(input0: _db, value1: 5);

        var batch = VerifySharedChecks3(input0: _db, input1: _logger, value0: 3, value1: 100);
        Assert.Equal(2, batch.Changes.Count);
        Assert.Equal(4, batch.Changes[0].Version);
        Assert.Equal(5, batch.Changes[1].Version);
    }

    [Fact]
    public void ProcessAllBatches_ProcessesMultipleBatches()
    {
        VerifySyncLoopWorkflow1(input0: _db, value1: 10);

        var appliedBatches = new List<SyncBatch>();
        var lastVersion = 0L;

        var result = BatchManager.ProcessAllBatches(
            0,
            new BatchConfig(BatchSize: 3),
            (from, limit) => new SyncLogListOk(_db.FetchChanges(from, limit)),
            batch =>
            {
                appliedBatches.Add(batch);
                return Dataset_ProcessAllBatches_ProcessesMultipleBatches_1(batch: batch);
            },
            version => lastVersion = version,
            _logger
        );

        var totalApplied = TestDb.AssertSuccess(result);
        Assert.Equal(10, totalApplied);
        Assert.Equal(4, appliedBatches.Count);
        Assert.Equal(10, lastVersion);
    }

    public void Dispose() => _db.Dispose();

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static SyncBatch VerifySharedChecks3(
        TestDb input0,
        NullLogger input1,
        int value0,
        int value1
    )
    {
        var result = BatchManager.FetchBatch(
            value0,
            value1,
            (from, limit) => new SyncLogListOk(input0.FetchChanges(from, limit)),
            input1
        );

        var batch = TestDb.AssertSuccess(result);

        return batch;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifySyncLoopWorkflow1(TestDb input0, int value1)
    {
        for (var i = 1; i <= value1; i++)
        {
            input0.InsertSyncLogEntry(
                "Person",
                $"{{\"Id\":\"{i}\"}}",
                "insert",
                $"{{\"Id\":\"{i}\"}}",
                "origin-1",
                "2025-01-01T00:00:00.000Z"
            );
        }
    }
}
