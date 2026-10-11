// Implements [CI-TEST-SHARED-COMMAND-PARAMETERS].
// Implements [CI-TEST-SHARED-COMMAND].
using System.Text.Json;
using Tests.Shared;

namespace Nimblesite.Sync.Http.Tests;

#pragma warning disable CA2213 // HttpClient managed by WebApplicationFactory - do not dispose

/// <summary>
/// E2E tests proving REAL sync over HTTP works.
/// Uses WebApplicationFactory for real ASP.NET Core server + real SQLite databases.
/// This is the PROOF that Nimblesite.Sync.Http extension methods work in a real scenario.
/// </summary>
public sealed partial class HttpSyncE2ETests
    : IClassFixture<SyncApiWebApplicationFactory>,
        IDisposable
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
        (_serverDbPath, _serverConn) = global::Tests.Shared.TempSqliteDatabase.Create(
            "sync_server"
        );
        InitializeDatabase(_serverConn, _serverOriginId);

        // Create client database
        (_clientDbPath, _clientConn) = global::Tests.Shared.TempSqliteDatabase.Create(
            "sync_client"
        );
        InitializeDatabase(_clientConn, _clientOriginId);
    }

    private static void InitializeDatabase(SqliteConnection conn, string originId)
    {
        global::Nimblesite.Sync.Http.Tests.CrossDatabaseSyncTests.VerifyReusableWorkflow1(
            configure0: command => command.CommandText = Data_InitializeDatabase_1,
            input0: conn,
            input1: originId
        );

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
        CrossDatabaseSyncTests.AssertPersonInsert(entry, originId);
        using var key = JsonDocument.Parse(Assert.IsType<string>(entry.PkValue));
        Assert.Equal(id, key.RootElement.GetProperty("Id").GetString());
        using var payload = JsonDocument.Parse(Assert.IsType<string>(entry.Payload));
        Assert.Equal(id, payload.RootElement.GetProperty("Id").GetString());
        Assert.Equal(name, payload.RootElement.GetProperty("Name").GetString());
        Assert.Equal(email, payload.RootElement.GetProperty("Email").GetString());
    }

    /// <summary>Parses a camelCase HTTP change payload into a sync log entry.</summary>
    private static SyncLogEntry ParseEntry(JsonElement change) =>
        new(
            Version: change.GetProperty("version").GetInt64(),
            TableName: change.GetProperty("tableName").GetString() ?? "",
            PkValue: change.GetProperty("pkValue").GetString() ?? "",
            Operation: (SyncOperation)change.GetProperty("operation").GetInt32(),
            Payload: change.GetProperty("payload").GetString() ?? "",
            Origin: change.GetProperty("origin").GetString() ?? "",
            Timestamp: change.GetProperty("timestamp").GetString() ?? ""
        );

    /// <summary>Builds a JSON push request body for the given changes and origin.</summary>
    private static string BuildPushJson(IReadOnlyList<SyncLogEntry> changes, string originId) =>
        JsonSerializer.Serialize(
            new
            {
                Changes = changes
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
                OriginId = originId,
            }
        );

    /// <summary>Builds a JSON push request content for the given changes and origin.</summary>
    private static StringContent BuildPushContent(
        IReadOnlyList<SyncLogEntry> changes,
        string originId
    ) => new(BuildPushJson(changes, originId), System.Text.Encoding.UTF8, "application/json");

    /// <summary>Asserts the number of rows in the Person table.</summary>
    private static void AssertPersonCount(SqliteConnection connection, long expected) =>
        Assert.Equal(
            expected,
            global::Tests.Shared.DatabaseCommands.ScalarInt64(
                connection: connection,
                configure: command => command.CommandText = "SELECT COUNT(*) FROM Person"
            )
        );

    /// <summary>Asserts the single logged client insert and returns the change list.</summary>
    private IReadOnlyList<SyncLogEntry> ExpectLoggedClientInsert(
        Outcome.Result<IReadOnlyList<SyncLogEntry>, SyncError> changes,
        string id,
        string name,
        string email
    ) =>
        VerifyReusableWorkflow8(
            input0: changes,
            input1: AssertLoggedInsert,
            input2: _clientOriginId,
            value0: id,
            value1: name,
            value2: email
        );

    /// <summary>Reads a response body after asserting the action succeeded.</summary>
    private static async Task<string> ReadBodyAssertSuccess(
        HttpResponseMessage response,
        string action
    )
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"{action} failed: {response.StatusCode} - {body}"
        );
        return body;
    }

    /// <summary>Issues a GET request, asserting success, and parses the JSON body.</summary>
    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.IsSuccessStatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    /// <summary>Asserts an OK response and parses its JSON body.</summary>
    private static async Task<JsonDocument> ReadJsonOkAsync(HttpResponseMessage response)
    {
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    public void Dispose()
    {
        _client.Dispose();
        VerifyReusableWorkflow3(
            input0: _serverConn,
            input1: _clientConn,
            input2: _serverDbPath,
            input3: _clientDbPath
        );
    }

    /// <summary>
    /// Tests that changes inserted on client can be pushed via HTTP and appear on server.
    /// </summary>
    [Fact]
    public async Task PushChanges_InsertsOnClient_AppearsOnServer()
    {
        // Arrange - insert data on client
        var clientChanges =
            global::Nimblesite.Sync.Http.Tests.CrossDatabaseSyncTests.VerifyReusableWorkflow2(
                configure0: cmd =>
                    cmd.CommandText =
                        "INSERT INTO Person (Id, Name, Email) VALUES ('http1', 'HTTP Test', 'http@test.com')",
                input0: _clientConn
            );
        var changesList = ExpectLoggedClientInsert(
            clientChanges,
            "http1",
            "HTTP Test",
            "http@test.com"
        );

        // Build request - OriginId is the origin to SKIP (server's own origin prevents echo)
        // We're pushing FROM client, so server should NOT skip client's changes
        var requestJson = BuildPushJson(changesList, _serverOriginId);
        var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");

        var connStr = Uri.EscapeDataString($"Data Source={_serverDbPath}");

        // Act - push changes via HTTP
        var response = await _client.PostAsync(
            $"/sync/changes?dbType=sqlite&connectionString={connStr}",
            content
        );

        // Assert - HTTP success
        _ = await ReadBodyAssertSuccess(response, "Push");

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
        AssertPersonCount(_serverConn, 1L);
    }

    /// <summary>
    /// Tests that changes on server can be pulled via HTTP and applied to client.
    /// </summary>
    [Fact]
    public async Task PullChanges_ChangesOnServer_AppearsOnClient()
    {
        // Arrange - insert data on server
        var connStr = VerifyReusableWorkflow9(
            configure0: cmd =>
                cmd.CommandText =
                    "INSERT INTO Person (Id, Name, Email) VALUES ('pull1', 'Pull Test', 'pull@test.com')",
            input0: _serverConn,
            input1: _serverDbPath
        );

        // Act - pull changes via HTTP
        var response = await _client.GetAsync(
            $"/sync/changes?fromVersion=0&batchSize=100&dbType=sqlite&connectionString={connStr}"
        );

        // Assert - HTTP success
        var body = await ReadBodyAssertSuccess(response, "Pull");

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
            var entry = ParseEntry(change);

            var result = CrossDatabaseSyncTests.ApplyChangeWithSuppression(_clientConn, entry);
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
        using var nextPageJson = await ReadJsonOkAsync(nextPage);
        Assert.Empty(nextPageJson.RootElement.GetProperty("changes").EnumerateArray());
    }

    /// <summary>
    /// Tests bidirectional sync - changes go both ways via HTTP.
    /// </summary>
    [Fact]
    public async Task BidirectionalSync_ChangesBothWays_BothDatabasesHaveBothRecords()
    {
        // Arrange - insert different data on each side
        CrossDatabaseSyncTests.ExecuteSql(
            _serverConn,
            "INSERT INTO Person (Id, Name, Email) VALUES ('bidir_server', 'Server Record', 'server@test.com')"
        );

        var serverConnStr = VerifyReusableWorkflow9(
            configure0: cmd =>
                cmd.CommandText =
                    "INSERT INTO Person (Id, Name, Email) VALUES ('bidir_client', 'Client Record', 'client@test.com')",
            input0: _clientConn,
            input1: _serverDbPath
        );
        var clientConnStr = Uri.EscapeDataString($"Data Source={_clientDbPath}");

        // Pull from server -> apply to client
        using var pullDoc = await GetJsonAsync(
            _client,
            $"/sync/changes?fromVersion=0&batchSize=100&dbType=sqlite&connectionString={serverConnStr}"
        );
        Assert.Single(pullDoc.RootElement.GetProperty("changes").EnumerateArray());

        foreach (var change in pullDoc.RootElement.GetProperty("changes").EnumerateArray())
        {
            var entry = ParseEntry(change);

            if (entry.Origin != _clientOriginId)
            {
                CrossDatabaseSyncTests.ApplyChangeWithSuppression(_clientConn, entry);
            }
        }

        // Push from client -> server (OriginId = server's origin to not skip client changes)
        var clientChanges = SyncLogRepository.FetchChanges(_clientConn, 0, 100);
        var clientChangesList = ExpectLoggedClientInsert(
            clientChanges,
            "bidir_client",
            "Client Record",
            "client@test.com"
        );

        var content = BuildPushContent(clientChangesList, _serverOriginId);

        var pushResponse = await _client.PostAsync(
            $"/sync/changes?dbType=sqlite&connectionString={serverConnStr}",
            content
        );
        Assert.True(pushResponse.IsSuccessStatusCode);

        // Assert - both databases have both records
        AssertPersonCount(_serverConn, 2L);
        AssertPersonCount(_clientConn, 2L);

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
            CrossDatabaseSyncTests.ExecuteSql(
                _serverConn,
                $"INSERT INTO Person (Id, Name, Email) VALUES ('state{i}', 'Person {i}', 'p{i}@test.com')"
            );
        }

        var connStr = Uri.EscapeDataString($"Data Source={_serverDbPath}");

        // Act
        using var doc = await GetJsonAsync(
            _client,
            $"/sync/state?dbType=sqlite&connectionString={connStr}"
        );

        var maxVersion = doc.RootElement.GetProperty("maxVersion").GetInt64();
        Assert.Equal(5L, maxVersion);

        using var pull = await _client.GetAsync(
            $"/sync/changes?fromVersion=0&batchSize=2&dbType=sqlite&connectionString={connStr}"
        );
        using var pullJson = await ReadJsonOkAsync(pull);
        var page = pullJson.RootElement.GetProperty("changes").EnumerateArray().ToArray();
        Assert.Equal(2, page.Length);
        Assert.Equal(1L, page[0].GetProperty("version").GetInt64());
        Assert.Equal(2L, page[1].GetProperty("version").GetInt64());
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow3(
        SqliteConnection input0,
        SqliteConnection input1,
        string input2,
        string input3
    )
    {
        input0.Close();
        input0.Dispose();
        input1.Close();
        input1.Dispose();

        try
        {
            if (File.Exists(input2))
                File.Delete(input2);
            if (File.Exists(input3))
                File.Delete(input3);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static IReadOnlyList<SyncLogEntry> VerifyReusableWorkflow8(
        Outcome.Result<IReadOnlyList<SyncLogEntry>, SyncError> input0,
        Action<SyncLogEntry, string, string, string, string> input1,
        string input2,
        string value0,
        string value1,
        string value2
    )
    {
        var changesList = Assert.IsType<SyncLogListOk>(input0).Value;
        input1(
            arg1: Assert.Single(changesList),
            arg2: value0,
            arg3: value1,
            arg4: value2,
            arg5: input2
        );

        return changesList;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static string VerifyReusableWorkflow9(
        SqliteConnection input0,
        string input1,
        global::System.Action<global::System.Data.Common.DbCommand> configure0
    )
    { // Arrange - insert data on server
        DatabaseCommands.Execute(connection: input0, configure: configure0);

        var connStr = Uri.EscapeDataString($"Data Source={input1}");

        return connStr;
    }
}
