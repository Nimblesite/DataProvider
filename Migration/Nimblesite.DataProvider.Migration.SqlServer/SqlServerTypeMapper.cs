namespace Nimblesite.DataProvider.Migration.SqlServer;

/// <summary>
/// Maps portable types to SQL Server column types and back. Every mapping
/// round-trips so the integrity verifier sees no drift after a migration.
/// Implements [MIG-SQLSERVER].
/// </summary>
public static class SqlServerTypeMapper
{
    /// <summary>
    /// Map a portable type to its SQL Server column type.
    /// </summary>
    public static string ToSqlServer(PortableType type) =>
        type switch
        {
            TinyIntType => "TINYINT",
            SmallIntType => "SMALLINT",
            IntType => "INT",
            BigIntType => "BIGINT",
            DecimalType d => $"DECIMAL({d.Precision},{d.Scale})",
            MoneyType => "MONEY",
            SmallMoneyType => "SMALLMONEY",
            FloatType => "REAL",
            DoubleType => "FLOAT",
            _ => ToSqlServerText(type),
        };

    private static string ToSqlServerText(PortableType type) =>
        type switch
        {
            CharType c => $"CHAR({c.Length})",
            VarCharType v => $"VARCHAR({Length(v.MaxLength)})",
            NCharType n => $"NCHAR({n.Length})",
            NVarCharType n => $"NVARCHAR({Length(n.MaxLength)})",
            TextType or JsonType => "NVARCHAR(MAX)",
            XmlType => "XML",
            _ => ToSqlServerOther(type),
        };

    private static string ToSqlServerOther(PortableType type) =>
        type switch
        {
            BinaryType b => $"BINARY({b.Length})",
            VarBinaryType v => $"VARBINARY({Length(v.MaxLength)})",
            BlobType => "VARBINARY(MAX)",
            DateType => "DATE",
            TimeType t => $"TIME({t.Precision})",
            DateTimeType d => $"DATETIME2({d.Precision})",
            DateTimeOffsetType => "DATETIMEOFFSET",
            RowVersionType => "ROWVERSION",
            UuidType => "UNIQUEIDENTIFIER",
            BooleanType => "BIT",
            GeometryType => "GEOMETRY",
            GeographyType => "GEOGRAPHY",
            _ => throw new NotSupportedException(
                $"SQL Server migration does not support portable type {type.GetType().Name}"
            ),
        };

    private static string Length(int maxLength) =>
        maxLength == int.MaxValue ? "MAX" : maxLength.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Map an INFORMATION_SCHEMA column description back to a portable type.
    /// </summary>
    /// <param name="dataType">INFORMATION_SCHEMA.COLUMNS.DATA_TYPE</param>
    /// <param name="maxLength">CHARACTER_MAXIMUM_LENGTH (-1 means MAX)</param>
    /// <param name="precision">NUMERIC_PRECISION or DATETIME_PRECISION</param>
    /// <param name="scale">NUMERIC_SCALE</param>
    public static PortableType FromSqlServer(
        string dataType,
        int? maxLength,
        int? precision,
        int? scale
    ) =>
        dataType.ToUpperInvariant() switch
        {
            "TINYINT" => new TinyIntType(),
            "SMALLINT" => new SmallIntType(),
            "INT" => new IntType(),
            "BIGINT" => new BigIntType(),
            "DECIMAL" or "NUMERIC" => new DecimalType(precision ?? 18, scale ?? 0),
            "MONEY" => new MoneyType(),
            "SMALLMONEY" => new SmallMoneyType(),
            "REAL" => new FloatType(),
            "FLOAT" => new DoubleType(),
            var other => FromSqlServerText(other, MaxLength(maxLength), precision),
        };

    private static PortableType FromSqlServerText(string dataType, int length, int? precision) =>
        dataType switch
        {
            "CHAR" => new CharType(length),
            "VARCHAR" => new VarCharType(length),
            "NCHAR" => new NCharType(length),
            "NVARCHAR" when length == int.MaxValue => new TextType(),
            "NVARCHAR" => new NVarCharType(length),
            "XML" => new XmlType(),
            "BINARY" => new BinaryType(length),
            "VARBINARY" when length == int.MaxValue => new BlobType(),
            "VARBINARY" => new VarBinaryType(length),
            _ => FromSqlServerOther(dataType, precision),
        };

    private static PortableType FromSqlServerOther(string dataType, int? precision) =>
        dataType switch
        {
            "DATE" => new DateType(),
            "TIME" => new TimeType(precision ?? 7),
            "DATETIME2" => new DateTimeType(precision ?? 7),
            "DATETIME" => new DateTimeType(3),
            "DATETIMEOFFSET" => new DateTimeOffsetType(),
            "TIMESTAMP" or "ROWVERSION" => new RowVersionType(),
            "UNIQUEIDENTIFIER" => new UuidType(),
            "BIT" => new BooleanType(),
            "GEOMETRY" => new GeometryType(null),
            "GEOGRAPHY" => new GeographyType(),
            _ => new TextType(),
        };

    private static int MaxLength(int? maxLength) =>
        maxLength is null or -1 ? int.MaxValue : maxLength.Value;
}
