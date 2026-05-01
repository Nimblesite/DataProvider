namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Migration runner for executing schema operations.
/// </summary>
public static class MigrationRunner
{
    /// <summary>
    /// Apply schema operations to a database connection.
    /// </summary>
    /// <param name="connection">Open database connection</param>
    /// <param name="operations">Operations to apply</param>
    /// <param name="generateDdl">Platform-specific DDL generator</param>
    /// <param name="options">Migration options</param>
    /// <param name="logger">Optional logger</param>
    /// <returns>Result indicating success or failure</returns>
    public static MigrationApplyResult Apply(
        IDbConnection connection,
        IReadOnlyList<SchemaOperation> operations,
        Func<SchemaOperation, string> generateDdl,
        MigrationOptions options,
        ILogger? logger = null
    )
    {
        if (operations.Count == 0)
        {
            logger?.LogInformation("No operations to apply, schema is up to date");
            return new MigrationApplyResult.Ok<bool, MigrationError>(true);
        }

        // Check for destructive operations
        if (!options.AllowDestructive)
        {
            var destructive = operations.Where(IsDestructive).ToList();
            if (destructive.Count > 0)
            {
                var msg =
                    $"Destructive operations detected but AllowDestructive=false: {string.Join(", ", destructive.Select(o => o.GetType().Name))}";
                logger?.LogError(msg);
                return new MigrationApplyResult.Error<bool, MigrationError>(
                    MigrationError.FromMessage(msg)
                );
            }
        }

        IDbTransaction? transaction = null;

        try
        {
            if (options.UseTransaction)
            {
                transaction = connection.BeginTransaction();
                logger?.LogDebug("Started migration transaction");
            }

            foreach (var operation in operations)
            {
                var ddl = generateDdl(operation);
                logger?.LogDebug("Executing DDL: {Ddl}", ddl);

                if (options.DryRun)
                {
                    logger?.LogInformation("[DRY RUN] Would execute: {Ddl}", ddl);
                    continue;
                }

                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = ddl;
                    command.Transaction = transaction;
                    command.ExecuteNonQuery();

                    logger?.LogInformation("Applied: {OperationType}", operation.GetType().Name);
                }
                catch (Exception ex) when (options.ContinueOnError)
                {
                    logger?.LogWarning(
                        ex,
                        "Failed to apply {OperationType}, continuing",
                        operation.GetType().Name
                    );
                }
            }

            transaction?.Commit();
            logger?.LogInformation(
                "Migration completed: {Count} operations applied",
                operations.Count
            );

            return new MigrationApplyResult.Ok<bool, MigrationError>(true);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Migration failed, rolling back");
            transaction?.Rollback();
            return new MigrationApplyResult.Error<bool, MigrationError>(
                MigrationError.FromException(ex)
            );
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    private static bool IsDestructive(SchemaOperation op) =>
        op
            is DropTableOperation
                or DropColumnOperation
                or DropIndexOperation
                or DropForeignKeyOperation;
}
