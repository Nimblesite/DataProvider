namespace Nimblesite.Sync.Tests;

/// <summary>
/// Tests for SubscriptionManager.
/// </summary>
public sealed partial class SubscriptionManagerTests
{
    private const string Timestamp = "2024-01-01T00:00:00Z";

    #region Subscription Creation Tests

    [Fact]
    public void CreateRecordSubscription_CreatesCorrectSubscription()
    {
        var sub = SubscriptionManager.CreateRecordSubscription(
            "sub-1",
            "origin-1",
            "Person",
            "[\"p1\", \"p2\"]",
            Timestamp,
            "2024-12-31T00:00:00Z"
        );

        VerifySyncLoopWorkflow2(
            input0: sub,
            value0: "sub-1",
            value1: SubscriptionType.Record,
            value2: "Person",
            value3: "[\"p1\", \"p2\"]"
        );
        Assert.Equal("2024-12-31T00:00:00Z", sub.ExpiresAt);
    }

    [Fact]
    public void CreateRecordSubscription_WithoutExpiry()
    {
        var sub = SubscriptionManager.CreateRecordSubscription(
            "sub-1",
            "origin-1",
            "Person",
            "[\"p1\"]",
            Timestamp
        );

        Assert.Null(sub.ExpiresAt);
    }

    [Fact]
    public void CreateTableSubscription_CreatesCorrectSubscription()
    {
        var sub = SubscriptionManager.CreateTableSubscription(
            "sub-2",
            "origin-1",
            "Department",
            Timestamp,
            "2024-12-31T00:00:00Z"
        );

        global::Nimblesite.Sync.Tests.SyncStateTests.VerifyReusableWorkflow3(
            input0: sub,
            value0: "sub-2",
            value2: SubscriptionType.Table,
            value3: "Department"
        );
        Assert.Null(sub.Filter);
        Assert.Equal(Timestamp, sub.CreatedAt);
    }

    [Fact]
    public void CreateTableSubscription_WithoutExpiry()
    {
        var sub = SubscriptionManager.CreateTableSubscription(
            "sub-2",
            "origin-1",
            "Department",
            Timestamp
        );

        Assert.Null(sub.ExpiresAt);
    }

    [Fact]
    public void CreateQuerySubscription_CreatesCorrectSubscription()
    {
        var sub = SubscriptionManager.CreateQuerySubscription(
            "sub-3",
            "origin-1",
            "Orders",
            "{\"status\":\"active\"}",
            Timestamp,
            "2024-12-31T00:00:00Z"
        );

        VerifySyncLoopWorkflow2(
            input0: sub,
            value0: "sub-3",
            value1: SubscriptionType.Query,
            value2: "Orders",
            value3: "{\"status\":\"active\"}"
        );
    }

    [Fact]
    public void CreateQuerySubscription_WithoutExpiry()
    {
        var sub = QuerySubscription("sub-3");

        Assert.Null(sub.ExpiresAt);
    }

    #endregion

    #region MatchesChange Tests

    [Fact]
    public void MatchesChange_TableSubscription_MatchesSameTable()
    {
        var sub = SubscriptionManager.CreateTableSubscription(
            "sub-1",
            "origin-1",
            "Person",
            Timestamp
        );

        var change = CreateChange("Person", "{\"Id\":\"p1\"}");

        Assert.True(SubscriptionManager.MatchesChange(sub, change));
    }

    [Fact]
    public void MatchesChange_TableSubscription_DoesNotMatchDifferentTable()
    {
        var sub = SubscriptionManager.CreateTableSubscription(
            "sub-1",
            "origin-1",
            "Person",
            Timestamp
        );

        VerifyReusableWorkflow4(
            input0: CreateChange,
            input1: sub,
            value0: "Department",
            value1: "{\"Id\":\"d1\"}"
        );
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(IndependentScenarioData14))]
    public void MatchesChange_RecordSubscription_MatchesPkInFilter_ScenarioCases(
        string scenarioName,
        System.String scenario3,
        System.String scenario5
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: scenarioName));

        var sub = SubscriptionManager.CreateRecordSubscription(
            "sub-1",
            "origin-1",
            "Person",
            scenario3,
            Timestamp
        );

        var change = CreateChange("Person", scenario5);

        Assert.True(SubscriptionManager.MatchesChange(sub, change));
    }

    [Fact]
    public void MatchesChange_RecordSubscription_DoesNotMatchPkNotInFilter()
    {
        var sub = SubscriptionManager.CreateRecordSubscription(
            "sub-1",
            "origin-1",
            "Person",
            "[\"p1\", \"p2\"]",
            Timestamp
        );

        VerifyReusableWorkflow4(
            input0: CreateChange,
            input1: sub,
            value0: "Person",
            value1: "{\"Id\":\"p99\"}"
        );
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(MatchesChange_RecordSubscription_NullFilter_ReturnsFalse_ContextCasesData))]
    public void MatchesChange_RecordSubscription_NullFilter_ReturnsFalse_ContextCases(
        string caseName,
        Func<SyncSubscription> input0
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: caseName));
        ExerciseProjectionCaseMatrix2(
            input0: input0,
            input1: VerifyReusableWorkflow4,
            input2: CreateChange
        );
    }

    [Fact]
    public void MatchesChange_QuerySubscription_AlwaysMatchesSameTable()
    {
        var sub = QuerySubscription("sub-1");

        var change = CreateChange("Orders", "{\"Id\":\"o1\"}");

        // Query matching returns true for table match (app-level logic)
        Assert.True(SubscriptionManager.MatchesChange(sub, change));
    }

    [Fact]
    public void MatchesChange_QuerySubscription_DifferentTable_ReturnsFalse()
    {
        var sub = QuerySubscription("sub-1");

        VerifyReusableWorkflow4(
            input0: CreateChange,
            input1: sub,
            value0: "Products",
            value1: "{\"Id\":\"p1\"}"
        );
    }

    #endregion

    #region IsExpired Tests

    [Fact]
    public void IsExpired_NoExpiry_ReturnsFalse()
    {
        var sub = SubscriptionManager.CreateTableSubscription(
            "sub-1",
            "origin-1",
            "Person",
            Timestamp
        );

        Assert.False(SubscriptionManager.IsExpired(sub, "2099-01-01T00:00:00Z"));
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(IndependentScenarioData15))]
    public void IsExpired_FutureExpiry_ReturnsFalse_ScenarioCases(
        string scenarioName,
        System.String scenario3
    )
    {
        Assert.False(condition: string.IsNullOrEmpty(value: scenarioName));

        var sub = SubscriptionManager.CreateTableSubscription(
            "sub-1",
            "origin-1",
            "Person",
            Timestamp,
            scenario3
        );

        Assert.False(SubscriptionManager.IsExpired(sub, "2024-06-01T00:00:00Z"));
    }

    [Fact]
    public void IsExpired_PastExpiry_ReturnsTrue()
    {
        var sub = SubscriptionManager.CreateTableSubscription(
            "sub-1",
            "origin-1",
            "Person",
            Timestamp,
            "2024-01-01T00:00:00Z"
        );

        Assert.True(SubscriptionManager.IsExpired(sub, "2024-06-01T00:00:00Z"));
    }

    #endregion

    #region FindMatchingSubscriptions Tests

    [Fact]
    public void FindMatchingSubscriptions_ReturnsAllMatching()
    {
        var subs = new[]
        {
            SubscriptionManager.CreateTableSubscription("s1", "o1", "Person", Timestamp),
            SubscriptionManager.CreateTableSubscription("s2", "o1", "Person", Timestamp),
            SubscriptionManager.CreateTableSubscription("s3", "o1", "Department", Timestamp),
        };

        var change = CreateChange("Person", "{\"Id\":\"p1\"}");

        var matches = SubscriptionManager.FindMatchingSubscriptions(subs, change);

        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, s => s.SubscriptionId == "s1");
        Assert.Contains(matches, s => s.SubscriptionId == "s2");
    }

    [Fact]
    public void FindMatchingSubscriptions_NoMatches_ReturnsEmpty()
    {
        var subs = new[]
        {
            SubscriptionManager.CreateTableSubscription("s1", "o1", "Person", Timestamp),
        };

        var change = CreateChange("Department", "{\"Id\":\"d1\"}");

        var matches = SubscriptionManager.FindMatchingSubscriptions(subs, change);

        Assert.Empty(matches);
    }

    [Fact]
    public void FindMatchingSubscriptions_EmptyInput_ReturnsEmpty()
    {
        var change = CreateChange("Person", "{\"Id\":\"p1\"}");

        var matches = SubscriptionManager.FindMatchingSubscriptions([], change);

        Assert.Empty(matches);
    }

    #endregion

    #region CreateNotifications Tests

    [Fact]
    public void CreateNotifications_CreatesForAllMatches()
    {
        var subs = new[]
        {
            SubscriptionManager.CreateTableSubscription("s1", "o1", "Person", Timestamp),
            SubscriptionManager.CreateTableSubscription("s2", "o1", "Person", Timestamp),
        };

        var change = CreateChange("Person", "{\"Id\":\"p1\"}");

        var notifications = SubscriptionManager.CreateNotifications(subs, change);

        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, n => Assert.Equal(change, n.Change));
        Assert.Contains(notifications, n => n.SubscriptionId == "s1");
        Assert.Contains(notifications, n => n.SubscriptionId == "s2");
    }

    [Fact]
    public void CreateNotifications_NoMatches_ReturnsEmpty()
    {
        var subs = new[]
        {
            SubscriptionManager.CreateTableSubscription("s1", "o1", "Department", Timestamp),
        };

        var change = CreateChange("Person", "{\"Id\":\"p1\"}");

        var notifications = SubscriptionManager.CreateNotifications(subs, change);

        Assert.Empty(notifications);
    }

    #endregion

    #region FilterExpired Tests

    [Fact]
    public void FilterExpired_RemovesExpired()
    {
        var subs = new[]
        {
            SubscriptionManager.CreateTableSubscription(
                "s1",
                "o1",
                "Person",
                Timestamp,
                "2024-01-01T00:00:00Z"
            ),
            SubscriptionManager.CreateTableSubscription(
                "s2",
                "o1",
                "Person",
                Timestamp,
                "2099-01-01T00:00:00Z"
            ),
            SubscriptionManager.CreateTableSubscription("s3", "o1", "Person", Timestamp),
        };

        var active = SubscriptionManager.FilterExpired(subs, "2024-06-01T00:00:00Z");

        Assert.Equal(2, active.Count);
        Assert.Contains(active, s => s.SubscriptionId == "s2");
        Assert.Contains(active, s => s.SubscriptionId == "s3");
    }

    [Fact]
    public void FilterExpired_AllExpired_ReturnsEmpty()
    {
        var subs = new[]
        {
            SubscriptionManager.CreateTableSubscription(
                "s1",
                "o1",
                "Person",
                Timestamp,
                "2024-01-01T00:00:00Z"
            ),
            SubscriptionManager.CreateTableSubscription(
                "s2",
                "o1",
                "Person",
                Timestamp,
                "2024-01-02T00:00:00Z"
            ),
        };

        var active = SubscriptionManager.FilterExpired(subs, "2024-06-01T00:00:00Z");

        Assert.Empty(active);
    }

    [Fact]
    public void FilterExpired_NoneExpired_ReturnsAll()
    {
        var subs = new[]
        {
            SubscriptionManager.CreateTableSubscription(
                "s1",
                "o1",
                "Person",
                Timestamp,
                "2099-01-01T00:00:00Z"
            ),
            SubscriptionManager.CreateTableSubscription("s2", "o1", "Person", Timestamp),
        };

        var active = SubscriptionManager.FilterExpired(subs, "2024-06-01T00:00:00Z");

        Assert.Equal(2, active.Count);
    }

    #endregion

    #region Helper Methods

    private static SyncSubscription QuerySubscription(string id) =>
        SubscriptionManager.CreateQuerySubscription(
            id,
            "origin-1",
            "Orders",
            "{\"status\":\"active\"}",
            Timestamp
        );

    private static SyncLogEntry CreateChange(string tableName, string pkValue) =>
        new(1, tableName, pkValue, SyncOperation.Insert, "{}", "test-origin", Timestamp);

    #endregion
    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow4(
        Func<string, string, SyncLogEntry> input0,
        SyncSubscription input1,
        string value0,
        string value1
    )
    {
        var change = input0(arg1: value0, arg2: value1);

        Assert.False(SubscriptionManager.MatchesChange(input1, change));
    }

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    private static void ExerciseProjectionCaseMatrix2(
        Func<SyncSubscription> input0,
        Action<Func<string, string, SyncLogEntry>, SyncSubscription, string, string> input1,
        Func<string, string, SyncLogEntry> input2
    )
    {
        var sub = input0();

        input1(arg1: input2, arg2: sub, arg3: "Person", arg4: "{\"Id\":\"p1\"}");
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifySyncLoopWorkflow2(
        SyncSubscription input0,
        string value0,
        global::Nimblesite.Sync.Core.SubscriptionType value1,
        string value2,
        string value3
    )
    {
        global::Nimblesite.Sync.Tests.SyncStateTests.VerifyReusableWorkflow3(
            input0: input0,
            value0: value0,
            value2: value1,
            value3: value2
        );
        Assert.Equal(value3, input0.Filter);
        Assert.Equal(Timestamp, input0.CreatedAt);
    }
}
