#pragma warning disable CA2100 // SQL is built from sync metadata - tables validated at trigger generation

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Nimblesite.Sync.Postgres;

/// <summary>
/// Applies sync changes to PostgreSQL tables.
/// Implements spec Section 11 (Bi-Directional Sync Protocol).
/// </summary>
public static class PostgresChangeApplier
{
    /// <summary>
    /// Applies a single sync log entry to the database.
    /// Returns true on success, false on FK violation (to defer), error on other failures.
    /// </summary>
    /// <param name="connection">PostgreSQL connection.</param>
    /// <param name="entry">Sync log entry to apply.</param>
    /// <param name="logger">Logger for change application.</param>
    /// <returns>True on success, false on FK violation, error on failure.</returns>
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Table names come from sync log entries - validated at trigger generation"
    )]
    public static BoolSyncResult ApplyChange(
        NpgsqlConnection connection,
        SyncLogEntry entry,
        ILogger logger
    )
    {
        logger.LogDebug(
            "POSTGRES APPLY: Applying {Op} to {Table}, pk={Pk}",
            entry.Operation,
            entry.TableName,
            entry.PkValue
        );

        try
        {
            return entry.Operation switch
            {
                SyncOperation.Insert => ApplyInsert(connection, entry, logger),
                SyncOperation.Update => ApplyUpdate(connection, entry, logger),
                SyncOperation.Delete => ApplyDelete(connection, entry, logger),
                _ => new BoolSyncError(
                    new SyncErrorDatabase($"Unknown operation: {entry.Operation}")
                ),
            };
        }
        catch (NpgsqlException ex) when (IsForeignKeyViolation(ex))
        {
            logger.LogDebug(
                "POSTGRES APPLY: FK violation for {Table} pk={Pk}, deferring",
                entry.TableName,
                entry.PkValue
            );
            return new BoolSyncOk(false); // Defer for retry
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(
                ex,
                "POSTGRES APPLY: Failed to apply {Op} to {Table}",
                entry.Operation,
                entry.TableName
            );
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to apply change: {ex.Message}")
            );
        }
    }

    private static BoolSyncResult ApplyInsert(
        NpgsqlConnection connection,
        SyncLogEntry entry,
        ILogger logger
    )
    {
        if (entry.Payload is null)
        {
            return new BoolSyncError(new SyncErrorDatabase("Insert requires payload"));
        }

        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entry.Payload);
        if (payload is null || payload.Count == 0)
        {
            return new BoolSyncError(new SyncErrorDatabase("Invalid payload format"));
        }

        // PostgreSQL lowercases unquoted identifiers, so we lowercase column and table names
        var columns = string.Join(", ", payload.Keys.Select(k => k.ToLowerInvariant()));
        var paramNames = string.Join(", ", payload.Keys.Select((_, i) => $"@p{i}"));
        var updateCols = string.Join(
            ", ",
            payload.Keys.Select(k => $"{k.ToLowerInvariant()} = EXCLUDED.{k.ToLowerInvariant()}")
        );

        // Get PK column for conflict clause
        var pkJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entry.PkValue);
        var pkColumn = pkJson?.Keys.FirstOrDefault()?.ToLowerInvariant() ?? "id";

        // Table name also needs to be lowercase
        var tableName = entry.TableName.ToLowerInvariant();

        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            $"INSERT INTO {tableName} ({columns}) VALUES ({paramNames}) ON CONFLICT ({pkColumn}) DO UPDATE SET {updateCols}";

        var i = 0;
        foreach (var kvp in payload)
        {
            cmd.Parameters.AddWithValue($"@p{i}", GetJsonValue(kvp.Value));
            i++;
        }

        cmd.ExecuteNonQuery();
        logger.LogDebug("POSTGRES APPLY: Insert/upsert successful for {Table}", entry.TableName);
        return new BoolSyncOk(true);
    }

    private static BoolSyncResult ApplyUpdate(
        NpgsqlConnection connection,
        SyncLogEntry entry,
        ILogger logger
    )
    {
        if (entry.Payload is null)
        {
            return new BoolSyncError(new SyncErrorDatabase("Update requires payload"));
        }

        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entry.Payload);
        if (payload is null || payload.Count == 0)
        {
            return new BoolSyncError(new SyncErrorDatabase("Invalid payload format"));
        }

        var pkParts = TryParsePk(entry, out var pkError);
        if (pkParts is not (var pkColumn, var pkColumnLower, var pkValue))
        {
            return pkError is { } err
                ? err
                : new BoolSyncError(new SyncErrorDatabase("Invalid pk_value format"));
        }

        var setClauses = new List<string>();
        var paramIndex = 0;

        using var cmd = connection.CreateCommand();

        foreach (var kvp in payload)
        {
            if (!kvp.Key.Equals(pkColumn, StringComparison.OrdinalIgnoreCase))
            {
                setClauses.Add($"{kvp.Key.ToLowerInvariant()} = @p{paramIndex}");
                cmd.Parameters.AddWithValue($"@p{paramIndex}", GetJsonValue(kvp.Value));
                paramIndex++;
            }
        }

        if (setClauses.Count == 0)
        {
            // No columns to update (only PK in payload)
            return new BoolSyncOk(true);
        }

        var tableName = entry.TableName.ToLowerInvariant();
        cmd.CommandText =
            $"UPDATE {tableName} SET {string.Join(", ", setClauses)} WHERE {pkColumnLower} = @pkValue";
        cmd.Parameters.AddWithValue("@pkValue", pkValue);

        var affected = cmd.ExecuteNonQuery();
        if (affected == 0)
        {
            // Row doesn't exist, do upsert via insert
            return ApplyInsert(connection, entry, logger);
        }

        logger.LogDebug("POSTGRES APPLY: Update successful for {Table}", entry.TableName);
        return new BoolSyncOk(true);
    }

    private static BoolSyncResult ApplyDelete(
        NpgsqlConnection connection,
        SyncLogEntry entry,
        ILogger logger
    )
    {
        var pkParts = TryParsePk(entry, out var pkError);
        if (pkParts is not (_, var pkColumnLower, var pkValue))
        {
            return pkError is { } err
                ? err
                : new BoolSyncError(new SyncErrorDatabase("Invalid pk_value format"));
        }

        var tableName = entry.TableName.ToLowerInvariant();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"DELETE FROM {tableName} WHERE {pkColumnLower} = @pkValue";
        cmd.Parameters.AddWithValue("@pkValue", pkValue);

        cmd.ExecuteNonQuery();
        logger.LogDebug("POSTGRES APPLY: Delete successful for {Table}", entry.TableName);
        return new BoolSyncOk(true);
    }

    // Shared PK-JSON parse+validate prologue for ApplyUpdate/ApplyDelete.
    private static (string PkColumn, string PkColumnLower, object PkValue)? TryParsePk(
        SyncLogEntry entry,
        out BoolSyncResult? error
    )
    {
        var pkJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entry.PkValue);
        if (pkJson is null || pkJson.Count == 0)
        {
            error = new BoolSyncError(new SyncErrorDatabase("Invalid pk_value format"));
            return null;
        }

        var pkColumn = pkJson.Keys.First();
        error = null;
        return (pkColumn, pkColumn.ToLowerInvariant(), GetJsonValue(pkJson[pkColumn]));
    }

    private static object GetJsonValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => DBNull.Value,
            _ => element.GetRawText(),
        };

    private static bool IsForeignKeyViolation(NpgsqlException ex) => ex.SqlState == "23503"; // PostgreSQL foreign key violation code
}
