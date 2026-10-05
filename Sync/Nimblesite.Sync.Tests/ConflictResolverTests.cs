namespace Nimblesite.Sync.Tests;

public sealed class ConflictResolverTests
{
    private static SyncLogEntry CreateEntry(
        long version,
        string tableName,
        string pkValue,
        string origin,
        string timestamp
    ) =>
        new(
            version,
            tableName,
            pkValue,
            SyncOperation.Update,
            "{\"Id\":\"" + pkValue + "\"}",
            origin,
            timestamp
        );

    [Fact]
    public void IsConflict_SameTableSamePkDifferentOrigin_ReturnsTrue()
    {
        var local = CreateEntry(1, "Person", "pk-1", "origin-A", "2025-01-01T00:00:00.000Z");
        var remote = CreateEntry(2, "Person", "pk-1", "origin-B", "2025-01-01T00:00:01.000Z");

        var result = ConflictResolver.IsConflict(local, remote);

        Assert.True(result);
    }

    [Fact]
    public void IsConflict_SameTableSamePkSameOrigin_ReturnsFalse()
    {
        var local = CreateEntry(1, "Person", "pk-1", "origin-A", "2025-01-01T00:00:00.000Z");
        var remote = CreateEntry(2, "Person", "pk-1", "origin-A", "2025-01-01T00:00:01.000Z");

        var result = ConflictResolver.IsConflict(local, remote);

        Assert.False(result);
    }

    [Fact]
    public void IsConflict_DifferentTable_ReturnsFalse()
    {
        var local = CreateEntry(1, "Person", "pk-1", "origin-A", "2025-01-01T00:00:00.000Z");
        var remote = CreateEntry(2, "Order", "pk-1", "origin-B", "2025-01-01T00:00:01.000Z");

        var result = ConflictResolver.IsConflict(local, remote);

        Assert.False(result);
    }

    [Fact]
    public void IsConflict_DifferentPk_ReturnsFalse()
    {
        var local = CreateEntry(1, "Person", "pk-1", "origin-A", "2025-01-01T00:00:00.000Z");
        var remote = CreateEntry(2, "Person", "pk-2", "origin-B", "2025-01-01T00:00:01.000Z");

        var result = ConflictResolver.IsConflict(local, remote);

        Assert.False(result);
    }

    [Fact]
    public void Resolve_LastWriteWins_NewerTimestampWins()
    {
        var local = CreateEntry(1, "Person", "pk-1", "origin-A", "2025-01-01T00:00:00.000Z");
        var remote = CreateEntry(2, "Person", "pk-1", "origin-B", "2025-01-01T00:00:01.000Z");

        var result = ConflictResolver.Resolve(local, remote, ConflictStrategy.LastWriteWins);

        Assert.Equal(remote, result.Winner);
        Assert.Equal(ConflictStrategy.LastWriteWins, result.Strategy);
    }

    [Fact]
    public void Resolve_LastWriteWins_OlderTimestampLoses()
    {
        var local = CreateEntry(1, "Person", "pk-1", "origin-A", "2025-01-01T00:00:01.000Z");
        var remote = CreateEntry(2, "Person", "pk-1", "origin-B", "2025-01-01T00:00:00.000Z");

        var result = ConflictResolver.Resolve(local, remote, ConflictStrategy.LastWriteWins);

        Assert.Equal(local, result.Winner);
    }

    [Fact]
    public void Resolve_LastWriteWins_EqualTimestamps_HigherVersionWins()
    {
        var local = CreateEntry(1, "Person", "pk-1", "origin-A", "2025-01-01T00:00:00.000Z");
        var remote = CreateEntry(2, "Person", "pk-1", "origin-B", "2025-01-01T00:00:00.000Z");

        var result = ConflictResolver.Resolve(local, remote, ConflictStrategy.LastWriteWins);

        Assert.Equal(remote, result.Winner);
    }

    [Fact]
    public void Resolve_ServerWins_AlwaysReturnsRemote()
    {
        var local = CreateEntry(1, "Person", "pk-1", "origin-A", "2025-01-01T00:00:01.000Z");
        var remote = CreateEntry(2, "Person", "pk-1", "origin-B", "2025-01-01T00:00:00.000Z");

        var result = ConflictResolver.Resolve(local, remote, ConflictStrategy.ServerWins);

        Assert.Equal(remote, result.Winner);
        Assert.Equal(ConflictStrategy.ServerWins, result.Strategy);
    }

    [Fact]
    public void Resolve_ClientWins_AlwaysReturnsLocal()
    {
        var local = CreateEntry(1, "Person", "pk-1", "origin-A", "2025-01-01T00:00:00.000Z");
        var remote = CreateEntry(2, "Person", "pk-1", "origin-B", "2025-01-01T00:00:01.000Z");

        var result = ConflictResolver.Resolve(local, remote, ConflictStrategy.ClientWins);

        Assert.Equal(local, result.Winner);
        Assert.Equal(ConflictStrategy.ClientWins, result.Strategy);
    }

    [Fact]
    public void ResolveLastWriteWins_DirectCall_WorksCorrectly()
    {
        var local = CreateEntry(1, "Person", "pk-1", "origin-A", "2025-01-01T00:00:00.000Z");
        var remote = CreateEntry(2, "Person", "pk-1", "origin-B", "2025-01-01T00:00:01.000Z");

        var result = ConflictResolver.ResolveLastWriteWins(local, remote);

        Assert.Equal(remote, result.Winner);
        Assert.Equal(ConflictStrategy.LastWriteWins, result.Strategy);
    }

    [Fact]
    public void ResolveCustom_SuccessfulResolver_ReturnsSuccess()
    {
        var local = CreateEntry(1, "Person", "pk-1", "origin-A", "2025-01-01T00:00:00.000Z");
        var remote = CreateEntry(2, "Person", "pk-1", "origin-B", "2025-01-01T00:00:01.000Z");

        var result = ConflictResolver.ResolveCustom(local, remote, (l, r) => new SyncLogEntryOk(l));

        var success = Assert.IsType<ConflictResolutionOk>(result);
        Assert.Equal(local, success.Value.Winner);
    }

    [Fact]
    public void ResolveCustom_FailingResolver_ReturnsFailure()
    {
        var local = CreateEntry(1, "Person", "pk-1", "origin-A", "2025-01-01T00:00:00.000Z");
        var remote = CreateEntry(2, "Person", "pk-1", "origin-B", "2025-01-01T00:00:01.000Z");
        var error = new SyncErrorDatabase("Custom resolver failed");

        var result = ConflictResolver.ResolveCustom(
            local,
            remote,
            (l, r) => new SyncLogEntryError(error)
        );

        var failure = Assert.IsType<ConflictResolutionError>(result);
        Assert.Equal(error, failure.Value);
    }
}
