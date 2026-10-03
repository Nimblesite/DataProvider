namespace Nimblesite.DataProvider.SQLite;

/// <summary>
/// Maps SQLite declared column types to C# type names.
/// </summary>
internal static class SqliteTypeMapper
{
    /// <summary>
    /// Maps a SQLite declared type to a C# type name, appending "?" when nullable.
    /// </summary>
    /// <param name="sqliteType">The SQLite declared column type.</param>
    /// <param name="isNullable">Whether the column is nullable.</param>
    /// <returns>The mapped C# type name.</returns>
    internal static string MapSqliteTypeToCSharpType(string sqliteType, bool isNullable)
    {
        var baseType = sqliteType.ToUpperInvariant() switch
        {
            var t when t.Contains("INT", StringComparison.OrdinalIgnoreCase) => "long",
            var t
                when t.Contains("REAL", StringComparison.OrdinalIgnoreCase)
                    || t.Contains("FLOAT", StringComparison.OrdinalIgnoreCase)
                    || t.Contains("DOUBLE", StringComparison.OrdinalIgnoreCase) => "double",
            var t
                when t.Contains("DECIMAL", StringComparison.OrdinalIgnoreCase)
                    || t.Contains("NUMERIC", StringComparison.OrdinalIgnoreCase) => "double",
            var t when t.Contains("BOOL", StringComparison.OrdinalIgnoreCase) => "bool",
            var t
                when t.Contains("DATE", StringComparison.OrdinalIgnoreCase)
                    || t.Contains("TIME", StringComparison.OrdinalIgnoreCase) => "string", // SQLite stores dates as text
            var t when t.Contains("BLOB", StringComparison.OrdinalIgnoreCase) => "byte[]",
            _ => "string",
        };

        if (isNullable && baseType != "string" && baseType != "byte[]")
        {
            return baseType + "?";
        }

        return baseType;
    }
}
