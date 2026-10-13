using SqliteConnection = Microsoft.Data.Sqlite.SqliteConnection;

namespace Tests.Shared;

// Implements [SYNC-TEST-SHARED-SQLITE-HELPERS].
/// <summary>
/// Shared SQLite scenario helpers for Sync test projects. Each method keeps the
/// original SQL text, parameter values and parameter prefixes used by the
/// caller's native schema so the observable database effect is unchanged.
/// </summary>
internal static class SyncFixtureSqliteHelpers
{
    // Implements [SYNC-TEST-SHARED-SQLITE-HELPERS].
    internal static string? GetName(SqliteConnection db, string table, string id) =>
        DatabaseCommands.Scalar(
            connection: db,
            configure: command => command.CommandText = $"SELECT Name FROM {table} WHERE Id = $id",
            parameters: () => [("$id", id)]
        )
            is string name
            ? name
            : null;

    internal static string? GetPersonName(SqliteConnection db, string id) =>
        GetName(db, "Person", id);

    internal static string? GetParentName(SqliteConnection db, string id) =>
        GetName(db, "Parent", id);

    internal static string? GetChildName(SqliteConnection db, string id) =>
        GetName(db, "Child", id);

    // Name-only Person schema (Id, Name): used by Nimblesite.Sync.Tests.
    internal static void ExecutePerson(SqliteConnection db, string sql, string id, string name) =>
        DatabaseCommands.Execute(
            connection: db,
            configure: cmd => cmd.CommandText = sql,
            parameters: () => [("$id", id), ("$name", name)]
        );

    internal static void InsertPerson(SqliteConnection db, string id, string name) =>
        ExecutePerson(
            db: db,
            sql: "INSERT INTO Person (Id, Name) VALUES ($id, $name)",
            id: id,
            name: name
        );

    internal static void UpdatePerson(SqliteConnection db, string id, string name) =>
        ExecutePerson(
            db: db,
            sql: "UPDATE Person SET Name = $name WHERE Id = $id",
            id: id,
            name: name
        );

    internal static void InsertParent(SqliteConnection db, string id, string name) =>
        ExecutePerson(
            db: db,
            sql: "INSERT INTO Parent (Id, Name) VALUES ($id, $name)",
            id: id,
            name: name
        );

    internal static void InsertChild(
        SqliteConnection db,
        string id,
        string parentId,
        string name
    ) =>
        DatabaseCommands.Execute(
            connection: db,
            configure: cmd =>
                cmd.CommandText =
                    "INSERT INTO Child (Id, ParentId, Name) VALUES ($id, $parentId, $name)",
            parameters: () => [("$id", id), ("$parentId", parentId), ("$name", name)]
        );

    // Email Person schema (Id, Name, Email): used by Nimblesite.Sync.SQLite.Tests.
    internal static void ExecutePerson(
        SqliteConnection db,
        string sql,
        string id,
        string name,
        string email
    ) =>
        DatabaseCommands.Execute(
            connection: db,
            configure: cmd => cmd.CommandText = sql,
            parameters: () => [("@id", id), ("@name", name), ("@email", email)]
        );

    internal static void InsertPerson(SqliteConnection db, string id, string name, string email) =>
        ExecutePerson(
            db: db,
            sql: "INSERT INTO Person (Id, Name, Email) VALUES (@id, @name, @email)",
            id: id,
            name: name,
            email: email
        );

    internal static void UpdatePerson(SqliteConnection db, string id, string name, string email) =>
        ExecutePerson(
            db: db,
            sql: "UPDATE Person SET Name = @name, Email = @email WHERE Id = @id",
            id: id,
            name: name,
            email: email
        );

    internal static void DeletePerson(SqliteConnection db, string id) =>
        DatabaseCommands.Execute(
            connection: db,
            configure: cmd => cmd.CommandText = "DELETE FROM Person WHERE Id = $id",
            parameters: () => [("$id", id)]
        );

    internal static void DeletePersonWithEmail(SqliteConnection db, string id) =>
        DatabaseCommands.Execute(
            connection: db,
            configure: cmd => cmd.CommandText = "DELETE FROM Person WHERE Id = @id",
            parameters: () => [("@id", id)]
        );

    // Implements [SYNC-TEST-SHARED-SQLITE-HELPERS].
    internal static void EmptyList<TValue, TError>(global::Outcome.Result<TValue, TError>? actual)
        where TValue : System.Collections.IEnumerable =>
        Xunit.Assert.Empty(ResultAssertions.ExactOk(actual: actual).Value);

    internal static void NullValue<TValue, TError>(
        global::Outcome.Result<TValue, TError>? actual
    ) => Xunit.Assert.Null(ResultAssertions.ExactOk(actual: actual).Value);

    internal static List<Nimblesite.Sync.Core.SyncLogEntry> FetchSqliteChanges(
        SqliteConnection db,
        long fromVersion,
        int batchSize = 1000
    ) =>
        [
            .. ResultAssertions
                .Ok(
                    actual: Nimblesite.Sync.SQLite.SyncLogRepository.FetchChanges(
                        db,
                        fromVersion,
                        batchSize
                    )
                )
                .Value,
        ];

    internal static void AssertMatches(
        Nimblesite.Sync.Core.SyncSubscription subscription,
        Nimblesite.Sync.Core.SyncLogEntry matching,
        Nimblesite.Sync.Core.SyncLogEntry nonMatching
    )
    {
        Xunit.Assert.True(
            Nimblesite.Sync.Core.SubscriptionManager.MatchesChange(subscription, matching)
        );
        Xunit.Assert.False(
            Nimblesite.Sync.Core.SubscriptionManager.MatchesChange(subscription, nonMatching)
        );
    }

    internal static void AssertBatchHashStable(
        IReadOnlyList<Nimblesite.Sync.Core.SyncLogEntry> entries
    ) =>
        Xunit.Assert.Equal(
            Nimblesite.Sync.Core.HashVerifier.ComputeBatchHash(entries),
            Nimblesite.Sync.Core.HashVerifier.ComputeBatchHash(entries)
        );
}
