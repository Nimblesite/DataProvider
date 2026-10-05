namespace Nimblesite.Sync.Tests;

public sealed class HashVerifierTests
{
    [Fact]
    public void ComputeBatchHash_EmptyBatch_ReturnsConsistentHash()
    {
        var hash1 = HashVerifier.ComputeBatchHash([]);
        var hash2 = HashVerifier.ComputeBatchHash([]);

        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length); // SHA-256 = 64 hex chars
    }

    [Fact]
    public void ComputeBatchHash_SameEntries_SameHash()
    {
        var entries = new[]
        {
            new SyncLogEntry(
                1,
                "Person",
                "{\"Id\":\"1\"}",
                SyncOperation.Insert,
                "{\"Id\":\"1\",\"Name\":\"Alice\"}",
                "origin-1",
                "2025-01-01T00:00:00.000Z"
            ),
            new SyncLogEntry(
                2,
                "Person",
                "{\"Id\":\"2\"}",
                SyncOperation.Insert,
                "{\"Id\":\"2\",\"Name\":\"Bob\"}",
                "origin-1",
                "2025-01-01T00:00:01.000Z"
            ),
        };

        var hash1 = HashVerifier.ComputeBatchHash(entries);
        var hash2 = HashVerifier.ComputeBatchHash(entries);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeBatchHash_DifferentEntries_DifferentHash()
    {
        var entries1 = new[]
        {
            new SyncLogEntry(
                1,
                "Person",
                "{\"Id\":\"1\"}",
                SyncOperation.Insert,
                "{\"Id\":\"1\",\"Name\":\"Alice\"}",
                "origin-1",
                "2025-01-01T00:00:00.000Z"
            ),
        };

        var entries2 = new[]
        {
            new SyncLogEntry(
                1,
                "Person",
                "{\"Id\":\"1\"}",
                SyncOperation.Insert,
                "{\"Id\":\"1\",\"Name\":\"Bob\"}",
                "origin-1",
                "2025-01-01T00:00:00.000Z"
            ),
        };

        var hash1 = HashVerifier.ComputeBatchHash(entries1);
        var hash2 = HashVerifier.ComputeBatchHash(entries2);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeBatchHash_DeleteWithNullPayload_HandlesCorrectly()
    {
        var entries = new[]
        {
            new SyncLogEntry(
                1,
                "Person",
                "{\"Id\":\"1\"}",
                SyncOperation.Delete,
                null,
                "origin-1",
                "2025-01-01T00:00:00.000Z"
            ),
        };

        var hash = HashVerifier.ComputeBatchHash(entries);

        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public void ToCanonicalJson_SortsKeysAlphabetically()
    {
        var data = new Dictionary<string, object?>
        {
            ["Zebra"] = "last",
            ["Apple"] = "first",
            ["Mango"] = "middle",
        };

        var json = HashVerifier.ToCanonicalJson(data);

        Assert.StartsWith("{\"Apple\":", json);
        Assert.Contains("\"Mango\":", json);
        Assert.EndsWith("\"Zebra\":\"last\"}", json);
    }

    [Fact]
    public void ToCanonicalJson_NoWhitespace()
    {
        var data = new Dictionary<string, object?> { ["Name"] = "Alice", ["Age"] = 30 };

        var json = HashVerifier.ToCanonicalJson(data);

        Assert.DoesNotContain(" ", json);
        Assert.DoesNotContain("\n", json);
    }

    [Fact]
    public void ToCanonicalJson_HandlesNullValues()
    {
        var data = new Dictionary<string, object?> { ["Name"] = "Alice", ["Email"] = null };

        var json = HashVerifier.ToCanonicalJson(data);

        Assert.Contains("\"Email\":null", json);
    }

    [Fact]
    public void VerifyHash_MatchingHashes_ReturnsSuccess()
    {
        var result = HashVerifier.VerifyHash("abc123", "ABC123");

        Assert.IsType<BoolSyncOk>(result);
    }

    [Fact]
    public void VerifyHash_MismatchedHashes_ReturnsFailure()
    {
        var result = HashVerifier.VerifyHash("abc123", "xyz789");

        var failure = Assert.IsType<BoolSyncError>(result);
        Assert.IsType<SyncErrorHashMismatch>(failure.Value);
    }

    [Fact]
    public void ParseJson_ValidJson_ReturnsDictionary()
    {
        var json = "{\"Id\":\"123\",\"Name\":\"Alice\"}";

        var result = HashVerifier.ParseJson(json);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ParseJson_NullOrEmpty_ReturnsEmptyDictionary()
    {
        Assert.Empty(HashVerifier.ParseJson(null));
        Assert.Empty(HashVerifier.ParseJson(""));
    }

    [Fact]
    public void ParseJson_InvalidJson_ReturnsEmptyDictionary()
    {
        var result = HashVerifier.ParseJson("not valid json");

        Assert.Empty(result);
    }

    [Fact]
    public void ComputeDatabaseHash_SameTables_SameHash()
    {
        var tables = new[] { "Person", "Order" };
        var rows = new Dictionary<string, List<Dictionary<string, object?>>>
        {
            ["Order"] = [new() { ["Id"] = "1", ["Total"] = 100 }],
            ["Person"] = [new() { ["Id"] = "1", ["Name"] = "Alice" }],
        };

        var hash1 = HashVerifier.ComputeDatabaseHash(tables, t => rows[t]);
        var hash2 = HashVerifier.ComputeDatabaseHash(tables, t => rows[t]);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeDatabaseHash_TablesProcessedAlphabetically()
    {
        // Even if passed in different order, same hash due to alphabetical sorting
        var rows = new Dictionary<string, List<Dictionary<string, object?>>>
        {
            ["Zebra"] = [new() { ["Id"] = "z1" }],
            ["Apple"] = [new() { ["Id"] = "a1" }],
        };

        var hash1 = HashVerifier.ComputeDatabaseHash(["Zebra", "Apple"], t => rows[t]);
        var hash2 = HashVerifier.ComputeDatabaseHash(["Apple", "Zebra"], t => rows[t]);

        Assert.Equal(hash1, hash2);
    }
}
