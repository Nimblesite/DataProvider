using System.Collections.Immutable;
using Microsoft.Extensions.Logging.Abstractions;
using StringSyncError = Outcome.Result<string, Nimblesite.Sync.Core.SyncError>.Error<
    string,
    Nimblesite.Sync.Core.SyncError
>;
using StringSyncOk = Outcome.Result<string, Nimblesite.Sync.Core.SyncError>.Ok<
    string,
    Nimblesite.Sync.Core.SyncError
>;
using StringSyncResult = Outcome.Result<string, Nimblesite.Sync.Core.SyncError>;

namespace Nimblesite.Sync.Tests;

// Implements [SYNC-TRIGGER-SHARED-GENERATION].
public sealed class TriggerGenerationWorkflowTests
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
        var sql = Assert.IsType<StringSyncOk>(@object: result).Value;
        Assert.Equal(expected: "People|Name,Id,OtherId|Id", actual: sql);
    }

    [Theory]
    [InlineData(true, "Table 'People' not found or has no columns")]
    [InlineData(false, "Table 'People' has no primary key")]
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
}
