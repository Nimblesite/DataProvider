namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-SQLITE] tests from docs/specs/rls-spec.md.

/// <summary>
/// E2E tests for SQLite row-level security trigger emulation.
/// </summary>
public sealed class SqliteRlsMigrationTests
{
    [Fact]
    public void Sqlite_EnableRls_CreatesRlsContextTable()
    {
        SqliteTestDb.WithDb(connection =>
        {
            SqliteTestDb.Apply(connection, [new EnableRlsOperation("main", "Documents")]);

            Assert.Equal(1, SqliteTestDb.CountMasterRows(connection, "table", "__rls_context"));
        });
    }

    [Fact]
    public void Sqlite_CreatePolicy_Insert_TriggerBlocksCrossOwnerInsert()
    {
        SqliteTestDb.WithDb(connection =>
        {
            SqliteTestDb.ApplySchema(
                connection,
                DocumentsSchema(OwnerPolicy([RlsOperation.Insert]))
            );
            SqliteTestDb.SetUser(connection, "user-a");

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
        SqliteTestDb.WithDb(connection =>
        {
            SqliteTestDb.ApplySchema(
                connection,
                DocumentsSchema(OwnerPolicy([RlsOperation.Update]))
            );
            SqliteTestDb.SetUser(connection, "user-a");
            InsertDocument(connection, "doc-a", "user-a");

            var ex = Assert.Throws<SqliteException>(() =>
                SqliteTestDb.Execute(connection, "UPDATE [Documents] SET [OwnerId]='user-b'")
            );
            Assert.Contains("RLS-SQLITE", ex.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Sqlite_CreatePolicy_Delete_TriggerBlocksCrossOwnerDelete()
    {
        SqliteTestDb.WithDb(connection =>
        {
            SqliteTestDb.ApplySchema(
                connection,
                DocumentsSchema(OwnerPolicy([RlsOperation.Delete]))
            );
            SqliteTestDb.SetUser(connection, "user-a");
            InsertDocument(connection, "doc-a", "user-a");
            SqliteTestDb.SetUser(connection, "user-b");

            var ex = Assert.Throws<SqliteException>(() =>
                SqliteTestDb.Execute(connection, "DELETE FROM [Documents] WHERE [Id]='doc-a'")
            );
            Assert.Contains("RLS-SQLITE", ex.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Sqlite_CreatePolicy_GroupMembership_TriggerUsesSubquery()
    {
        SqliteTestDb.WithDb(connection =>
        {
            SqliteTestDb.ApplySchema(connection, GroupMembershipSchema());
            SqliteTestDb.SetUser(connection, "user-a");

            Assert.Throws<SqliteException>(() => InsertDocument(connection, "doc-a", "user-a"));

            InsertMembership(connection, "membership-a", "user-a");
            InsertDocument(connection, "doc-b", "user-a");
            Assert.Equal(1, SqliteTestDb.CountRows(connection, "Documents"));
        });
    }

    [Fact]
    public void Sqlite_SelectPolicy_CreatesSecureView()
    {
        SqliteTestDb.WithDb(connection =>
        {
            SqliteTestDb.ApplySchema(
                connection,
                DocumentsSchema(OwnerPolicy([RlsOperation.Select]))
            );
            InsertDocument(connection, "doc-a", "user-a");
            InsertDocument(connection, "doc-b", "user-b");
            SqliteTestDb.SetUser(connection, "user-a");

            Assert.Equal(1, SqliteTestDb.CountRows(connection, "Documents_secure"));
        });
    }

    [Fact]
    public void Sqlite_SchemaInspector_ReadsBackTriggers()
    {
        SqliteTestDb.WithDb(connection =>
        {
            SqliteTestDb.ApplySchema(connection, DocumentsSchema(OwnerPolicy([RlsOperation.All])));

            var inspected = SqliteTestDb.Inspect(connection);
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
        SqliteTestDb.WithDb(connection =>
        {
            SqliteTestDb.ApplySchema(
                connection,
                DocumentsSchema(OwnerPolicy([RlsOperation.Select]))
            );

            SqliteTestDb.Apply(
                connection,
                [new DisableRlsOperation("main", "Documents")],
                MigrationOptions.Destructive
            );

            Assert.Equal(0, SqliteTestDb.CountMasterRows(connection, "view", "Documents_secure"));
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

    private static void InsertDocument(SqliteConnection connection, string id, string ownerId) =>
        SqliteTestDb.Execute(
            connection,
            $"INSERT INTO [Documents]([Id], [OwnerId], [Title]) VALUES ('{id}', '{ownerId}', 't')"
        );

    private static void InsertMembership(SqliteConnection connection, string id, string userId) =>
        SqliteTestDb.Execute(
            connection,
            $"INSERT INTO [UserGroupMemberships]([Id], [UserId]) VALUES ('{id}', '{userId}')"
        );
}
