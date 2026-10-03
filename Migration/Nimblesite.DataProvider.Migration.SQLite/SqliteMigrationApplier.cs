namespace Nimblesite.DataProvider.Migration.SQLite;

/// <summary>
/// Applies migration operations to SQLite. Constraint changes SQLite cannot ALTER
/// (nullability, foreign keys, checks, unique constraints, dropped columns) are
/// replaced by one <see cref="RebuildTableOperation"/> per table, placed before any
/// other operation on that table. Implements [MIG-SQLITE-REBUILD].
/// </summary>
public static class SqliteMigrationApplier
{
    /// <summary>
    /// Plan table rebuilds, then apply the operations. Foreign keys are turned off
    /// while rebuilding and foreign keys are checked before committing.
    /// </summary>
    public static MigrationApplyResult Apply(
        SqliteConnection connection,
        SchemaDefinition desired,
        IReadOnlyList<SchemaOperation> operations,
        MigrationOptions options,
        ILogger? logger = null
    )
    {
        try
        {
            var planned = Plan(connection, desired, operations);
            var rebuilt = planned
                .OfType<RebuildTableOperation>()
                .Select(r => r.Table.Name)
                .ToList();
            logger?.LogInformation("SQLite table rebuilds planned: {Count}", rebuilt.Count);
            return rebuilt.Count == 0
                ? MigrationRunner.Apply(
                    connection,
                    planned,
                    SqliteDdlGenerator.Generate,
                    options,
                    logger
                )
                : ApplyWithoutForeignKeys(connection, planned, options, logger);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "SQLite migration apply failed");
            return new MigrationApplyResult.Error<bool, MigrationError>(
                MigrationError.FromException(ex)
            );
        }
    }

    private static MigrationApplyResult ApplyWithoutForeignKeys(
        SqliteConnection connection,
        IReadOnlyList<SchemaOperation> planned,
        MigrationOptions options,
        ILogger? logger
    )
    {
        var enabled = Scalar(connection, "PRAGMA foreign_keys") is 1L;
        Execute(connection, "PRAGMA foreign_keys = OFF");
        try
        {
            return MigrationRunner.Apply(
                connection,
                planned,
                SqliteDdlGenerator.Generate,
                options,
                logger,
                verifyBeforeCommit: () => ForeignKeysValid(connection, logger)
            );
        }
        finally
        {
            Execute(connection, enabled ? "PRAGMA foreign_keys = ON" : "PRAGMA foreign_keys = OFF");
        }
    }

    // Implements [MIG-SQLITE-REBUILD]: also check referencing tables that were
    // not rebuilt, while the migration transaction can still roll back.
    private static bool ForeignKeysValid(SqliteConnection connection, ILogger? logger)
    {
        if (Scalar(connection, "PRAGMA foreign_key_check") is null)
        {
            return true;
        }
        logger?.LogError("SQLite foreign key validation failed before commit");
        return false;
    }

    internal static IReadOnlyList<SchemaOperation> Plan(
        SqliteConnection connection,
        SchemaDefinition desired,
        IReadOnlyList<SchemaOperation> operations
    )
    {
        var rebuilds = operations
            .Where(NeedsRebuild)
            .Select(TableOf)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(table => Rebuild(connection, desired, table, operations))
            .OfType<RebuildTableOperation>()
            .ToDictionary(r => r.Table.Name, StringComparer.OrdinalIgnoreCase);
        var planned = new List<SchemaOperation>();
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var op in operations)
        {
            if (TableOf(op) is not { } table || !rebuilds.TryGetValue(table, out var rebuild))
            {
                planned.Add(op);
                continue;
            }
            if (emitted.Add(table))
            {
                planned.Add(rebuild);
            }
            if (!IsAbsorbed(op))
            {
                planned.Add(op);
            }
        }
        return planned;
    }

    private static RebuildTableOperation? Rebuild(
        SqliteConnection connection,
        SchemaDefinition desired,
        string table,
        IReadOnlyList<SchemaOperation> operations
    )
    {
        var target = desired.Tables.FirstOrDefault(t => Same(t.Name, table));
        if (
            target is null
            || SqliteSchemaInspector.InspectTable(connection, table)
                is not TableResult.Ok<TableDefinition, MigrationError> live
        )
        {
            return null;
        }
        var onTable = operations.Where(op => TableOf(op) is { } t && Same(t, table)).ToList();
        var dropped = onTable.OfType<DropColumnOperation>().Select(d => d.ColumnName).ToList();
        var kept = live.Value.Columns.Where(c =>
            !target.Columns.Any(t => Same(t.Name, c.Name)) && !dropped.Any(d => Same(d, c.Name))
        );
        var shape = target with
        {
            Name = live.Value.Name,
            Columns = [.. target.Columns, .. kept],
            ForeignKeys = ForeignKeysAfterChanges(live.Value, onTable),
        };
        return new RebuildTableOperation(
            Table: shape,
            CopyColumns:
            [
                .. live
                    .Value.Columns.Select(c => c.Name)
                    .Where(c => shape.Columns.Any(s => Same(s.Name, c))),
            ],
            DependentObjectSql: DependentObjectSql(connection, live.Value.Name, target),
            Destructive: onTable.Any(op => op is DropColumnOperation or DropForeignKeyOperation)
        );
    }

    // Implements [MIG-SQLITE-REBUILD]: rebuilding must not introduce drops that
    // were excluded by the additive diff or destructive-operation gate.
    private static IReadOnlyList<ForeignKeyDefinition> ForeignKeysAfterChanges(
        TableDefinition live,
        IReadOnlyList<SchemaOperation> operations
    ) =>
        [
            .. live.ForeignKeys.Where(fk =>
                !operations
                    .OfType<DropForeignKeyOperation>()
                    .Any(drop => fk.Name is { } name && Same(name, drop.ConstraintName))
                && !operations
                    .OfType<AddForeignKeyOperation>()
                    .Any(add =>
                        SchemaDiff.ForeignKeyRelationshipsMatch(
                            current: fk,
                            desired: add.ForeignKey
                        )
                    )
            ),
            .. operations.OfType<AddForeignKeyOperation>().Select(add => add.ForeignKey),
        ];

    // Indexes declared on the target are recreated from the definition; every
    // other index and trigger on the table is replayed from sqlite_master.
    private static List<string> DependentObjectSql(
        SqliteConnection connection,
        string table,
        TableDefinition target
    )
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name, sql FROM sqlite_master
            WHERE type IN ('index', 'trigger') AND tbl_name = $table AND sql IS NOT NULL
            ORDER BY type, name
            """;
        command.Parameters.AddWithValue("$table", table);
        using var reader = command.ExecuteReader();
        var sql = new List<string>();
        while (reader.Read())
        {
            if (!target.Indexes.Any(i => Same(i.Name, reader.GetString(0))))
            {
                sql.Add(reader.GetString(1));
            }
        }
        return sql;
    }

    private static bool NeedsRebuild(SchemaOperation op) =>
        op
            is MakeColumnNullableOperation
                or AddForeignKeyOperation
                or AddCheckConstraintOperation
                or AddUniqueConstraintOperation
                or DropColumnOperation
                or DropForeignKeyOperation
                or DropCheckConstraintOperation;

    // The rebuild creates every declared column and index, so these are subsumed.
    private static bool IsAbsorbed(SchemaOperation op) =>
        NeedsRebuild(op) || op is AddColumnOperation or CreateIndexOperation;

    private static string? TableOf(SchemaOperation op) =>
        op switch
        {
            AddColumnOperation o => o.TableName,
            MakeColumnNullableOperation o => o.TableName,
            CreateIndexOperation o => o.TableName,
            AddForeignKeyOperation o => o.TableName,
            AddCheckConstraintOperation o => o.TableName,
            AddUniqueConstraintOperation o => o.TableName,
            DropColumnOperation o => o.TableName,
            DropIndexOperation o => o.TableName,
            DropForeignKeyOperation o => o.TableName,
            DropCheckConstraintOperation o => o.TableName,
            _ => TableOfPolicyOrTrigger(op),
        };

    private static string? TableOfPolicyOrTrigger(SchemaOperation op) =>
        op switch
        {
            EnableRlsOperation o => o.TableName,
            EnableForceRlsOperation o => o.TableName,
            CreateRlsPolicyOperation o => o.TableName,
            AlterRlsPolicyOperation o => o.TableName,
            ReplaceRlsPolicyOperation o => o.TableName,
            DropRlsPolicyOperation o => o.TableName,
            DisableRlsOperation o => o.TableName,
            DisableForceRlsOperation o => o.TableName,
            CreateTriggerOperation o => o.TableName,
            DropTriggerOperation o => o.TableName,
            _ => null,
        };

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
