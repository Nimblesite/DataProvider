namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Calculates the difference between two schema definitions.
/// Produces a list of operations to transform current schema into desired schema.
/// </summary>
/// <example>
/// <code>
/// // Compare current database schema against desired schema
/// var currentSchema = await schemaInspector.InspectAsync(connection);
/// var desiredSchema = Schema.Define("mydb")
///     .Table("users", t => t
///         .Column("id", PortableTypes.Uuid, c => c.PrimaryKey())
///         .Column("email", PortableTypes.VarChar(255), c => c.NotNull())
///         .Column("name", PortableTypes.VarChar(100)))  // New column
///     .Build();
///
/// // Calculate safe (additive-only) migration operations
/// var result = SchemaDiff.Calculate(currentSchema, desiredSchema);
/// if (result is OperationsResult.Ok&lt;IReadOnlyList&lt;SchemaOperation&gt;, MigrationError&gt; ok)
/// {
///     foreach (var op in ok.Value)
///     {
///         var ddl = ddlGenerator.Generate(op);
///         await connection.ExecuteAsync(ddl);
///     }
/// }
///
/// // Or allow destructive changes (DROP operations)
/// var destructiveResult = SchemaDiff.Calculate(
///     currentSchema, desiredSchema, allowDestructive: true);
/// </code>
/// </example>
public static class SchemaDiff
{
    /// <summary>
    /// Calculate operations needed to transform current schema into desired schema.
    /// By default, only produces additive operations (safe upgrades).
    /// </summary>
    /// <param name="current">Current database schema</param>
    /// <param name="desired">Desired target schema</param>
    /// <param name="allowDestructive">If true, include DROP operations</param>
    /// <param name="logger">Logger for diagnostics</param>
    /// <returns>List of operations to apply</returns>
    public static OperationsResult Calculate(
        SchemaDefinition current,
        SchemaDefinition desired,
        bool allowDestructive = false,
        ILogger? logger = null
    )
    {
        try
        {
            var operations = new List<SchemaOperation>();

            // Use table name only for matching (schema-agnostic comparison)
            // This handles differences between SQLite (main) and Postgres (public) default schemas
            var currentTables = current.Tables.ToDictionary(t => t.Name.ToLowerInvariant());

            // Find tables to create or update
            foreach (var desiredTable in desired.Tables)
            {
                var key = desiredTable.Name.ToLowerInvariant();

                if (!currentTables.TryGetValue(key, out var currentTable))
                {
                    // Table doesn't exist - create it
                    logger?.LogDebug(
                        "Table {Schema}.{Table} not found, will create",
                        desiredTable.Schema,
                        desiredTable.Name
                    );
                    operations.Add(new CreateTableOperation(desiredTable));

                    // Also create operations for any indexes on the new table
                    foreach (var index in desiredTable.Indexes)
                    {
                        logger?.LogDebug(
                            "Creating index {Index} on new table {Schema}.{Table}",
                            index.Name,
                            desiredTable.Schema,
                            desiredTable.Name
                        );
                        operations.Add(
                            new CreateIndexOperation(desiredTable.Schema, desiredTable.Name, index)
                        );
                    }

                    operations.AddRange(
                        CalculateRlsDiff(null, desiredTable, allowDestructive, logger)
                    );
                }
                else
                {
                    // Table exists - check for column additions
                    var columnOps = CalculateColumnDiff(
                        currentTable,
                        desiredTable,
                        allowDestructive,
                        logger
                    );
                    operations.AddRange(columnOps);

                    // Check for index additions
                    var indexOps = CalculateIndexDiff(
                        currentTable,
                        desiredTable,
                        allowDestructive,
                        logger
                    );
                    operations.AddRange(indexOps);

                    // Check for foreign key additions
                    var fkOps = CalculateForeignKeyDiff(
                        currentTable,
                        desiredTable,
                        allowDestructive,
                        logger
                    );
                    operations.AddRange(fkOps);

                    operations.AddRange(
                        CalculateRlsDiff(currentTable, desiredTable, allowDestructive, logger)
                    );
                }
            }

            // Find tables to drop (only if destructive allowed)
            if (allowDestructive)
            {
                // Use table name only for matching (schema-agnostic)
                var desiredTableNames = desired
                    .Tables.Select(t => t.Name.ToLowerInvariant())
                    .ToHashSet();

                foreach (var currentTable in current.Tables)
                {
                    var exists = desiredTableNames.Contains(currentTable.Name.ToLowerInvariant());

                    if (!exists)
                    {
                        logger?.LogWarning(
                            "Table {Schema}.{Table} will be DROPPED",
                            currentTable.Schema,
                            currentTable.Name
                        );
                        operations.Add(
                            new DropTableOperation(currentTable.Schema, currentTable.Name)
                        );
                    }
                }
            }

            return new OperationsResult.Ok<IReadOnlyList<SchemaOperation>, MigrationError>(
                operations.AsReadOnly()
            );
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error calculating schema diff");
            return new OperationsResult.Error<IReadOnlyList<SchemaOperation>, MigrationError>(
                MigrationError.FromException(ex)
            );
        }
    }

    private static IEnumerable<SchemaOperation> CalculateColumnDiff(
        TableDefinition current,
        TableDefinition desired,
        bool allowDestructive,
        ILogger? logger
    )
    {
        var currentColumns = current.Columns.ToDictionary(
            c => c.Name,
            StringComparer.OrdinalIgnoreCase
        );

        // Add new columns
        foreach (var desiredColumn in desired.Columns)
        {
            if (!currentColumns.ContainsKey(desiredColumn.Name))
            {
                logger?.LogDebug(
                    "Column {Schema}.{Table}.{Column} not found, will add",
                    desired.Schema,
                    desired.Name,
                    desiredColumn.Name
                );
                yield return new AddColumnOperation(desired.Schema, desired.Name, desiredColumn);
            }
        }

        // Drop removed columns (only if destructive allowed)
        if (allowDestructive)
        {
            var desiredColumns = desired.Columns.ToDictionary(
                c => c.Name,
                StringComparer.OrdinalIgnoreCase
            );

            foreach (var currentColumn in current.Columns)
            {
                if (!desiredColumns.ContainsKey(currentColumn.Name))
                {
                    logger?.LogWarning(
                        "Column {Schema}.{Table}.{Column} will be DROPPED",
                        current.Schema,
                        current.Name,
                        currentColumn.Name
                    );
                    yield return new DropColumnOperation(
                        current.Schema,
                        current.Name,
                        currentColumn.Name
                    );
                }
            }
        }
    }

    private static IEnumerable<SchemaOperation> CalculateIndexDiff(
        TableDefinition current,
        TableDefinition desired,
        bool allowDestructive,
        ILogger? logger
    )
    {
        var currentIndexes = current.Indexes.ToDictionary(
            i => i.Name,
            StringComparer.OrdinalIgnoreCase
        );

        // Add new indexes
        foreach (var desiredIndex in desired.Indexes)
        {
            if (!currentIndexes.ContainsKey(desiredIndex.Name))
            {
                logger?.LogDebug(
                    "Index {IndexName} on {Schema}.{Table} not found, will create",
                    desiredIndex.Name,
                    desired.Schema,
                    desired.Name
                );
                yield return new CreateIndexOperation(desired.Schema, desired.Name, desiredIndex);
            }
        }

        // Drop removed indexes (only if destructive allowed)
        if (allowDestructive)
        {
            var desiredIndexes = desired.Indexes.ToDictionary(
                i => i.Name,
                StringComparer.OrdinalIgnoreCase
            );

            foreach (var currentIndex in current.Indexes)
            {
                if (!desiredIndexes.ContainsKey(currentIndex.Name))
                {
                    logger?.LogDebug("Index {IndexName} will be dropped", currentIndex.Name);
                    yield return new DropIndexOperation(
                        current.Schema,
                        current.Name,
                        currentIndex.Name
                    );
                }
            }
        }
    }

    // Implements [RLS-DIFF].
    private static IEnumerable<SchemaOperation> CalculateRlsDiff(
        TableDefinition? current,
        TableDefinition desired,
        bool allowDestructive,
        ILogger? logger
    )
    {
        var desiredRls = desired.RowLevelSecurity;
        var currentRls = current?.RowLevelSecurity;

        var desiredEnabled = desiredRls?.Enabled == true;
        var currentEnabled = currentRls?.Enabled == true;

        if (desiredEnabled && !currentEnabled)
        {
            logger?.LogDebug("Enabling RLS on {Schema}.{Table}", desired.Schema, desired.Name);
            yield return new EnableRlsOperation(desired.Schema, desired.Name);
        }

        // Implements [RLS-DIFF] for FORCE (issue #37).
        var desiredForced = desiredRls?.Forced == true;
        var currentForced = currentRls?.Forced == true;
        if (desiredEnabled && desiredForced && !currentForced)
        {
            logger?.LogDebug("Setting FORCE RLS on {Schema}.{Table}", desired.Schema, desired.Name);
            yield return new EnableForceRlsOperation(desired.Schema, desired.Name);
        }

        var currentPolicyNames =
            currentRls?.Policies.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (desiredEnabled)
        {
            foreach (var policy in desiredRls!.Policies)
            {
                if (!currentPolicyNames.Contains(policy.Name))
                {
                    logger?.LogDebug(
                        "Creating RLS policy {Policy} on {Schema}.{Table}",
                        policy.Name,
                        desired.Schema,
                        desired.Name
                    );
                    yield return new CreateRlsPolicyOperation(desired.Schema, desired.Name, policy);
                }
            }
        }

        if (allowDestructive)
        {
            var desiredPolicyNames =
                desiredRls?.Policies.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (currentRls is not null)
            {
                foreach (var policy in currentRls.Policies)
                {
                    if (!desiredPolicyNames.Contains(policy.Name))
                    {
                        logger?.LogWarning(
                            "RLS policy {Policy} on {Schema}.{Table} will be DROPPED",
                            policy.Name,
                            desired.Schema,
                            desired.Name
                        );
                        yield return new DropRlsPolicyOperation(
                            desired.Schema,
                            desired.Name,
                            policy.Name
                        );
                    }
                }
            }

            // FORCE flip true -> false (issue #37, destructive).
            if (currentForced && !desiredForced)
            {
                logger?.LogWarning(
                    "FORCE RLS will be REMOVED on {Schema}.{Table}",
                    desired.Schema,
                    desired.Name
                );
                yield return new DisableForceRlsOperation(desired.Schema, desired.Name);
            }

            if (currentEnabled && !desiredEnabled)
            {
                logger?.LogWarning(
                    "RLS will be DISABLED on {Schema}.{Table}",
                    desired.Schema,
                    desired.Name
                );
                yield return new DisableRlsOperation(desired.Schema, desired.Name);
            }
        }
    }

    private static IEnumerable<SchemaOperation> CalculateForeignKeyDiff(
        TableDefinition current,
        TableDefinition desired,
        bool allowDestructive,
        ILogger? logger
    )
    {
        var currentFks = current
            .ForeignKeys.Where(fk => fk.Name is not null)
            .ToDictionary(fk => fk.Name!, StringComparer.OrdinalIgnoreCase);

        // Add new foreign keys
        foreach (var desiredFk in desired.ForeignKeys)
        {
            if (desiredFk.Name is not null && !currentFks.ContainsKey(desiredFk.Name))
            {
                logger?.LogDebug(
                    "Foreign key {FkName} on {Schema}.{Table} not found, will add",
                    desiredFk.Name,
                    desired.Schema,
                    desired.Name
                );
                yield return new AddForeignKeyOperation(desired.Schema, desired.Name, desiredFk);
            }
        }

        // Drop removed foreign keys (only if destructive allowed)
        if (allowDestructive)
        {
            var desiredFks = desired
                .ForeignKeys.Where(fk => fk.Name is not null)
                .ToDictionary(fk => fk.Name!, StringComparer.OrdinalIgnoreCase);

            foreach (var currentFk in current.ForeignKeys)
            {
                if (currentFk.Name is not null && !desiredFks.ContainsKey(currentFk.Name))
                {
                    logger?.LogDebug("Foreign key {FkName} will be dropped", currentFk.Name);
                    yield return new DropForeignKeyOperation(
                        current.Schema,
                        current.Name,
                        currentFk.Name
                    );
                }
            }
        }
    }
}
