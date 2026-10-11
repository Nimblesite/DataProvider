namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// PostgreSQL-specific edge case tests for migrations.
/// Tests PostgreSQL-specific types, behaviors, and edge cases.
/// Uses the shared postgres container; each test gets its own database.
/// </summary>
[Collection(PostgresTestSuite.Name)]
public sealed partial class PostgresEdgeCaseTests(PostgresContainerFixture fixture)
    : PostgresDatabaseTest(fixture)
{
    protected override string DatabaseName => "edge_test";

    #region Nullable Column Edge Cases

    [Fact]
    public void NullableColumn_InsertNull_Success()
    {
        VerifySharedChecks1(input0: Fixture_NullableColumn_InsertNull_Success_1);

        // Insert row with all nulls except PK
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = "INSERT INTO nullable_test (id) VALUES (1)";
        cmd.ExecuteNonQuery();

        // Verify nulls are stored
        using var reader = DatabaseCommands.ReadFirstRow(
            cmd,
            command =>
                command.CommandText =
                    "SELECT name, email, age, balance FROM nullable_test WHERE id = 1"
        );
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
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = "INSERT INTO evolving_table (id, name) VALUES (1, 'Test User')";
        cmd.ExecuteNonQuery();

        // Upgrade to v2 with new nullable column
        var v2 = Fixture_AddNullableColumn_ExistingDataUnaffected_Success_2();

        var operations = VerifyUnifiedWorkflow103(input0: v2);

        Assert.Single(operations);
        Assert.IsType<AddColumnOperation>(operations[0]);

        global::Tests.Shared.ResultAssertions.Ok(
            actual: PostgresTestDb.TryApply(
                connection: Connection,
                operations: operations,
                logger: Logger
            )
        );

        // Verify existing data preserved with NULL in new column
        using var reader = DatabaseCommands.ReadFirstRow(
            cmd,
            command => command.CommandText = "SELECT id, name, email FROM evolving_table"
        );
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal("Test User", reader.GetString(1));
        Assert.True(reader.IsDBNull(2));
    }

    #endregion

    #region Default Value Edge Cases

    [Fact]
    public void DefaultValue_NegativeNumber_Success()
    {
        InsertAndAssertRow(
            input0: Fixture_DefaultValue_NegativeNumber_Success_1,
            value0: "INSERT INTO negative_defaults (id) VALUES (1)",
            value1: "SELECT balance, adjustment FROM negative_defaults WHERE id = 1",
            assert: reader =>
            {
                Assert.Equal(-100.50m, reader.GetDecimal(0));
                Assert.Equal(-1, reader.GetInt32(1));
            }
        );
    }

    [Fact]
    public void DefaultValue_EmptyString_Success()
    {
        using var cmd = PrepareRow(
            input0: Fixture_DefaultValue_EmptyString_Success_1,
            value0: "INSERT INTO empty_defaults (id) VALUES (1)",
            value1: "SELECT notes FROM empty_defaults WHERE id = 1"
        );
        var result = cmd.ExecuteScalar();
        Assert.Equal("", result);
    }

    [Fact]
    public void DefaultValue_BooleanTrue_Success()
    {
        InsertAndAssertRow(
            input0: Fixture_DefaultValue_BooleanTrue_Success_1,
            value0: "INSERT INTO bool_defaults (id) VALUES (1)",
            value1: "SELECT is_active, is_deleted FROM bool_defaults WHERE id = 1",
            assert: reader =>
            {
                Assert.True(reader.GetBoolean(0));
                Assert.False(reader.GetBoolean(1));
            }
        );
    }

    [Fact]
    public void DefaultValue_UuidGeneration_Success()
    {
        using var cmd = PrepareRow(
            input0: Fixture_DefaultValue_UuidGeneration_Success_1,
            value0: "INSERT INTO uuid_defaults (id) VALUES (1), (2)",
            value1: "SELECT external_id FROM uuid_defaults ORDER BY id"
        );
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

    // Implements [CI-TEST-SHARED-CASE-MATRIX].
    [Theory]
    [MemberData(nameof(InsertAndVerifyStringsCases))]
    public void InsertAndVerifyStrings_ContextCases(
        Func<SchemaDefinition> input0,
        string insertTemplate,
        string parameterName,
        string selectSql,
        string[] values
    ) => InsertAndVerifyStrings(input0, insertTemplate, parameterName, selectSql, values);

    #endregion

    #region JSON Type Edge Cases

    [Fact]
    public void JsonColumn_StoreAndQuery_Success()
    {
        VerifySharedChecks1(input0: Fixture_JsonColumn_StoreAndQuery_Success_1);

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

        using var cmd = Connection.CreateCommand();
        for (var i = 0; i < jsonValues.Length; i++)
        {
            cmd.CommandText = $"INSERT INTO json_test (id, data) VALUES ({i + 1}, @data::jsonb)";
            VerifyReusableWorkflow6(input0: cmd, input1: jsonValues, input2: i, value0: "@data");
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
        VerifySharedChecks1(input0: Fixture_JsonColumn_NullVsJsonNull_Distinction_1);

        using var cmd = Connection.CreateCommand();

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
        InsertAndAssertRow(
            input0: Fixture_Decimal_ExtremeValues_Success_1,
            value0: @"
            INSERT INTO decimal_extremes (id, tiny, large, precise)
            VALUES (1, 0.0001, 999999999999999999, 1234567890.12345678)",
            value1: "SELECT tiny, large, precise FROM decimal_extremes WHERE id = 1",
            assert: reader =>
            {
                Assert.Equal(0.0001m, reader.GetDecimal(0));
                Assert.Equal(999999999999999999m, reader.GetDecimal(1));
                Assert.Equal(1234567890.12345678m, reader.GetDecimal(2));
            }
        );
    }

    [Fact]
    public void Decimal_ZeroAndNegative_Success()
    {
        InsertAndAssertRow(
            input0: Fixture_Decimal_ZeroAndNegative_Success_1,
            value0: "INSERT INTO decimal_zero_neg (id, zero, negative, negative_small) VALUES (1, 0.00, -99999.99, -0.00000001)",
            value1: "SELECT zero, negative, negative_small FROM decimal_zero_neg WHERE id = 1",
            assert: reader =>
            {
                Assert.Equal(0.00m, reader.GetDecimal(0));
                Assert.Equal(-99999.99m, reader.GetDecimal(1));
                Assert.Equal(-0.00000001m, reader.GetDecimal(2));
            }
        );
    }

    #endregion

    #region Timestamp Edge Cases

    [Fact]
    public void Timestamp_ExtremeValues_Success()
    {
        VerifySharedChecks1(input0: Fixture_Timestamp_ExtremeValues_Success_1);

        using var cmd = Connection.CreateCommand();
        ExecuteAndPrepareCommand(
            input0: cmd,
            value0: @"
            INSERT INTO timestamp_extremes (id, past, future)
            VALUES (1, '1900-01-01 00:00:00+00', '2999-12-31 23:59:59+00')",
            value1: "SELECT past, future FROM timestamp_extremes WHERE id = 1"
        );
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

        global::Tests.Shared.ResultAssertions.Ok(actual: ApplySchema(schema));
    }

    [Fact]
    public void Index_AllColumnsInTable_Success()
    {
        var schema = Fixture_Index_AllColumnsInTable_Success_1();

        global::Tests.Shared.ResultAssertions.Ok(actual: ApplySchema(schema));
    }

    #endregion

    #region Foreign Key Cascade Actions

    [Fact]
    public void ForeignKey_AllCascadeTypes_Success()
    {
        var schema = Fixture_ForeignKey_AllCascadeTypes_Success_1();

        global::Tests.Shared.ResultAssertions.Ok(actual: ApplySchema(schema));

        // Test cascade behavior
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = Data_ForeignKey_AllCascadeTypes_Success_1;
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
        var v2 = Fixture_MultipleOperations_CorrectOrder_Success_2();

        var operations = VerifyUnifiedWorkflow103(input0: v2);

        // Should have: 2 AddColumn, 2 CreateIndex, 1 CreateTable
        Assert.Equal(5, operations.Count);

        global::Tests.Shared.ResultAssertions.Ok(
            actual: PostgresTestDb.TryApply(
                connection: Connection,
                operations: operations,
                logger: Logger
            )
        );
    }

    #endregion

    #region Helper Methods

    private Outcome.Result<bool, MigrationError> ApplySchema(SchemaDefinition schema)
    {
        var operations = VerifyUnifiedWorkflow103(input0: schema);

        return PostgresTestDb.TryApply(
            connection: Connection,
            operations: operations,
            logger: Logger
        );
    }

    #endregion

    private void InsertAndAssertRow(
        Func<SchemaDefinition> input0,
        string value0,
        string value1,
        Action<NpgsqlDataReader> assert
    )
    {
        using var cmd = PrepareRow(input0: input0, value0: value0, value1: value1);
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        assert(obj: reader);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private NpgsqlCommand PrepareRow(Func<SchemaDefinition> input0, string value0, string value1)
    {
        VerifySharedChecks1(input0: input0);

        var cmd = Connection.CreateCommand();
        ExecuteAndPrepareCommand(input0: cmd, value0: value0, value1: value1);
        return cmd;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void InsertAndVerifyStrings(
        Func<SchemaDefinition> input0,
        string insertTemplate,
        string parameterName,
        string selectSql,
        string[] values
    )
    {
        VerifySharedChecks1(input0: input0);

        using var cmd = Connection.CreateCommand();
        for (var i = 0; i < values.Length; i++)
        {
            cmd.CommandText = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                insertTemplate,
                i + 1
            );
            VerifyReusableWorkflow6(input0: cmd, input1: values, input2: i, value0: parameterName);
        }

        cmd.CommandText = selectSql;
        cmd.Parameters.Clear();
        using var reader = cmd.ExecuteReader();
        for (var i = 0; i < values.Length; i++)
        {
            Assert.True(reader.Read(), $"Expected row {i + 1}");
            Assert.Equal(values[i], reader.GetString(1));
        }
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private static void ExecuteAndPrepareCommand(
        global::Npgsql.NpgsqlCommand input0,
        string value0,
        string value1
    )
    {
        input0.CommandText = value0;
        input0.ExecuteNonQuery();

        input0.CommandText = value1;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    private void VerifySharedChecks1(Func<SchemaDefinition> input0)
    {
        var schema = input0();

        ApplySchema(schema);
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal IReadOnlyList<SchemaOperation> VerifyUnifiedWorkflow103(SchemaDefinition input0)
    {
        var currentSchema = global::Tests
            .Shared.ResultAssertions.Ok(
                actual: PostgresSchemaInspector.Inspect(Connection, "public", Logger)
            )
            .Value;

        var operations = global::Tests
            .Shared.ResultAssertions.Ok(
                actual: SchemaDiff.Calculate(currentSchema, input0, logger: Logger)
            )
            .Value;

        return operations;
    }

    // Implements [CI-TEST-SHARED-SCENARIOS].
    internal static void VerifyReusableWorkflow6(
        NpgsqlCommand input0,
        string[] input1,
        int input2,
        string value0
    )
    {
        input0.Parameters.Clear();
        global::Tests.Shared.DatabaseCommands.Bind(
            command: input0,
            name: value0,
            value: input1[input2]
        );
        input0.ExecuteNonQuery();
    }
}
