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
            var tableNames = new List<string>();
            PostgresCatalogQuery.Read(
                connection: connection,
                configure: tablesCmd =>
                {
                    tablesCmd.CommandText = global::Nimblesite
                        .DataProvider
                        .Migration
                        .Postgres
                        .PostgresPolicyCatalogNormalizer
                        .SqlSpecification2;
                    tablesCmd.Parameters.AddWithValue("@schema", schemaName);
                },
                read: reader =>
                {
                    tableNames.Add(reader.GetString(0));
                }
            );

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
            var uniqueConstraints = new List<UniqueConstraintDefinition>();
            var checkConstraints = new List<CheckConstraintDefinition>();
            PrimaryKeyDefinition? primaryKey = null;

            // Get column info. Implements [MIG-TYPES-VECTOR] §5.4.4: LEFT JOIN
            // pg_attribute so pgvector columns (data_type = USER-DEFINED,
            // udt_name = vector) can expose their atttypmod to the reverse
            // mapping. pgvector stores the dimension raw in atttypmod, no
            // VARHDRSZ offset. Non-vector rows get atttypmod = NULL here and
            // the switch ignores it.

            PostgresCatalogQuery.Read(
                connection: connection,
                configure: colCmd =>
                    PostgresCatalogQuery.Configure(
                        command: colCmd,
                        sql: CatalogQuery_InspectTable_1,
                        schema: schemaName,
                        table: tableName
                    ),
                read: reader =>
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
            );

            // Get primary key
            var pkColumns = new List<string>();
            PostgresCatalogQuery.Read(
                connection: connection,
                configure: pkCmd =>
                    PostgresCatalogQuery.Configure(
                        command: pkCmd,
                        sql: CatalogQuery_InspectTable_2,
                        schema: schemaName,
                        table: tableName
                    ),
                read: reader =>
                {
                    pkColumns.Add(reader.GetString(0));
                }
            );

            if (pkColumns.Count > 0)
            {
                primaryKey = new PrimaryKeyDefinition
                {
                    Name = $"PK_{tableName}",
                    Columns = pkColumns.AsReadOnly(),
                };
            }

            InspectUniqueConstraints(connection, schemaName, tableName, uniqueConstraints);

            // Get indexes (both column-based and expression indexes)

            PostgresCatalogQuery.Read(
                connection: connection,
                configure: idxCmd =>
                    PostgresCatalogQuery.Configure(
                        command: idxCmd,
                        sql: CatalogQuery_InspectTable_3,
                        schema: schemaName,
                        table: tableName
                    ),
                read: reader =>
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
            );

            // [MIG-EXISTING-DATABASE-UPGRADE] Keep each relationship intact,
            // including composite keys and references to another schema.

            PostgresCatalogQuery.Read(
                connection: connection,
                configure: fkCmd =>
                    PostgresCatalogQuery.Configure(
                        command: fkCmd,
                        sql: CatalogQuery_InspectTable_4,
                        schema: schemaName,
                        table: tableName
                    ),
                read: reader =>
                {
                    var constraintName = reader.GetString(0);
                    var columnNames = reader.GetFieldValue<string[]>(1);
                    var refTable = reader.GetString(2);
                    var refSchema = reader.GetString(3);
                    var refColumns = reader.GetFieldValue<string[]>(4);
                    var deleteRule = reader.GetString(5);
                    var updateRule = reader.GetString(6);

                    foreignKeys.Add(
                        new ForeignKeyDefinition
                        {
                            Name = constraintName,
                            Columns = columnNames,
                            ReferencedTable = refTable,
                            ReferencedSchema = refSchema,
                            ReferencedColumns = refColumns,
                            OnDelete = ParseForeignKeyAction(deleteRule),
                            OnUpdate = ParseForeignKeyAction(updateRule),
                        }
                    );
                }
            );

            InspectCheckConstraints(connection, schemaName, tableName, columns, checkConstraints);

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
                    UniqueConstraints = uniqueConstraints.AsReadOnly(),
                    CheckConstraints = checkConstraints.AsReadOnly(),
                    RowLevelSecurity = rls,
                    // [MIG-TRIGGER-PG] read declarative trigger guards by name.
                    Triggers = PostgresTriggerSchemaInspector.Inspect(
                        connection,
                        schemaName,
                        tableName
                    ),
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

    private static void InspectUniqueConstraints(
        NpgsqlConnection connection,
        string schemaName,
        string tableName,
        List<UniqueConstraintDefinition> uniqueConstraints
    )
    {
        // Implements [MIG-PG-UNIQUE-CONSTRAINT-INSPECTION].

        PostgresCatalogQuery.Read(
            connection: connection,
            configure: uniqueCmd =>
                PostgresCatalogQuery.Configure(
                    command: uniqueCmd,
                    sql: CatalogQuery_InspectUniqueConstraints_5,
                    schema: schemaName,
                    table: tableName
                ),
            read: reader =>
            {
                uniqueConstraints.Add(
                    new UniqueConstraintDefinition
                    {
                        Name = reader.GetString(0),
                        Columns = ((string[])reader.GetValue(1)).ToList().AsReadOnly(),
                    }
                );
            }
        );
    }

    private static void InspectCheckConstraints(
        NpgsqlConnection connection,
        string schemaName,
        string tableName,
        List<ColumnDefinition> columns,
        List<CheckConstraintDefinition> tableChecks
    )
    {
        // Implements [MIG-PG-NAMED-COLUMN-CHECK-CONSTRAINT].

        PostgresCatalogQuery.Read(
            connection: connection,
            configure: checkCmd =>
                PostgresCatalogQuery.Configure(
                    command: checkCmd,
                    sql: CatalogQuery_InspectCheckConstraints_6,
                    schema: schemaName,
                    table: tableName
                ),
            read: reader =>
            {
                var name = reader.GetString(0);
                var expression = PostgresCheckExpressionNormalizer.Normalize(reader.GetString(1));
                var columnCount = reader.GetInt32(2);

                if (columnCount == 1 && !reader.IsDBNull(3))
                {
                    ApplyColumnCheckConstraint(columns, reader.GetString(3), name, expression);
                }
                else
                {
                    tableChecks.Add(
                        new CheckConstraintDefinition { Name = name, Expression = expression }
                    );
                }
            }
        );
    }

    private static void ApplyColumnCheckConstraint(
        List<ColumnDefinition> columns,
        string columnName,
        string constraintName,
        string expression
    )
    {
        for (var i = 0; i < columns.Count; i++)
        {
            if (!string.Equals(columns[i].Name, columnName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            columns[i] = columns[i] with
            {
                CheckConstraint = expression,
                CheckConstraintName = constraintName,
            };
            return;
        }
    }

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
            enabledCmd.CommandText = CatalogQuery_InspectRls_7;
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
            polCmd.CommandText = CatalogQuery_InspectRls_8;
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
