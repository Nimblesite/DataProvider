using Microsoft.Extensions.Logging.Abstractions;

namespace Nimblesite.Sync.Tests;

public sealed class BatchManagerTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly NullLogger _logger = NullLogger.Instance;

    [Fact]
    public void FetchBatch_EmptyLog_ReturnsEmptyBatch()
    {
        var result = BatchManager.FetchBatch(
            0,
            100,
            (from, limit) => new SyncLogListOk(_db.FetchChanges(from, limit)),
            _logger
        );

        var batch = TestDb.AssertSuccess(result);
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

        var result = BatchManager.FetchBatch(
            0,
            100,
            (from, limit) => new SyncLogListOk(_db.FetchChanges(from, limit)),
            _logger
        );

        var batch = TestDb.AssertSuccess(result);
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

        var result = BatchManager.FetchBatch(
            0,
            3,
            (from, limit) => new SyncLogListOk(_db.FetchChanges(from, limit)),
            _logger
        );

        var batch = TestDb.AssertSuccess(result);
        Assert.Equal(3, batch.Changes.Count);
        Assert.True(batch.HasMore);
        Assert.Equal(3, batch.ToVersion);
    }

    [Fact]
    public void FetchBatch_FromVersion_SkipsOlderEntries()
    {
        for (var i = 1; i <= 5; i++)
        {
            _db.InsertSyncLogEntry(
                "Person",
                $"{{\"Id\":\"{i}\"}}",
                "insert",
                $"{{\"Id\":\"{i}\"}}",
                "origin-1",
                "2025-01-01T00:00:00.000Z"
            );
        }

        var result = BatchManager.FetchBatch(
            3,
            100,
            (from, limit) => new SyncLogListOk(_db.FetchChanges(from, limit)),
            _logger
        );

        var batch = TestDb.AssertSuccess(result);
        Assert.Equal(2, batch.Changes.Count);
        Assert.Equal(4, batch.Changes[0].Version);
        Assert.Equal(5, batch.Changes[1].Version);
    }

    [Fact]
    public void ProcessAllBatches_ProcessesMultipleBatches()
    {
        for (var i = 1; i <= 10; i++)
        {
            _db.InsertSyncLogEntry(
                "Person",
                $"{{\"Id\":\"{i}\"}}",
                "insert",
                $"{{\"Id\":\"{i}\"}}",
                "origin-1",
                "2025-01-01T00:00:00.000Z"
            );
        }

        var appliedBatches = new List<SyncBatch>();
        var lastVersion = 0L;

        var result = BatchManager.ProcessAllBatches(
            0,
            new BatchConfig(BatchSize: 3),
            (from, limit) => new SyncLogListOk(_db.FetchChanges(from, limit)),
            batch =>
            {
                appliedBatches.Add(batch);
                return new BatchApplyResultOk(
                    new BatchApplyResult(batch.Changes.Count, 0, batch.ToVersion)
                );
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
}
