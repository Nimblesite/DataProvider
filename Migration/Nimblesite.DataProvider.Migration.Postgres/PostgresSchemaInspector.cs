namespace Nimblesite.DataProvider.Migration.Postgres;

/// <summary>
/// Inspects PostgreSQL database schema and returns a SchemaDefinition.
/// </summary>
public static partial class PostgresSchemaInspector
{
    /// <summary>
    /// Inspect all tables in a PostgreSQL database.
    /// </summary>
    /// <param name="connection">Open PostgreSQL connection</param>
    /// <param name="schemaName">Schema to inspect (default: public)</param>
    /// <param name="logger">Optional logger</param>
    /// <returns>Schema definition of the database</returns>
    public static SchemaResult Inspect(
        NpgsqlConnection connection,
        string schemaName = "public",
        ILogger? logger = null
    )
    {
        try
        {
            var tables = new List<TableDefinition>();

            // Get all user tables
            using var tablesCmd = connection.CreateCommand();
            tablesCmd.CommandText = """
                SELECT table_name 
                FROM information_schema.tables 
                WHERE table_schema = @schema 
                AND table_type = 'BASE TABLE'
                ORDER BY table_name
                """;
            tablesCmd.Parameters.AddWithValue("@schema", schemaName);

            var tableNames = new List<string>();
            using (var reader = tablesCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    tableNames.Add(reader.GetString(0));
                }
            }

            foreach (var tableName in tableNames)
            {
                var tableResult = InspectTable(connection, schemaName, tableName, logger);
                if (tableResult is TableResult.Ok<TableDefinition, MigrationError> ok)
                {
                    tables.Add(ok.Value);
                }
                else if (
                    tableResult is TableResult.Error<TableDefinition, MigrationError> tableError
                )
                {
                    logger?.LogWarning(
                        "Failed to inspect table {Table}: {Error}",
                        tableName,
                        tableError.Value
                    );
                }
            }

            return new SchemaResult.Ok<SchemaDefinition, MigrationError>(
                new SchemaDefinition
                {
                    Name = schemaName,
                    Tables = tables.AsReadOnly(),
                    Roles = PostgresSupportSchemaInspector.InspectRoles(connection),
                    Functions = PostgresSupportSchemaInspector.InspectFunctions(
                        connection,
                        schemaName
                    ),
                    Grants = PostgresSupportSchemaInspector.InspectGrants(connection, schemaName),
                }
            );
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to inspect PostgreSQL schema");
            return new SchemaResult.Error<SchemaDefinition, MigrationError>(
                MigrationError.FromException(ex)
            );
        }
    }

    /// <summary>
    /// Inspect a single table.
    /// </summary>
    public static TableResult InspectTable(
        NpgsqlConnection connection,
        string schemaName,
        string tableName,
        ILogger? logger = null
    )
    {
        try
        {
            var columns = new List<ColumnDefinition>();
            var indexes = new List<IndexDefinition>();
            var foreignKeys = new List<ForeignKeyDefinition>();
            PrimaryKeyDefinition? primaryKey = null;

            // Get column info. Implements [MIG-TYPES-VECTOR] §5.4.4: LEFT JOIN
            // pg_attribute so pgvector columns (data_type = USER-DEFINED,
            // udt_name = vector) can expose their atttypmod to the reverse
            // mapping. pgvector stores the dimension raw in atttypmod, no
            // VARHDRSZ offset. Non-vector rows get atttypmod = NULL here and
            // the switch ignores it.
            using var colCmd = connection.CreateCommand();
            colCmd.CommandText = """
                SELECT
                    c.column_name,
                    c.data_type,
                    c.is_nullable,
                    c.column_default,
                    c.character_maximum_length,
                    c.numeric_precision,
                    c.numeric_scale,
                    c.udt_name,
                    a.atttypmod
                FROM information_schema.columns c
                LEFT JOIN pg_catalog.pg_namespace n
                    ON n.nspname = c.table_schema
                LEFT JOIN pg_catalog.pg_class cls
                    ON cls.relname = c.table_name AND cls.relnamespace = n.oid
                LEFT JOIN pg_catalog.pg_attribute a
                    ON a.attrelid = cls.oid AND a.attname = c.column_name AND a.attnum > 0
                WHERE c.table_schema = @schema AND c.table_name = @table
                ORDER BY c.ordinal_position
                """;
            colCmd.Parameters.AddWithValue("@schema", schemaName);
            colCmd.Parameters.AddWithValue("@table", tableName);

            using (var reader = colCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var name = reader.GetString(0);
                    var dataType = reader.GetString(1);
                    var isNullable = reader.GetString(2) == "YES";
                    var defaultValue = reader.IsDBNull(3) ? null : reader.GetString(3);
                    var charMaxLen = reader.IsDBNull(4) ? null : (int?)reader.GetInt32(4);
                    var numPrecision = reader.IsDBNull(5) ? null : (int?)reader.GetInt32(5);
                    var numScale = reader.IsDBNull(6) ? null : (int?)reader.GetInt32(6);
                    var udtName = reader.IsDBNull(7) ? null : reader.GetString(7);
                    var attTypMod = reader.IsDBNull(8) ? null : (int?)reader.GetInt32(8);

                    var isIdentity =
                        defaultValue?.Contains("nextval", StringComparison.OrdinalIgnoreCase)
                        ?? false;

                    columns.Add(
                        new ColumnDefinition
                        {
                            Name = name,
                            Type = PostgresTypeToPortable(
                                dataType,
                                charMaxLen,
                                numPrecision,
                                numScale,
                                udtName,
                                attTypMod
                            ),
                            IsNullable = isNullable,
                            DefaultValue = isIdentity ? null : defaultValue,
                            IsIdentity = isIdentity,
                        }
                    );
                }
            }

            // Get primary key
            using var pkCmd = connection.CreateCommand();
            pkCmd.CommandText = """
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
            pkCmd.Parameters.AddWithValue("@schema", schemaName);
            pkCmd.Parameters.AddWithValue("@table", tableName);

            var pkColumns = new List<string>();
            using (var reader = pkCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    pkColumns.Add(reader.GetString(0));
                }
            }

            if (pkColumns.Count > 0)
            {
                primaryKey = new PrimaryKeyDefinition
                {
                    Name = $"PK_{tableName}",
                    Columns = pkColumns.AsReadOnly(),
                };
            }

            // Get indexes (both column-based and expression indexes)
            using var idxCmd = connection.CreateCommand();
            idxCmd.CommandText = """
                SELECT
                    i.relname AS index_name,
                    ix.indisunique AS is_unique,
                    pg_get_indexdef(ix.indexrelid) AS index_def,
                    (SELECT array_agg(a.attname ORDER BY ord.n)
                     FROM unnest(ix.indkey) WITH ORDINALITY AS ord(attnum, n)
                     LEFT JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ord.attnum
                     WHERE ord.attnum > 0) AS column_names,
                    (SELECT bool_or(ord.attnum = 0)
                     FROM unnest(ix.indkey) WITH ORDINALITY AS ord(attnum, n)) AS has_expressions
                FROM pg_class t
                JOIN pg_index ix ON t.oid = ix.indrelid
                JOIN pg_class i ON i.oid = ix.indexrelid
                JOIN pg_namespace n ON n.oid = t.relnamespace
                WHERE n.nspname = @schema
                AND t.relname = @table
                AND NOT ix.indisprimary
                ORDER BY i.relname
                """;
            idxCmd.Parameters.AddWithValue("@schema", schemaName);
            idxCmd.Parameters.AddWithValue("@table", tableName);

            using (var reader = idxCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var indexName = reader.GetString(0);
                    var isUnique = reader.GetBoolean(1);
                    var indexDef = reader.GetString(2);
                    var columnNames = reader.IsDBNull(3) ? [] : (string[])reader.GetValue(3);
                    var hasExpressions = reader.IsDBNull(4) ? false : reader.GetBoolean(4);

                    if (hasExpressions)
                    {
                        // Expression index - parse expressions from the index definition
                        // Format: CREATE [UNIQUE] INDEX name ON table (expr1, expr2, ...)
                        var expressions = ParseIndexExpressions(indexDef);
                        indexes.Add(
                            new IndexDefinition
                            {
                                Name = indexName,
                                Expressions = expressions.AsReadOnly(),
                                IsUnique = isUnique,
                            }
                        );
                    }
                    else
                    {
                        // Simple column index
                        indexes.Add(
                            new IndexDefinition
                            {
                                Name = indexName,
                                Columns = columnNames.ToList().AsReadOnly(),
                                IsUnique = isUnique,
                            }
                        );
                    }
                }
            }

            // Get foreign keys
            using var fkCmd = connection.CreateCommand();
            fkCmd.CommandText = """
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
                """;
            fkCmd.Parameters.AddWithValue("@schema", schemaName);
            fkCmd.Parameters.AddWithValue("@table", tableName);

            using (var reader = fkCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var constraintName = reader.GetString(0);
                    var columnName = reader.GetString(1);
                    var refTable = reader.GetString(2);
                    var refColumn = reader.GetString(3);
                    var deleteRule = reader.GetString(4);
                    var updateRule = reader.GetString(5);

                    foreignKeys.Add(
                        new ForeignKeyDefinition
                        {
                            Name = constraintName,
                            Columns = [columnName],
                            ReferencedTable = refTable,
                            ReferencedSchema = schemaName,
                            ReferencedColumns = [refColumn],
                            OnDelete = ParseForeignKeyAction(deleteRule),
                            OnUpdate = ParseForeignKeyAction(updateRule),
                        }
                    );
                }
            }

            // [RLS-DIFF] read pg_policies + relrowsecurity into RowLevelSecurity.
            var rls = InspectRls(connection, schemaName, tableName);

            return new TableResult.Ok<TableDefinition, MigrationError>(
                new TableDefinition
                {
                    Schema = schemaName,
                    Name = tableName,
                    Columns = columns.AsReadOnly(),
                    Indexes = indexes.AsReadOnly(),
                    ForeignKeys = foreignKeys.AsReadOnly(),
                    PrimaryKey = primaryKey,
                    RowLevelSecurity = rls,
                }
            );
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to inspect table {Schema}.{Table}", schemaName, tableName);
            return new TableResult.Error<TableDefinition, MigrationError>(
                MigrationError.FromException(ex)
            );
        }
    }

    /// <summary>
    /// Convert PostgreSQL type to portable type. Implements [MIG-TYPES-VECTOR] §5.4.4:
    /// pgvector columns report data_type = "USER-DEFINED" and udt_name = "vector";
    /// the dimension is stored directly in pg_attribute.atttypmod (no VARHDRSZ offset).
    /// </summary>
    public static PortableType PostgresTypeToPortable(
        string pgType,
        int? charMaxLen,
        int? numPrecision,
        int? numScale,
        string? udtName = null,
        int? attTypMod = null
    )
    {
        var upper = pgType.ToUpperInvariant();

        // [MIG-TYPES-VECTOR] §5.4.4: pgvector reverse-map. information_schema reports
        // USER-DEFINED for extension types, so we lean on udt_name + atttypmod.
        if (
            string.Equals(udtName, "vector", StringComparison.OrdinalIgnoreCase)
            && attTypMod.HasValue
            && attTypMod.Value > 0
        )
        {
            return new VectorType(attTypMod.Value);
        }

        return upper switch
        {
            "SMALLINT" => new SmallIntType(),
            "INTEGER" or "INT" or "INT4" => new IntType(),
            "BIGINT" or "INT8" => new BigIntType(),
            "BOOLEAN" or "BOOL" => new BooleanType(),

            "NUMERIC" or "DECIMAL" when numPrecision.HasValue && numScale.HasValue =>
                new DecimalType(numPrecision.Value, numScale.Value),
            "NUMERIC" or "DECIMAL" => new DecimalType(18, 2),
            "REAL" or "FLOAT4" => new FloatType(),
            "DOUBLE PRECISION" or "FLOAT8" => new DoubleType(),

            "CHARACTER VARYING" or "VARCHAR" when charMaxLen.HasValue => new VarCharType(
                charMaxLen.Value
            ),
            "CHARACTER VARYING" or "VARCHAR" => new TextType(),
            "CHARACTER" or "CHAR" when charMaxLen.HasValue => new CharType(charMaxLen.Value),
            "TEXT" => new TextType(),
            "JSONB" or "JSON" => new JsonType(),
            "XML" => new XmlType(),

            "BYTEA" => new BlobType(),

            "DATE" => new DateType(),
            "TIME" or "TIME WITHOUT TIME ZONE" => new TimeType(),
            "TIMESTAMP" or "TIMESTAMP WITHOUT TIME ZONE" => new DateTimeType(),
            "TIMESTAMP WITH TIME ZONE" or "TIMESTAMPTZ" => new DateTimeOffsetType(),

            "UUID" => new UuidType(),

            _ => new TextType(),
        };
    }

    private static ForeignKeyAction ParseForeignKeyAction(string action) =>
        action.ToUpperInvariant() switch
        {
            "CASCADE" => ForeignKeyAction.Cascade,
            "SET NULL" => ForeignKeyAction.SetNull,
            "SET DEFAULT" => ForeignKeyAction.SetDefault,
            "RESTRICT" => ForeignKeyAction.Restrict,
            _ => ForeignKeyAction.NoAction,
        };

    /// <summary>
    /// Parse expressions from a PostgreSQL index definition string.
    /// Example: "CREATE UNIQUE INDEX uq_name ON public.table USING btree (lower(name), suburb_id)"
    /// Returns: ["lower(name)", "suburb_id"]
    /// </summary>
    private static List<string> ParseIndexExpressions(string indexDef)
    {
        var expressions = new List<string>();

        // Find the opening parenthesis after USING btree (or just after table name)
        var parenStart = indexDef.LastIndexOf('(');
        var parenEnd = indexDef.LastIndexOf(')');

        if (parenStart < 0 || parenEnd < 0 || parenEnd <= parenStart)
        {
            return expressions;
        }

        var content = indexDef.Substring(parenStart + 1, parenEnd - parenStart - 1);

        // Split by comma, but respect nested parentheses (for function calls)
        var current = new StringBuilder();
        var depth = 0;

        foreach (var ch in content)
        {
            switch (ch)
            {
                case '(':
                    depth++;
                    current.Append(ch);
                    break;
                case ')':
                    depth--;
                    current.Append(ch);
                    break;
                case ',' when depth == 0:
                    var expr = current.ToString().Trim();
                    if (!string.IsNullOrEmpty(expr))
                    {
                        expressions.Add(expr);
                    }
                    current.Clear();
                    break;
                default:
                    current.Append(ch);
                    break;
            }
        }

        // Add the last expression
        var lastExpr = current.ToString().Trim();
        if (!string.IsNullOrEmpty(lastExpr))
        {
            expressions.Add(lastExpr);
        }

        return expressions;
    }

    /// <summary>
    /// Read RLS state for a table from pg_class.relrowsecurity + pg_policies.
    /// Returns null when RLS is disabled and no policies exist.
    /// Implements [RLS-DIFF].
    /// </summary>
    private static RlsPolicySetDefinition? InspectRls(
        NpgsqlConnection connection,
        string schemaName,
        string tableName
    )
    {
        var enabled = false;
        var forced = false;
        using (var enabledCmd = connection.CreateCommand())
        {
            enabledCmd.CommandText = """
                SELECT c.relrowsecurity, c.relforcerowsecurity
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = @schema AND c.relname = @table
                """;
            enabledCmd.Parameters.AddWithValue("@schema", schemaName);
            enabledCmd.Parameters.AddWithValue("@table", tableName);
            using var reader = enabledCmd.ExecuteReader();
            if (reader.Read())
            {
                enabled = !reader.IsDBNull(0) && reader.GetBoolean(0);
                forced = !reader.IsDBNull(1) && reader.GetBoolean(1);
            }
        }

        var policies = new List<RlsPolicyDefinition>();
        using (var polCmd = connection.CreateCommand())
        {
            polCmd.CommandText = """
                SELECT policyname, permissive, cmd, roles, qual, with_check
                FROM pg_policies
                WHERE schemaname = @schema AND tablename = @table
                ORDER BY policyname
                """;
            polCmd.Parameters.AddWithValue("@schema", schemaName);
            polCmd.Parameters.AddWithValue("@table", tableName);
            using var reader = polCmd.ExecuteReader();
            while (reader.Read())
            {
                var policyName = reader.GetString(0);
                var permissive = reader.GetString(1) == "PERMISSIVE";
                var cmd = reader.GetString(2);
                var rolesArr = reader.GetValue(3) as string[] ?? [];
                var qual = reader.IsDBNull(4) ? null : reader.GetString(4);
                var withCheck = reader.IsDBNull(5) ? null : reader.GetString(5);

                policies.Add(
                    new RlsPolicyDefinition
                    {
                        Name = policyName,
                        IsPermissive = permissive,
                        Operations = [PgCmdToRlsOperation(cmd)],
                        Roles = rolesArr.Where(r => r != "public").ToArray(),
                        // pg_policies returns the parsed qual/with_check as
                        // SQL text — round-trip them as raw-SQL escape hatch
                        // (issue #36), not LQL. We do not attempt SQL→LQL
                        // round-tripping; predicates that originated as LQL
                        // come back as their raw-SQL form on diff.
                        UsingSql = qual,
                        WithCheckSql = withCheck,
                    }
                );
            }
        }

        if (!enabled && policies.Count == 0 && !forced)
        {
            return null;
        }
        return new RlsPolicySetDefinition
        {
            Enabled = enabled,
            Forced = forced,
            Policies = policies.AsReadOnly(),
        };
    }

    private static RlsOperation PgCmdToRlsOperation(string cmd) =>
        cmd.ToUpperInvariant() switch
        {
            "ALL" => RlsOperation.All,
            "SELECT" => RlsOperation.Select,
            "INSERT" => RlsOperation.Insert,
            "UPDATE" => RlsOperation.Update,
            "DELETE" => RlsOperation.Delete,
            _ => RlsOperation.All,
        };
}
