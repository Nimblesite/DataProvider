namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// PostgreSQL-specific edge case tests for migrations.
/// Tests PostgreSQL-specific types, behaviors, and edge cases.
/// Uses the shared postgres container; each test gets its own database.
/// </summary>
[Collection(PostgresTestSuite.Name)]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Usage",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposed via IAsyncLifetime.DisposeAsync"
)]
public sealed partial class PostgresEdgeCaseTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private NpgsqlConnection _connection = null!;
    private readonly ILogger _logger = NullLogger.Instance;

    public async Task InitializeAsync()
    {
        _connection = await fixture.CreateDatabaseAsync("edge_test").ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync().ConfigureAwait(false);
    }

    #region Nullable Column Edge Cases

    [Fact]
    public void NullableColumn_InsertNull_Success()
    {
        var schema = Fixture_NullableColumn_InsertNull_Success_1();

        ApplySchema(schema);

        // Insert row with all nulls except PK
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT INTO nullable_test (id) VALUES (1)";
        cmd.ExecuteNonQuery();

        // Verify nulls are stored
        cmd.CommandText = "SELECT name, email, age, balance FROM nullable_test WHERE id = 1";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.True(reader.IsDBNull(0));
        Assert.True(reader.IsDBNull(1));
        Assert.True(reader.IsDBNull(2));
        Assert.True(reader.IsDBNull(3));
    }

    [Fact]
    public void AddNullableColumn_ExistingDataUnaffected_Success()
    {
        // Create v1 schema with data
        var v1 = Fixture_AddNullableColumn_ExistingDataUnaffected_Success_1();

        ApplySchema(v1);

        // Insert data
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT INTO evolving_table (id, name) VALUES (1, 'Test User')";
        cmd.ExecuteNonQuery();

        // Upgrade to v2 with new nullable column
        var v2 = Fixture_AddNullableColumn_ExistingDataUnaffected_Success_2();

        var currentSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;

        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(currentSchema, v2, logger: _logger)
        ).Value;

        Assert.Single(operations);
        Assert.IsType<AddColumnOperation>(operations[0]);

        var result = PostgresTestDb.TryApply(
            connection: _connection,
            operations: operations,
            logger: _logger
        );

        Assert.True(result is MigrationApplyResultOk);

        // Verify existing data preserved with NULL in new column
        cmd.CommandText = "SELECT id, name, email FROM evolving_table";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal("Test User", reader.GetString(1));
        Assert.True(reader.IsDBNull(2));
    }

    #endregion

    #region Default Value Edge Cases

    [Fact]
    public void DefaultValue_NegativeNumber_Success()
    {
        var schema = Fixture_DefaultValue_NegativeNumber_Success_1();

        ApplySchema(schema);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT INTO negative_defaults (id) VALUES (1)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT balance, adjustment FROM negative_defaults WHERE id = 1";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(-100.50m, reader.GetDecimal(0));
        Assert.Equal(-1, reader.GetInt32(1));
    }

    [Fact]
    public void DefaultValue_EmptyString_Success()
    {
        var schema = Fixture_DefaultValue_EmptyString_Success_1();

        ApplySchema(schema);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT INTO empty_defaults (id) VALUES (1)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT notes FROM empty_defaults WHERE id = 1";
        var result = cmd.ExecuteScalar();
        Assert.Equal("", result);
    }

    [Fact]
    public void DefaultValue_BooleanTrue_Success()
    {
        var schema = Fixture_DefaultValue_BooleanTrue_Success_1();

        ApplySchema(schema);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT INTO bool_defaults (id) VALUES (1)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT is_active, is_deleted FROM bool_defaults WHERE id = 1";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.True(reader.GetBoolean(0));
        Assert.False(reader.GetBoolean(1));
    }

    [Fact]
    public void DefaultValue_UuidGeneration_Success()
    {
        var schema = Fixture_DefaultValue_UuidGeneration_Success_1();

        ApplySchema(schema);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT INTO uuid_defaults (id) VALUES (1), (2)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT external_id FROM uuid_defaults ORDER BY id";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        var uuid1 = reader.GetGuid(0);
        Assert.True(reader.Read());
        var uuid2 = reader.GetGuid(0);

        // Both should be valid UUIDs and different
        Assert.NotEqual(Guid.Empty, uuid1);
        Assert.NotEqual(Guid.Empty, uuid2);
        Assert.NotEqual(uuid1, uuid2);
    }

    #endregion

    #region Unicode and Special Characters

    [Fact]
    public void UnicodeData_InsertAndRetrieve_Success()
    {
        var schema = Fixture_UnicodeData_InsertAndRetrieve_Success_1();

        ApplySchema(schema);

        var unicodeStrings = new[]
        {
            "日本語テスト",
            "العربية",
            "🎉🚀💻",
            "Ñoño español",
            "Ελληνικά",
            "中文测试",
            "한국어 테스트",
            "Тест кириллицы",
        };

        using var cmd = _connection.CreateCommand();
        for (var i = 0; i < unicodeStrings.Length; i++)
        {
            cmd.CommandText = $"INSERT INTO unicode_test (id, name) VALUES ({i + 1}, @name)";
            cmd.Parameters.Clear();
            var param = cmd.CreateParameter();
            param.ParameterName = "@name";
            param.Value = unicodeStrings[i];
            cmd.Parameters.Add(param);
            cmd.ExecuteNonQuery();
        }

        // Verify all data retrieved correctly
        cmd.CommandText = "SELECT id, name FROM unicode_test ORDER BY id";
        cmd.Parameters.Clear();
        using var reader = cmd.ExecuteReader();

        for (var i = 0; i < unicodeStrings.Length; i++)
        {
            Assert.True(reader.Read());
            Assert.Equal(unicodeStrings[i], reader.GetString(1));
        }
    }

    [Fact]
    public void SpecialChars_InData_Success()
    {
        var schema = Fixture_SpecialChars_InData_Success_1();

        ApplySchema(schema);

        var specialStrings = new[]
        {
            "Line1\nLine2\nLine3",
            "Tab\there\ttoo",
            "Quote's \"double\" `backtick`",
            "Backslash \\ path",
            // Note: Null byte \0 is not valid in PostgreSQL text columns
            "<html>&amp;entity</html>",
            "{ \"json\": true }",
            "100% of $money",
        };

        using var cmd = _connection.CreateCommand();
        for (var i = 0; i < specialStrings.Length; i++)
        {
            cmd.CommandText = $"INSERT INTO special_chars (id, value) VALUES ({i + 1}, @value)";
            cmd.Parameters.Clear();
            var param = cmd.CreateParameter();
            param.ParameterName = "@value";
            param.Value = specialStrings[i];
            cmd.Parameters.Add(param);
            cmd.ExecuteNonQuery();
        }

        cmd.CommandText = "SELECT id, value FROM special_chars ORDER BY id";
        cmd.Parameters.Clear();
        using var reader = cmd.ExecuteReader();

        for (var i = 0; i < specialStrings.Length; i++)
        {
            Assert.True(reader.Read(), $"Expected row {i + 1}");
            Assert.Equal(specialStrings[i], reader.GetString(1));
        }
    }

    #endregion

    #region JSON Type Edge Cases

    [Fact]
    public void JsonColumn_StoreAndQuery_Success()
    {
        var schema = Fixture_JsonColumn_StoreAndQuery_Success_1();

        ApplySchema(schema);

        var jsonValues = new[]
        {
            "{}",
            "[]",
            "null",
            "{\"key\": \"value\"}",
            "[1, 2, 3, 4, 5]",
            "{\"nested\": {\"deep\": {\"value\": 42}}}",
            "{\"unicode\": \"日本語\"}",
            "{\"special\": \"quote\\\"here\"}",
        };

        using var cmd = _connection.CreateCommand();
        for (var i = 0; i < jsonValues.Length; i++)
        {
            cmd.CommandText = $"INSERT INTO json_test (id, data) VALUES ({i + 1}, @data::jsonb)";
            cmd.Parameters.Clear();
            var param = cmd.CreateParameter();
            param.ParameterName = "@data";
            param.Value = jsonValues[i];
            cmd.Parameters.Add(param);
            cmd.ExecuteNonQuery();
        }

        // Verify JSON query works
        cmd.CommandText = "SELECT data->>'key' FROM json_test WHERE id = 4";
        cmd.Parameters.Clear();
        var result = cmd.ExecuteScalar();
        Assert.Equal("value", result);
    }

    [Fact]
    public void JsonColumn_NullVsJsonNull_Distinction()
    {
        var schema = Fixture_JsonColumn_NullVsJsonNull_Distinction_1();

        ApplySchema(schema);

        using var cmd = _connection.CreateCommand();

        // Insert SQL NULL
        cmd.CommandText = "INSERT INTO json_null_test (id, data) VALUES (1, NULL)";
        cmd.ExecuteNonQuery();

        // Insert JSON null
        cmd.CommandText = "INSERT INTO json_null_test (id, data) VALUES (2, 'null'::jsonb)";
        cmd.ExecuteNonQuery();

        // Verify distinction
        cmd.CommandText =
            "SELECT data IS NULL, data = 'null'::jsonb FROM json_null_test ORDER BY id";
        using var reader = cmd.ExecuteReader();

        Assert.True(reader.Read());
        Assert.True(reader.GetBoolean(0)); // SQL NULL
        Assert.True(reader.IsDBNull(1)); // Can't compare with JSON null

        Assert.True(reader.Read());
        Assert.False(reader.GetBoolean(0)); // Not SQL NULL
        Assert.True(reader.GetBoolean(1)); // Is JSON null
    }

    #endregion

    #region Decimal Precision Edge Cases

    [Fact]
    public void Decimal_ExtremeValues_Success()
    {
        var schema = Fixture_Decimal_ExtremeValues_Success_1();

        ApplySchema(schema);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            @"
            INSERT INTO decimal_extremes (id, tiny, large, precise)
            VALUES (1, 0.0001, 999999999999999999, 1234567890.12345678)";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT tiny, large, precise FROM decimal_extremes WHERE id = 1";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(0.0001m, reader.GetDecimal(0));
        Assert.Equal(999999999999999999m, reader.GetDecimal(1));
        Assert.Equal(1234567890.12345678m, reader.GetDecimal(2));
    }

    [Fact]
    public void Decimal_ZeroAndNegative_Success()
    {
        var schema = Fixture_Decimal_ZeroAndNegative_Success_1();

        ApplySchema(schema);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "INSERT INTO decimal_zero_neg (id, zero, negative, negative_small) VALUES (1, 0.00, -99999.99, -0.00000001)";
        cmd.ExecuteNonQuery();

        cmd.CommandText =
            "SELECT zero, negative, negative_small FROM decimal_zero_neg WHERE id = 1";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(0.00m, reader.GetDecimal(0));
        Assert.Equal(-99999.99m, reader.GetDecimal(1));
        Assert.Equal(-0.00000001m, reader.GetDecimal(2));
    }

    #endregion

    #region Timestamp Edge Cases

    [Fact]
    public void Timestamp_ExtremeValues_Success()
    {
        var schema = Fixture_Timestamp_ExtremeValues_Success_1();

        ApplySchema(schema);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            @"
            INSERT INTO timestamp_extremes (id, past, future)
            VALUES (1, '1900-01-01 00:00:00+00', '2999-12-31 23:59:59+00')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT past, future FROM timestamp_extremes WHERE id = 1";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());

        var past = reader.GetDateTime(0);
        var future = reader.GetDateTime(1);

        Assert.Equal(1900, past.Year);
        Assert.Equal(2999, future.Year);
    }

    #endregion

    #region Index Edge Cases

    [Fact]
    public void Index_VeryLongName_Success()
    {
        var longName = "idx_" + new string('a', 59); // Max 63 chars in PG

        var schema = Fixture_Index_VeryLongName_Success_1(longName: longName);

        var result = ApplySchema(schema);

        Assert.True(result is MigrationApplyResultOk);
    }

    [Fact]
    public void Index_AllColumnsInTable_Success()
    {
        var schema = Fixture_Index_AllColumnsInTable_Success_1();

        var result = ApplySchema(schema);

        Assert.True(result is MigrationApplyResultOk);
    }

    #endregion

    #region Foreign Key Cascade Actions

    [Fact]
    public void ForeignKey_AllCascadeTypes_Success()
    {
        var schema = Fixture_ForeignKey_AllCascadeTypes_Success_1();

        var result = ApplySchema(schema);

        Assert.True(result is MigrationApplyResultOk);

        // Test cascade behavior
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            @"
            INSERT INTO parent_table (id) VALUES (1);
            INSERT INTO child_cascade (id, parent_id) VALUES (1, 1);
            INSERT INTO child_setnull (id, parent_id) VALUES (1, 1);
            DELETE FROM parent_table WHERE id = 1;
        ";
        cmd.ExecuteNonQuery();

        // Verify cascade delete worked
        cmd.CommandText = "SELECT COUNT(*) FROM child_cascade";
        var cascadeCount = Convert.ToInt32(
            cmd.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture
        );
        Assert.Equal(0, cascadeCount);

        // Verify set null worked
        cmd.CommandText = "SELECT parent_id FROM child_setnull WHERE id = 1";
        var setNullValue = cmd.ExecuteScalar();
        Assert.Equal(DBNull.Value, setNullValue);
    }

    #endregion

    #region Schema Operations Order

    [Fact]
    public void MultipleOperations_CorrectOrder_Success()
    {
        // Start with minimal schema
        var v1 = Fixture_MultipleOperations_CorrectOrder_Success_1();

        ApplySchema(v1);

        // Upgrade with multiple changes
        var v2 = Fixture_MultipleOperations_CorrectOrder_Success_2(unique: unique);

        var currentSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;

        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(currentSchema, v2, logger: _logger)
        ).Value;

        // Should have: 2 AddColumn, 2 CreateIndex, 1 CreateTable
        Assert.Equal(5, operations.Count);

        var result = PostgresTestDb.TryApply(
            connection: _connection,
            operations: operations,
            logger: _logger
        );

        Assert.True(result is MigrationApplyResultOk);
    }

    #endregion

    #region Helper Methods

    private Outcome.Result<bool, MigrationError> ApplySchema(SchemaDefinition schema)
    {
        var currentSchema = (
            (SchemaResultOk)PostgresSchemaInspector.Inspect(_connection, "public", _logger)
        ).Value;

        var operations = (
            (OperationsResultOk)SchemaDiff.Calculate(currentSchema, schema, logger: _logger)
        ).Value;

        return PostgresTestDb.TryApply(
            connection: _connection,
            operations: operations,
            logger: _logger
        );
    }

    #endregion
}
