using static global::Tests.Shared.ResultAssertions;

namespace Nimblesite.DataProvider.Migration.Tests;

// Implements [MIG-TRIGGER-SQLITE] and [MIG-TRIGGER-DIFF] from
// docs/specs/declarative-triggers-spec.md (GitHub issue 82).

/// <summary>
/// E2E tests for declarative BEFORE-trigger guards on SQLite. The schema
/// declares a last-owner guard on TenantMembers: demoting or deleting the
/// only 'owner' row of a tenant must fail with the declared error message.
/// </summary>
public sealed partial class SqliteDeclarativeTriggerTests
{
    private static readonly ILogger Logger = NullLogger.Instance;
    private static readonly string OwnerA = Guid.NewGuid().ToString();
    private static readonly string OwnerB = Guid.NewGuid().ToString();
    private static readonly string MemberC = Guid.NewGuid().ToString();
    private static readonly string Tenant1 = Guid.NewGuid().ToString();
    private static readonly string Tenant2 = Guid.NewGuid().ToString();

    private const string GuardYaml = Data_Shared_1;

    private const string PlainYaml = Data_Shared_2;

    [Fact]
    public void Sqlite_TriggerGuard_BlocksDeleteOfLastOwner()
    {
        SqliteTestDb.WithDb(connection =>
        {
            ArrangeOwnerAndTrigger(
                connection: connection,
                action: DeleteMember,
                actionId: OwnerA,
                insertedId: MemberC,
                insertedTenant: Tenant1,
                expectedRole: "member"
            );
            Assert.Equal(2, SqliteTestDb.CountRows(connection, "TenantMembers"));
        });
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(Sqlite_TriggerGuard_AllowsDeleteWhenAnotherOwnerExists_ContextCasesData))]
    public void Sqlite_TriggerGuard_AllowsDeleteWhenAnotherOwnerExists_ContextCases(
        string caseName,
        Action<SqliteConnection, string> input5,
        global::System.Func<
            global::Microsoft.Data.Sqlite.SqliteConnection,
            global::System.String,
            global::System.Int32
        > value4,
        string value5
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseOperationCaseMatrix1(
            input0: ApplyGuardSchema,
            input1: InsertMember,
            input2: OwnerA,
            input3: Tenant1,
            input4: OwnerB,
            input5: input5,
            value4: value4,
            value5: value5
        );
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(Sqlite_TriggerGuard_BlocksLastOwnerAction_ContextCasesData))]
    public void Sqlite_TriggerGuard_BlocksLastOwnerAction_ContextCases(
        string caseName,
        Action<SqliteConnection, string> action,
        string insertedId,
        string insertedTenant,
        string expectedRole
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        SqliteTestDb.WithDb(connection =>
            ArrangeOwnerAndTrigger(
                connection: connection,
                action: action,
                actionId: OwnerA,
                insertedId: insertedId,
                insertedTenant: insertedTenant,
                expectedRole: expectedRole
            )
        );
    }

    [Fact]
    public void Sqlite_Trigger_RediffAfterApply_ProducesNoOperations()
    {
        SqliteTestDb.WithDb(connection =>
        {
            ApplyGuardSchema(connection);

            var current = SqliteTestDb.Inspect(connection);
            var ops = (
                Ok(
                    actual: SchemaDiff.Calculate(
                        current,
                        SchemaYamlSerializer.FromYaml(GuardYaml),
                        logger: Logger
                    )
                )
            ).Value;

            Assert.Empty(ops);
        });
    }

    [Fact]
    public void Sqlite_TriggerRemoval_WithoutDestructive_KeepsGuard()
    {
        SqliteTestDb.WithDb(connection =>
        {
            ApplyGuardSchema(connection);
            SqliteTestDb.ApplySchema(connection, SchemaYamlSerializer.FromYaml(PlainYaml));
            InsertMember(connection, OwnerA, Tenant1, "owner");

            var ex = Assert.Throws<SqliteException>(() => DeleteMember(connection, OwnerA));

            Assert.Contains("last owner", ex.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Sqlite_TriggerRemoval_WithDestructive_DropsGuard()
    {
        SqliteTestDb.WithDb(connection =>
        {
            ApplyGuardSchema(connection);
            SqliteTestDb.ApplySchema(
                connection,
                SchemaYamlSerializer.FromYaml(PlainYaml),
                allowDestructive: true
            );
            InsertMember(connection, OwnerA, Tenant1, "owner");

            DeleteMember(connection, OwnerA);

            Assert.Equal(0, SqliteTestDb.CountRows(connection, "TenantMembers"));
        });
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(IndependentCommentScenarioData5))]
    public void Sqlite_TriggerGuard_OldReferenceOnInsertEvent_FailsLoudly_CommentScenarioCases(
        string scenarioName,
        System.String scenario0,
        System.String scenario1,
        System.String scenario2
    )
    {
        Assert.False(string.IsNullOrEmpty(scenarioName)); // Implements [MIG-TRIGGER-GUARD-LQL]: old. does not exist on INSERT.
        // Without validation SQLite fails at DML time while Postgres silently
        // never fires the guard.
        SqliteTestDb.WithDb(connection =>
        {
            var result = TryApplyGuardVariant(connection, events: scenario0, raiseWhen: scenario1);

            var error = Assert.IsType<MigrationApplyResultError>(result);
            Assert.Contains(scenario2, error.Value.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Sqlite_TriggerGuard_ForEachRowFalse_FailsLoudly()
    {
        // Implements [MIG-TRIGGER-GUARD-LQL]: guards are row-level on every
        // platform; statement-level triggers see no OLD/NEW row on Postgres.
        SqliteTestDb.WithDb(connection =>
        {
            var result = TryApplyGuardVariant(
                connection,
                events: "[Delete]",
                raiseWhen: "old.Role = 'owner'",
                extraTriggerYaml: "forEachRow: false"
            );

            var error = Assert.IsType<MigrationApplyResultError>(result);
            Assert.Contains("statement-level", error.Value.Message, StringComparison.Ordinal);
        });
    }

    private static MigrationApplyResult TryApplyGuardVariant(
        SqliteConnection connection,
        string events,
        string raiseWhen,
        string? extraTriggerYaml = null
    )
    {
        var extra = extraTriggerYaml is null ? string.Empty : $"\n        {extraTriggerYaml}";
        var yaml = SqlData_TryApplyGuardVariant_1(
            events: events,
            raiseWhen: raiseWhen,
            extra: extra
        );
        return SqliteTestDb.TryApplySchema(connection, SchemaYamlSerializer.FromYaml(yaml));
    }

    private static void ApplyGuardSchema(SqliteConnection connection) =>
        SqliteTestDb.ApplySchema(connection, SchemaYamlSerializer.FromYaml(GuardYaml));

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void ArrangeOwnerAndTrigger(
        SqliteConnection connection,
        Action<SqliteConnection, string> action,
        string actionId,
        string insertedId,
        string insertedTenant,
        string expectedRole
    )
    {
        ApplyGuardSchema(connection: connection);
        InsertMember(connection: connection, id: OwnerA, tenantId: Tenant1, role: "owner");
        VerifyReusableWorkflow14(
            input0: InsertMember,
            input1: connection,
            input2: insertedId,
            input3: insertedTenant,
            input4: action,
            input5: actionId,
            value0: expectedRole
        );
    }

    private static void InsertMember(
        SqliteConnection connection,
        string id,
        string tenantId,
        string role
    ) =>
        SqliteTestDb.Execute(
            connection,
            $"INSERT INTO [TenantMembers]([Id], [TenantId], [Role]) VALUES ('{id}', '{tenantId}', '{role}')"
        );

    private static void DeleteMember(SqliteConnection connection, string id) =>
        SqliteTestDb.Execute(connection, $"DELETE FROM [TenantMembers] WHERE [Id]='{id}'");

    private static void DemoteMember(SqliteConnection connection, string id) =>
        SqliteTestDb.Execute(
            connection,
            $"UPDATE [TenantMembers] SET [Role]='member' WHERE [Id]='{id}'"
        );

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow14(
        Action<SqliteConnection, string, string, string> input0,
        SqliteConnection input1,
        string input2,
        string input3,
        Action<SqliteConnection, string> input4,
        string input5,
        string value0
    )
    {
        input0(arg1: input1, arg2: input2, arg3: input3, arg4: value0);

        var ex = Assert.Throws<SqliteException>(() => input4(arg1: input1, arg2: input5));

        Assert.Contains("last owner", ex.Message, StringComparison.Ordinal);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseOperationCaseMatrix1(
        Action<SqliteConnection> input0,
        Action<SqliteConnection, string, string, string> input1,
        string input2,
        string input3,
        string input4,
        Action<SqliteConnection, string> input5,
        global::System.Func<
            global::Microsoft.Data.Sqlite.SqliteConnection,
            global::System.String,
            global::System.Int32
        > value4,
        string value5
    )
    {
        SqliteTestDb.WithDb(connection =>
        {
            input0(obj: connection);
            input1(arg1: connection, arg2: input2, arg3: input3, arg4: "owner");
            input1(arg1: connection, arg2: input4, arg3: input3, arg4: "owner");

            input5(arg1: connection, arg2: input2);

            Assert.Equal(1, value4(arg1: connection, arg2: value5));
        });
    }
}
