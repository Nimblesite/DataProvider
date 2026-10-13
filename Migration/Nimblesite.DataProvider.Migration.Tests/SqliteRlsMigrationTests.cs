// Implements [MIG-TEST-SCHEMA-FIXTURES].
namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [RLS-SQLITE] tests from docs/specs/rls-spec.md.

/// <summary>
/// E2E tests for SQLite row-level security trigger emulation.
/// </summary>
public sealed partial class SqliteRlsMigrationTests
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
            VerifySharedStep4(input0: connection, value0: RlsOperation.Insert);

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
            VerifySharedStep4(input0: connection, value0: RlsOperation.Update);

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
            VerifySharedStep4(input0: connection, value0: RlsOperation.Delete);
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

    // Implements [RLS-DIFF]: inspection must make an unchanged ALL policy a no-op.
    [Fact]
    public void Sqlite_AllPolicy_RerunHasNoPolicyOperations()
    {
        SqliteTestDb.WithDb(test: connection =>
        {
            var desired = DocumentsSchema(policy: OwnerPolicy(ops: [RlsOperation.All]));
            SqliteTestDb.ApplySchema(connection: connection, schema: desired);
            var operations = Assert
                .IsType<OperationsResultOk>(
                    SchemaDiff.Calculate(
                        current: SqliteTestDb.Inspect(connection: connection),
                        desired: desired
                    )
                )
                .Value;
            Assert.Empty(operations);
        });
    }

    // Implements [RLS-DIFF]: SELECT-only policies must replace stale secure-view predicates.
    [Theory]
    [MemberData(
        nameof(Sqlite_SelectPolicy_ChangedUsingPredicateBlocksPreviouslyVisibleRowsInlineCases)
    )]
    public void Sqlite_SelectPolicy_ChangedUsingPredicateBlocksPreviouslyVisibleRows(
        bool legacyView
    )
    {
        SqliteTestDb.WithDb(test: connection =>
        {
            var policy = OwnerPolicy(ops: [RlsOperation.Select]) with { WithCheckLql = null };
            SqliteTestDb.ApplySchema(
                connection: connection,
                schema: DocumentsSchema(policy: policy)
            );
            if (legacyView)
            {
                SqliteTestDb.Execute(
                    connection: connection,
                    sql: LegacySelectViewDdl(policy: policy)
                );
            }
            SqliteTestDb.SetUser(connection: connection, userId: "user-a");
            InsertDocument(connection: connection, id: "doc-a", ownerId: "user-a");
            Assert.Equal(
                expected: 1,
                actual: SqliteTestDb.CountRows(
                    connection: connection,
                    tableName: "Documents_secure"
                )
            );

            var desired = DocumentsSchema(policy: policy with { UsingLql = "false" });
            SqliteTestDb.ApplySchema(connection: connection, schema: desired);

            Assert.Equal(
                expected: 0,
                actual: SqliteTestDb.CountRows(
                    connection: connection,
                    tableName: "Documents_secure"
                )
            );
            Assert.Empty(
                Assert
                    .IsType<OperationsResultOk>(
                        SchemaDiff.Calculate(
                            current: SqliteTestDb.Inspect(connection: connection),
                            desired: desired
                        )
                    )
                    .Value
            );
        });
    }

    // Implements [RLS-DIFF]: SQL comments cannot hide a stored policy predicate.
    [Fact]
    public void Sqlite_TriggerPredicate_IgnoresKeywordsInsideComments()
    {
        var predicate = SqliteRlsPredicateReader.TriggerPredicate(
            triggerSql: Data_Sqlite_TriggerPredicate_IgnoresKeywordsInsideComments_1
        );
        Assert.Equal(expected: "[OwnerId] = 'allowed'", actual: predicate);
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
        SchemaTestData.Schema(name: "sqlite", tables: [DocumentsTable(policy)]);

    private static string LegacySelectViewDdl(RlsPolicyDefinition policy) =>
        SqliteDdlGenerator
            .Generate(
                operation: new CreateRlsPolicyOperation(
                    Schema: "main",
                    TableName: "Documents",
                    Policy: policy
                )
            )
            .Replace(
                oldValue: SqliteRlsViewMetadata.Encode(policyName: policy.Name),
                newValue: string.Empty,
                comparisonType: StringComparison.Ordinal
            );

    private static SchemaDefinition GroupMembershipSchema() =>
        SchemaTestData.Schema(
            name: "sqlite",
            tables: [MembershipTable(), DocumentsTable(GroupMembershipPolicy())]
        );

    private static ColumnDefinition RequiredText(string name) =>
        SchemaTestData.Column(name: name, type: PortableTypes.Text, isNullable: false);

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

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifySharedStep4(
        SqliteConnection input0,
        global::Nimblesite.DataProvider.Migration.Core.RlsOperation value0
    )
    {
        SqliteTestDb.ApplySchema(input0, DocumentsSchema(OwnerPolicy([value0])));
        SqliteTestDb.SetUser(input0, "user-a");

        InsertDocument(input0, "doc-a", "user-a");
    }
}
