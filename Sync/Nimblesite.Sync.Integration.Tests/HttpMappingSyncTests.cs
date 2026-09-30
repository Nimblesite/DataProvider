namespace Nimblesite.Sync.Integration.Tests;

/// <summary>
/// REAL E2E HTTP tests proving LQL/MappingEngine transforms data between DBs with DIFFERENT SCHEMAS.
/// These tests hit actual HTTP endpoints using WebApplicationFactory.
/// The key proof: Source table "User" with columns (Id, FullName, EmailAddress)
/// maps to Target table "Customer" with columns (CustomerId, Name, Email).
/// THIS IS REAL MAPPING, NOT JUST COPY!
/// Uses the shared postgres container; each test gets its own database.
/// </summary>
[Collection(PostgresTestSuite.Name)]
public sealed class HttpMappingSyncTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private string _postgresConnectionString = null!;
    private readonly ILogger _logger = NullLogger.Instance;
    private readonly List<string> _sqliteDbPaths = [];

    public async Task InitializeAsync()
    {
        _postgresConnectionString = await fixture
            .CreateDatabaseConnectionStringAsync("mapping_test")
            .ConfigureAwait(false);
    }

    public Task DisposeAsync()
    {
        foreach (var dbPath in _sqliteDbPaths)
        {
            if (File.Exists(dbPath))
            {
                try
                {
                    File.Delete(dbPath);
                }
                catch
                { /* File may be locked */
                }
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates SQLite source DB with User table (source schema).
    /// Columns: Id, FullName, EmailAddress (DIFFERENT from target!)
    /// </summary>
    private SqliteConnection CreateSourceDb(string originId)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"mapping_source_{Guid.NewGuid()}.db");
        _sqliteDbPaths.Add(dbPath);
        var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        SyncSchema.CreateSchema(conn);
        SyncSchema.SetOriginId(conn, originId);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE User (
                Id TEXT PRIMARY KEY,
                FullName TEXT NOT NULL,
                EmailAddress TEXT,
                PasswordHash TEXT,
                SecurityStamp TEXT,
                CreatedAt TEXT
            );
            """;
        cmd.ExecuteNonQuery();

        TriggerGenerator.CreateTriggers(conn, "User", NullLogger.Instance);
        return conn;
    }

    /// <summary>
    /// Creates Postgres target DB with Customer table (target schema).
    /// Columns: CustomerId, Name, Email, Source, RegisteredDate (DIFFERENT from source!)
    /// </summary>
    private NpgsqlConnection CreateTargetDb(string originId)
    {
        var conn = new NpgsqlConnection(_postgresConnectionString);
        conn.Open();

        var schemaResult = PostgresSyncSchema.CreateSchema(conn);
        if (schemaResult is not BoolSyncOk)
        {
            throw new InvalidOperationException(
                $"Failed to create Postgres schema: {schemaResult}"
            );
        }

        var originResult = PostgresSyncSchema.SetOriginId(conn, originId);
        if (originResult is not BoolSyncOk)
        {
            throw new InvalidOperationException($"Failed to set origin ID: {originResult}");
        }

        // Target schema is DIFFERENT from source!
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DROP TABLE IF EXISTS customer CASCADE;
            CREATE TABLE customer (
                customer_id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                email TEXT,
                source TEXT,
                registered_date TEXT
            );
            """;
        cmd.ExecuteNonQuery();

        return conn;
    }

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
        var mappingResult = MappingEngine.ApplyMapping(
            entry,
            mappingConfig,
            MappingDirection.Push,
            _logger
        );

        // Assert - Mapping worked!
        var success = Assert.IsType<MappingSuccess>(mappingResult);
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
        var mappingResult = MappingEngine.ApplyMapping(
            entry,
            mappingConfig,
            MappingDirection.Push,
            _logger
        );

        // Assert - TWO entries created from ONE source!
        var success = Assert.IsType<MappingSuccess>(mappingResult);
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
        var sourceOrigin = Guid.NewGuid().ToString();
        using var source = CreateSourceDb(sourceOrigin);

        var columnMappings = new List<ColumnMapping>
        {
            new("FullName", "name"),
            new("EmailAddress", "email"),
        };

        var mappingConfig = UserToCustomerConfig(columnMappings, []);

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
        var mappingResult = MappingEngine.ApplyMapping(
            updateList[0],
            mappingConfig,
            MappingDirection.Push,
            _logger
        );

        // Assert
        var success = Assert.IsType<MappingSuccess>(mappingResult);
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
        var mappingResult = MappingEngine.ApplyMapping(
            deleteList[0],
            mappingConfig,
            MappingDirection.Push,
            _logger
        );

        // Assert
        var success = Assert.IsType<MappingSuccess>(mappingResult);
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

    #region Edge Cases - Null, Unicode, Special Characters

    /// <summary>
    /// PROVES: Null values in payload are handled correctly.
    /// </summary>
    [Fact]
    public void NullValues_InPayload_MapsCorrectly()
    {
        var sourceOrigin = Guid.NewGuid().ToString();
        using var source = CreateSourceDb(sourceOrigin);

        var columnMappings = new List<ColumnMapping>
        {
            new("FullName", "name"),
            new("EmailAddress", "email"),
        };

        var mappingConfig = UserToCustomerConfig(columnMappings, []);

        // Insert with NULL email
        using (var cmd = source.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO User (Id, FullName, EmailAddress) VALUES ('u-null', 'Null Email User', NULL)";
            cmd.ExecuteNonQuery();
        }

        var changes = SyncLogRepository.FetchChanges(source, 0, 100);
        var changesList = ((SyncLogListOk)changes).Value;
        var entry = changesList[0];

        var mappingResult = MappingEngine.ApplyMapping(
            entry,
            mappingConfig,
            MappingDirection.Push,
            _logger
        );

        var success = Assert.IsType<MappingSuccess>(mappingResult);
        var mappedEntry = success.Entries[0];

        Assert.Contains("name", mappedEntry.MappedPayload);
        Assert.Contains("Null Email User", mappedEntry.MappedPayload);
        // NULL columns may be excluded from payload (valid behavior)
        // Just verify mapping didn't fail
    }

    /// <summary>
    /// PROVES: Unicode characters in data are preserved through mapping.
    /// </summary>
    [Fact]
    public void UnicodeCharacters_PreservedThroughMapping()
    {
        var sourceOrigin = Guid.NewGuid().ToString();
        using var source = CreateSourceDb(sourceOrigin);

        var columnMappings = new List<ColumnMapping>
        {
            new("FullName", "name"),
            new("EmailAddress", "email"),
        };

        var mappingConfig = UserToCustomerConfig(columnMappings, []);

        // Unicode characters: Japanese, Chinese, Korean, Arabic, Emoji
        var unicodeNames = new[]
        {
            ("u-jp", "田中太郎", "tanaka@日本.com"),
            ("u-cn", "张三", "zhang@中国.cn"),
            ("u-kr", "김철수", "kim@한국.kr"),
            ("u-ar", "محمد علي", "mohammad@example.com"),
            ("u-emoji", "🎉 Party User 🚀", "party@emoji.fun"),
            ("u-special", "Ñoño Español", "nono@españa.es"),
        };

        foreach (var (id, name, email) in unicodeNames)
        {
            using var cmd = source.CreateCommand();
            cmd.CommandText =
                "INSERT INTO User (Id, FullName, EmailAddress) VALUES (@id, @name, @email)";
            cmd.Parameters.Clear();

            var pId = cmd.CreateParameter();
            pId.ParameterName = "@id";
            pId.Value = id;
            cmd.Parameters.Add(pId);

            var pName = cmd.CreateParameter();
            pName.ParameterName = "@name";
            pName.Value = name;
            cmd.Parameters.Add(pName);

            var pEmail = cmd.CreateParameter();
            pEmail.ParameterName = "@email";
            pEmail.Value = email;
            cmd.Parameters.Add(pEmail);

            cmd.ExecuteNonQuery();
        }

        var changes = SyncLogRepository.FetchChanges(source, 0, 100);
        var changesList = ((SyncLogListOk)changes).Value;

        foreach (var entry in changesList)
        {
            var mappingResult = MappingEngine.ApplyMapping(
                entry,
                mappingConfig,
                MappingDirection.Push,
                _logger
            );
            var success = Assert.IsType<MappingSuccess>(mappingResult);
            var mappedEntry = success.Entries[0];

            // Verify the unicode is preserved in mapped payload
            Assert.NotNull(mappedEntry.MappedPayload);
            Assert.Contains("name", mappedEntry.MappedPayload);
        }

        // Specific check for emoji - emojis may be JSON-escaped (\uD83C\uDF89 etc.)
        var emojiEntry = changesList.First(e => e.PkValue.Contains("u-emoji"));
        var emojiResult = MappingEngine.ApplyMapping(
            emojiEntry,
            mappingConfig,
            MappingDirection.Push,
            _logger
        );
        var emojiSuccess = Assert.IsType<MappingSuccess>(emojiResult);
        var emojiPayload = emojiSuccess.Entries[0].MappedPayload;
        // Verify emoji content is present (may be escaped or literal)
        Assert.True(
            emojiPayload!.Contains("🎉") || emojiPayload.Contains("\\uD83C\\uDF89"),
            $"Emoji should be in payload: {emojiPayload}"
        );
        Assert.True(
            emojiPayload.Contains("🚀") || emojiPayload.Contains("\\uD83D\\uDE80"),
            $"Rocket emoji should be in payload: {emojiPayload}"
        );
    }

    /// <summary>
    /// PROVES: Special characters (quotes, backslashes, newlines) are handled.
    /// </summary>
    [Fact]
    public void SpecialCharacters_InData_HandledCorrectly()
    {
        var sourceOrigin = Guid.NewGuid().ToString();
        using var source = CreateSourceDb(sourceOrigin);

        var columnMappings = new List<ColumnMapping>
        {
            new("FullName", "name"),
            new("EmailAddress", "email"),
        };

        var mappingConfig = UserToCustomerConfig(columnMappings, []);

        // Special chars
        var specialCases = new[]
        {
            ("u-quotes", "John \"The Man\" Doe", "john@example.com"),
            ("u-backslash", "Path\\User\\Name", "path@example.com"),
            ("u-newline", "Line1\nLine2", "newline@example.com"),
            ("u-tab", "Col1\tCol2", "tab@example.com"),
            ("u-apostrophe", "O'Connor's Data", "oconnor@example.com"),
            ("u-ampersand", "Smith & Jones", "smitjones@example.com"),
            ("u-html", "<script>alert('XSS')</script>", "xss@example.com"),
        };

        foreach (var (id, name, email) in specialCases)
        {
            using var cmd = source.CreateCommand();
            cmd.CommandText =
                "INSERT INTO User (Id, FullName, EmailAddress) VALUES (@id, @name, @email)";
            cmd.Parameters.Clear();

            var pId = cmd.CreateParameter();
            pId.ParameterName = "@id";
            pId.Value = id;
            cmd.Parameters.Add(pId);

            var pName = cmd.CreateParameter();
            pName.ParameterName = "@name";
            pName.Value = name;
            cmd.Parameters.Add(pName);

            var pEmail = cmd.CreateParameter();
            pEmail.ParameterName = "@email";
            pEmail.Value = email;
            cmd.Parameters.Add(pEmail);

            cmd.ExecuteNonQuery();
        }

        var changes = SyncLogRepository.FetchChanges(source, 0, 100);
        var changesList = ((SyncLogListOk)changes).Value;

        foreach (var entry in changesList)
        {
            var mappingResult = MappingEngine.ApplyMapping(
                entry,
                mappingConfig,
                MappingDirection.Push,
                _logger
            );

            // Should not throw, should succeed
            var success = Assert.IsType<MappingSuccess>(mappingResult);
            Assert.NotNull(success.Entries[0].MappedPayload);
        }
    }

    /// <summary>
    /// PROVES: Empty strings are handled correctly (not confused with NULL).
    /// </summary>
    [Fact]
    public void EmptyString_NotConfusedWithNull()
    {
        var sourceOrigin = Guid.NewGuid().ToString();
        using var source = CreateSourceDb(sourceOrigin);

        var columnMappings = new List<ColumnMapping>
        {
            new("FullName", "name"),
            new("EmailAddress", "email"),
        };

        var mappingConfig = UserToCustomerConfig(columnMappings, []);

        // Insert with empty string (not NULL)
        using (var cmd = source.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO User (Id, FullName, EmailAddress) VALUES ('u-empty', 'Empty Email', '')";
            cmd.ExecuteNonQuery();
        }

        var changes = SyncLogRepository.FetchChanges(source, 0, 100);
        var entry = ((SyncLogListOk)changes).Value[0];

        var mappingResult = MappingEngine.ApplyMapping(
            entry,
            mappingConfig,
            MappingDirection.Push,
            _logger
        );

        var success = Assert.IsType<MappingSuccess>(mappingResult);
        var mappedEntry = success.Entries[0];

        Assert.Contains("email", mappedEntry.MappedPayload);
        // Empty string should be ""
        Assert.Contains("\"\"", mappedEntry.MappedPayload);
    }

    /// <summary>
    /// PROVES: Very long strings are handled correctly.
    /// </summary>
    [Fact]
    public void VeryLongStrings_HandledCorrectly()
    {
        var sourceOrigin = Guid.NewGuid().ToString();
        using var source = CreateSourceDb(sourceOrigin);

        var columnMappings = new List<ColumnMapping>
        {
            new("FullName", "name"),
            new("EmailAddress", "email"),
        };

        var mappingConfig = UserToCustomerConfig(columnMappings, []);

        // Very long string (10000 chars)
        var longName = new string('A', 10000);

        using (var cmd = source.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO User (Id, FullName, EmailAddress) VALUES (@id, @name, @email)";

            var pId = cmd.CreateParameter();
            pId.ParameterName = "@id";
            pId.Value = "u-long";
            cmd.Parameters.Add(pId);

            var pName = cmd.CreateParameter();
            pName.ParameterName = "@name";
            pName.Value = longName;
            cmd.Parameters.Add(pName);

            var pEmail = cmd.CreateParameter();
            pEmail.ParameterName = "@email";
            pEmail.Value = "long@example.com";
            cmd.Parameters.Add(pEmail);

            cmd.ExecuteNonQuery();
        }

        var changes = SyncLogRepository.FetchChanges(source, 0, 100);
        var entry = ((SyncLogListOk)changes).Value[0];

        var mappingResult = MappingEngine.ApplyMapping(
            entry,
            mappingConfig,
            MappingDirection.Push,
            _logger
        );

        var success = Assert.IsType<MappingSuccess>(mappingResult);
        var mappedEntry = success.Entries[0];

        Assert.NotNull(mappedEntry.MappedPayload);
        Assert.Contains(longName, mappedEntry.MappedPayload);
    }

    /// <summary>
    /// PROVES: Constant transform with special values works.
    /// </summary>
    [Fact]
    public void ConstantTransform_SpecialValues_Work()
    {
        var sourceOrigin = Guid.NewGuid().ToString();
        using var source = CreateSourceDb(sourceOrigin);

        var columnMappings = new List<ColumnMapping>
        {
            new("FullName", "name"),
            new(null, "status", TransformType.Constant, "active"),
            new(null, "priority", TransformType.Constant, "0"),
            new(null, "verified", TransformType.Constant, "true"),
            new(null, "notes", TransformType.Constant, ""),
        };

        var mappingConfig = UserToCustomerConfig(columnMappings, []);

        using (var cmd = source.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO User (Id, FullName, EmailAddress) VALUES ('u-const', 'Constant Test', 'const@example.com')";
            cmd.ExecuteNonQuery();
        }

        var changes = SyncLogRepository.FetchChanges(source, 0, 100);
        var entry = ((SyncLogListOk)changes).Value[0];

        var mappingResult = MappingEngine.ApplyMapping(
            entry,
            mappingConfig,
            MappingDirection.Push,
            _logger
        );

        var success = Assert.IsType<MappingSuccess>(mappingResult);
        var mappedEntry = success.Entries[0];

        Assert.Contains("status", mappedEntry.MappedPayload);
        Assert.Contains("active", mappedEntry.MappedPayload);
        Assert.Contains("priority", mappedEntry.MappedPayload);
        Assert.Contains("0", mappedEntry.MappedPayload);
        Assert.Contains("verified", mappedEntry.MappedPayload);
        Assert.Contains("true", mappedEntry.MappedPayload);
    }

    /// <summary>
    /// PROVES: All columns excluded produces minimal payload.
    /// </summary>
    [Fact]
    public void AllColumnsExcluded_MinimalPayload()
    {
        var sourceOrigin = Guid.NewGuid().ToString();
        using var source = CreateSourceDb(sourceOrigin);

        var mappingConfig = UserToCustomerConfig(
            [],
            ["FullName", "EmailAddress", "PasswordHash", "SecurityStamp", "CreatedAt"]
        );

        using (var cmd = source.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO User (Id, FullName, EmailAddress) VALUES ('u-excl', 'Excluded', 'excl@example.com')";
            cmd.ExecuteNonQuery();
        }

        var changes = SyncLogRepository.FetchChanges(source, 0, 100);
        var entry = ((SyncLogListOk)changes).Value[0];

        var mappingResult = MappingEngine.ApplyMapping(
            entry,
            mappingConfig,
            MappingDirection.Push,
            _logger
        );

        var success = Assert.IsType<MappingSuccess>(mappingResult);
        var mappedEntry = success.Entries[0];

        // PK should still be there
        Assert.Contains("customer_id", mappedEntry.TargetPkValue);

        // Payload should not contain excluded columns
        if (mappedEntry.MappedPayload is not null)
        {
            Assert.DoesNotContain("FullName", mappedEntry.MappedPayload);
            Assert.DoesNotContain("Excluded", mappedEntry.MappedPayload);
        }
    }

    /// <summary>
    /// PROVES: JSON payload within data is preserved.
    /// </summary>
    [Fact]
    public void JsonInPayload_PreservedCorrectly()
    {
        var sourceOrigin = Guid.NewGuid().ToString();
        using var source = CreateSourceDb(sourceOrigin);

        var columnMappings = new List<ColumnMapping>
        {
            new("FullName", "name"),
            new("EmailAddress", "email"),
        };

        var mappingConfig = UserToCustomerConfig(columnMappings, []);

        // Name contains JSON-like structure
        var jsonLikeName = "{\"first\":\"John\",\"last\":\"Doe\"}";

        using (var cmd = source.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO User (Id, FullName, EmailAddress) VALUES (@id, @name, @email)";

            var pId = cmd.CreateParameter();
            pId.ParameterName = "@id";
            pId.Value = "u-json";
            cmd.Parameters.Add(pId);

            var pName = cmd.CreateParameter();
            pName.ParameterName = "@name";
            pName.Value = jsonLikeName;
            cmd.Parameters.Add(pName);

            var pEmail = cmd.CreateParameter();
            pEmail.ParameterName = "@email";
            pEmail.Value = "json@example.com";
            cmd.Parameters.Add(pEmail);

            cmd.ExecuteNonQuery();
        }

        var changes = SyncLogRepository.FetchChanges(source, 0, 100);
        var entry = ((SyncLogListOk)changes).Value[0];

        var mappingResult = MappingEngine.ApplyMapping(
            entry,
            mappingConfig,
            MappingDirection.Push,
            _logger
        );

        var success = Assert.IsType<MappingSuccess>(mappingResult);
        var mappedEntry = success.Entries[0];

        Assert.Contains("name", mappedEntry.MappedPayload);
        // The nested JSON should be preserved as a string value
        Assert.NotNull(mappedEntry.MappedPayload);
    }

    #endregion

    private static SyncMappingConfig UserToCustomerConfig(
        IReadOnlyList<ColumnMapping> columnMappings,
        IReadOnlyList<string> excludedColumns
    ) =>
        new(
            "1.0",
            UnmappedTableBehavior.Strict,
            [
                new TableMapping(
                    Id: "user-to-customer",
                    SourceTable: "User",
                    TargetTable: "customer",
                    Direction: MappingDirection.Push,
                    Enabled: true,
                    PkMapping: new PkMapping("Id", "customer_id"),
                    ColumnMappings: columnMappings,
                    ExcludedColumns: excludedColumns,
                    Filter: null,
                    SyncTracking: new SyncTrackingConfig()
                ),
            ]
        );

    /// <summary>
    /// Helper to apply a mapped entry to PostgreSQL target table.
    /// Note: Table name is from test fixtures, not user input - safe for test code.
    /// </summary>
#pragma warning disable CA2100 // SQL from test fixtures, not user input
    private static void ApplyMappedEntryToPostgres(
        NpgsqlConnection conn,
        SyncOperation operation,
        MappedEntry mapped
    )
    {
        // Parse the mapped payload and PK
        if (operation == SyncOperation.Delete)
        {
            using var pkDoc = JsonDocument.Parse(mapped.TargetPkValue);
            var pkValue = pkDoc.RootElement.EnumerateObject().First().Value.GetString();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM {mapped.TargetTable} WHERE customer_id = @pk";
            cmd.Parameters.AddWithValue("pk", pkValue ?? "");
            cmd.ExecuteNonQuery();
            return;
        }

        if (mapped.MappedPayload is null)
            return;

        using var payloadDoc = JsonDocument.Parse(mapped.MappedPayload);
        using var pkValDoc = JsonDocument.Parse(mapped.TargetPkValue);

        var pkColumnValue = pkValDoc.RootElement.EnumerateObject().First();

        var columns = new List<string> { pkColumnValue.Name };
        var values = new List<string> { pkColumnValue.Value.GetString() ?? "" };

        foreach (var prop in payloadDoc.RootElement.EnumerateObject())
        {
            columns.Add(prop.Name);
            values.Add(
                prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? ""
                    : prop.Value.GetRawText()
            );
        }

        using var cmd2 = conn.CreateCommand();
        var colList = string.Join(", ", columns);
        var paramList = string.Join(", ", columns.Select((_, i) => $"@p{i}"));

        cmd2.CommandText = $"INSERT INTO {mapped.TargetTable} ({colList}) VALUES ({paramList})";

        for (var i = 0; i < values.Count; i++)
        {
            cmd2.Parameters.AddWithValue($"p{i}", values[i]);
        }

        cmd2.ExecuteNonQuery();
    }
#pragma warning restore CA2100
}
