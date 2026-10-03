using System.Collections.Immutable;

namespace Nimblesite.Sync.Http.Tests;

public sealed partial class CrossDatabaseSyncTests
{
    [Fact]
    public void BatchSync_LargeDataset()
    {
        var sqliteOriginId = Guid.NewGuid().ToString();
        using var sqlite = CreateSqliteDb(sqliteOriginId);
        using var postgres = CreatePostgresDb(Guid.NewGuid().ToString());

        SeedBatchRows(sqlite, recordCount: 100);
        AssertBatchTransfer(sqlite, postgres, sqliteOriginId, recordCount: 100, batchSize: 25);
        AssertTransferredRows(postgres, recordCount: 100);
    }

    private static void SeedBatchRows(SqliteConnection sqlite, int recordCount)
    {
        for (var i = 0; i < recordCount; i++)
        {
            using var command = sqlite.CreateCommand();
            command.CommandText =
                $"INSERT INTO Person (Id, Name, Email) VALUES ('batch{i}', 'Person {i}', 'p{i}@example.com')";
            Assert.Equal(1, command.ExecuteNonQuery());
        }
    }

    private void AssertBatchTransfer(
        SqliteConnection sqlite,
        NpgsqlConnection postgres,
        string originId,
        int recordCount,
        int batchSize
    )
    {
        var fromVersion = 0L;
        var totalSynced = 0;
        while (true)
        {
            var result = SyncLogRepository.FetchChanges(sqlite, fromVersion, batchSize);
            var batch = Assert.IsType<SyncLogListOk>(result).Value;
            if (batch.Count == 0)
            {
                break;
            }
            Assert.Equal(batchSize, batch.Count);
            Assert.Equal(fromVersion + 1, batch[0].Version);
            ApplyBatch(postgres, batch, originId);
            totalSynced += batch.Count;
            fromVersion = batch[^1].Version;
        }
        Assert.Equal(recordCount, totalSynced);
        Assert.Equal(recordCount, fromVersion);
    }

    private void ApplyBatch(
        NpgsqlConnection postgres,
        IReadOnlyList<SyncLogEntry> batch,
        string originId
    )
    {
        PostgresSyncSession.EnableSuppression(postgres);
        foreach (var entry in batch)
        {
            Assert.Equal("Person", entry.TableName);
            Assert.Equal(SyncOperation.Insert, entry.Operation);
            Assert.Equal(originId, entry.Origin);
            Assert.IsType<BoolSyncOk>(PostgresChangeApplier.ApplyChange(postgres, entry, _logger));
        }
        PostgresSyncSession.DisableSuppression(postgres);
    }

    private static void AssertTransferredRows(NpgsqlConnection postgres, int recordCount)
    {
        using var command = postgres.CreateCommand();
        command.CommandText = "SELECT id, name, email FROM person";
        using var reader = command.ExecuteReader();
        var ids = ImmutableHashSet.CreateBuilder<string>();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            Assert.StartsWith("batch", id, StringComparison.Ordinal);
            Assert.True(int.TryParse(id.AsSpan(5), out var number));
            Assert.InRange(number, 0, recordCount - 1);
            Assert.True(ids.Add(id), $"Duplicate Person {id}");
            Assert.Equal($"Person {number}", reader.GetString(1));
            Assert.Equal($"p{number}@example.com", reader.GetString(2));
        }
        Assert.Equal(recordCount, ids.Count);
    }
}
