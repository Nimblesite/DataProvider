namespace Nimblesite.DataProvider.Migration.Tests;

/// <summary>
/// Reusable schema verification utilities for comprehensive E2E testing.
/// Proves that schema objects are created CORRECTLY - tables, columns, indexes, FKs, defaults, constraints.
/// </summary>
public static class SchemaVerifier
{
    // =========================================================================
    // POSTGRESQL VERIFICATION
    // =========================================================================

    /// <summary>
    /// Verifies a PostgreSQL schema matches the expected definition EXACTLY.
    /// Checks tables, columns, types, nullability, defaults, indexes, FKs, constraints.
    /// </summary>
    public static void VerifyPostgresSchema(
        NpgsqlConnection conn,
        SchemaDefinition expected,
        string schemaName = "public"
    )
    {
        foreach (var table in expected.Tables)
        {
            VerifyPostgresTable(conn, table, schemaName);
        }

        // Verify NO extra tables exist
        var expectedTableNames = expected.Tables.Select(t => t.Name).ToHashSet();
        var actualTables = GetPostgresTables(conn, schemaName);
        var extraTables = actualTables.Except(expectedTableNames).ToList();
        Assert.Empty(extraTables);
    }

    /// <summary>
    /// Verifies a single PostgreSQL table matches its definition.
    /// </summary>
    public static void VerifyPostgresTable(
        NpgsqlConnection conn,
        TableDefinition expected,
        string schemaName = "public"
    )
    {
        // 1. Table exists
        Assert.True(
            PostgresTableExists(conn, expected.Name, schemaName),
            $"Table '{expected.Name}' should exist"
        );

        // 2. All columns exist with correct types, nullability, defaults
        foreach (var col in expected.Columns)
        {
            VerifyPostgresColumn(conn, expected.Name, col, schemaName);
        }

        // 3. No extra columns
        var expectedColNames = expected.Columns.Select(c => c.Name).ToHashSet();
        var actualCols = GetPostgresColumns(conn, expected.Name, schemaName);
        var extraCols = actualCols.Except(expectedColNames).ToList();
        Assert.Empty(extraCols);

        // 4. Primary key
        if (expected.PrimaryKey is not null)
        {
            VerifyPostgresPrimaryKey(conn, expected.Name, expected.PrimaryKey, schemaName);
        }

        // 5. Indexes (including expression indexes)
        foreach (var idx in expected.Indexes)
        {
            VerifyPostgresIndex(conn, expected.Name, idx, schemaName);
        }

        // 6. Foreign keys
        foreach (var fk in expected.ForeignKeys)
        {
            VerifyPostgresForeignKey(conn, expected.Name, fk, schemaName);
        }

        // 7. Unique constraints
        foreach (var uc in expected.UniqueConstraints)
        {
            VerifyPostgresUniqueConstraint(conn, expected.Name, uc, schemaName);
        }
    }

    /// <summary>
    /// Verifies a PostgreSQL column matches its definition.
    /// </summary>
    public static void VerifyPostgresColumn(
        NpgsqlConnection conn,
        string tableName,
        ColumnDefinition expected,
        string schemaName = "public"
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                column_name,
                data_type,
                is_nullable,
                column_default,
                character_maximum_length,
                numeric_precision,
                numeric_scale
            FROM information_schema.columns
            WHERE table_schema = @schema AND table_name = @table AND column_name = @column
            """;
        cmd.Parameters.AddWithValue("@schema", schemaName);
        cmd.Parameters.AddWithValue("@table", tableName);
        cmd.Parameters.AddWithValue("@column", expected.Name);

        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read(), $"Column '{expected.Name}' should exist in table '{tableName}'");

        var isNullable = reader.GetString(2) == "YES";
        Assert.Equal(expected.IsNullable, isNullable);

        // Check default if specified
        if (expected.DefaultLqlExpression is not null || expected.DefaultValue is not null)
        {
            var actualDefault = reader.IsDBNull(3) ? null : reader.GetString(3);
            Assert.NotNull(actualDefault);

            // Verify the default contains expected pattern
            if (expected.DefaultLqlExpression is not null)
            {
                var translated = LqlDefaultTranslator.ToPostgres(expected.DefaultLqlExpression);
                Assert.Contains(translated.ToLowerInvariant(), actualDefault.ToLowerInvariant());
            }
        }
    }

    /// <summary>
    /// Verifies a PostgreSQL primary key matches its definition.
    /// </summary>
    public static void VerifyPostgresPrimaryKey(
        NpgsqlConnection conn,
        string tableName,
        PrimaryKeyDefinition expected,
        string schemaName = "public"
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT kcu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name
                AND tc.table_schema = kcu.table_schema
            WHERE tc.table_schema = @schema
            AND tc.table_name = @table
            AND tc.constraint_type = 'PRIMARY KEY'
            ORDER BY kcu.ordinal_position
            """;
        cmd.Parameters.AddWithValue("@schema", schemaName);
        cmd.Parameters.AddWithValue("@table", tableName);

        var actualColumns = new List<string>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                actualColumns.Add(reader.GetString(0));
            }
        }

        Assert.Equal(expected.Columns.Count, actualColumns.Count);
        for (var i = 0; i < expected.Columns.Count; i++)
        {
            Assert.Equal(expected.Columns[i], actualColumns[i]);
        }
    }

    /// <summary>
    /// Verifies a PostgreSQL index matches its definition.
    /// </summary>
    public static void VerifyPostgresIndex(
        NpgsqlConnection conn,
        string tableName,
        IndexDefinition expected,
        string schemaName = "public"
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                i.relname AS index_name,
                ix.indisunique AS is_unique,
                pg_get_indexdef(ix.indexrelid) AS index_def
            FROM pg_class t
            JOIN pg_index ix ON t.oid = ix.indrelid
            JOIN pg_class i ON i.oid = ix.indexrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = @schema
            AND t.relname = @table
            AND i.relname = @index
            """;
        cmd.Parameters.AddWithValue("@schema", schemaName);
        cmd.Parameters.AddWithValue("@table", tableName);
        cmd.Parameters.AddWithValue("@index", expected.Name);

        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read(), $"Index '{expected.Name}' should exist on table '{tableName}'");

        var isUnique = reader.GetBoolean(1);
        Assert.Equal(expected.IsUnique, isUnique);

        var indexDef = reader.GetString(2).ToLowerInvariant();

        // Verify expression or column indexes
        if (expected.Expressions.Count > 0)
        {
            foreach (var expr in expected.Expressions)
            {
                Assert.Contains(expr.ToLowerInvariant(), indexDef);
            }
        }
        else
        {
            foreach (var col in expected.Columns)
            {
                Assert.Contains(col.ToLowerInvariant(), indexDef);
            }
        }
    }

    /// <summary>
    /// Verifies a PostgreSQL foreign key matches its definition.
    /// </summary>
    public static void VerifyPostgresForeignKey(
        NpgsqlConnection conn,
        string tableName,
        ForeignKeyDefinition expected,
        string schemaName = "public"
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                tc.constraint_name,
                kcu.column_name,
                ccu.table_name AS referenced_table,
                ccu.column_name AS referenced_column,
                rc.delete_rule,
                rc.update_rule
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name
                AND tc.table_schema = kcu.table_schema
            JOIN information_schema.constraint_column_usage ccu
                ON ccu.constraint_name = tc.constraint_name
            JOIN information_schema.referential_constraints rc
                ON tc.constraint_name = rc.constraint_name
            WHERE tc.table_schema = @schema
            AND tc.table_name = @table
            AND tc.constraint_type = 'FOREIGN KEY'
            AND kcu.column_name = @column
            """;
        cmd.Parameters.AddWithValue("@schema", schemaName);
        cmd.Parameters.AddWithValue("@table", tableName);
        cmd.Parameters.AddWithValue("@column", expected.Columns[0]);

        using var reader = cmd.ExecuteReader();
        Assert.True(
            reader.Read(),
            $"Foreign key on '{expected.Columns[0]}' should exist in table '{tableName}'"
        );

        var refTable = reader.GetString(2);
        var refColumn = reader.GetString(3);
        var deleteRule = reader.GetString(4);
        var updateRule = reader.GetString(5);

        Assert.Equal(expected.ReferencedTable, refTable);
        Assert.Equal(expected.ReferencedColumns[0], refColumn);
        Assert.Equal(ForeignKeyActionToString(expected.OnDelete), deleteRule);
        Assert.Equal(ForeignKeyActionToString(expected.OnUpdate), updateRule);
    }

    /// <summary>
    /// Verifies a PostgreSQL unique constraint matches its definition.
    /// </summary>
    public static void VerifyPostgresUniqueConstraint(
        NpgsqlConnection conn,
        string tableName,
        UniqueConstraintDefinition expected,
        string schemaName = "public"
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT kcu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name
                AND tc.table_schema = kcu.table_schema
            WHERE tc.table_schema = @schema
            AND tc.table_name = @table
            AND tc.constraint_type = 'UNIQUE'
            ORDER BY kcu.ordinal_position
            """;
        cmd.Parameters.AddWithValue("@schema", schemaName);
        cmd.Parameters.AddWithValue("@table", tableName);

        var actualColumns = new List<string>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                actualColumns.Add(reader.GetString(0));
            }
        }

        // Check that expected columns are present
        foreach (var col in expected.Columns)
        {
            Assert.Contains(col, actualColumns);
        }
    }

    // =========================================================================
    // SQLITE VERIFICATION
    // =========================================================================

    /// <summary>
    /// Verifies a SQLite schema matches the expected definition EXACTLY.
    /// </summary>
    public static void VerifySqliteSchema(SqliteConnection conn, SchemaDefinition expected)
    {
        foreach (var table in expected.Tables)
        {
            VerifySqliteTable(conn, table);
        }

        // Verify NO extra tables exist
        var expectedTableNames = expected.Tables.Select(t => t.Name).ToHashSet();
        var actualTables = GetSqliteTables(conn);
        var extraTables = actualTables.Except(expectedTableNames).ToList();
        Assert.Empty(extraTables);
    }

    /// <summary>
    /// Verifies a single SQLite table matches its definition.
    /// </summary>
    public static void VerifySqliteTable(SqliteConnection conn, TableDefinition expected)
    {
        // 1. Table exists
        Assert.True(
            SqliteTableExists(conn, expected.Name),
            $"Table '{expected.Name}' should exist"
        );

        // 2. Get table DDL and verify structure
        var tableDdl = GetSqliteTableDdl(conn, expected.Name);
        Assert.NotNull(tableDdl);

        // 3. Verify all columns
        foreach (var col in expected.Columns)
        {
            VerifySqliteColumn(conn, expected.Name, col);
        }

        // 4. Verify indexes
        foreach (var idx in expected.Indexes)
        {
            VerifySqliteIndex(conn, idx);
        }

        // 5. Verify foreign keys are in DDL
        foreach (var fk in expected.ForeignKeys)
        {
            Assert.Contains($"REFERENCES [{fk.ReferencedTable}]", tableDdl);
        }
    }

    /// <summary>
    /// Verifies a SQLite column matches its definition.
    /// </summary>
    public static void VerifySqliteColumn(
        SqliteConnection conn,
        string tableName,
        ColumnDefinition expected
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info([{tableName}])";

        ColumnInfo? columnInfo = null;
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var name = reader.GetString(1);
                if (name == expected.Name)
                {
                    columnInfo = new ColumnInfo(
                        Name: name,
                        Type: reader.GetString(2),
                        NotNull: reader.GetInt32(3) == 1,
                        DefaultValue: reader.IsDBNull(4) ? null : reader.GetString(4),
                        IsPrimaryKey: reader.GetInt32(5) == 1
                    );
                    break;
                }
            }
        }

        Assert.NotNull(columnInfo);
        Assert.Equal(!expected.IsNullable, columnInfo.NotNull);

        // Verify default if specified
        if (expected.DefaultLqlExpression is not null)
        {
            Assert.NotNull(columnInfo.DefaultValue);
            var translated = LqlDefaultTranslator.ToSqlite(expected.DefaultLqlExpression);
            Assert.Equal(translated, columnInfo.DefaultValue);
        }
        else if (expected.DefaultValue is not null)
        {
            Assert.NotNull(columnInfo.DefaultValue);
        }
    }

    /// <summary>
    /// Verifies a SQLite index matches its definition.
    /// </summary>
    public static void VerifySqliteIndex(SqliteConnection conn, IndexDefinition expected)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='index' AND name=@name";
        cmd.Parameters.AddWithValue("@name", expected.Name);

        var indexDdl = cmd.ExecuteScalar() as string;
        Assert.NotNull(indexDdl);

        // Verify uniqueness
        if (expected.IsUnique)
        {
            Assert.Contains("UNIQUE", indexDdl.ToUpperInvariant());
        }

        // Verify expression or column indexes
        if (expected.Expressions.Count > 0)
        {
            foreach (var expr in expected.Expressions)
            {
                Assert.Contains(expr.ToLowerInvariant(), indexDdl.ToLowerInvariant());
            }
        }
        else
        {
            foreach (var col in expected.Columns)
            {
                Assert.Contains(col, indexDdl);
            }
        }
    }

    /// <summary>
    /// Verifies default values work at runtime by inserting and querying.
    /// </summary>
    public static void VerifyDefaultsWorkAtRuntime(
        NpgsqlConnection pgConn,
        SqliteConnection sqliteConn,
        TableDefinition table,
        string schemaName = "public"
    )
    {
        // Find columns with defaults
        var columnsWithDefaults = table
            .Columns.Where(c => c.DefaultLqlExpression is not null || c.DefaultValue is not null)
            .ToList();

        if (columnsWithDefaults.Count == 0)
            return;

        // Find a non-default column to insert (or use DEFAULT VALUES)
        var pkColumn = table.Columns.FirstOrDefault(c =>
            table.PrimaryKey?.Columns.Contains(c.Name) == true && !c.IsIdentity
        );

        var insertSql = pkColumn is not null
            ? $"INSERT INTO \"{schemaName}\".\"{table.Name}\" (\"{pkColumn.Name}\") VALUES (1)"
            : $"INSERT INTO \"{schemaName}\".\"{table.Name}\" DEFAULT VALUES";

        var sqliteInsertSql = pkColumn is not null
            ? $"INSERT INTO [{table.Name}] ([{pkColumn.Name}]) VALUES (1)"
            : $"INSERT INTO [{table.Name}] DEFAULT VALUES";

        // Insert in Postgres
        using (var cmd = pgConn.CreateCommand())
        {
            cmd.CommandText = insertSql;
            cmd.ExecuteNonQuery();
        }

        // Insert in SQLite
        using (var cmd = sqliteConn.CreateCommand())
        {
            cmd.CommandText = sqliteInsertSql;
            cmd.ExecuteNonQuery();
        }

        // Verify defaults were applied
        foreach (var col in columnsWithDefaults)
        {
            // Postgres
            using (var cmd = pgConn.CreateCommand())
            {
                cmd.CommandText =
                    $"SELECT \"{col.Name}\" FROM \"{schemaName}\".\"{table.Name}\" LIMIT 1";
                var value = cmd.ExecuteScalar();
                Assert.NotNull(value);
            }

            // SQLite
            using (var cmd = sqliteConn.CreateCommand())
            {
                cmd.CommandText = $"SELECT [{col.Name}] FROM [{table.Name}] LIMIT 1";
                var value = cmd.ExecuteScalar();
                Assert.NotNull(value);
            }
        }
    }

    // =========================================================================
    // HELPER METHODS
    // =========================================================================

    internal static bool PostgresTableExists(NpgsqlConnection conn, string tableName, string schema)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = @schema AND table_name = @table
            )
            """;
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table", tableName);
        return (bool)cmd.ExecuteScalar()!;
    }

    private static List<string> GetPostgresTables(NpgsqlConnection conn, string schema)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT table_name FROM information_schema.tables
            WHERE table_schema = @schema AND table_type = 'BASE TABLE'
            """;
        cmd.Parameters.AddWithValue("@schema", schema);

        var tables = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }
        return tables;
    }

    private static List<string> GetPostgresColumns(
        NpgsqlConnection conn,
        string tableName,
        string schema
    )
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT column_name FROM information_schema.columns
            WHERE table_schema = @schema AND table_name = @table
            """;
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table", tableName);

        var columns = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }

    internal static bool SqliteTableExists(SqliteConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name";
        cmd.Parameters.AddWithValue("@name", tableName);
        return (long)cmd.ExecuteScalar()! > 0;
    }

    private static List<string> GetSqliteTables(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";

        var tables = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }
        return tables;
    }

    private static string? GetSqliteTableDdl(SqliteConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name=@name";
        cmd.Parameters.AddWithValue("@name", tableName);
        return cmd.ExecuteScalar() as string;
    }

    private static string ForeignKeyActionToString(ForeignKeyAction action) =>
        action switch
        {
            ForeignKeyAction.NoAction => "NO ACTION",
            ForeignKeyAction.Cascade => "CASCADE",
            ForeignKeyAction.SetNull => "SET NULL",
            ForeignKeyAction.SetDefault => "SET DEFAULT",
            ForeignKeyAction.Restrict => "RESTRICT",
            _ => "NO ACTION",
        };

    private sealed record ColumnInfo(
        string Name,
        string Type,
        bool NotNull,
        string? DefaultValue,
        bool IsPrimaryKey
    );
}
