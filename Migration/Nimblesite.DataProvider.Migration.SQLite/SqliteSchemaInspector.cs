namespace Nimblesite.DataProvider.Migration.SQLite;

/// <summary>
/// Inspects SQLite database schema and returns a SchemaDefinition.
/// </summary>
public static class SqliteSchemaInspector
{
    /// <summary>
    /// Inspect all tables in a SQLite database.
    /// </summary>
    /// <param name="connection">Open SQLite connection</param>
    /// <param name="logger">Optional logger</param>
    /// <returns>Schema definition of the database</returns>
    public static SchemaResult Inspect(SqliteConnection connection, ILogger? logger = null)
    {
        try
        {
            var tables = new List<TableDefinition>();

            // Get all user tables
            using var tablesCmd = connection.CreateCommand();
            tablesCmd.CommandText = """
                SELECT name FROM sqlite_master 
                WHERE type = 'table' 
                AND name NOT LIKE 'sqlite_%'
                AND name <> '__rls_context'
                ORDER BY name
                """;

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
                var tableResult = InspectTable(connection, tableName, logger);
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
                new SchemaDefinition { Name = "sqlite", Tables = tables.AsReadOnly() }
            );
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to inspect SQLite schema");
            return new SchemaResult.Error<SchemaDefinition, MigrationError>(
                MigrationError.FromException(ex)
            );
        }
    }

    /// <summary>
    /// Inspect a single table.
    /// </summary>
    public static TableResult InspectTable(
        SqliteConnection connection,
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

            // Get column info using PRAGMA table_info
            using var colCmd = connection.CreateCommand();
            colCmd.CommandText = $"PRAGMA table_info([{tableName}])";

            var pkColumns = new List<string>();

            using (var reader = colCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var name = reader.GetString(1);
                    var sqlType = reader.GetString(2);
                    var notNull = reader.GetInt32(3) == 1;
                    var defaultValue = reader.IsDBNull(4) ? null : reader.GetString(4);
                    var pk = reader.GetInt32(5);

                    if (pk > 0)
                    {
                        pkColumns.Add(name);
                    }

                    columns.Add(
                        new ColumnDefinition
                        {
                            Name = name,
                            Type = SqliteTypeToPortable(sqlType),
                            IsNullable = !notNull,
                            DefaultValue = defaultValue,
                            IsIdentity =
                                pk == 1
                                && string.Equals(
                                    sqlType,
                                    "INTEGER",
                                    StringComparison.OrdinalIgnoreCase
                                ),
                        }
                    );
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

            // Get indexes using PRAGMA index_list
            using var idxCmd = connection.CreateCommand();
            idxCmd.CommandText = $"PRAGMA index_list([{tableName}])";

            var indexNames = new List<(string Name, bool IsUnique)>();
            using (var reader = idxCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var indexName = reader.GetString(1);
                    var isUnique = reader.GetInt32(2) == 1;
                    var origin = reader.GetString(3);

                    // Skip auto-created indexes (c=constraint, pk=primary key, u=unique)
                    // Only include manually-created indexes (origin = "c" means "CREATE INDEX")
                    // Origin values: c=created, pk=primary key, u=unique constraint
                    if (origin.Equals("c", StringComparison.OrdinalIgnoreCase))
                    {
                        indexNames.Add((indexName, isUnique));
                    }
                }
            }

            foreach (var (indexName, isUnique) in indexNames)
            {
                // First check if this is an expression index by looking at index_info
                // Expression columns return NULL for the column name
                using var idxColCmd = connection.CreateCommand();
                idxColCmd.CommandText = $"PRAGMA index_info([{indexName}])";

                var indexColumns = new List<string>();
                var hasExpressions = false;
                using (var reader = idxColCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader.IsDBNull(2))
                        {
                            // NULL column name means expression index
                            hasExpressions = true;
                            break;
                        }
                        indexColumns.Add(reader.GetString(2));
                    }
                }

                if (hasExpressions)
                {
                    // Expression index - get the full definition from sqlite_master
                    using var sqlCmd = connection.CreateCommand();
                    sqlCmd.CommandText = """
                        SELECT sql FROM sqlite_master
                        WHERE type = 'index' AND name = @name
                        """;
                    sqlCmd.Parameters.AddWithValue("@name", indexName);
                    var sql = sqlCmd.ExecuteScalar() as string;

                    if (sql is not null)
                    {
                        var expressions = ParseIndexExpressions(sql);
                        indexes.Add(
                            new IndexDefinition
                            {
                                Name = indexName,
                                Expressions = expressions.AsReadOnly(),
                                IsUnique = isUnique,
                            }
                        );
                    }
                }
                else if (indexColumns.Count > 0)
                {
                    indexes.Add(
                        new IndexDefinition
                        {
                            Name = indexName,
                            Columns = indexColumns.AsReadOnly(),
                            IsUnique = isUnique,
                        }
                    );
                }
            }

            // Get foreign keys using PRAGMA foreign_key_list
            using var fkCmd = connection.CreateCommand();
            fkCmd.CommandText = $"PRAGMA foreign_key_list([{tableName}])";

            using (var reader = fkCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var refTable = reader.GetString(2);
                    var fromCol = reader.GetString(3);
                    var toCol = reader.GetString(4);
                    var onUpdate = reader.GetString(5);
                    var onDelete = reader.GetString(6);

                    foreignKeys.Add(
                        new ForeignKeyDefinition
                        {
                            Name = $"FK_{tableName}_{fromCol}",
                            Columns = [fromCol],
                            ReferencedTable = refTable,
                            ReferencedColumns = [toCol],
                            OnDelete = ParseForeignKeyAction(onDelete),
                            OnUpdate = ParseForeignKeyAction(onUpdate),
                        }
                    );
                }
            }

            return new TableResult.Ok<TableDefinition, MigrationError>(
                new TableDefinition
                {
                    Schema = "main",
                    Name = tableName,
                    Columns = columns.AsReadOnly(),
                    Indexes = indexes.AsReadOnly(),
                    ForeignKeys = foreignKeys.AsReadOnly(),
                    PrimaryKey = primaryKey,
                    RowLevelSecurity = SqliteRlsSchemaInspector.Inspect(connection, tableName),
                }
            );
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to inspect table {Table}", tableName);
            return new TableResult.Error<TableDefinition, MigrationError>(
                MigrationError.FromException(ex)
            );
        }
    }

    /// <summary>
    /// Convert SQLite type to portable type.
    /// </summary>
    public static PortableType SqliteTypeToPortable(string sqliteType)
    {
        var upper = sqliteType.ToUpperInvariant();

        return upper switch
        {
            "INTEGER" => new BigIntType(),
            "REAL" => new DoubleType(),
            "TEXT" => new TextType(),
            "BLOB" => new BlobType(),
            _ when upper.Contains("INT", StringComparison.Ordinal) => new BigIntType(),
            _ when upper.Contains("CHAR", StringComparison.Ordinal) => new TextType(),
            _ when upper.Contains("TEXT", StringComparison.Ordinal) => new TextType(),
            _ when upper.Contains("BLOB", StringComparison.Ordinal) => new BlobType(),
            _ when upper.Contains("REAL", StringComparison.Ordinal) => new DoubleType(),
            _ when upper.Contains("FLOA", StringComparison.Ordinal) => new DoubleType(),
            _ when upper.Contains("DOUB", StringComparison.Ordinal) => new DoubleType(),
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
    /// Parse expressions from a SQLite index definition string.
    /// Example: "CREATE UNIQUE INDEX uq_name ON table (lower(name), suburb_id)"
    /// Returns: ["lower(name)", "suburb_id"]
    /// </summary>
    private static List<string> ParseIndexExpressions(string indexDef)
    {
        var expressions = new List<string>();

        // Find "ON tablename" then the first ( after it - that's the index columns/expressions
        // We can't use LastIndexOf because expressions like lower(name) contain nested parens
        var onIndex = indexDef.IndexOf(" ON ", StringComparison.OrdinalIgnoreCase);
        if (onIndex < 0)
        {
            return expressions;
        }

        // Find the first ( after ON - this is the start of the index columns
        var parenStart = indexDef.IndexOf('(', onIndex);
        if (parenStart < 0)
        {
            return expressions;
        }

        // Find matching closing paren by counting depth
        var depth = 0;
        var parenEnd = -1;
        for (var i = parenStart; i < indexDef.Length; i++)
        {
            switch (indexDef[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    if (depth == 0)
                    {
                        parenEnd = i;
                        goto found;
                    }
                    break;
            }
        }
        found:

        if (parenEnd < 0)
        {
            return expressions;
        }

        var content = indexDef.Substring(parenStart + 1, parenEnd - parenStart - 1);

        // Split by comma, but respect nested parentheses (for function calls)
        var current = new StringBuilder();
        depth = 0;

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
}
