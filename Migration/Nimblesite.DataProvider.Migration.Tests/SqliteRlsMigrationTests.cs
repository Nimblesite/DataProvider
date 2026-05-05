using System.Globalization;

namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-SQLITE] tests from docs/specs/rls-spec.md.

/// <summary>
/// E2E tests for SQLite row-level security trigger emulation.
/// </summary>
public sealed class SqliteRlsMigrationTests
{
    private static readonly ILogger Logger = NullLogger.Instance;

    [Fact]
    public void Sqlite_EnableRls_CreatesRlsContextTable()
    {
        WithDb(connection =>
        {
            Apply(connection, [new EnableRlsOperation("main", "Documents")]);

            Assert.Equal(1, CountMasterRows(connection, "table", "__rls_context"));
        });
    }

    [Fact]
    public void Sqlite_CreatePolicy_Insert_TriggerBlocksCrossOwnerInsert()
    {
        WithDb(connection =>
        {
            ApplySchema(connection, DocumentsSchema(OwnerPolicy([RlsOperation.Insert])));
            SetUser(connection, "user-a");

            InsertDocument(connection, "doc-a", "user-a");

            var ex = Assert.Throws<SqliteException>(() =>
                InsertDocument(connection, "doc-b", "user-b")
            );
            Assert.Contains("RLS-SQLITE", ex.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Sqlite_CreatePolicy_Update_TriggerBlocksCrossOwnerUpdate()
    {
        WithDb(connection =>
        {
            ApplySchema(connection, DocumentsSchema(OwnerPolicy([RlsOperation.Update])));
            SetUser(connection, "user-a");
            InsertDocument(connection, "doc-a", "user-a");

            var ex = Assert.Throws<SqliteException>(() =>
                Execute(connection, "UPDATE [Documents] SET [OwnerId]='user-b'")
            );
            Assert.Contains("RLS-SQLITE", ex.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Sqlite_CreatePolicy_Delete_TriggerBlocksCrossOwnerDelete()
    {
        WithDb(connection =>
        {
            ApplySchema(connection, DocumentsSchema(OwnerPolicy([RlsOperation.Delete])));
            SetUser(connection, "user-a");
            InsertDocument(connection, "doc-a", "user-a");
            SetUser(connection, "user-b");

            var ex = Assert.Throws<SqliteException>(() =>
                Execute(connection, "DELETE FROM [Documents] WHERE [Id]='doc-a'")
            );
            Assert.Contains("RLS-SQLITE", ex.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Sqlite_CreatePolicy_GroupMembership_TriggerUsesSubquery()
    {
        WithDb(connection =>
        {
            ApplySchema(connection, GroupMembershipSchema());
            SetUser(connection, "user-a");

            Assert.Throws<SqliteException>(() => InsertDocument(connection, "doc-a", "user-a"));

            InsertMembership(connection, "membership-a", "user-a");
            InsertDocument(connection, "doc-b", "user-a");
            Assert.Equal(1, CountRows(connection, "Documents"));
        });
    }

    [Fact]
    public void Sqlite_SelectPolicy_CreatesSecureView()
    {
        WithDb(connection =>
        {
            ApplySchema(connection, DocumentsSchema(OwnerPolicy([RlsOperation.Select])));
            InsertDocument(connection, "doc-a", "user-a");
            InsertDocument(connection, "doc-b", "user-b");
            SetUser(connection, "user-a");

            Assert.Equal(1, CountRows(connection, "Documents_secure"));
        });
    }

    [Fact]
    public void Sqlite_SchemaInspector_ReadsBackTriggers()
    {
        WithDb(connection =>
        {
            ApplySchema(connection, DocumentsSchema(OwnerPolicy([RlsOperation.All])));

            var inspected = (
                (SchemaResultOk)SqliteSchemaInspector.Inspect(connection, Logger)
            ).Value;
            var rls = inspected.Tables.Single(t => t.Name == "Documents").RowLevelSecurity;

            Assert.NotNull(rls);
            Assert.Equal("owner_isolation", Assert.Single(rls.Policies).Name);
        });
    }

    [Fact]
    public void Sqlite_RestrictivePolicy_EmitsWarning()
    {
        var ddl = SqliteDdlGenerator.Generate(
            new CreateRlsPolicyOperation(
                "main",
                "Documents",
                OwnerPolicy([RlsOperation.Insert]) with
                {
                    IsPermissive = false,
                }
            )
        );

        Assert.Contains("MIG-W-RLS-SQLITE-RESTRICTIVE-APPROX", ddl, StringComparison.Ordinal);
    }

    [Fact]
    public void Sqlite_DisableRls_DropsSecureView()
    {
        WithDb(connection =>
        {
            ApplySchema(connection, DocumentsSchema(OwnerPolicy([RlsOperation.Select])));

            Apply(
                connection,
                [new DisableRlsOperation("main", "Documents")],
                MigrationOptions.Destructive
            );

            Assert.Equal(0, CountMasterRows(connection, "view", "Documents_secure"));
        });
    }

    private static SchemaDefinition DocumentsSchema(RlsPolicyDefinition policy) =>
        new() { Name = "sqlite", Tables = [DocumentsTable(policy)] };

    private static TableDefinition DocumentsTable(RlsPolicyDefinition policy) =>
        new()
        {
            Schema = "main",
            Name = "Documents",
            Columns =
            [
                RequiredText("Id"),
                RequiredText("OwnerId"),
                new ColumnDefinition { Name = "Title", Type = PortableTypes.Text },
            ],
            PrimaryKey = new PrimaryKeyDefinition { Name = "PK_Documents", Columns = ["Id"] },
            RowLevelSecurity = new RlsPolicySetDefinition { Policies = [policy] },
        };

    private static SchemaDefinition GroupMembershipSchema() =>
        new()
        {
            Name = "sqlite",
            Tables = [MembershipTable(), DocumentsTable(GroupMembershipPolicy())],
        };

    private static TableDefinition MembershipTable() =>
        new()
        {
            Schema = "main",
            Name = "UserGroupMemberships",
            Columns = [RequiredText("Id"), RequiredText("UserId")],
            PrimaryKey = new PrimaryKeyDefinition
            {
                Name = "PK_UserGroupMemberships",
                Columns = ["Id"],
            },
        };

    private static ColumnDefinition RequiredText(string name) =>
        new()
        {
            Name = name,
            Type = PortableTypes.Text,
            IsNullable = false,
        };

    private static RlsPolicyDefinition OwnerPolicy(IReadOnlyList<RlsOperation> ops) =>
        new()
        {
            Name = "owner_isolation",
            Operations = ops,
            UsingLql = "OwnerId = current_user_id()",
            WithCheckLql = "OwnerId = current_user_id()",
        };

    private static RlsPolicyDefinition GroupMembershipPolicy() =>
        new()
        {
            Name = "group_member_insert",
            Operations = [RlsOperation.Insert],
            WithCheckLql = """
                exists(
                  UserGroupMemberships
                  |> filter(fn(m) => m.UserId = current_user_id())
                )
                """,
        };

    private static void ApplySchema(SqliteConnection connection, SchemaDefinition schema)
    {
        var current = ((SchemaResultOk)SqliteSchemaInspector.Inspect(connection, Logger)).Value;
        var ops = ((OperationsResultOk)SchemaDiff.Calculate(current, schema, logger: Logger)).Value;
        Apply(connection, ops);
    }

    private static void Apply(
        SqliteConnection connection,
        IReadOnlyList<SchemaOperation> ops,
        MigrationOptions? options = null
    )
    {
        var result = MigrationRunner.Apply(
            connection,
            ops,
            SqliteDdlGenerator.Generate,
            options ?? MigrationOptions.Default,
            Logger
        );
        Assert.True(result is MigrationApplyResultOk);
    }

    private static void SetUser(SqliteConnection connection, string userId)
    {
        Execute(connection, "DELETE FROM [__rls_context]");
        Execute(connection, $"INSERT INTO [__rls_context]([current_user_id]) VALUES ('{userId}')");
    }

    private static void InsertDocument(SqliteConnection connection, string id, string ownerId) =>
        Execute(
            connection,
            $"INSERT INTO [Documents]([Id], [OwnerId], [Title]) VALUES ('{id}', '{ownerId}', 't')"
        );

    private static void InsertMembership(SqliteConnection connection, string id, string userId) =>
        Execute(
            connection,
            $"INSERT INTO [UserGroupMemberships]([Id], [UserId]) VALUES ('{id}', '{userId}')"
        );

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static int CountRows(SqliteConnection connection, string tableName) =>
        Count(connection, $"SELECT COUNT(*) FROM [{tableName}]");

    private static int CountMasterRows(SqliteConnection connection, string type, string name) =>
        Count(
            connection,
            $"SELECT COUNT(*) FROM sqlite_master WHERE type='{type}' AND name='{name}'"
        );

    private static int Count(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void WithDb(Action<SqliteConnection> test)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sqliterls_{Guid.NewGuid()}.db");
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        try
        {
            test(connection);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }
}
