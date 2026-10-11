namespace Nimblesite.Sync.Tests;

/// <summary>
/// Tests for SyncError types.
/// </summary>
public sealed partial class SyncErrorTests
{
    [Fact]
    public void SyncErrorForeignKeyViolation_CreatesCorrectRecord()
    {
        var error = Dataset_SyncErrorForeignKeyViolation_CreatesCorrectRecord_1();

        Assert.Equal("Person", error.TableName);
        Assert.Equal("{\"Id\":\"p1\"}", error.PkValue);
        Assert.Equal("FK_Person_Department", error.Details);
    }

    [Fact]
    public void SyncErrorDeferredChangeFailed_CreatesCorrectRecord()
    {
        var entry = Fixture_SyncErrorDeferredChangeFailed_CreatesCorrectRecord_1();

        var error = new SyncErrorDeferredChangeFailed(entry, "Max retries exceeded");

        Assert.Equal(entry, error.Entry);
        Assert.Equal("Max retries exceeded", error.Reason);
    }

    [Fact]
    public void SyncErrorFullResyncRequired_CreatesCorrectRecord()
    {
        var error = new SyncErrorFullResyncRequired(100, 500);

        Assert.Equal(100, error.ClientVersion);
        Assert.Equal(500, error.OldestAvailableVersion);
    }

    [Fact]
    public void SyncErrorHashMismatch_CreatesCorrectRecord()
    {
        var error = new SyncErrorHashMismatch("abc123", "def456");

        Assert.Equal("abc123", error.ExpectedHash);
        Assert.Equal("def456", error.ActualHash);
    }

    [Fact]
    public void SyncErrorDatabase_CreatesCorrectRecord()
    {
        var error = new SyncErrorDatabase("Connection failed");

        Assert.Equal("Connection failed", error.Message);
    }

    [Fact]
    public void SyncErrorUnresolvedConflict_CreatesCorrectRecord()
    {
        var localChange = Fixture_SyncErrorUnresolvedConflict_CreatesCorrectRecord_1();

        var remoteChange = Fixture_SyncErrorUnresolvedConflict_CreatesCorrectRecord_2();

        var error = new SyncErrorUnresolvedConflict(localChange, remoteChange);

        Assert.Equal(localChange, error.LocalChange);
        Assert.Equal(remoteChange, error.RemoteChange);
    }

    [Fact]
    public void AllSyncErrors_DeriveFromSyncError()
    {
        var errors = new SyncError[]
        {
            new SyncErrorForeignKeyViolation("T", "PK", "Details"),
            Dataset_AllSyncErrors_DeriveFromSyncError_1(),
            new SyncErrorFullResyncRequired(1, 100),
            new SyncErrorHashMismatch("E", "A"),
            new SyncErrorDatabase("Msg"),
            Dataset_AllSyncErrors_DeriveFromSyncError_2(),
        };

        foreach (var error in errors)
        {
            Assert.IsAssignableFrom<SyncError>(error);
        }
    }

    [Fact]
    public void SyncErrors_CanBePatternMatched()
    {
        SyncError error = new SyncErrorDatabase("test");

        var result = error switch
        {
            SyncErrorForeignKeyViolation fk => $"FK: {fk.TableName}",
            SyncErrorDeferredChangeFailed dc => $"Deferred: {dc.Reason}",
            SyncErrorFullResyncRequired fr => $"Resync: {fr.ClientVersion}",
            SyncErrorHashMismatch hm => $"Hash: {hm.ExpectedHash}",
            SyncErrorDatabase db => $"DB: {db.Message}",
            SyncErrorUnresolvedConflict uc => $"Conflict: {uc.LocalChange.TableName}",
            _ => "Unknown",
        };

        Assert.Equal("DB: test", result);
    }
}
