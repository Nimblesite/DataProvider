using System.Diagnostics.CodeAnalysis;
using Nimblesite.DataProvider.Core.CodeGeneration;
using Npgsql;

namespace Nimblesite.DataProvider.Postgres.CodeGeneration;

// Implements [CON-SHARED-CORE]. Thin shell over Core.AdoNetDatabaseEffects.
// Supplies an NpgsqlConnection factory + the Postgres type mapper. Every
// line of connection/reader/parameter-binding logic lives in Core.
/// <summary>
/// Postgres-specific <see cref="IDatabaseEffects"/> — composed from
/// <see cref="AdoNetDatabaseEffects"/> with an
/// <see cref="NpgsqlConnection"/> factory and
/// <see cref="PostgresTypeMapper.MapPostgresTypeToCSharp"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public static class PostgresDatabaseEffects
{
    /// <summary>
    /// Creates a ready-to-use <see cref="IDatabaseEffects"/> for Postgres.
    /// </summary>
    public static IDatabaseEffects Create() =>
        new AdoNetDatabaseEffects(
            connectionString => new NpgsqlConnection(connectionString),
            MapFromReader
        );

    private static string MapFromReader(Type fieldType, string dataTypeName, bool isNullable)
    {
        // Prefer the driver-declared Postgres type name (e.g. "uuid",
        // "timestamp with time zone") because it's strictly more specific
        // than the CLR type reported by the reader. PostgresTypeMapper
        // already handles the unknown-type fallback.
        return PostgresTypeMapper.MapPostgresTypeToCSharp(
            pgType: fieldType == typeof(decimal) ? "numeric" : dataTypeName,
            isNullable: isNullable
        );
    }
}
