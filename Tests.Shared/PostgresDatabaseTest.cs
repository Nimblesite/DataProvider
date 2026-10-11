// Shared across all *.Tests projects via Compile Include from /DataProvider/Tests.Shared/.
// Centralises the per-test Postgres database lifetime: each test class declares only
// its database name instead of repeating the fixture field + IAsyncLifetime boilerplate.

namespace Nimblesite.TestSupport;

/// <summary>
/// Base class for Postgres-backed test classes that own exactly one database
/// connection created from the shared <see cref="PostgresContainerFixture"/>.
/// </summary>
/// <remarks>
/// Derived classes supply <see cref="DatabaseName"/> and may override
/// <see cref="OnDatabaseCreated"/> to run bootstrap SQL once the database exists.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Usage",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposed via IAsyncLifetime.DisposeAsync"
)]
public abstract class PostgresDatabaseTest(PostgresContainerFixture fixture) : IAsyncLifetime
{
    /// <summary>Connection to the per-test database created in <see cref="InitializeAsync"/>.</summary>
    protected NpgsqlConnection Connection { get; private set; } = null!;

    /// <summary>Logger shared by the test class.</summary>
    protected ILogger Logger { get; } = NullLogger.Instance;

    /// <summary>Name passed to <see cref="PostgresContainerFixture.CreateDatabaseAsync"/>.</summary>
    protected abstract string DatabaseName { get; }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        Connection = await fixture.CreateDatabaseAsync(DatabaseName).ConfigureAwait(false);
        OnDatabaseCreated();
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await Connection.DisposeAsync().ConfigureAwait(false);

    /// <summary>Hook invoked after the connection is created and before the tests run.</summary>
    protected virtual void OnDatabaseCreated() { }
}
