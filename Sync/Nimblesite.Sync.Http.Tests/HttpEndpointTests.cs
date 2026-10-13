// Implements [CI-TEST-SHARED-COMMAND-PARAMETERS].
// Implements [CI-TEST-SHARED-COMMAND].
using System.Net;
using System.Text.Json;
using Tests.Shared;
using static global::Tests.Shared.ResultAssertions;

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
public sealed partial class HttpEndpointTests : IClassFixture<SyncApiWebApplicationFactory>
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
        await AssertStatusAndBodyContains(response, HttpStatusCode.OK, "Healthy");
    }

    #endregion

    #region Pull Changes Endpoint

    [Theory]
    [MemberData(nameof(PullChanges_WithoutConnectionString_ReturnsBadRequestInlineCases))]
    public async Task PullChanges_WithoutConnectionString_ReturnsBadRequest(string provider)
    {
        // Act
        var response = await _client.GetAsync(
            $"/sync/changes?fromVersion=0&batchSize=100&dbType={provider}"
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

    [Theory]
    [MemberData(nameof(PushChanges_WithoutBody_ReturnsBadRequestInlineCases))]
    public async Task PushChanges_WithoutBody_ReturnsBadRequest(string provider)
    {
        // Arrange
        var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(
            $"/sync/changes?dbType={provider}&connectionString=test",
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
        var tempDbPath = global::Tests.Shared.TempSqliteDatabase.PathFor("temp_test");
        var response = await _client.PostAsync(
            $"/sync/changes?dbType=sqlite&connectionString=Data Source={Uri.EscapeDataString(tempDbPath)}",
            content
        );

        // Assert
        await AssertStatusAndBodyContains(response, HttpStatusCode.BadRequest, "10000");
    }

    #endregion

    #region Register Client Endpoint

    [Theory]
    [MemberData(nameof(RegisterClient_WithoutOriginId_ReturnsBadRequestInlineCases))]
    public async Task RegisterClient_WithoutOriginId_ReturnsBadRequest(string provider)
    {
        // Arrange
        var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync(
            $"/sync/clients?dbType={provider}&connectionString=test",
            content
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(RegisterClient_WithoutConnectionString_ReturnsBadRequestInlineCases))]
    public async Task RegisterClient_WithoutConnectionString_ReturnsBadRequest(string provider)
    {
        // Arrange
        var request = new { OriginId = Guid.NewGuid().ToString(), LastSyncVersion = 0L };
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        // Act
        var response = await _client.PostAsync($"/sync/clients?dbType={provider}", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Sync State Endpoint

    [Theory]
    [MemberData(nameof(SyncState_WithoutConnectionString_ReturnsBadRequestInlineCases))]
    public async Task SyncState_WithoutConnectionString_ReturnsBadRequest(string provider)
    {
        // Act
        var response = await _client.GetAsync($"/sync/state?dbType={provider}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    private static async Task AssertStatusAndBodyContains(
        HttpResponseMessage response,
        HttpStatusCode status,
        string expected
    )
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(expected, body);
    }
}

/// <summary>
/// E2E tests with real SQLite database for HTTP endpoints.
/// </summary>
public sealed partial class HttpEndpointWithDatabaseTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _connection;
    private readonly string _originId = Guid.NewGuid().ToString();

    public HttpEndpointWithDatabaseTests()
    {
        (_dbPath, _connection) = global::Tests.Shared.TempSqliteDatabase.Create("sync_test");

        // Initialize sync schema
        global::Tests.Shared.TempSqliteDatabase.InitializeSync(
            connection: _connection,
            origin: _originId
        );

        // Create test table with triggers
        CrossDatabaseSyncTests.ExecuteSql(_connection, Data_Shared_1);

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
        global::Tests.Shared.ResultAssertions.ExactOk(actual: result);
        Assert.Equal(_originId, (Ok(actual: result)).Value);
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

        var list = (Assert.IsType<SyncLogListOk>(changes)).Value;
        Assert.Single(list);
        Assert.Equal("Person", list[0].TableName);
        Assert.Equal(SyncOperation.Insert, list[0].Operation);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(Update_CreatesChangelog_ContextCasesData))]
    public void Update_CreatesChangelog_ContextCases(
        string caseName,
        string value0,
        string value1,
        global::Nimblesite.Sync.Core.SyncOperation value2
    )
    {
        NamedCase(caseName: caseName);
        ExerciseProjectionCaseMatrix1(
            input0: InsertAndFetchLogEntries,
            value0: value0,
            value1: value1,
            value2: value2
        );
    }

    [Fact]
    public void TriggerSuppression_PreventsLogging()
    {
        // Act - insert with suppression enabled
        global::Nimblesite.Sync.Http.Tests.CrossDatabaseSyncTests.VerifyReusableWorkflow7(
            configure0: cmd =>
                cmd.CommandText =
                    "INSERT INTO Person (Id, Name, Email) VALUES ('suppressed', 'Suppressed', 's@test.com')",
            input0: _connection
        );
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
        var result = CrossDatabaseSyncTests.ApplyChangeWithSuppression(_connection, incomingEntry);

        // Assert - change applied but not logged
        global::Tests.Shared.ResultAssertions.ExactOk(actual: result);

        var list = (Ok(read: () => SyncLogRepository.FetchChanges(_connection, 0, 100))).Value;
        Assert.Empty(list);

        // Verify data exists
        Assert.Equal(
            "Incoming",
            CrossDatabaseSyncTests.ScalarString(
                _connection,
                "SELECT Name FROM Person WHERE Id = 'incoming1'"
            )
        );
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private global::System.Collections.Generic.IReadOnlyList<global::Nimblesite.Sync.Core.SyncLogEntry> InsertAndFetchLogEntries(
        string value0,
        string value1
    )
    { // Arrange
        DatabaseCommands.Execute(connection: _connection, configure: cmd => cmd.CommandText = value0);

        // Act
        var list = global::Nimblesite.Sync.Http.Tests.CrossDatabaseSyncTests.VerifySharedPhase1(
            configure0: cmd => cmd.CommandText = value1,
            input0: _connection
        );
        Assert.Equal(2, list.Count);

        return list;
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private void ExerciseProjectionCaseMatrix1(
        Func<string, string, IReadOnlyList<SyncLogEntry>> input0,
        string value0,
        string value1,
        global::Nimblesite.Sync.Core.SyncOperation value2
    )
    { // Arrange
        var list = input0(arg1: value0, arg2: value1);
        Assert.Equal(value2, list[1].Operation);
    }
}
