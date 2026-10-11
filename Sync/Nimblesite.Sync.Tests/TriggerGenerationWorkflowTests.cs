using System.Collections.Immutable;
using ColumnsError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<string>,
    Nimblesite.Sync.Core.SyncError
>.Error<System.Collections.Generic.IReadOnlyList<string>, Nimblesite.Sync.Core.SyncError>;
using ColumnsOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<string>,
    Nimblesite.Sync.Core.SyncError
>.Ok<System.Collections.Generic.IReadOnlyList<string>, Nimblesite.Sync.Core.SyncError>;
using StringSyncError = Outcome.Result<string, Nimblesite.Sync.Core.SyncError>.Error<
    string,
    Nimblesite.Sync.Core.SyncError
>;
using StringSyncResult = Outcome.Result<string, Nimblesite.Sync.Core.SyncError>;

namespace Nimblesite.Sync.Tests;

// Implements [SYNC-TRIGGER-SHARED-GENERATION].
public sealed partial record TriggerGenerationWorkflowTests
{
    [Fact]
    public void Compose_PreservesBlankSeparatorsAndTrailingNewline()
    {
        var sql = TriggerGenerationWorkflow.Compose(
            insert: "insert",
            update: "update",
            delete: "delete"
        );
        var newline = Environment.NewLine;
        Assert.Equal(
            expected: $"insert{newline}{newline}update{newline}{newline}delete{newline}",
            actual: sql
        );
    }

    [Fact]
    public void Generate_PreservesColumnOrderAndSelectsFirstPrimaryKey()
    {
        var result = Generate(columns: ["Name", "Id", "OtherId"]);
        var sql = global::Tests.Shared.ResultAssertions.ExactOk(actual: result).Value;
        Assert.Equal(expected: "People|Name,Id,OtherId|Id", actual: sql);
    }

    [Theory]
    [MemberData(nameof(Generate_InvalidSchema_ReturnsSpecificDatabaseErrorInlineCases))]
    public void Generate_InvalidSchema_ReturnsSpecificDatabaseError(bool empty, string message)
    {
        var result = Generate(columns: empty ? [] : ["Name"]);
        var error = Assert.IsType<StringSyncError>(@object: result).Value;
        Assert.Equal(
            expected: message,
            actual: Assert.IsType<SyncErrorDatabase>(@object: error).Message
        );
    }

    [Fact]
    public void ColumnError_PreservesOriginalMetadataFailure()
    {
        var error = new SyncErrorDatabase(Message: "metadata unavailable");
        var result = TriggerGenerationWorkflow.ColumnError(
            tableName: "People",
            error: error,
            logger: NullLogger.Instance
        );
        Assert.Same(expected: error, actual: Assert.IsType<StringSyncError>(@object: result).Value);
    }

    private static StringSyncResult Generate(ImmutableArray<string> columns) =>
        TriggerGenerationWorkflow.Generate(
            tableName: "People",
            columns: columns,
            isPrimaryKey: column =>
                column.EndsWith(value: "Id", comparisonType: StringComparison.Ordinal),
            columnName: column => column,
            generate: (table, names, key) =>
                $"{table}|{string.Join(separator: ",", values: names)}|{key}",
            logger: NullLogger.Instance
        );

    [Fact]
    public void Compose_ForwardsColumnsAndKeysToEveryOperation()
    {
        var sql = TriggerGenerationWorkflow.Compose(
            tableName: "People",
            columns: ["Id", "Name"],
            pkColumn: "Id",
            change: (table, columns, key, operation, eventName) =>
                $"{table}|{string.Join(",", columns)}|{key}|{operation}|{eventName}",
            delete: (table, key) => $"{table}|{key}|delete"
        );
        Assert.Equal(
            TriggerGenerationWorkflow.Compose(
                insert: "People|Id,Name|Id|insert|INSERT",
                update: "People|Id,Name|Id|update|UPDATE",
                delete: "People|Id|delete"
            ),
            sql
        );
    }

    [Theory]
    [MemberData(nameof(FromSchema_PropagatesMetadataFailuresAndBuildsValidSchemaInlineCases))]
    public void FromSchema_PropagatesMetadataFailuresAndBuildsValidSchema(bool fails)
    {
        var error = new SyncErrorDatabase(Message: "metadata unavailable");
        var result = TriggerGenerationWorkflow.FromSchema<string>(
            tableName: "People",
            readColumns: () => fails ? new ColumnsError(error) : new ColumnsOk(["Id", "Name"]),
            isPrimaryKey: column => column == "Id",
            columnName: column => column,
            generate: (table, columns, key) => $"{table}|{string.Join(",", columns)}|{key}",
            logger: NullLogger.Instance
        );
        if (fails)
            Assert.Same(error, Assert.IsType<StringSyncError>(result).Value);
        else
            global::Tests.Shared.ResultAssertions.EqualValue(
                expected: "People|Id,Name|Id",
                actual: result
            );
    }

    [Theory]
    [MemberData(nameof(Create_FailureStopsBeforeExecutingSqlInlineCases))]
    public void Create_FailureStopsBeforeExecutingSql(bool preparationFails)
    {
        var error = new SyncErrorDatabase(Message: "metadata unavailable");
        var result = TriggerGenerationWorkflow.Create<InvalidOperationException>(
            tableName: "People",
            prepare: () => preparationFails ? new BoolSyncError(error) : new BoolSyncOk(true),
            generate: () =>
            {
                Assert.False(preparationFails, "Generation must not run after preparation failed");
                return new StringSyncError(error);
            },
            execute: _ => Assert.Fail("SQL must not execute after a failure"),
            logger: NullLogger.Instance
        );
        Assert.Same(error, Assert.IsType<BoolSyncError>(result).Value);
    }
}
