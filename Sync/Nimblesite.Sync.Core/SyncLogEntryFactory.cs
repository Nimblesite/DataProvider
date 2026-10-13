using System.Data.Common;

namespace Nimblesite.Sync.Core;

// Implements [SYNC-REPOSITORY-SHARED-READS].
/// <summary>
/// Shared projection of a sync log reader row into a <see cref="SyncLogEntry"/>.
/// </summary>
internal static class SyncLogEntryFactory
{
    /// <summary>
    /// Reads a sync log entry from the current row of a data reader.
    /// </summary>
    /// <param name="reader">Reader positioned on a sync log row.</param>
    /// <param name="fallback">Operation to use when the stored operation is unknown.</param>
    /// <returns>The projected sync log entry.</returns>
    internal static SyncLogEntry Read(DbDataReader reader, SyncOperation fallback) =>
        new(
            Version: reader.GetInt64(0),
            TableName: reader.GetString(1),
            PkValue: reader.GetString(2),
            Operation: ParseOperation(operation: reader.GetString(3), fallback: fallback),
            Payload: reader.IsDBNull(4) ? null : reader.GetString(4),
            Origin: reader.GetString(5),
            Timestamp: reader.GetString(6)
        );

    /// <summary>
    /// Parses a stored operation name into its <see cref="SyncOperation"/> value.
    /// </summary>
    /// <param name="operation">Stored operation name.</param>
    /// <param name="fallback">Operation to use when the stored operation is unknown.</param>
    /// <returns>The parsed operation or the supplied fallback.</returns>
    internal static SyncOperation ParseOperation(string operation, SyncOperation fallback) =>
        operation.ToLowerInvariant() switch
        {
            "insert" => SyncOperation.Insert,
            "update" => SyncOperation.Update,
            "delete" => SyncOperation.Delete,
            _ => fallback,
        };
}
