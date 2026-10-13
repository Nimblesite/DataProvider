using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;
using Nimblesite.DataProvider.Core.CodeGeneration;

namespace Nimblesite.DataProvider.SQLite.CodeGeneration;

// Implements [CON-SHARED-CORE]. Thin shell that constructs a
// Core.AdoNetDatabaseEffects with a SqliteConnection factory + the SQLite
// type mapper. Every line of connection/reader/parameter-binding logic now
// lives in Core.
/// <summary>
/// SQLite-specific <see cref="IDatabaseEffects"/> — composed from
/// <see cref="AdoNetDatabaseEffects"/> with a
/// <see cref="SqliteConnection"/> factory and the SQLite type mapper.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class SqliteDatabaseEffects
{
    /// <summary>
    /// Creates a ready-to-use <see cref="IDatabaseEffects"/> for SQLite.
    /// </summary>
    public static IDatabaseEffects Create() =>
        new AdoNetDatabaseEffects(
            connectionString => new SqliteConnection(connectionString),
            MapSqliteTypeToCSharpType
        );

    private static string MapSqliteTypeToCSharpType(
        Type fieldType,
        string dataTypeName,
        bool isNullable
    )
    {
        var baseType = ResolveClrType(fieldType);

        // BOOLEAN columns in SQLite have NUMERIC affinity; the reader reports
        // them as string, so override based on the declared type name.
        if (
            baseType == "string"
            && dataTypeName.Contains("BOOL", StringComparison.OrdinalIgnoreCase)
        )
        {
            return isNullable ? "bool?" : "bool";
        }

        return isNullable && !baseType.EndsWith('?') ? $"{baseType}?" : baseType;
    }

    private static string ResolveClrType(Type fieldType)
    {
        if (fieldType == typeof(int) || fieldType == typeof(int?))
            return fieldType == typeof(int?) ? "int?" : "int";
        if (fieldType == typeof(long) || fieldType == typeof(long?))
            return fieldType == typeof(long?) ? "long?" : "long";
        if (fieldType == typeof(double) || fieldType == typeof(double?))
            return fieldType == typeof(double?) ? "double?" : "double";
        if (fieldType == typeof(decimal) || fieldType == typeof(decimal?))
            return fieldType == typeof(decimal?) ? "decimal?" : "decimal";
        if (fieldType == typeof(bool) || fieldType == typeof(bool?))
            return fieldType == typeof(bool?) ? "bool?" : "bool";
        if (fieldType == typeof(DateTime) || fieldType == typeof(DateTime?))
            return fieldType == typeof(DateTime?) ? "DateTime?" : "DateTime";
        if (fieldType == typeof(string))
            return "string";
        if (fieldType == typeof(byte[]))
            return "byte[]";
        if (fieldType == typeof(Guid) || fieldType == typeof(Guid?))
            return fieldType == typeof(Guid?) ? "Guid?" : "Guid";
        return "string";
    }
}
