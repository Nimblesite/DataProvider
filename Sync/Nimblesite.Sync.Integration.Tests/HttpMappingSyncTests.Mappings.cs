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
        var sourceOrigin = Guid.NewGuid().ToString();
        var targetOrigin = Guid.NewGuid().ToString();

        using var source = CreateSourceDb(sourceOrigin);
        using var target = CreateTargetDb(targetOrigin);

        // Define the mapping configuration
        var columnMappings = new List<ColumnMapping>
        {
            new("FullName", "name"),
            new("EmailAddress", "email"),
            new(null, "source", TransformType.Constant, "mobile-app"),
        };

        var mappingConfig = UserToCustomerConfig(columnMappings, ["PasswordHash", "SecurityStamp"]);

        // Act - Insert in source with SOURCE schema columns
        using (var cmd = source.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO User (Id, FullName, EmailAddress, PasswordHash, SecurityStamp, CreatedAt)
                VALUES ('u1', 'Alice Smith', 'alice@example.com', 'secret123', 'stamp-xyz', '2024-01-15');
                """;
            cmd.ExecuteNonQuery();
        }

        // Fetch changes from source
        var changes = SyncLogRepository.FetchChanges(source, 0, 100);
        Assert.True(changes is SyncLogListOk, $"FetchChanges failed: {changes}");
        var changesList = ((SyncLogListOk)changes).Value;
        Assert.Single(changesList);

        // Apply mapping to transform the entry
        var entry = changesList[0];
        var success = MapSuccessfully(entry: entry, mappingConfig: mappingConfig);
        Assert.Single(success.Entries);

        var mappedEntry = success.Entries[0];

        // Target table name changed
        Assert.Equal("customer", mappedEntry.TargetTable);

        // Primary key column renamed: Id -> customer_id
        Assert.Contains("customer_id", mappedEntry.TargetPkValue);
        Assert.Contains("u1", mappedEntry.TargetPkValue);
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
        var sourceOrigin = Guid.NewGuid().ToString();
        var targetOrigin = Guid.NewGuid().ToString();

        using var source = CreateSourceDb(sourceOrigin);
        using var target = CreateTargetDb(targetOrigin);

        var columnMappings = new List<ColumnMapping>
        {
            new("FullName", "name"),
            new("EmailAddress", "email"),
            new(null, "source", TransformType.Constant, "sync-test"),
        };

        var mappingConfig = UserToCustomerConfig(columnMappings, ["PasswordHash", "SecurityStamp"]);

        // Insert in source
        using (var cmd = source.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO User (Id, FullName, EmailAddress, PasswordHash, SecurityStamp)
                VALUES ('u2', 'Bob Johnson', 'bob@example.com', 'hash123', 'stamp-abc');
                """;
            cmd.ExecuteNonQuery();
        }

        // Fetch changes
        var changes = SyncLogRepository.FetchChanges(source, 0, 100);
        var changesList = ((SyncLogListOk)changes).Value;

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
        var sourceOrigin = Guid.NewGuid().ToString();
        var dbPath = Path.Combine(Path.GetTempPath(), $"multi_target_{Guid.NewGuid()}.db");
        _sqliteDbPaths.Add(dbPath);
        using var source = new SqliteConnection($"Data Source={dbPath}");
        source.Open();

        SyncSchema.CreateSchema(source);
        SyncSchema.SetOriginId(source, sourceOrigin);

        // Use SalesOrder instead of Order (reserved word in SQL)
        using (var cmd = source.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE SalesOrder (
                    Id TEXT PRIMARY KEY,
                    CustomerId TEXT NOT NULL,
                    Total REAL NOT NULL,
                    CreatedAt TEXT
                );
                """;
            cmd.ExecuteNonQuery();
        }

        TriggerGenerator.CreateTriggers(source, "SalesOrder", NullLogger.Instance);

        // Define multi-target mapping
        var targets = new List<TargetConfig>
        {
            new(
                "OrderHeader",
                [
                    new ColumnMapping("Id", "OrderId"),
                    new ColumnMapping("CustomerId", "CustomerId"),
                    new ColumnMapping("Total", "Amount"),
                ]
            ),
            new(
                "OrderAudit",
                [
                    new ColumnMapping("Id", "OrderId"),
                    new ColumnMapping("CreatedAt", "EventTime"),
                    new ColumnMapping(null, "EventType", TransformType.Constant, "order_created"),
                ]
            ),
        };

        var mapping = new TableMapping(
            Id: "order-split",
            SourceTable: "SalesOrder",
            TargetTable: null,
            Direction: MappingDirection.Push,
            Enabled: true,
            PkMapping: null,
            ColumnMappings: [],
            ExcludedColumns: [],
            Filter: null,
            SyncTracking: new SyncTrackingConfig(),
            IsMultiTarget: true,
            Targets: targets
        );

        var mappingConfig = new SyncMappingConfig("1.0", UnmappedTableBehavior.Strict, [mapping]);

        // Insert order
        using (var cmd = source.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO SalesOrder (Id, CustomerId, Total, CreatedAt)
                VALUES ('o1', 'c123', 249.99, '2024-01-15T10:30:00Z');
                """;
            cmd.ExecuteNonQuery();
        }

        // Fetch changes
        var changes = SyncLogRepository.FetchChanges(source, 0, 100);
        var changesList = ((SyncLogListOk)changes).Value;
        Assert.Single(changesList);

        // Apply mapping
        var entry = changesList[0];
        var success = MapSuccessfully(entry: entry, mappingConfig: mappingConfig);
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
        using (var cmd = source.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO User (Id, FullName, EmailAddress)
                VALUES ('u3', 'Original Name', 'original@example.com');
                """;
            cmd.ExecuteNonQuery();
        }

        var insertChanges = SyncLogRepository.FetchChanges(source, 0, 100);
        var insertVersion = ((SyncLogListOk)insertChanges).Value.Max(e => e.Version);

        // Update
        using (var cmd = source.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE User SET FullName = 'Updated Name', EmailAddress = 'updated@example.com'
                WHERE Id = 'u3';
                """;
            cmd.ExecuteNonQuery();
        }

        // Fetch update
        var updateChanges = SyncLogRepository.FetchChanges(source, insertVersion, 100);
        var updateList = ((SyncLogListOk)updateChanges).Value;
        Assert.Single(updateList);
        Assert.Equal(SyncOperation.Update, updateList[0].Operation);

        // Apply mapping to update
        var success = MapSuccessfully(entry: updateList[0], mappingConfig: mappingConfig);
        var mappedEntry = success.Entries[0];

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
        var sourceOrigin = Guid.NewGuid().ToString();
        using var source = CreateSourceDb(sourceOrigin);

        var mappingConfig = UserToCustomerConfig([], []);

        // Insert then delete
        using (var cmd = source.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO User (Id, FullName, EmailAddress) VALUES ('u4', 'Delete Me', 'd@x.com')";
            cmd.ExecuteNonQuery();
        }

        var insertChanges = SyncLogRepository.FetchChanges(source, 0, 100);
        var insertVersion = ((SyncLogListOk)insertChanges).Value.Max(e => e.Version);

        using (var cmd = source.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM User WHERE Id = 'u4'";
            cmd.ExecuteNonQuery();
        }

        var deleteChanges = SyncLogRepository.FetchChanges(source, insertVersion, 100);
        var deleteList = ((SyncLogListOk)deleteChanges).Value;
        Assert.Single(deleteList);
        Assert.Equal(SyncOperation.Delete, deleteList[0].Operation);

        // Apply mapping to delete
        var success = MapSuccessfully(entry: deleteList[0], mappingConfig: mappingConfig);
        var mappedEntry = success.Entries[0];

        Assert.Equal("customer", mappedEntry.TargetTable);
        Assert.Contains("customer_id", mappedEntry.TargetPkValue);
        Assert.Contains("u4", mappedEntry.TargetPkValue);
        Assert.Null(mappedEntry.MappedPayload); // Delete has no payload
    }

    /// <summary>
    /// PROVES: Bidirectional mapping with different configs for push/pull.
    /// </summary>
    [Fact]
    public void BidirectionalMapping_DifferentConfigsPerDirection()
    {
        // Push: User -> Customer (rename columns)
        var pushMapping = new TableMapping(
            Id: "user-push",
            SourceTable: "User",
            TargetTable: "Customer",
            Direction: MappingDirection.Push,
            Enabled: true,
            PkMapping: new PkMapping("Id", "CustomerId"),
            ColumnMappings: [new("FullName", "Name")],
            ExcludedColumns: [],
            Filter: null,
            SyncTracking: new SyncTrackingConfig()
        );

        // Pull: Customer -> User (reverse rename)
        var pullMapping = new TableMapping(
            Id: "customer-pull",
            SourceTable: "Customer",
            TargetTable: "User",
            Direction: MappingDirection.Pull,
            Enabled: true,
            PkMapping: new PkMapping("CustomerId", "Id"),
            ColumnMappings: [new("Name", "FullName")],
            ExcludedColumns: [],
            Filter: null,
            SyncTracking: new SyncTrackingConfig()
        );

        var config = new SyncMappingConfig(
            "1.0",
            UnmappedTableBehavior.Strict,
            [pushMapping, pullMapping]
        );

        // Find push mapping
        var foundPush = MappingEngine.FindMapping("User", config, MappingDirection.Push);
        Assert.NotNull(foundPush);
        Assert.Equal("user-push", foundPush.Id);

        // Find pull mapping
        var foundPull = MappingEngine.FindMapping("Customer", config, MappingDirection.Pull);
        Assert.NotNull(foundPull);
        Assert.Equal("customer-pull", foundPull.Id);

        // Push doesn't find pull
        var pushNoPull = MappingEngine.FindMapping("Customer", config, MappingDirection.Push);
        Assert.Null(pushNoPull);
    }
}
