using Nimblesite.DataProvider.Core;
using Nimblesite.DataProvider.Migration.Core;
using Nimblesite.Sql.Model;

namespace Nimblesite.DataProvider.Postgres.CodeGeneration;

// Ported verbatim from DataProvider/DataProvider/Program.cs's
// MapPostgresTypeToCSharp / MapPortableTypeToCSharp / GetReaderExpression /
// InferParameterType helpers so the new Postgres library owns the mapping
// logic and the old monolith can lose it per [CON-PARSER-ONLY] +
// [CON-SHARED-CORE]. No behavioural changes.
/// <summary>
/// Postgres dialect type mapping: SQL type name + CLR type + nullability →
/// C# type literal + reader call shapes. Used by
/// <see cref="PostgresDatabaseEffects"/> and
/// <see cref="PostgresCodeGenerator"/>.
/// </summary>
public static class PostgresTypeMapper
{
    /// <summary>
    /// Maps a Postgres SQL type name (as reported by the driver or an LQL
    /// schema file) to a C# type literal. Honours
    /// <paramref name="isNullable"/> for value types and strings, but always
    /// returns <c>byte[]?</c> for <c>bytea</c> because Postgres metadata for
    /// that type is unreliable (see bug #14 in the original monolith).
    /// </summary>
    /// <param name="pgType">The Postgres SQL type name.</param>
    /// <param name="isNullable">Whether the column is nullable.</param>
    /// <returns>A C# type literal suitable for emission in generated code.</returns>
    public static string MapPostgresTypeToCSharp(string pgType, bool isNullable)
    {
        var baseType = pgType.ToLowerInvariant() switch
        {
            "uuid" => "Guid",
            "boolean" or "bool" => "bool",
            "smallint" or "int2" => "short",
            "integer" or "int4" or "int" => "int",
            "bigint" or "int8" => "long",
            "real" or "float4" => "float",
            "double precision" or "float8" => "double",
            "numeric" or "decimal" or "money" => "decimal",
            "date" => "DateOnly",
            "time" or "time without time zone" => "TimeOnly",
            "time with time zone" or "timetz" => "TimeOnly",
            "timestamp" or "timestamp without time zone" => "DateTime",
            "timestamp with time zone" or "timestamptz" => "DateTimeOffset",
            "interval" => "TimeSpan",
            "bytea" => "byte[]",
            "text" or "varchar" or "character varying" or "char" or "character" or "name" =>
                "string",
            "json" or "jsonb" => "string",
            var t when t.EndsWith("[]", StringComparison.Ordinal) => "string[]",

            // PortableType names (from schema.yaml)
            "uuidtype" => "Guid",
            "booleantype" => "bool",
            "smallinttype" => "short",
            "inttype" => "int",
            "biginttype" => "long",
            "floattype" => "float",
            "doubletype" => "double",
            "decimaltype" => "decimal",
            "moneytype" => "decimal",
            "smallmoneytype" => "decimal",
            "datetype" => "DateOnly",
            "timetype" => "TimeOnly",
            "datetimetype" => "DateTime",
            "datetimeoffsettype" => "DateTimeOffset",
            "texttype" => "string",
            "chartype" => "string",
            "varchartype" => "string",
            "nchartype" => "string",
            "nvarchartype" => "string",
            "jsontype" => "string",
            "xmltype" => "string",
            "binarytype" => "byte[]",
            "varbinarytype" => "byte[]",
            "blobtype" => "byte[]",
            "rowversiontype" => "byte[]",

            _ => "string",
        };

        return ApplyNullability(baseType: baseType, isNullable: isNullable);
    }

    /// <summary>
    /// Maps a <see cref="PortableType"/> (LQL schema-derived) to a C# type literal.
    /// </summary>
    /// <param name="type">The portable type.</param>
    /// <param name="isNullable">Whether the column is nullable.</param>
    /// <returns>A C# type literal.</returns>
    public static string MapPortableTypeToCSharp(PortableType type, bool isNullable)
    {
        var baseType = type switch
        {
            UuidType => "Guid",
            BooleanType => "bool",
            SmallIntType => "short",
            IntType => "int",
            BigIntType => "long",
            FloatType => "float",
            DoubleType => "double",
            DecimalType => "decimal",
            MoneyType => "decimal",
            SmallMoneyType => "decimal",
            DateType => "DateOnly",
            TimeType => "TimeOnly",
            DateTimeType => "DateTime",
            DateTimeOffsetType => "DateTimeOffset",
            TextType => "string",
            CharType => "string",
            VarCharType => "string",
            NCharType => "string",
            NVarCharType => "string",
            JsonType => "string",
            XmlType => "string",
            BinaryType => "byte[]",
            VarBinaryType => "byte[]",
            BlobType => "byte[]",
            RowVersionType => "byte[]",
            _ => "string",
        };

        return ApplyNullability(baseType: baseType, isNullable: isNullable);
    }

    /// <summary>
    /// Returns the reader-call expression used in generated code to read
    /// a column at the supplied ordinal into its C# type.
    /// </summary>
    /// <param name="col">The database column (supplies the C# type + nullability).</param>
    /// <param name="ordinal">The column ordinal in the reader.</param>
    /// <returns>A C# expression of the form <c>reader.GetXxx(N)</c>.</returns>
    public static string GetReaderExpression(DatabaseColumn col, int ordinal)
    {
        var nullCheck = col.IsNullable ? $"reader.IsDBNull({ordinal}) ? null : " : "";

        return col.CSharpType.TrimEnd('?') switch
        {
            "Guid" => $"{nullCheck}reader.GetGuid({ordinal})",
            "bool" => $"{nullCheck}reader.GetBoolean({ordinal})",
            "short" => $"{nullCheck}reader.GetInt16({ordinal})",
            "int" => $"{nullCheck}reader.GetInt32({ordinal})",
            "long" => $"{nullCheck}reader.GetInt64({ordinal})",
            "float" => $"{nullCheck}reader.GetFloat({ordinal})",
            "double" => $"{nullCheck}reader.GetDouble({ordinal})",
            "decimal" => $"{nullCheck}reader.GetDecimal({ordinal})",
            "DateOnly" => $"{nullCheck}DateOnly.FromDateTime(reader.GetDateTime({ordinal}))",
            "TimeOnly" => $"{nullCheck}TimeOnly.FromTimeSpan(reader.GetTimeSpan({ordinal}))",
            "DateTime" => $"{nullCheck}reader.GetDateTime({ordinal})",
            "DateTimeOffset" => $"{nullCheck}reader.GetFieldValue<DateTimeOffset>({ordinal})",
            "TimeSpan" => $"{nullCheck}reader.GetTimeSpan({ordinal})",
            "byte[]" => $"{nullCheck}reader.GetFieldValue<byte[]>({ordinal})",
            "string[]" => $"reader.GetFieldValue<string[]>({ordinal})",
            _ => $"{nullCheck}reader.GetString({ordinal})",
        };
    }

    /// <summary>
    /// Infers a parameter's C# type. If a matching column is supplied, uses
    /// that column's C# type (stripped of its nullable suffix). Otherwise
    /// falls back to name-based heuristics (<c>*id</c> → string,
    /// <c>limit/offset/count</c> → int, everything else → object).
    /// </summary>
    /// <param name="paramName">The parameter name.</param>
    /// <param name="columns">Optional columns to match against.</param>
    /// <returns>A C# type literal for the parameter's method signature.</returns>
    public static string InferParameterType(
        string paramName,
        IReadOnlyList<DatabaseColumn>? columns = null
    )
    {
        if (columns is not null)
        {
            foreach (var col in columns)
            {
                if (string.Equals(col.Name, paramName, StringComparison.OrdinalIgnoreCase))
                {
                    var t = col.CSharpType;
                    if (t.EndsWith("?", StringComparison.Ordinal))
                    {
                        t = t[..^1];
                    }
                    return t;
                }
            }
        }

        var lower = paramName.ToLowerInvariant();
        if (lower.EndsWith("id", StringComparison.Ordinal))
            return "string";
        if (
            lower.Contains("limit", StringComparison.Ordinal)
            || lower.Contains("offset", StringComparison.Ordinal)
            || lower.Contains("count", StringComparison.Ordinal)
        )
            return "int";
        return "object";
    }

    // Implements [DP-MODEL-SHARED-EMISSION].
    private static string ApplyNullability(string baseType, bool isNullable) =>
        baseType == "byte[]" ? "byte[]?"
        : isNullable && !baseType.EndsWith("[]", StringComparison.Ordinal) ? baseType + "?"
        : baseType;
}
