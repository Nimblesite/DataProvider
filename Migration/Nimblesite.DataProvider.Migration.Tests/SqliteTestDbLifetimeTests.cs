using System.Data;

namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-TEST-SQLITE-LIFETIME].
public sealed record SqliteTestDbLifetimeTests
{
    [Fact]
    public void WithDb_ClosesConnectionBeforeDeletingDatabase()
    {
        string? databasePath = null;
        var fileExistsWhenClosed = false;
        SqliteTestDb.WithDb(test: connection =>
        {
            databasePath = connection.DataSource;
            connection.StateChange += (_, change) =>
                fileExistsWhenClosed =
                    change.CurrentState == ConnectionState.Closed && File.Exists(databasePath);
        });
        Assert.True(fileExistsWhenClosed);
        Assert.NotNull(databasePath);
        Assert.False(File.Exists(databasePath));
    }
}
