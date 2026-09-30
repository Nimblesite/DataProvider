using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Nimblesite.Sync.SQLite;

/// <summary>
/// Applies sync changes to SQLite database.
/// Implements spec Section 11 (Bi-Directional Sync Protocol).
/// </summary>
public static class ChangeApplierSQLite
{
    /// <summary>
    /// Applies a single sync log entry to the database.
    /// Returns true on success, false on FK violation (for defer/retry).
    /// </summary>
    /// <param name="connection">SQLite connection.</param>
    /// <param name="entry">The sync log entry to apply.</param>
    /// <returns>True if applied, false if FK violation, or error.</returns>
    public static BoolSyncResult ApplyChange(SqliteConnection connection, SyncLogEntry entry)
    {
        try
        {
            return entry.Operation switch
            {
                SyncOperation.Insert => ApplyInsert(connection, entry),
                SyncOperation.Update => ApplyUpdate(connection, entry),
                SyncOperation.Delete => ApplyDelete(connection, entry),
                _ => new BoolSyncError(
                    new SyncErrorDatabase($"Unknown operation: {entry.Operation}")
                ),
            };
        }
        catch (SqliteException ex) when (IsForeignKeyViolation(ex))
        {
            // FK violation - return false to defer for retry
            return new BoolSyncOk(false);
        }
        catch (SqliteException ex)
        {
            return new BoolSyncError(
                new SyncErrorDatabase($"Failed to apply change: {ex.Message}")
            );
        }
    }

    private static BoolSyncResult ApplyInsert(SqliteConnection connection, SyncLogEntry entry)
    {
        if (string.IsNullOrEmpty(entry.Payload))
        {
            return new BoolSyncError(new SyncErrorDatabase("Insert requires payload"));
        }

        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entry.Payload);
        if (data == null || data.Count == 0)
        {
            return new BoolSyncError(new SyncErrorDatabase("Invalid payload JSON"));
        }

        return ApplyUpsert(connection, entry, data);
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Table names come from internal sync log, not user input"
    )]
    private static BoolSyncResult ApplyUpsert(
        SqliteConnection connection,
        SyncLogEntry entry,
        Dictionary<string, JsonElement> data
    )
    {
        var columns = string.Join(", ", data.Keys);
        var parameters = string.Join(", ", data.Keys.Select(k => $"@{k}"));

        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            $"INSERT OR REPLACE INTO {entry.TableName} ({columns}) VALUES ({parameters})";

        foreach (var kvp in data)
        {
            cmd.Parameters.AddWithValue($"@{kvp.Key}", JsonElementToValue(kvp.Value));
        }

        cmd.ExecuteNonQuery();
        return new BoolSyncOk(true);
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Table names come from internal sync log, not user input"
    )]
    private static BoolSyncResult ApplyUpdate(SqliteConnection connection, SyncLogEntry entry)
    {
        if (string.IsNullOrEmpty(entry.Payload))
        {
            return new BoolSyncError(new SyncErrorDatabase("Update requires payload"));
        }

        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entry.Payload);
        if (data == null || data.Count == 0)
        {
            return new BoolSyncError(new SyncErrorDatabase("Invalid payload JSON"));
        }

        // Extract PK info
        if (ParsePkValue(entry.PkValue) is not (string pkColumn, object pkValue))
        {
            return new BoolSyncError(new SyncErrorDatabase("Invalid pk_value JSON"));
        }

        // Check for version-based conflict resolution
        // If record has a Version column, only apply if incoming version is newer
        if (data.TryGetValue("Version", out var incomingVersionElement))
        {
            var incomingVersion = incomingVersionElement.TryGetInt64(out var v) ? v : 0;

            using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = $"SELECT Version FROM {entry.TableName} WHERE {pkColumn} = @pk";
            checkCmd.Parameters.AddWithValue("@pk", pkValue);

            var existingVersionObj = checkCmd.ExecuteScalar();
            if (existingVersionObj is long existingVersion && existingVersion >= incomingVersion)
            {
                // Existing version is same or newer - skip this update (conflict resolution: server wins)
                // This prevents older changes from overwriting newer data
                return new BoolSyncOk(true); // Return success but don't apply
            }
        }
        else
        {
            // No Version column - use timestamp-based conflict resolution
            using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = $"SELECT 1 FROM {entry.TableName} WHERE {pkColumn} = @pk";
            checkCmd.Parameters.AddWithValue("@pk", pkValue);

            var exists = checkCmd.ExecuteScalar() is not null;
            if (exists && !string.IsNullOrEmpty(entry.Timestamp))
            {
                // For timestamp comparison, we'd need to store last-modified timestamp
                // For now, if record exists and no version, apply the update (last-write-wins)
            }
        }

        // Apply the update using UPSERT
        return ApplyUpsert(connection, entry, data);
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Table names come from internal sync log, not user input"
    )]
    private static BoolSyncResult ApplyDelete(SqliteConnection connection, SyncLogEntry entry)
    {
        if (ParsePkValue(entry.PkValue) is not (string pkColumn, object pkValue))
        {
            return new BoolSyncError(new SyncErrorDatabase("Invalid pk_value JSON"));
        }

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"DELETE FROM {entry.TableName} WHERE {pkColumn} = @pk";
        cmd.Parameters.AddWithValue("@pk", pkValue);
        cmd.ExecuteNonQuery();

        return new BoolSyncOk(true);
    }

    private static (string PkColumn, object PkValue)? ParsePkValue(string pkValue)
    {
        var pkData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(pkValue);
        if (pkData == null || pkData.Count == 0)
        {
            return null;
        }

        var pkColumn = pkData.Keys.First();
        return (pkColumn, JsonElementToValue(pkData[pkColumn]));
    }

    private static object JsonElementToValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => 1L,
            JsonValueKind.False => 0L,
            JsonValueKind.Null => DBNull.Value,
            _ => element.ToString(),
        };

    private static bool IsForeignKeyViolation(SqliteException ex) =>
        ex.SqliteErrorCode == 19
        && // SQLITE_CONSTRAINT
        ex.Message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase);
}
