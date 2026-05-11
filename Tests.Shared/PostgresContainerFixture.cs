// Shared across all *.Tests projects via Compile Include from /DataProvider/Tests.Shared/.
// This file exists ONCE on disk; each test assembly links it so the fixture lives once
// per test-assembly run (xUnit collection fixture lifetime).
//
// Goal: spin up exactly ONE PostgreSQL container for the entire test assembly, then hand
// each test a freshly-created database inside that container. This replaces the old pattern
// of one container per test method, which was burning ~3s per test on container churn.

using System.Globalization;
using System.Text;
using Testcontainers.PostgreSql;

namespace Nimblesite.TestSupport;

/// <summary>
/// Collection fixture that owns a single shared PostgreSQL container for the lifetime of the
/// test assembly. Tests obtain isolated databases via <see cref="CreateDatabaseAsync"/> or
/// <see cref="CreateDatabase"/>, which issue cheap <c>CREATE DATABASE</c> statements against
/// the long-running container instead of churning containers per test.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private string _adminConnectionString = null!;
    private long _databaseCounter;

    /// <summary>
    /// Starts the shared container. Called once per collection fixture lifetime.
    /// </summary>
    public async Task InitializeAsync()
    {
        // pgvector/pgvector:pg16 is a drop-in superset of postgres:16 that
        // ships with the pgvector extension preinstalled. Required for any
        // test that needs vector columns (VectorType). Non-vector tests are
        // unaffected.
        _container = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg16")
            .WithDatabase("postgres")
            .WithUsername("test")
            .WithPassword("test")
            .Build();

        await _container.StartAsync().ConfigureAwait(false);
        _adminConnectionString = _container.GetConnectionString();
    }

    /// <summary>
    /// Disposes the shared container. Called once per collection fixture lifetime.
    /// </summary>
    public async Task DisposeAsync()
    {
        await _container.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a fresh, uniquely-named database inside the shared container and returns an
    /// open connection to it. The caller owns the connection and must dispose it.
    /// </summary>
    /// <param name="namePrefix">
    /// A short prefix used to name the database. Helpful for diagnostics; the fixture appends
    /// a unique counter so concurrent tests cannot collide.
    /// </param>
    public async Task<NpgsqlConnection> CreateDatabaseAsync(string namePrefix)
    {
        var connectionString = await CreateDatabaseConnectionStringAsync(namePrefix)
            .ConfigureAwait(false);
        var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync().ConfigureAwait(false);
        return conn;
    }

    /// <summary>
    /// Synchronous variant of <see cref="CreateDatabaseAsync"/>. Returns an open connection
    /// to a freshly created database.
    /// </summary>
    public NpgsqlConnection CreateDatabase(string namePrefix)
    {
        var connectionString = CreateDatabaseConnectionString(namePrefix);
        var conn = new NpgsqlConnection(connectionString);
        conn.Open();
        return conn;
    }

    /// <summary>
    /// Creates a fresh, uniquely-named database inside the shared container and returns the
    /// connection string for it. Use this when callers need to construct their own connections
    /// (for example, multiple connections to the same DB, or for HTTP test harnesses).
    /// </summary>
    public async Task<string> CreateDatabaseConnectionStringAsync(string namePrefix)
    {
        var dbName = NextDatabaseName(namePrefix);
        var adminConn = new NpgsqlConnection(_adminConnectionString);
        try
        {
            await adminConn.OpenAsync().ConfigureAwait(false);
            using var cmd = adminConn.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE \"{dbName}\"";
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        finally
        {
            await adminConn.DisposeAsync().ConfigureAwait(false);
        }
        return BuildConnectionString(dbName);
    }

    /// <summary>
    /// Synchronous variant of <see cref="CreateDatabaseConnectionStringAsync"/>.
    /// </summary>
    public string CreateDatabaseConnectionString(string namePrefix)
    {
        var dbName = NextDatabaseName(namePrefix);
        using var adminConn = new NpgsqlConnection(_adminConnectionString);
        adminConn.Open();
        using var cmd = adminConn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE \"{dbName}\"";
        cmd.ExecuteNonQuery();
        return BuildConnectionString(dbName);
    }

    // Pooling=false: with the default pool, every test in the assembly retains
    // its server-side connection slot after disposing the NpgsqlConnection, and
    // a single test run easily exhausts Postgres's default max_connections=100,
    // causing later tests to fail with SQLSTATE 53300 ("too many clients
    // already"). The shared container only services this test process, so
    // pool reuse delivers no real benefit; turning it off makes each test
    // hand its slot back immediately.
    private string BuildConnectionString(string dbName) =>
        new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Database = dbName,
            Pooling = false,
        }.ConnectionString;

    private string NextDatabaseName(string namePrefix)
    {
        var counter = Interlocked.Increment(ref _databaseCounter);
        return SanitizeDbName(
            string.Create(CultureInfo.InvariantCulture, $"{namePrefix}_{counter:D5}")
        );
    }

    private static string SanitizeDbName(string raw)
    {
        var lowered = raw.ToLowerInvariant();
        var buf = new StringBuilder(lowered.Length);
        foreach (var ch in lowered)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                buf.Append(ch);
            }
            else
            {
                buf.Append('_');
            }
        }
        if (buf.Length == 0 || char.IsDigit(buf[0]))
        {
            buf.Insert(0, "db_");
        }
        // PostgreSQL identifier max length is 63 bytes; leave a margin.
        if (buf.Length > 60)
        {
            buf.Length = 60;
        }
        return buf.ToString();
    }
}
