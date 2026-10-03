using System.Globalization;

namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Reflection-free scalar encode/parse helpers shared by the hand-written
/// YAML writer and reader. Implements [MIG-AOT-YAML]. The encodings here are
/// the single source of truth for how <see cref="PortableType" />, foreign-key
/// actions, RLS operations and grant targets appear in schema YAML; they match
/// the format the previous YamlDotNet converters produced so existing schema
/// files and tests keep round-tripping.
/// </summary>
internal static class SchemaYamlScalars
{
    /// <summary>Encodes a <see cref="PortableType" /> as its scalar string form.</summary>
    internal static string Encode(PortableType value) =>
        value switch
        {
            TinyIntType => "TinyInt",
            SmallIntType => "SmallInt",
            IntType => "Int",
            BigIntType => "BigInt",
            DecimalType d => $"Decimal({d.Precision},{d.Scale})",
            FloatType => "Float",
            DoubleType => "Double",
            MoneyType => "Money",
            SmallMoneyType => "SmallMoney",
            BooleanType => "Boolean",
            CharType c => $"Char({c.Length})",
            VarCharType v => $"VarChar({v.MaxLength})",
            NCharType nc => $"NChar({nc.Length})",
            NVarCharType nv when nv.MaxLength == int.MaxValue => "NVarChar(max)",
            NVarCharType nv => $"NVarChar({nv.MaxLength})",
            TextType => "Text",
            BinaryType b => $"Binary({b.Length})",
            VarBinaryType vb when vb.MaxLength == int.MaxValue => "VarBinary(max)",
            VarBinaryType vb => $"VarBinary({vb.MaxLength})",
            BlobType => "Blob",
            DateType => "Date",
            TimeType t when t.Precision == 7 => "Time",
            TimeType t => $"Time({t.Precision})",
            DateTimeType dt when dt.Precision == 3 => "DateTime",
            DateTimeType dt => $"DateTime({dt.Precision})",
            DateTimeOffsetType => "DateTimeOffset",
            UuidType => "Uuid",
            JsonType => "Json",
            XmlType => "Xml",
            RowVersionType => "RowVersion",
            GeometryType g when g.Srid.HasValue => $"Geometry({g.Srid})",
            GeometryType => "Geometry",
            GeographyType g when g.Srid == 4326 => "Geography",
            GeographyType g => $"Geography({g.Srid})",
            EnumType e => $"Enum({e.Name}:{string.Join("|", e.Values)})",
            VectorType v => $"Vector({v.Dimensions})",
            _ => "Text",
        };

    /// <summary>Parses a scalar string into a <see cref="PortableType" />.</summary>
    internal static PortableType ParseType(string typeStr)
    {
        var trimmed = typeStr.Trim();

        var parenIndex = trimmed.IndexOf('(', StringComparison.Ordinal);
        if (parenIndex > 0)
        {
            var typeName = trimmed[..parenIndex];
            var paramsStr = trimmed[(parenIndex + 1)..^1];
            return ParseParameterized(typeName.ToUpperInvariant(), paramsStr);
        }

        return trimmed.ToUpperInvariant() switch
        {
            "TINYINT" => new TinyIntType(),
            "SMALLINT" => new SmallIntType(),
            "INT" or "INTEGER" => new IntType(),
            "BIGINT" => new BigIntType(),
            "FLOAT" or "REAL" => new FloatType(),
            "DOUBLE" => new DoubleType(),
            "MONEY" => new MoneyType(),
            "SMALLMONEY" => new SmallMoneyType(),
            "BOOLEAN" or "BOOL" => new BooleanType(),
            "TEXT" => new TextType(),
            "BLOB" => new BlobType(),
            "DATE" => new DateType(),
            "TIME" => new TimeType(),
            "DATETIME" => new DateTimeType(),
            "DATETIMEOFFSET" => new DateTimeOffsetType(),
            "UUID" or "GUID" => new UuidType(),
            "JSON" or "JSONB" => new JsonType(),
            "XML" => new XmlType(),
            "ROWVERSION" or "TIMESTAMP" => new RowVersionType(),
            "GEOMETRY" => new GeometryType(null),
            "GEOGRAPHY" => new GeographyType(),
            _ => new TextType(),
        };
    }

    private static PortableType ParseParameterized(string typeName, string paramsStr) =>
        typeName switch
        {
            "DECIMAL" => ParseDecimal(paramsStr),
            "CHAR" => new CharType(ParseInt(paramsStr)),
            "VARCHAR" => new VarCharType(ParseMaxLength(paramsStr)),
            "NCHAR" => new NCharType(ParseInt(paramsStr)),
            "NVARCHAR" => new NVarCharType(ParseMaxLength(paramsStr)),
            "BINARY" => new BinaryType(ParseInt(paramsStr)),
            "VARBINARY" => new VarBinaryType(ParseMaxLength(paramsStr)),
            "TIME" => new TimeType(ParseInt(paramsStr)),
            "DATETIME" => new DateTimeType(ParseInt(paramsStr)),
            "GEOMETRY" => new GeometryType(ParseInt(paramsStr)),
            "GEOGRAPHY" => new GeographyType(ParseInt(paramsStr)),
            "ENUM" => ParseEnum(paramsStr),
            "VECTOR" => new VectorType(ParseInt(paramsStr)),
            _ => new TextType(),
        };

    private static int ParseInt(string s) => int.Parse(s, CultureInfo.InvariantCulture);

    private static int ParseMaxLength(string s) =>
        s.Equals("max", StringComparison.OrdinalIgnoreCase) ? int.MaxValue : ParseInt(s);

    private static DecimalType ParseDecimal(string paramsStr)
    {
        var parts = paramsStr.Split(',');
        return parts.Length == 2
            ? new DecimalType(
                int.Parse(parts[0].Trim(), CultureInfo.InvariantCulture),
                int.Parse(parts[1].Trim(), CultureInfo.InvariantCulture)
            )
            : new DecimalType(int.Parse(parts[0].Trim(), CultureInfo.InvariantCulture), 0);
    }

    private static EnumType ParseEnum(string paramsStr)
    {
        var colonIndex = paramsStr.IndexOf(':', StringComparison.Ordinal);
        if (colonIndex > 0)
        {
            var name = paramsStr[..colonIndex];
            var values = paramsStr[(colonIndex + 1)..].Split('|');
            return new EnumType(name, values);
        }

        return new EnumType("enum", paramsStr.Split('|'));
    }

    /// <summary>Encodes a foreign-key action enum value.</summary>
    internal static string Encode(ForeignKeyAction action) =>
        action switch
        {
            ForeignKeyAction.NoAction => "NoAction",
            ForeignKeyAction.Cascade => "Cascade",
            ForeignKeyAction.SetNull => "SetNull",
            ForeignKeyAction.SetDefault => "SetDefault",
            ForeignKeyAction.Restrict => "Restrict",
            _ => "NoAction",
        };

    /// <summary>Parses a foreign-key action scalar, tolerant of spelling variants.</summary>
    internal static ForeignKeyAction ParseForeignKeyAction(string value) =>
        value.ToUpperInvariant() switch
        {
            "NOACTION" or "NO_ACTION" or "NO ACTION" => ForeignKeyAction.NoAction,
            "CASCADE" => ForeignKeyAction.Cascade,
            "SETNULL" or "SET_NULL" or "SET NULL" => ForeignKeyAction.SetNull,
            "SETDEFAULT" or "SET_DEFAULT" or "SET DEFAULT" => ForeignKeyAction.SetDefault,
            "RESTRICT" => ForeignKeyAction.Restrict,
            _ => ForeignKeyAction.NoAction,
        };

    /// <summary>Encodes an RLS operation enum value to its scalar string form.</summary>
    internal static string Encode(RlsOperation operation) =>
        operation switch
        {
            RlsOperation.All => "All",
            RlsOperation.Select => "Select",
            RlsOperation.Insert => "Insert",
            RlsOperation.Update => "Update",
            RlsOperation.Delete => "Delete",
            _ => "All",
        };

    /// <summary>Parses an RLS operation scalar, tolerant of case.</summary>
    internal static RlsOperation ParseRlsOperation(string value) =>
        value.ToUpperInvariant() switch
        {
            "ALL" => RlsOperation.All,
            "SELECT" => RlsOperation.Select,
            "INSERT" => RlsOperation.Insert,
            "UPDATE" => RlsOperation.Update,
            "DELETE" => RlsOperation.Delete,
            _ => RlsOperation.All,
        };

    /// <summary>Encodes a grant-target enum value to its scalar string form.</summary>
    internal static string Encode(PostgresGrantTarget target) =>
        target switch
        {
            PostgresGrantTarget.Schema => "Schema",
            PostgresGrantTarget.Table => "Table",
            PostgresGrantTarget.AllTablesInSchema => "AllTablesInSchema",
            _ => "Table",
        };

    /// <summary>Parses a grant-target scalar, tolerant of spelling variants.</summary>
    internal static PostgresGrantTarget ParseGrantTarget(string value) =>
        value.ToUpperInvariant() switch
        {
            "SCHEMA" => PostgresGrantTarget.Schema,
            "TABLE" => PostgresGrantTarget.Table,
            "ALLTABLESINSCHEMA" or "ALL_TABLES_IN_SCHEMA" or "ALL TABLES IN SCHEMA" =>
                PostgresGrantTarget.AllTablesInSchema,
            _ => PostgresGrantTarget.Table,
        };
}
