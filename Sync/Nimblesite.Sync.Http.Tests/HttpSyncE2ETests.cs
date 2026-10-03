using System.Text.Json;

namespace Nimblesite.Sync.Http.Tests;

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

    private static void AssertPerson(
        SqliteConnection connection,
        string id,
        string expectedName,
        string expectedEmail
    )
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name, Email FROM Person WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read(), $"Missing Person {id}");
        Assert.Equal(expectedName, reader.GetString(0));
        Assert.Equal(expectedEmail, reader.GetString(1));
        Assert.False(reader.Read(), $"Duplicate Person {id}");
    }

    private static void AssertLoggedInsert(
        SyncLogEntry entry,
        string id,
        string name,
        string email,
        string originId
    )
    {
        Assert.True(entry.Version > 0);
        Assert.Equal("Person", entry.TableName);
        Assert.Equal(SyncOperation.Insert, entry.Operation);
        Assert.Equal(originId, entry.Origin);
        using var key = JsonDocument.Parse(Assert.IsType<string>(entry.PkValue));
        Assert.Equal(id, key.RootElement.GetProperty("Id").GetString());
        using var payload = JsonDocument.Parse(Assert.IsType<string>(entry.Payload));
        Assert.Equal(id, payload.RootElement.GetProperty("Id").GetString());
        Assert.Equal(name, payload.RootElement.GetProperty("Name").GetString());
        Assert.Equal(email, payload.RootElement.GetProperty("Email").GetString());
    }

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
        var changesList = Assert.IsType<SyncLogListOk>(clientChanges).Value;
        AssertLoggedInsert(
            Assert.Single(changesList),
            "http1",
            "HTTP Test",
            "http@test.com",
            _clientOriginId
        );

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

        var requestJson = JsonSerializer.Serialize(pushRequest);
        var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");

        var connStr = Uri.EscapeDataString($"Data Source={_serverDbPath}");

        // Act - push changes via HTTP
        var response = await _client.PostAsync(
            $"/sync/changes?dbType=sqlite&connectionString={connStr}",
            content
        );

        // Assert - HTTP success
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Push failed: {response.StatusCode} - {body}");

        AssertPerson(_serverConn, "http1", "HTTP Test", "http@test.com");

        using var repeatContent = new StringContent(
            requestJson,
            System.Text.Encoding.UTF8,
            "application/json"
        );
        using var repeat = await _client.PostAsync(
            $"/sync/changes?dbType=sqlite&connectionString={connStr}",
            repeatContent
        );
        Assert.True(repeat.IsSuccessStatusCode, await repeat.Content.ReadAsStringAsync());
        AssertPerson(_serverConn, "http1", "HTTP Test", "http@test.com");
        using var count = _serverConn.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM Person";
        Assert.Equal(
            1L,
            Convert.ToInt64(
                count.ExecuteScalar(),
                System.Globalization.CultureInfo.InvariantCulture
            )
        );
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
        var pulled = Assert.Single(changesArray.EnumerateArray());
        Assert.True(pulled.GetProperty("version").GetInt64() > 0);
        Assert.Equal("Person", pulled.GetProperty("tableName").GetString());
        Assert.Equal((int)SyncOperation.Insert, pulled.GetProperty("operation").GetInt32());
        Assert.Equal(_serverOriginId, pulled.GetProperty("origin").GetString());
        using var pulledPayload = JsonDocument.Parse(
            pulled.GetProperty("payload").GetString() ?? ""
        );
        Assert.Equal("pull1", pulledPayload.RootElement.GetProperty("Id").GetString());
        Assert.Equal("Pull Test", pulledPayload.RootElement.GetProperty("Name").GetString());

        // Apply changes to client (operation is returned as integer enum value)
        foreach (var change in changesArray.EnumerateArray())
        {
            var entry = new SyncLogEntry(
                Version: change.GetProperty("version").GetInt64(),
                TableName: change.GetProperty("tableName").GetString() ?? "",
                PkValue: change.GetProperty("pkValue").GetString() ?? "",
                Operation: (SyncOperation)change.GetProperty("operation").GetInt32(),
                Payload: change.GetProperty("payload").GetString() ?? "",
                Origin: change.GetProperty("origin").GetString() ?? "",
                Timestamp: change.GetProperty("timestamp").GetString() ?? ""
            );

            SyncSessionManager.EnableSuppression(_clientConn);
            var result = ChangeApplierSQLite.ApplyChange(_clientConn, entry);
            SyncSessionManager.DisableSuppression(_clientConn);
            Assert.True(result is BoolSyncOk, $"Apply failed: {result}");
        }

        AssertPerson(_clientConn, "pull1", "Pull Test", "pull@test.com");
        Assert.Empty(
            Assert.IsType<SyncLogListOk>(SyncLogRepository.FetchChanges(_clientConn, 0, 100)).Value
        );

        var lastVersion = pulled.GetProperty("version").GetInt64();
        using var nextPage = await _client.GetAsync(
            $"/sync/changes?fromVersion={lastVersion}&batchSize=100&dbType=sqlite&connectionString={connStr}"
        );
        Assert.Equal(System.Net.HttpStatusCode.OK, nextPage.StatusCode);
        using var nextPageJson = JsonDocument.Parse(await nextPage.Content.ReadAsStringAsync());
        Assert.Empty(nextPageJson.RootElement.GetProperty("changes").EnumerateArray());
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
        Assert.Single(pullDoc.RootElement.GetProperty("changes").EnumerateArray());

        foreach (var change in pullDoc.RootElement.GetProperty("changes").EnumerateArray())
        {
            var entry = new SyncLogEntry(
                Version: change.GetProperty("version").GetInt64(),
                TableName: change.GetProperty("tableName").GetString() ?? "",
                PkValue: change.GetProperty("pkValue").GetString() ?? "",
                Operation: (SyncOperation)change.GetProperty("operation").GetInt32(),
                Payload: change.GetProperty("payload").GetString() ?? "",
                Origin: change.GetProperty("origin").GetString() ?? "",
                Timestamp: change.GetProperty("timestamp").GetString() ?? ""
            );

            if (entry.Origin != _clientOriginId)
            {
                SyncSessionManager.EnableSuppression(_clientConn);
                ChangeApplierSQLite.ApplyChange(_clientConn, entry);
                SyncSessionManager.DisableSuppression(_clientConn);
            }
        }

        // Push from client -> server (OriginId = server's origin to not skip client changes)
        var clientChanges = SyncLogRepository.FetchChanges(_clientConn, 0, 100);
        var clientChangesList = Assert.IsType<SyncLogListOk>(clientChanges).Value;
        AssertLoggedInsert(
            Assert.Single(clientChangesList),
            "bidir_client",
            "Client Record",
            "client@test.com",
            _clientOriginId
        );

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
        AssertPerson(_serverConn, "bidir_server", "Server Record", "server@test.com");
        AssertPerson(_serverConn, "bidir_client", "Client Record", "client@test.com");
        AssertPerson(_clientConn, "bidir_server", "Server Record", "server@test.com");
        AssertPerson(_clientConn, "bidir_client", "Client Record", "client@test.com");
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

        using var pull = await _client.GetAsync(
            $"/sync/changes?fromVersion=0&batchSize=2&dbType=sqlite&connectionString={connStr}"
        );
        Assert.Equal(System.Net.HttpStatusCode.OK, pull.StatusCode);
        using var pullJson = JsonDocument.Parse(await pull.Content.ReadAsStringAsync());
        var page = pullJson.RootElement.GetProperty("changes").EnumerateArray().ToArray();
        Assert.Equal(2, page.Length);
        Assert.Equal(1L, page[0].GetProperty("version").GetInt64());
        Assert.Equal(2L, page[1].GetProperty("version").GetInt64());
    }
}
