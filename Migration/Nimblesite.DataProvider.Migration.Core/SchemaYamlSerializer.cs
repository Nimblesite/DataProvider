using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.ObjectGraphVisitors;

namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Serializes and deserializes schema definitions to/from YAML.
/// Used for storing schema definitions as portable configuration files.
/// </summary>
public static class SchemaYamlSerializer
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithTypeConverter(new PortableTypeYamlConverter())
        .WithTypeConverter(new ForeignKeyActionYamlConverter())
        .WithTypeConverter(new RlsOperationYamlConverter())
        .WithTypeConverter(new PostgresGrantTargetYamlConverter())
        .ConfigureDefaultValuesHandling(
            DefaultValuesHandling.OmitDefaults
                | DefaultValuesHandling.OmitNull
                | DefaultValuesHandling.OmitEmptyCollections
        )
        .WithEmissionPhaseObjectGraphVisitor(args => new PropertyDefaultValueFilter(
            args.InnerVisitor
        ))
        .DisableAliases()
        .Build();

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithTypeConverter(new PortableTypeYamlConverter())
        .WithTypeConverter(new ForeignKeyActionYamlConverter())
        .WithTypeConverter(new RlsOperationYamlConverter())
        .WithTypeConverter(new PostgresGrantTargetYamlConverter())
        .WithTypeMapping<IReadOnlyList<TableDefinition>, List<TableDefinition>>()
        .WithTypeMapping<IReadOnlyList<PostgresRoleDefinition>, List<PostgresRoleDefinition>>()
        .WithTypeMapping<
            IReadOnlyList<PostgresFunctionDefinition>,
            List<PostgresFunctionDefinition>
        >()
        .WithTypeMapping<
            IReadOnlyList<PostgresFunctionArgumentDefinition>,
            List<PostgresFunctionArgumentDefinition>
        >()
        .WithTypeMapping<IReadOnlyList<PostgresGrantDefinition>, List<PostgresGrantDefinition>>()
        .WithTypeMapping<IReadOnlyList<ColumnDefinition>, List<ColumnDefinition>>()
        .WithTypeMapping<IReadOnlyList<IndexDefinition>, List<IndexDefinition>>()
        .WithTypeMapping<IReadOnlyList<ForeignKeyDefinition>, List<ForeignKeyDefinition>>()
        .WithTypeMapping<
            IReadOnlyList<UniqueConstraintDefinition>,
            List<UniqueConstraintDefinition>
        >()
        .WithTypeMapping<
            IReadOnlyList<CheckConstraintDefinition>,
            List<CheckConstraintDefinition>
        >()
        .WithTypeMapping<IReadOnlyList<RlsPolicyDefinition>, List<RlsPolicyDefinition>>()
        .WithTypeMapping<IReadOnlyList<RlsOperation>, List<RlsOperation>>()
        .WithTypeMapping<IReadOnlyList<string>, List<string>>()
        .Build();

    /// <summary>
    /// Serialize a schema definition to YAML string.
    /// </summary>
    /// <param name="schema">Schema to serialize.</param>
    /// <returns>YAML representation of the schema.</returns>
    public static string ToYaml(SchemaDefinition schema)
    {
        ValidateSupportFunctionBodies(schema);
        return Serializer.Serialize(schema);
    }

    /// <summary>
    /// Deserialize a schema definition from YAML string.
    /// </summary>
    /// <param name="yaml">YAML string.</param>
    /// <returns>Deserialized schema definition.</returns>
    public static SchemaDefinition FromYaml(string yaml)
    {
        var schema =
            Deserializer.Deserialize<SchemaDefinition>(yaml)
            ?? new SchemaDefinition { Name = string.Empty, Tables = [] };
        ValidateSupportFunctionBodies(schema);
        return schema;
    }

    /// <summary>
    /// Load a schema definition from a YAML file.
    /// </summary>
    /// <param name="filePath">Path to YAML file.</param>
    /// <returns>Deserialized schema definition.</returns>
    public static SchemaDefinition FromYamlFile(string filePath)
    {
        var yaml = File.ReadAllText(filePath);
        return FromYaml(yaml);
    }

    /// <summary>
    /// Save a schema definition to a YAML file.
    /// </summary>
    /// <param name="schema">Schema to save.</param>
    /// <param name="filePath">Path to YAML file.</param>
    public static void ToYamlFile(SchemaDefinition schema, string filePath)
    {
        var yaml = ToYaml(schema);
        File.WriteAllText(filePath, yaml);
    }

    private static void ValidateSupportFunctionBodies(SchemaDefinition schema)
    {
        foreach (var function in schema.Functions)
        {
            if (
                !string.IsNullOrWhiteSpace(function.Body)
                && !string.IsNullOrWhiteSpace(function.BodyLql)
            )
            {
                throw new InvalidOperationException(
                    "PostgreSQL function body and bodyLql are mutually exclusive: "
                        + $"{function.Schema}.{function.Name}"
                );
            }
        }
    }
}

/// <summary>
/// YAML type converter for PortableType discriminated union.
/// Serializes types as simple strings like "Text", "Int", "VarChar(255)".
/// </summary>
internal sealed class PortableTypeYamlConverter : IYamlTypeConverter
{
    /// <inheritdoc />
    public bool Accepts(Type type) => typeof(PortableType).IsAssignableFrom(type);

    /// <inheritdoc />
    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var scalar = parser.Consume<Scalar>();
        return ParseType(scalar.Value);
    }

    /// <inheritdoc />
    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var typeStr = value switch
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
        emitter.Emit(new Scalar(typeStr));
    }

    private static PortableType ParseType(string typeStr)
    {
        var trimmed = typeStr.Trim();

        // Handle parameterized types
        var parenIndex = trimmed.IndexOf('(', StringComparison.Ordinal);
        if (parenIndex > 0)
        {
            var typeName = trimmed[..parenIndex];
            var paramsStr = trimmed[(parenIndex + 1)..^1];

            return typeName.ToUpperInvariant() switch
            {
                "DECIMAL" => ParseDecimal(paramsStr),
                "CHAR" => new CharType(int.Parse(paramsStr, CultureInfo.InvariantCulture)),
                "VARCHAR" => new VarCharType(ParseMaxLength(paramsStr)),
                "NCHAR" => new NCharType(int.Parse(paramsStr, CultureInfo.InvariantCulture)),
                "NVARCHAR" => new NVarCharType(ParseMaxLength(paramsStr)),
                "BINARY" => new BinaryType(int.Parse(paramsStr, CultureInfo.InvariantCulture)),
                "VARBINARY" => new VarBinaryType(ParseMaxLength(paramsStr)),
                "TIME" => new TimeType(int.Parse(paramsStr, CultureInfo.InvariantCulture)),
                "DATETIME" => new DateTimeType(int.Parse(paramsStr, CultureInfo.InvariantCulture)),
                "GEOMETRY" => new GeometryType(int.Parse(paramsStr, CultureInfo.InvariantCulture)),
                "GEOGRAPHY" => new GeographyType(
                    int.Parse(paramsStr, CultureInfo.InvariantCulture)
                ),
                "ENUM" => ParseEnum(paramsStr),
                "VECTOR" => new VectorType(int.Parse(paramsStr, CultureInfo.InvariantCulture)),
                _ => new TextType(),
            };
        }

        // Handle simple types
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

    private static int ParseMaxLength(string s) =>
        s.Equals("max", StringComparison.OrdinalIgnoreCase)
            ? int.MaxValue
            : int.Parse(s, CultureInfo.InvariantCulture);

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
}

/// <summary>
/// YAML type converter for ForeignKeyAction enum.
/// </summary>
internal sealed class ForeignKeyActionYamlConverter : IYamlTypeConverter
{
    /// <inheritdoc />
    public bool Accepts(Type type) => type == typeof(ForeignKeyAction);

    /// <inheritdoc />
    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var scalar = parser.Consume<Scalar>();
        return scalar.Value.ToUpperInvariant() switch
        {
            "NOACTION" or "NO_ACTION" or "NO ACTION" => ForeignKeyAction.NoAction,
            "CASCADE" => ForeignKeyAction.Cascade,
            "SETNULL" or "SET_NULL" or "SET NULL" => ForeignKeyAction.SetNull,
            "SETDEFAULT" or "SET_DEFAULT" or "SET DEFAULT" => ForeignKeyAction.SetDefault,
            "RESTRICT" => ForeignKeyAction.Restrict,
            _ => ForeignKeyAction.NoAction,
        };
    }

    /// <inheritdoc />
    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var action = (ForeignKeyAction)(value ?? ForeignKeyAction.NoAction);
        var str = action switch
        {
            ForeignKeyAction.NoAction => "NoAction",
            ForeignKeyAction.Cascade => "Cascade",
            ForeignKeyAction.SetNull => "SetNull",
            ForeignKeyAction.SetDefault => "SetDefault",
            ForeignKeyAction.Restrict => "Restrict",
            _ => "NoAction",
        };
        emitter.Emit(new Scalar(str));
    }
}

/// <summary>
/// YAML type converter for the <see cref="RlsOperation"/> enum. Maps
/// to/from a single scalar like <c>All</c>, <c>Select</c>, etc.
/// </summary>
internal sealed class RlsOperationYamlConverter : IYamlTypeConverter
{
    /// <inheritdoc />
    public bool Accepts(Type type) => type == typeof(RlsOperation);

    /// <inheritdoc />
    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var scalar = parser.Consume<Scalar>();
        return scalar.Value.ToUpperInvariant() switch
        {
            "ALL" => RlsOperation.All,
            "SELECT" => RlsOperation.Select,
            "INSERT" => RlsOperation.Insert,
            "UPDATE" => RlsOperation.Update,
            "DELETE" => RlsOperation.Delete,
            _ => RlsOperation.All,
        };
    }

    /// <inheritdoc />
    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var op = (RlsOperation)(value ?? RlsOperation.All);
        emitter.Emit(new Scalar(op.ToString()));
    }
}

/// <summary>
/// YAML type converter for the <see cref="PostgresGrantTarget"/> enum.
/// Implements [RLS-PG-SUPPORT-DDL].
/// </summary>
internal sealed class PostgresGrantTargetYamlConverter : IYamlTypeConverter
{
    /// <inheritdoc />
    public bool Accepts(Type type) => type == typeof(PostgresGrantTarget);

    /// <inheritdoc />
    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var scalar = parser.Consume<Scalar>();
        return scalar.Value.ToUpperInvariant() switch
        {
            "SCHEMA" => PostgresGrantTarget.Schema,
            "TABLE" => PostgresGrantTarget.Table,
            "ALLTABLESINSCHEMA" or "ALL_TABLES_IN_SCHEMA" or "ALL TABLES IN SCHEMA" =>
                PostgresGrantTarget.AllTablesInSchema,
            _ => PostgresGrantTarget.Table,
        };
    }

    /// <inheritdoc />
    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var target = (PostgresGrantTarget)(value ?? PostgresGrantTarget.Table);
        emitter.Emit(new Scalar(target.ToString()));
    }
}

/// <summary>
/// Filters out properties that have their semantic default values.
/// This handles cases where the property initializer differs from the type default.
/// </summary>
internal sealed class PropertyDefaultValueFilter(IObjectGraphVisitor<IEmitter> next)
    : ChainedObjectGraphVisitor(next)
{
    /// <summary>
    /// Default values per property name -> (expected value type, default value).
    /// Uses camelCase names (after naming convention applied).
    /// </summary>
    private static readonly Dictionary<string, (Type ValueType, object Default)> SemanticDefaults =
        new()
        {
            // ColumnDefinition semantic defaults
            { "isNullable", (typeof(bool), true) },
            { "identitySeed", (typeof(long), 1L) },
            { "identityIncrement", (typeof(long), 1L) },
            // TableDefinition and ForeignKeyDefinition semantic defaults
            { "schema", (typeof(string), "public") },
            { "referencedSchema", (typeof(string), "public") },
            { "onDelete", (typeof(ForeignKeyAction), ForeignKeyAction.NoAction) },
            { "onUpdate", (typeof(ForeignKeyAction), ForeignKeyAction.NoAction) },
            // RlsPolicySetDefinition / RlsPolicyDefinition semantic defaults
            { "enabled", (typeof(bool), true) },
            { "isPermissive", (typeof(bool), true) },
            // PostgreSQL support object semantic defaults
            { "language", (typeof(string), "sql") },
            { "volatility", (typeof(string), "stable") },
            { "revokePublicExecute", (typeof(bool), true) },
        };

    /// <inheritdoc />
    public override bool EnterMapping(
        IPropertyDescriptor key,
        IObjectDescriptor value,
        IEmitter context,
        ObjectSerializer serializer
    )
    {
        if (SemanticDefaults.TryGetValue(key.Name, out var defaultInfo))
        {
            // Match by property name and ensure value type matches expectation
            if (
                value.Value != null
                && defaultInfo.ValueType.IsAssignableFrom(value.Value.GetType())
                && Equals(value.Value, defaultInfo.Default)
            )
            {
                return false;
            }
        }

        return base.EnterMapping(key, value, context, serializer);
    }
}
