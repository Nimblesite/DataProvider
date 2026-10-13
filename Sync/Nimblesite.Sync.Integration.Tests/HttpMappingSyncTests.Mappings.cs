// Implements [CI-TEST-SHARED-COMMAND-PARAMETERS].

namespace Nimblesite.Sync.Integration.Tests;

// Implements [SYNC-MAPPING-E2E-SETUP].
public sealed partial class HttpMappingSyncTests
{
    /// <summary>
    /// PROVES: MappingEngine transforms User -> Customer with column renaming.
    /// Source: User(Id, FullName, EmailAddress)
    /// Target: Customer(CustomerId, Name, Email)
    /// </summary>
    [Fact]
    public void Mapping_TransformsColumnsFromUserToCustomer()
    {
        // Arrange - Source and target with DIFFERENT schemas
        using var source = CreateSourceDb(Guid.NewGuid().ToString());
        using var target = CreateTargetDb(Guid.NewGuid().ToString());

        // Define the mapping configuration
        var mappingConfig = UserToCustomerConfig(
            Dataset_Mapping_TransformsColumnsFromUserToCustomer_1(),
            ["PasswordHash", "SecurityStamp"]
        );

        // Act - Insert in source with SOURCE schema columns and map the change
        var mappedEntry = InsertAndMapFirst(
            source,
            mappingConfig,
            """
            INSERT INTO User (Id, FullName, EmailAddress, PasswordHash, SecurityStamp, CreatedAt)
            VALUES ('u1', 'Alice Smith', 'alice@example.com', 'secret123', 'stamp-xyz', '2024-01-15');
            """
        );

        // Target table name changed
        global::Tests.Shared.MappingAssertions.Customer(mapped: mappedEntry, pkSubstring: "u1");
        Assert.DoesNotContain("\"Id\"", mappedEntry.TargetPkValue);

        // Payload transformed: FullName -> name, EmailAddress -> email
        Assert.NotNull(mappedEntry.MappedPayload);
        Assert.Contains("name", mappedEntry.MappedPayload);
        Assert.Contains("Alice Smith", mappedEntry.MappedPayload);
        Assert.Contains("email", mappedEntry.MappedPayload);
        Assert.Contains("alice@example.com", mappedEntry.MappedPayload);

        // Constant value added
        Assert.Contains("source", mappedEntry.MappedPayload);
        Assert.Contains("mobile-app", mappedEntry.MappedPayload);

        // Excluded columns NOT present
        Assert.DoesNotContain("PasswordHash", mappedEntry.MappedPayload);
        Assert.DoesNotContain("SecurityStamp", mappedEntry.MappedPayload);
        Assert.DoesNotContain("secret123", mappedEntry.MappedPayload);
    }

    /// <summary>
    /// PROVES: Sync can transform and apply mapped data to target database.
    /// Full E2E: Insert in source -> Transform via MappingEngine -> Apply to target.
    /// </summary>
    [Fact]
    public void FullSync_WithMapping_TransformsAndApplies()
    {
        // Arrange
        using var source = CreateSourceDb(Guid.NewGuid().ToString());
        using var target = CreateTargetDb(Guid.NewGuid().ToString());

        var mappingConfig = UserToCustomerConfig(
            Dataset_FullSync_WithMapping_TransformsAndApplies_1(),
            ["PasswordHash", "SecurityStamp"]
        );

        // Insert in source and fetch changes
        var changesList = ExecuteAndFetchAll(
            source,
            """
            INSERT INTO User (Id, FullName, EmailAddress, PasswordHash, SecurityStamp)
            VALUES ('u2', 'Bob Johnson', 'bob@example.com', 'hash123', 'stamp-abc');
            """
        );

        // Transform and apply
        PostgresSyncSession.EnableSuppression(target);
        foreach (var entry in changesList)
        {
            var mappingResult = MappingEngine.ApplyMapping(
                entry,
                mappingConfig,
                MappingDirection.Push,
                _logger
            );

            if (mappingResult is MappingSuccess success)
            {
                foreach (var mappedEntry in success.Entries)
                {
                    // Apply the TRANSFORMED entry to target
                    ApplyMappedEntryToPostgres(target, entry.Operation, mappedEntry);
                }
            }
        }
        PostgresSyncSession.DisableSuppression(target);

        // Assert - Data in target with TRANSFORMED schema
        using var verifyCmd = target.CreateCommand();
        verifyCmd.CommandText = "SELECT name, email, source FROM customer WHERE customer_id = 'u2'";
        using var reader = verifyCmd.ExecuteReader();

        Assert.True(reader.Read(), "Row should exist in target");
        Assert.Equal("Bob Johnson", reader.GetString(0));
        Assert.Equal("bob@example.com", reader.GetString(1));
        Assert.Equal("sync-test", reader.GetString(2));
    }

    /// <summary>
    /// PROVES: Multi-target mapping works - one source record creates multiple target records.
    /// Source: Order(Id, CustomerId, Total, CreatedAt)
    /// Target 1: OrderHeader(OrderId, CustomerId, Amount)
    /// Target 2: OrderAudit(OrderId, EventTime, EventType)
    /// </summary>
    [Fact]
    public void MultiTargetMapping_OneSourceToManyTargets()
    {
        // Arrange
        var dbPath = global::Tests.Shared.TempSqliteDatabase.PathFor("multi_target");
        _sqliteDbPaths.Add(dbPath);
        using var source = global::Tests.Shared.TempSqliteDatabase.OpenSync(
            path: dbPath,
            origin: Guid.NewGuid().ToString()
        );

        // Use SalesOrder instead of Order (reserved word in SQL)
        using (var cmd = source.CreateCommand())
        {
            cmd.CommandText = Data_MultiTargetMapping_OneSourceToManyTargets_1;
            cmd.ExecuteNonQuery();
        }

        TriggerGenerator.CreateTriggers(source, "SalesOrder", NullLogger.Instance);

        // Define multi-target mapping
        var targets = Dataset_MultiTargetMapping_OneSourceToManyTargets_1();

        var mapping = Dataset_MultiTargetMapping_OneSourceToManyTargets_2(targets: targets);

        var mappingConfig = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);

        // Insert order and fetch changes
        var changesList = ExecuteAndFetchAll(
            source,
            """
            INSERT INTO SalesOrder (Id, CustomerId, Total, CreatedAt)
            VALUES ('o1', 'c123', 249.99, '2024-01-15T10:30:00Z');
            """
        );

        // Apply mapping
        var success = MapSuccessfully(entry: changesList[0], mappingConfig: mappingConfig);
        Assert.Equal(2, success.Entries.Count);

        // OrderHeader entry
        var header = success.Entries.First(e => e.TargetTable == "OrderHeader");
        Assert.Contains("OrderId", header.MappedPayload);
        Assert.Contains("o1", header.MappedPayload);
        Assert.Contains("Amount", header.MappedPayload);
        Assert.Contains("249.99", header.MappedPayload);

        // OrderAudit entry
        var audit = success.Entries.First(e => e.TargetTable == "OrderAudit");
        Assert.Contains("EventType", audit.MappedPayload);
        Assert.Contains("order_created", audit.MappedPayload);
        Assert.Contains("EventTime", audit.MappedPayload);
    }

    /// <summary>
    /// PROVES: Update operations also get mapped correctly.
    /// </summary>
    [Fact]
    public void UpdateOperation_MapsCorrectly()
    {
        // Arrange
        using var source = CreateDefaultSourceDb(mappingConfig: out var mappingConfig);

        // Insert
        ExecuteSql(
            source,
            """
            INSERT INTO User (Id, FullName, EmailAddress)
            VALUES ('u3', 'Original Name', 'original@example.com');
            """
        );

        var insertVersion = FetchAllChanges(source).Max(e => e.Version);

        // Update
        ExecuteSql(
            source,
            """
            UPDATE User SET FullName = 'Updated Name', EmailAddress = 'updated@example.com'
            WHERE Id = 'u3';
            """
        );

        // Apply mapping to update
        var mappedEntry = MapSingle(
            FetchSingleOperation(source, insertVersion, SyncOperation.Update),
            mappingConfig
        );

        Assert.Contains("name", mappedEntry.MappedPayload);
        Assert.Contains("Updated Name", mappedEntry.MappedPayload);
        Assert.Contains("email", mappedEntry.MappedPayload);
        Assert.Contains("updated@example.com", mappedEntry.MappedPayload);
    }

    /// <summary>
    /// PROVES: Delete operations map correctly (PK transformation, null payload).
    /// </summary>
    [Fact]
    public void DeleteOperation_MapsPrimaryKeyCorrectly()
    {
        // Arrange
        using var source = CreateSourceDb(Guid.NewGuid().ToString());

        var mappingConfig = UserToCustomerConfig([], []);

        // Insert then delete
        ExecuteSql(
            source,
            "INSERT INTO User (Id, FullName, EmailAddress) VALUES ('u4', 'Delete Me', 'd@x.com')"
        );

        var insertVersion = FetchAllChanges(source).Max(e => e.Version);
        ExecuteSql(source, "DELETE FROM User WHERE Id = 'u4'");

        // Apply mapping to delete
        var mappedEntry = MapSingle(
            FetchSingleOperation(source, insertVersion, SyncOperation.Delete),
            mappingConfig
        );

        global::Tests.Shared.MappingAssertions.Customer(mapped: mappedEntry, pkSubstring: "u4");
        Assert.Null(mappedEntry.MappedPayload); // Delete has no payload
    }

    /// <summary>
    /// PROVES: Bidirectional mapping with different configs for push/pull.
    /// </summary>
    [Fact]
    public void BidirectionalMapping_DifferentConfigsPerDirection()
    {
        // Push: User -> Customer (rename columns)
        var pushMapping = Dataset_BidirectionalMapping_DifferentConfigsPerDirection_1();

        // Pull: Customer -> User (reverse rename)
        var pullMapping = Dataset_BidirectionalMapping_DifferentConfigsPerDirection_2();

        var config = Dataset_BidirectionalMapping_DifferentConfigsPerDirection_3(
            pushMapping: pushMapping,
            pullMapping: pullMapping
        );

        // Find push mapping
        AssertMappingFound("User", config, MappingDirection.Push, "user-push");

        // Find pull mapping
        AssertMappingFound("Customer", config, MappingDirection.Pull, "customer-pull");

        // Push doesn't find pull
        AssertMappingMissing("Customer", config, MappingDirection.Push);
    }
}
