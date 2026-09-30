using System.Net;
using System.Text.Json;

namespace Nimblesite.Sync.Http.Tests;

/// <summary>
/// Custom WebApplicationFactory that sets the correct content root path.
/// </summary>
public sealed class SyncApiWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Use the output directory where the test assembly runs from
        // This avoids path resolution issues with the source directory structure
        var contentRoot = Path.GetDirectoryName(typeof(Program).Assembly.Location)!;
        builder.UseContentRoot(contentRoot);
    }
}

/// <summary>
/// E2E HTTP integration tests for sync API endpoints.
/// Tests the FULL HTTP stack including rate limiting, validation, and error handling.
/// Uses WebApplicationFactory for real ASP.NET Core hosting.
/// </summary>
public sealed class HttpEndpointTests : IClassFixture<SyncApiWebApplicationFactory>
{
    private readonly SyncApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public HttpEndpointTests(SyncApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    #region Health Endpoint

    [Fact]
    public async Task Health_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", content);
    }

    #endregion

    #region Pull Changes Endpoint

    [Fact]
    public async Task PullChanges_WithoutConnectionString_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync(
            "/sync/changes?fromVersion=0&batchSize=100&dbType=sqlite"
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PullChanges_WithInvalidDbType_ReturnsProblem()
    {
        // Act
        var response = await _client.GetAsync(
            "/sync/changes?fromVersion=0&batchSize=100&dbType=invalid&connectionString=test"
        );

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    #endregion

    #region Push Changes Endpoint

    [Fact]
    public async Task PushChanges_WithoutBody_ReturnsBadRequest()
    {
        // Arrange
        var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(
            "/sync/changes?dbType=sqlite&connectionString=test",
            content
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PushChanges_WithTooManyChanges_ReturnsBadRequest()
    {
        // Arrange - create 10001 fake changes (over the 10000 limit)
        var changes = Enumerable
            .Range(0, 10001)
            .Select(i => new
            {
                Version = (long)i,
                TableName = "Person",
                PkValue = $"{{\"Id\":\"{i}\"}}",
                Operation = "insert",
                Payload = $"{{\"Id\":\"{i}\",\"Name\":\"Person {i}\"}}",
                Origin = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow.ToString("O"),
            })
            .ToList();

        var request = new { Changes = changes, OriginId = Guid.NewGuid().ToString() };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        // Act - using temp file path (test expects BadRequest before connection is used)
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"temp_test_{Guid.NewGuid()}.db");
        var response = await _client.PostAsync(
            $"/sync/changes?dbType=sqlite&connectionString=Data Source={Uri.EscapeDataString(tempDbPath)}",
            content
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("10000", body);
    }

    #endregion

    #region Register Client Endpoint

    [Fact]
    public async Task RegisterClient_WithoutOriginId_ReturnsBadRequest()
    {
        // Arrange
        var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(
            "/sync/clients?dbType=sqlite&connectionString=test",
            content
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterClient_WithoutConnectionString_ReturnsBadRequest()
    {
        // Arrange
        var request = new { OriginId = Guid.NewGuid().ToString(), LastSyncVersion = 0L };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        // Act
        var response = await _client.PostAsync("/sync/clients?dbType=sqlite", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Sync State Endpoint

    [Fact]
    public async Task SyncState_WithoutConnectionString_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/sync/state?dbType=sqlite");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion
}

/// <summary>
/// E2E tests with real SQLite database for HTTP endpoints.
/// </summary>
public sealed class HttpEndpointWithDatabaseTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _connection;
    private readonly string _originId = Guid.NewGuid().ToString();

    public HttpEndpointWithDatabaseTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sync_test_{Guid.NewGuid()}.db");
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();

        // Initialize sync schema
        SyncSchema.CreateSchema(_connection);
        SyncSchema.SetOriginId(_connection, _originId);

        // Create test table with triggers
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Person (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Email TEXT
            );
            """;
        cmd.ExecuteNonQuery();

        TriggerGenerator.CreateTriggers(_connection, "Person", NullLogger.Instance);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public void Database_InitializesCorrectly()
    {
        // Verify origin ID is set
        var result = SyncSchema.GetOriginId(_connection);
        Assert.IsType<StringSyncOk>(result);
        Assert.Equal(_originId, ((StringSyncOk)result).Value);
    }

    [Fact]
    public void Insert_CreatesChangelog()
    {
        // Act
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "INSERT INTO Person (Id, Name, Email) VALUES ('p1', 'Alice', 'alice@test.com')";
        cmd.ExecuteNonQuery();

        // Assert
        var changes = SyncLogRepository.FetchChanges(_connection, 0, 100);
        Assert.IsType<SyncLogListOk>(changes);
        var list = ((SyncLogListOk)changes).Value;
        Assert.Single(list);
        Assert.Equal("Person", list[0].TableName);
        Assert.Equal(SyncOperation.Insert, list[0].Operation);
    }

    [Fact]
    public void Update_CreatesChangelog()
    {
        // Arrange
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Person (Id, Name, Email) VALUES ('p2', 'Bob', 'bob@test.com')";
            cmd.ExecuteNonQuery();
        }

        // Act
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE Person SET Name = 'Bob Updated' WHERE Id = 'p2'";
            cmd.ExecuteNonQuery();
        }

        // Assert
        var changes = SyncLogRepository.FetchChanges(_connection, 0, 100);
        var list = ((SyncLogListOk)changes).Value;
        Assert.Equal(2, list.Count);
        Assert.Equal(SyncOperation.Update, list[1].Operation);
    }

    [Fact]
    public void Delete_CreatesTombstone()
    {
        // Arrange
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Person (Id, Name, Email) VALUES ('p3', 'Charlie', 'charlie@test.com')";
            cmd.ExecuteNonQuery();
        }

        // Act
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM Person WHERE Id = 'p3'";
            cmd.ExecuteNonQuery();
        }

        // Assert
        var changes = SyncLogRepository.FetchChanges(_connection, 0, 100);
        var list = ((SyncLogListOk)changes).Value;
        Assert.Equal(2, list.Count);
        Assert.Equal(SyncOperation.Delete, list[1].Operation);
    }

    [Fact]
    public void TriggerSuppression_PreventsLogging()
    {
        // Act - insert with suppression enabled
        SyncSessionManager.EnableSuppression(_connection);
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Person (Id, Name, Email) VALUES ('suppressed', 'Suppressed', 's@test.com')";
            cmd.ExecuteNonQuery();
        }
        SyncSessionManager.DisableSuppression(_connection);

        // Assert - no changes logged
        var changes = SyncLogRepository.FetchChanges(_connection, 0, 100);
        var list = ((SyncLogListOk)changes).Value;
        Assert.Empty(list);
    }

    [Fact]
    public void ApplyChange_WithSuppression_DoesNotEcho()
    {
        // Arrange - simulate incoming change
        var incomingEntry = new SyncLogEntry(
            Version: 1,
            TableName: "Person",
            PkValue: "{\"Id\":\"incoming1\"}",
            Operation: SyncOperation.Insert,
            Payload: "{\"Id\":\"incoming1\",\"Name\":\"Incoming\",\"Email\":\"in@test.com\"}",
            Origin: "remote-origin",
            Timestamp: DateTime.UtcNow.ToString("O")
        );

        // Act - apply with suppression
        SyncSessionManager.EnableSuppression(_connection);
        var result = ChangeApplierSQLite.ApplyChange(_connection, incomingEntry);
        SyncSessionManager.DisableSuppression(_connection);

        // Assert - change applied but not logged
        Assert.IsType<BoolSyncOk>(result);

        var changes = SyncLogRepository.FetchChanges(_connection, 0, 100);
        var list = ((SyncLogListOk)changes).Value;
        Assert.Empty(list);

        // Verify data exists
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Person WHERE Id = 'incoming1'";
        Assert.Equal("Incoming", cmd.ExecuteScalar()?.ToString());
    }
}

#pragma warning disable CA2213 // HttpClient managed by WebApplicationFactory - do not dispose

/// <summary>
/// E2E tests proving REAL sync over HTTP works.
/// Uses WebApplicationFactory for real ASP.NET Core server + real SQLite databases.
/// This is the PROOF that Nimblesite.Sync.Http extension methods work in a real scenario.
/// </summary>
public sealed class HttpSyncE2ETests : IClassFixture<SyncApiWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client;
    private readonly string _serverDbPath;
    private readonly SqliteConnection _serverConn;
    private readonly string _clientDbPath;
    private readonly SqliteConnection _clientConn;
    private readonly string _serverOriginId = Guid.NewGuid().ToString();
    private readonly string _clientOriginId = Guid.NewGuid().ToString();

    public HttpSyncE2ETests(SyncApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();

        // Create server database
        _serverDbPath = Path.Combine(Path.GetTempPath(), $"sync_server_{Guid.NewGuid()}.db");
        _serverConn = new SqliteConnection($"Data Source={_serverDbPath}");
        _serverConn.Open();
        InitializeDatabase(_serverConn, _serverOriginId);

        // Create client database
        _clientDbPath = Path.Combine(Path.GetTempPath(), $"sync_client_{Guid.NewGuid()}.db");
        _clientConn = new SqliteConnection($"Data Source={_clientDbPath}");
        _clientConn.Open();
        InitializeDatabase(_clientConn, _clientOriginId);
    }

    private static void InitializeDatabase(SqliteConnection conn, string originId)
    {
        SyncSchema.CreateSchema(conn);
        SyncSchema.SetOriginId(conn, originId);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Person (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Email TEXT
            );
            """;
        cmd.ExecuteNonQuery();

        TriggerGenerator.CreateTriggers(conn, "Person", NullLogger.Instance);
    }

    private static SyncLogEntry ParseChangeEntry(JsonElement change) =>
        new SyncLogEntry(
            Version: change.GetProperty("version").GetInt64(),
            TableName: change.GetProperty("tableName").GetString() ?? "",
            PkValue: change.GetProperty("pkValue").GetString() ?? "",
            Operation: (SyncOperation)change.GetProperty("operation").GetInt32(),
            Payload: change.GetProperty("payload").GetString() ?? "",
            Origin: change.GetProperty("origin").GetString() ?? "",
            Timestamp: change.GetProperty("timestamp").GetString() ?? ""
        );

    public void Dispose()
    {
        _client.Dispose();
        _serverConn.Close();
        _serverConn.Dispose();
        _clientConn.Close();
        _clientConn.Dispose();

        try
        {
            if (File.Exists(_serverDbPath))
                File.Delete(_serverDbPath);
            if (File.Exists(_clientDbPath))
                File.Delete(_clientDbPath);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Tests that changes inserted on client can be pushed via HTTP and appear on server.
    /// </summary>
    [Fact]
    public async Task PushChanges_InsertsOnClient_AppearsOnServer()
    {
        // Arrange - insert data on client
        using (var cmd = _clientConn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Person (Id, Name, Email) VALUES ('http1', 'HTTP Test', 'http@test.com')";
            cmd.ExecuteNonQuery();
        }

        // Get changes from client
        var clientChanges = SyncLogRepository.FetchChanges(_clientConn, 0, 100);
        Assert.True(clientChanges is SyncLogListOk);
        var changesList = ((SyncLogListOk)clientChanges).Value;
        Assert.Single(changesList);

        // Build request - OriginId is the origin to SKIP (server's own origin prevents echo)
        // We're pushing FROM client, so server should NOT skip client's changes
        var pushRequest = new
        {
            Changes = changesList
                .Select(c => new
                {
                    c.Version,
                    c.TableName,
                    c.PkValue,
                    Operation = c.Operation.ToString().ToLowerInvariant(),
                    c.Payload,
                    c.Origin,
                    c.Timestamp,
                })
                .ToList(),
            OriginId = _serverOriginId,
        };

        var content = new StringContent(
            JsonSerializer.Serialize(pushRequest),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var connStr = Uri.EscapeDataString($"Data Source={_serverDbPath}");

        // Act - push changes via HTTP
        var response = await _client.PostAsync(
            $"/sync/changes?dbType=sqlite&connectionString={connStr}",
            content
        );

        // Assert - HTTP success
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Push failed: {response.StatusCode} - {body}");

        // Verify data appears on server
        using var verifyCmd = _serverConn.CreateCommand();
        verifyCmd.CommandText = "SELECT Name FROM Person WHERE Id = 'http1'";
        var name = verifyCmd.ExecuteScalar()?.ToString();
        Assert.Equal("HTTP Test", name);
    }

    /// <summary>
    /// Tests that changes on server can be pulled via HTTP and applied to client.
    /// </summary>
    [Fact]
    public async Task PullChanges_ChangesOnServer_AppearsOnClient()
    {
        // Arrange - insert data on server
        using (var cmd = _serverConn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Person (Id, Name, Email) VALUES ('pull1', 'Pull Test', 'pull@test.com')";
            cmd.ExecuteNonQuery();
        }

        var connStr = Uri.EscapeDataString($"Data Source={_serverDbPath}");

        // Act - pull changes via HTTP
        var response = await _client.GetAsync(
            $"/sync/changes?fromVersion=0&batchSize=100&dbType=sqlite&connectionString={connStr}"
        );

        // Assert - HTTP success
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Pull failed: {response.StatusCode} - {body}");

        // Parse response (ASP.NET Core uses camelCase by default)
        using var doc = JsonDocument.Parse(body);
        var changesArray = doc.RootElement.GetProperty("changes");
        Assert.True(changesArray.GetArrayLength() > 0, "No changes returned");

        // Apply changes to client (operation is returned as integer enum value)
        foreach (var change in changesArray.EnumerateArray())
        {
            var entry = ParseChangeEntry(change);

            SyncSessionManager.EnableSuppression(_clientConn);
            var result = ChangeApplierSQLite.ApplyChange(_clientConn, entry);
            SyncSessionManager.DisableSuppression(_clientConn);
            Assert.True(result is BoolSyncOk, $"Apply failed: {result}");
        }

        // Verify data appears on client
        using var verifyCmd = _clientConn.CreateCommand();
        verifyCmd.CommandText = "SELECT Name FROM Person WHERE Id = 'pull1'";
        var name = verifyCmd.ExecuteScalar()?.ToString();
        Assert.Equal("Pull Test", name);
    }

    /// <summary>
    /// Tests bidirectional sync - changes go both ways via HTTP.
    /// </summary>
    [Fact]
    public async Task BidirectionalSync_ChangesBothWays_BothDatabasesHaveBothRecords()
    {
        // Arrange - insert different data on each side
        using (var cmd = _serverConn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Person (Id, Name, Email) VALUES ('bidir_server', 'Server Record', 'server@test.com')";
            cmd.ExecuteNonQuery();
        }

        using (var cmd = _clientConn.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO Person (Id, Name, Email) VALUES ('bidir_client', 'Client Record', 'client@test.com')";
            cmd.ExecuteNonQuery();
        }

        var serverConnStr = Uri.EscapeDataString($"Data Source={_serverDbPath}");
        var clientConnStr = Uri.EscapeDataString($"Data Source={_clientDbPath}");

        // Pull from server -> apply to client
        var pullResponse = await _client.GetAsync(
            $"/sync/changes?fromVersion=0&batchSize=100&dbType=sqlite&connectionString={serverConnStr}"
        );
        Assert.True(pullResponse.IsSuccessStatusCode);

        var pullBody = await pullResponse.Content.ReadAsStringAsync();
        using var pullDoc = JsonDocument.Parse(pullBody);

        foreach (var change in pullDoc.RootElement.GetProperty("changes").EnumerateArray())
        {
            var entry = ParseChangeEntry(change);

            if (entry.Origin != _clientOriginId)
            {
                SyncSessionManager.EnableSuppression(_clientConn);
                ChangeApplierSQLite.ApplyChange(_clientConn, entry);
                SyncSessionManager.DisableSuppression(_clientConn);
            }
        }

        // Push from client -> server (OriginId = server's origin to not skip client changes)
        var clientChanges = SyncLogRepository.FetchChanges(_clientConn, 0, 100);
        var clientChangesList = ((SyncLogListOk)clientChanges).Value;

        var pushRequest = new
        {
            Changes = clientChangesList
                .Select(c => new
                {
                    c.Version,
                    c.TableName,
                    c.PkValue,
                    Operation = c.Operation.ToString().ToLowerInvariant(),
                    c.Payload,
                    c.Origin,
                    c.Timestamp,
                })
                .ToList(),
            OriginId = _serverOriginId,
        };

        var content = new StringContent(
            JsonSerializer.Serialize(pushRequest),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var pushResponse = await _client.PostAsync(
            $"/sync/changes?dbType=sqlite&connectionString={serverConnStr}",
            content
        );
        Assert.True(pushResponse.IsSuccessStatusCode);

        // Assert - both databases have both records
        using var serverCmd = _serverConn.CreateCommand();
        serverCmd.CommandText = "SELECT COUNT(*) FROM Person";
        Assert.Equal(
            2L,
            Convert.ToInt64(
                serverCmd.ExecuteScalar(),
                System.Globalization.CultureInfo.InvariantCulture
            )
        );

        using var clientCmd = _clientConn.CreateCommand();
        clientCmd.CommandText = "SELECT COUNT(*) FROM Person";
        Assert.Equal(
            2L,
            Convert.ToInt64(
                clientCmd.ExecuteScalar(),
                System.Globalization.CultureInfo.InvariantCulture
            )
        );
    }

    /// <summary>
    /// Tests sync state endpoint returns correct max version.
    /// </summary>
    [Fact]
    public async Task SyncState_ReturnsCorrectMaxVersion()
    {
        // Arrange - insert multiple records
        for (var i = 0; i < 5; i++)
        {
            using var cmd = _serverConn.CreateCommand();
            cmd.CommandText =
                $"INSERT INTO Person (Id, Name, Email) VALUES ('state{i}', 'Person {i}', 'p{i}@test.com')";
            cmd.ExecuteNonQuery();
        }

        var connStr = Uri.EscapeDataString($"Data Source={_serverDbPath}");

        // Act
        var response = await _client.GetAsync(
            $"/sync/state?dbType=sqlite&connectionString={connStr}"
        );

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        var maxVersion = doc.RootElement.GetProperty("maxVersion").GetInt64();
        Assert.Equal(5L, maxVersion);
    }
}
