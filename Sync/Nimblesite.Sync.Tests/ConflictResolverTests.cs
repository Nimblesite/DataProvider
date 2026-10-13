using static global::Tests.Shared.ResultAssertions;

namespace Nimblesite.Sync.Tests;

public sealed partial class ConflictResolverTests
{
    [Fact]
    public void IsConflict_SameTableSamePkDifferentOrigin_ReturnsTrue()
    {
        var (local, remote) = VerifySharedChecks7(
            value4: "2025-01-01T00:00:00.000Z",
            value9: "2025-01-01T00:00:01.000Z"
        );

        var result = ConflictResolver.IsConflict(local, remote);

        Assert.True(result);
    }

    [Theory]
    // IsConflict_SameTableSamePkSameOrigin_ReturnsFalse
    [MemberData(nameof(IsConflict_SameTableSamePkSameOrigin_ReturnsFalse_CasesInlineCases))]
    // IsConflict_DifferentTable_ReturnsFalse

    // IsConflict_DifferentPk_ReturnsFalse

    public void IsConflict_SameTableSamePkSameOrigin_ReturnsFalse_Cases(
        string caseValue6,
        string caseValue7,
        string caseValue8
    )
    {
        var local = CreateEntry(1, "Person", "pk-1", "origin-A", "2025-01-01T00:00:00.000Z");
        var remote = CreateEntry(2, caseValue6, caseValue7, caseValue8, "2025-01-01T00:00:01.000Z");

        var result = ConflictResolver.IsConflict(local, remote);

        Assert.False(result);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(Resolve_LastWriteWins_NewerTimestampWins_ContextCasesData))]
    public void Resolve_LastWriteWins_NewerTimestampWins_ContextCases(
        string caseName,
        string value0,
        string value1,
        global::Nimblesite.Sync.Core.ConflictStrategy value2,
        global::Nimblesite.Sync.Core.ConflictStrategy value4
    )
    {
        NamedCase(caseName: caseName);
        ExerciseProjectionCaseMatrix1(
            input0: VerifyReusableWorkflow5,
            input1: VerifySharedChecks7,
            value0: value0,
            value1: value1,
            value2: value2,
            value4: value4
        );
    }

    [Fact]
    public void Resolve_LastWriteWins_OlderTimestampLoses()
    {
        var result = VerifyLocalWinner(
            localTimestamp: "2025-01-01T00:00:01.000Z",
            remoteTimestamp: "2025-01-01T00:00:00.000Z",
            strategy: ConflictStrategy.LastWriteWins
        );
        _ = result;
    }

    [Fact]
    public void Resolve_LastWriteWins_EqualTimestamps_HigherVersionWins()
    {
        var (remote, result) = VerifyReusableWorkflow5(
            input0: VerifySharedChecks7,
            value0: "2025-01-01T00:00:00.000Z",
            value1: "2025-01-01T00:00:00.000Z",
            value2: ConflictStrategy.LastWriteWins
        );

        Assert.Equal(remote, result.Winner);
    }

    [Fact]
    public void Resolve_ClientWins_AlwaysReturnsLocal()
    {
        var result = VerifyLocalWinner(
            localTimestamp: "2025-01-01T00:00:00.000Z",
            remoteTimestamp: "2025-01-01T00:00:01.000Z",
            strategy: ConflictStrategy.ClientWins
        );

        Assert.Equal(ConflictStrategy.ClientWins, result.Strategy);
    }

    [Fact]
    public void ResolveLastWriteWins_DirectCall_WorksCorrectly()
    {
        var (local, remote) = VerifySharedChecks7(
            value4: "2025-01-01T00:00:00.000Z",
            value9: "2025-01-01T00:00:01.000Z"
        );

        var result = ConflictResolver.ResolveLastWriteWins(local, remote);

        Assert.Equal(remote, result.Winner);
        Assert.Equal(ConflictStrategy.LastWriteWins, result.Strategy);
    }

    [Fact]
    public void ResolveCustom_SuccessfulResolver_ReturnsSuccess()
    {
        var (local, remote) = VerifySharedChecks7(
            value4: "2025-01-01T00:00:00.000Z",
            value9: "2025-01-01T00:00:01.000Z"
        );

        var result = ConflictResolver.ResolveCustom(local, remote, (l, r) => new SyncLogEntryOk(l));

        var success = global::Tests.Shared.ResultAssertions.ExactOk(actual: result);
        Assert.Equal(local, success.Value.Winner);
    }

    [Fact]
    public void ResolveCustom_FailingResolver_ReturnsFailure()
    {
        var (local, remote) = VerifySharedChecks7(
            value4: "2025-01-01T00:00:00.000Z",
            value9: "2025-01-01T00:00:01.000Z"
        );
        var error = new SyncErrorDatabase("Custom resolver failed");

        var result = ConflictResolver.ResolveCustom(
            local,
            remote,
            (l, r) => new SyncLogEntryError(error)
        );

        var failure = Assert.IsType<ConflictResolutionError>(result);
        Assert.Equal(error, failure.Value);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static (SyncLogEntry, SyncLogEntry) VerifySharedChecks7(string value4, string value9)
    {
        var local = CreateEntry(1, "Person", "pk-1", "origin-A", value4);
        var remote = CreateEntry(2, "Person", "pk-1", "origin-B", value9);

        return (local, remote);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private ConflictResolution VerifyLocalWinner(
        string localTimestamp,
        string remoteTimestamp,
        ConflictStrategy strategy
    )
    {
        var (local, remote) = VerifySharedChecks7(value4: localTimestamp, value9: remoteTimestamp);
        var result = ConflictResolver.Resolve(local, remote, strategy);
        Assert.Equal(local, result.Winner);
        return result;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static (SyncLogEntry, ConflictResolution) VerifyReusableWorkflow5(
        Func<string, string, (SyncLogEntry, SyncLogEntry)> input0,
        string value0,
        string value1,
        global::Nimblesite.Sync.Core.ConflictStrategy value2
    )
    {
        var (local, remote) = input0(arg1: value0, arg2: value1);

        var result = ConflictResolver.Resolve(local, remote, value2);

        return (remote, result);
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseProjectionCaseMatrix1(
        Func<
            Func<string, string, (SyncLogEntry, SyncLogEntry)>,
            string,
            string,
            ConflictStrategy,
            (SyncLogEntry, ConflictResolution)
        > input0,
        Func<string, string, (SyncLogEntry, SyncLogEntry)> input1,
        string value0,
        string value1,
        global::Nimblesite.Sync.Core.ConflictStrategy value2,
        global::Nimblesite.Sync.Core.ConflictStrategy value4
    )
    {
        var (remote, result) = input0(arg1: input1, arg2: value0, arg3: value1, arg4: value2);

        Assert.Equal(remote, result.Winner);
        Assert.Equal(value4, result.Strategy);
    }
}
