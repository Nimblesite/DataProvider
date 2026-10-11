using System.Net;
using System.Text;
using System.Text.Json;

namespace Nimblesite.Sync.Http.Tests;

public sealed partial record HttpEndpointPlatformTests
{
    // Implements [SYNC-HTTP-PROVIDERS]. Every provider runs identical CRUD and echo assertions.
    [Theory]
    [MemberData(nameof(HttpPush_ReplayUpdateDeleteAndEchoHaveIdenticalBehaviorInlineCases))]
    public Task HttpPush_ReplayUpdateDeleteAndEchoHaveIdenticalBehavior(string provider) =>
        WithTargetAsync(
            provider,
            async connectionString =>
            {
                using var client = _factory.CreateClient();
                var id = Guid.Parse("cefe2151-5420-4e4f-a9a6-13944e6cfd07");
                await PushAsync(
                    client,
                    provider,
                    connectionString,
                    Entry(id, "insert", "Ada"),
                    expected: 1
                );
                await PushAsync(
                    client,
                    provider,
                    connectionString,
                    Entry(id, "insert", "Ada"),
                    expected: 1
                );
                await AssertPersonAsync(provider, connectionString, id);
                await PushAsync(
                    client,
                    provider,
                    connectionString,
                    Entry(id, "update", "Updated"),
                    expected: 1
                );
                await AssertPersonAsync(provider, connectionString, id, expectedName: "Updated");
                await PushAsync(
                    client,
                    provider,
                    connectionString,
                    Entry(id, "delete", "Ada", origin: "server-origin"),
                    expected: 0
                );
                await AssertPersonAsync(provider, connectionString, id, expectedName: "Updated");
                Assert.Equal(
                    0L,
                    SyncHelpers.GetMaxVersion(connectionString, provider, NullLogger.Instance)
                );
                await PushAsync(
                    client,
                    provider,
                    connectionString,
                    Entry(id, "delete", "Ada"),
                    expected: 1
                );
                await PushAsync(
                    client,
                    provider,
                    connectionString,
                    Entry(id, "delete", "Ada"),
                    expected: 1
                );
                Assert.Equal(0L, CountPeople(provider, connectionString));
            }
        );

    // Implements [SYNC-HTTP-PROVIDERS] and [SYNC-TRIGGER-SHARED-GENERATION].
    [Theory]
    [MemberData(nameof(LocalWrites_LogInsertUpdateDeleteAndPullInVersionOrderInlineCases))]
    public Task LocalWrites_LogInsertUpdateDeleteAndPullInVersionOrder(string provider) =>
        WithTargetAsync(
            provider,
            async connectionString =>
            {
                var id = Guid.Parse("c745af71-d9d2-4e55-9658-3bb4b9d5f591");
                ApplyLocal(provider, connectionString, Entry(id, "insert", "O'Brien 雪"));
                ApplyLocal(provider, connectionString, Entry(id, "update", "Updated"));
                ApplyLocal(provider, connectionString, Entry(id, "delete", "Updated"));
                var changes = SyncHelpers.FetchChanges(
                    connectionString,
                    provider,
                    0,
                    100,
                    NullLogger.Instance
                );
                Assert.Equal(3, changes.Count);
                Assert.Equal(
                    new[] { SyncOperation.Insert, SyncOperation.Update, SyncOperation.Delete },
                    changes.Select(change => change.Operation)
                );
                Assert.All(changes, change => Assert.Equal("server-origin", change.Origin));
                Assert.Equal(3, changes.Select(change => change.Version).Distinct().Count());
                Assert.Equal(
                    changes.Select(change => change.Version).Order(),
                    changes.Select(change => change.Version)
                );
                using var payload = JsonDocument.Parse(Assert.IsType<string>(changes[0].Payload));
                Assert.Equal("O'Brien 雪", payload.RootElement.GetProperty("name").GetString());
                using var client = _factory.CreateClient();
                await AssertPullPageAsync(
                    client,
                    provider,
                    connectionString,
                    changes[0].Version,
                    changes[1].Version
                );
                Assert.Equal(
                    changes[^1].Version,
                    SyncHelpers.GetMaxVersion(connectionString, provider, NullLogger.Instance)
                );
            }
        );

    // Implements [SYNC-HTTP-PROVIDERS]. Deferred writes must not be acknowledged as applied.
    [Theory]
    [MemberData(nameof(HttpPush_ForeignKeyFailureCanRetryAfterParentArrivesInlineCases))]
    public Task HttpPush_ForeignKeyFailureCanRetryAfterParentArrives(string provider) =>
        WithTargetAsync(
            provider,
            async connectionString =>
            {
                using var client = _factory.CreateClient();
                var parentId = Guid.Parse("06b66469-1cf7-4006-be17-3a855c820db2");
                var childId = Guid.Parse("315bb510-e2ad-4965-b539-270a0cbdbba3");
                var child = Entry(childId, "insert", "Child") with
                {
                    Payload = JsonSerializer.Serialize(
                        new
                        {
                            id = childId,
                            name = "Child",
                            email = "ada@example.test",
                            manager_id = parentId,
                        }
                    ),
                };
                await PushAsync(client, provider, connectionString, child, expected: 0);
                Assert.Equal(0L, CountPeople(provider, connectionString));
                await PushAsync(
                    client,
                    provider,
                    connectionString,
                    Entry(parentId, "insert", "Parent"),
                    expected: 1
                );
                await PushAsync(client, provider, connectionString, child, expected: 1);
                Assert.Equal(2L, CountPeople(provider, connectionString));
            }
        );

    // Implements [SYNC-HTTP-PROVIDERS]. CHECK errors are distinct from deferred foreign keys.
    [Theory]
    [MemberData(nameof(CheckConstraintFailuresReturnErrorAndLeaveConnectionUsableInlineCases))]
    public Task CheckConstraintFailuresReturnErrorAndLeaveConnectionUsable(
        string provider,
        bool tableChecks
    ) =>
        WithTargetAsync(
            provider,
            connectionString =>
            {
                var id = Guid.Parse("cc6a230b-0b8b-4a56-a9b2-667dc67ed5c6");
                Assert.IsType<Outcome.Result<bool, SyncError>.Error<bool, SyncError>>(
                    ApplyLocalResult(provider, connectionString, Entry(id, "insert", ""))
                );
                ApplyLocal(provider, connectionString, Entry(id, "insert", "Valid"));
                Assert.Equal(1L, CountPeople(provider, connectionString));
                return Task.CompletedTask;
            },
            tableChecks: tableChecks
        );

    private Task WithTargetAsync(
        string provider,
        Func<string, Task> run,
        bool tableChecks = false
    ) =>
        SyncDatabaseTargets.WithTargetAsync(
            provider: provider,
            postgres: _postgres,
            sqlServer: _sqlServer,
            run: run,
            tableChecks: tableChecks
        );

    private static SyncLogEntryDto Entry(
        Guid id,
        string operation,
        string name,
        string origin = "remote-origin"
    ) =>
        new(
            Version: 1,
            TableName: "person",
            PkValue: JsonSerializer.Serialize(new { id }),
            Operation: operation,
            Payload: operation == "delete"
                ? null
                : JsonSerializer.Serialize(
                    new
                    {
                        id,
                        name,
                        email = "ada@example.test",
                    }
                ),
            Origin: origin,
            Timestamp: "2024-01-01T00:00:00Z"
        );

    private static async Task PushAsync(
        HttpClient client,
        string provider,
        string connectionString,
        SyncLogEntryDto entry,
        int expected
    )
    {
        using var content = new StringContent(
            JsonSerializer.Serialize(new { OriginId = "server-origin", Changes = new[] { entry } }),
            Encoding.UTF8,
            "application/json"
        );
        using var response = await client
            .PostAsync($"/sync/changes?{Query(provider, connectionString)}", content)
            .ConfigureAwait(true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync().ConfigureAwait(true)
        );
        Assert.Equal(expected, json.RootElement.GetProperty("applied").GetInt32());
    }

    private static void ApplyLocal(
        string provider,
        string connectionString,
        SyncLogEntryDto change
    ) =>
        Assert.True(
            Assert.IsType<BoolSyncOk>(ApplyLocalResult(provider, connectionString, change)).Value
        );

    private static Outcome.Result<bool, SyncError>? ApplyLocalResult(
        string provider,
        string connectionString,
        SyncLogEntryDto change
    )
    {
        using var connection = OpenConnection(provider, connectionString);
        var entry = new SyncLogEntry(
            Version: change.Version,
            TableName: change.TableName,
            PkValue: change.PkValue,
            Operation: Enum.Parse<SyncOperation>(change.Operation, ignoreCase: true),
            Payload: change.Payload,
            Origin: change.Origin,
            Timestamp: change.Timestamp
        );
        var result = connection switch
        {
            SqliteConnection sqlite => ChangeApplierSQLite.ApplyChange(sqlite, entry),
            NpgsqlConnection postgres => PostgresChangeApplier.ApplyChange(
                postgres,
                entry,
                NullLogger.Instance
            ),
            Microsoft.Data.SqlClient.SqlConnection sqlServer =>
                Nimblesite.Sync.SqlServer.SqlServerChangeApplier.ApplyChange(
                    sqlServer,
                    entry,
                    NullLogger.Instance
                ),
            _ => null,
        };
        return result;
    }

    private static System.Data.Common.DbConnection OpenConnection(
        string provider,
        string connectionString
    )
    {
        System.Data.Common.DbConnection connection = provider switch
        {
            "sqlite" => new SqliteConnection(connectionString),
            "postgres" => new NpgsqlConnection(connectionString),
            _ => new Microsoft.Data.SqlClient.SqlConnection(connectionString),
        };
        connection.Open();
        return connection;
    }

    private static long CountPeople(string provider, string connectionString)
    {
        using var connection = OpenConnection(provider, connectionString);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM person";
        return Convert.ToInt64(
            command.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture
        );
    }

    private static async Task AssertPullPageAsync(
        HttpClient client,
        string provider,
        string connectionString,
        long fromVersion,
        long nextVersion
    )
    {
        using var response = await client
            .GetAsync(
                $"/sync/changes?fromVersion={fromVersion}&batchSize=1&{Query(provider, connectionString)}"
            )
            .ConfigureAwait(true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync().ConfigureAwait(true)
        );
        Assert.True(json.RootElement.GetProperty("hasMore").GetBoolean());
        Assert.Equal(nextVersion, json.RootElement.GetProperty("toVersion").GetInt64());
        Assert.Equal(
            nextVersion,
            Assert
                .Single(json.RootElement.GetProperty("changes").EnumerateArray())
                .GetProperty("version")
                .GetInt64()
        );
    }

    private static string Query(string provider, string connectionString) =>
        $"dbType={provider}&connectionString={Uri.EscapeDataString(connectionString)}";
}
