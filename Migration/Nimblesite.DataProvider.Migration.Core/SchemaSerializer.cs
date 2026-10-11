using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Serializes and deserializes schema definitions to/from JSON and YAML.
/// Used for capturing existing database schemas and storing as metadata.
/// </summary>
public static class SchemaSerializer
{
    // Implements [MIG-AOT-JSON]: serialize through the source-generated
    // JsonTypeInfo so the call carries no reflection (IL2026/IL3050). The
    // PortableType converter is attached to the generated context's options;
    // the typed JsonTypeInfo overloads — not the generic Serialize<T>(options)
    // overloads — are what keep the path AOT/trim clean.
    private static readonly SchemaJsonContext Context = new(
        new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new PortableTypeJsonConverter() },
        }
    );

    /// <summary>
    /// Serialize a schema definition to JSON string.
    /// </summary>
    /// <param name="schema">Schema to serialize.</param>
    /// <returns>JSON representation of the schema.</returns>
    public static string ToJson(SchemaDefinition schema) =>
        JsonSerializer.Serialize(schema, Context.SchemaDefinition);

    /// <summary>
    /// Deserialize a schema definition from JSON string.
    /// </summary>
    /// <param name="json">JSON string.</param>
    /// <returns>Deserialized schema definition.</returns>
    public static SchemaDefinition FromJson(string json) =>
        JsonSerializer.Deserialize(json, Context.SchemaDefinition)
        ?? throw new JsonException("Failed to deserialize schema");

    /// <summary>
    /// Serialize a schema definition to YAML string.
    /// Delegates to SchemaYamlSerializer.
    /// </summary>
    /// <param name="schema">Schema to serialize.</param>
    /// <returns>YAML representation of the schema.</returns>
    public static string ToYaml(SchemaDefinition schema) => SchemaYamlSerializer.ToYaml(schema);

    /// <summary>
    /// Deserialize a schema definition from YAML string.
    /// Delegates to SchemaYamlSerializer.
    /// </summary>
    /// <param name="yaml">YAML string.</param>
    /// <returns>Deserialized schema definition.</returns>
    public static SchemaDefinition FromYaml(string yaml) => SchemaYamlSerializer.FromYaml(yaml);
}

/// <summary>
/// JSON converter for PortableType discriminated union.
/// </summary>
internal sealed class PortableTypeJsonConverter : JsonConverter<PortableType>
{
    /// <inheritdoc />
    public override PortableType? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeElement))
        {
            throw new JsonException("Missing 'type' property in PortableType");
        }

        var typeName = typeElement.GetString();

        return typeName switch
        {
            "TinyInt" => new TinyIntType(),
            "SmallInt" => new SmallIntType(),
            "Int" => new IntType(),
            "BigInt" => new BigIntType(),
            "Decimal" => new DecimalType(
                ReadInteger(element: root, propertyName: "precision", fallback: 18),
                ReadInteger(element: root, propertyName: "scale", fallback: 2)
            ),
            "Float" => new FloatType(),
            "Double" => new DoubleType(),
            "Money" => new MoneyType(),
            "SmallMoney" => new SmallMoneyType(),
            "Boolean" => new BooleanType(),
            "Char" => new CharType(ReadInteger(element: root, propertyName: "length", fallback: 1)),
            "VarChar" => new VarCharType(
                ReadInteger(element: root, propertyName: "length", fallback: 255)
            ),
            "NChar" => new NCharType(
                ReadInteger(element: root, propertyName: "length", fallback: 1)
            ),
            "NVarChar" => new NVarCharType(
                ReadInteger(element: root, propertyName: "length", fallback: 255)
            ),
            "Text" => new TextType(),
            "Binary" => new BinaryType(
                ReadInteger(element: root, propertyName: "length", fallback: 1)
            ),
            "VarBinary" => new VarBinaryType(
                ReadInteger(element: root, propertyName: "length", fallback: 255)
            ),
            "Blob" => new BlobType(),
            "Date" => new DateType(),
            "Time" => new TimeType(
                ReadInteger(element: root, propertyName: "precision", fallback: 7)
            ),
            "DateTime" => new DateTimeType(
                ReadInteger(element: root, propertyName: "precision", fallback: 3)
            ),
            "DateTimeOffset" => new DateTimeOffsetType(),
            "Uuid" => new UuidType(),
            "Json" => new JsonType(),
            "Xml" => new XmlType(),
            "RowVersion" => new RowVersionType(),
            "Geometry" => new GeometryType(
                root.TryGetProperty("srid", out var gs) ? gs.GetInt32() : null
            ),
            "Geography" => new GeographyType(
                ReadInteger(element: root, propertyName: "srid", fallback: 4326)
            ),
            "Enum" => new EnumType(
                root.TryGetProperty("name", out var en) ? en.GetString() ?? "enum" : "enum",
                root.TryGetProperty("values", out var ev)
                    ? ev.EnumerateArray().Select(e => e.GetString()!).ToArray()
                    : []
            ),
            _ => new TextType(),
        };
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        PortableType value,
        JsonSerializerOptions options
    )
    {
        writer.WriteStartObject();

        writer.WriteString(propertyName: "type", value: SerializedTypeName(value: value));

        switch (value)
        {
            case DecimalType d:
                writer.WriteNumber("precision", d.Precision);
                writer.WriteNumber("scale", d.Scale);
                break;
            case CharType c:
                writer.WriteNumber("length", c.Length);
                break;
            case VarCharType v:
                writer.WriteNumber("length", v.MaxLength);
                break;
            case NCharType nc:
                writer.WriteNumber("length", nc.Length);
                break;
            case NVarCharType nv:
                writer.WriteNumber("length", nv.MaxLength);
                break;
            case BinaryType b:
                writer.WriteNumber("length", b.Length);
                break;
            case VarBinaryType vb:
                writer.WriteNumber("length", vb.MaxLength);
                break;
            case TimeType t:
                writer.WriteNumber("precision", t.Precision);
                break;
            case DateTimeType dt:
                writer.WriteNumber("precision", dt.Precision);
                break;
            case GeometryType g:
                if (g.Srid.HasValue)
                {
                    writer.WriteNumber("srid", g.Srid.Value);
                }
                break;
            case GeographyType geo:
                writer.WriteNumber("srid", geo.Srid);
                break;
            case EnumType e:
                writer.WriteString("name", e.Name);
                writer.WriteStartArray("values");
                foreach (var val in e.Values)
                {
                    writer.WriteStringValue(val);
                }
                writer.WriteEndArray();
                break;
        }

        writer.WriteEndObject();
    }

    // Implements [MIG-JSON-SHARED-PORTABLE-TYPE-FIELDS].
    private static string SerializedTypeName(PortableType value) =>
        value switch
        {
            TinyIntType => "TinyInt",
            SmallIntType => "SmallInt",
            IntType => "Int",
            BigIntType => "BigInt",
            DecimalType => "Decimal",
            FloatType => "Float",
            DoubleType => "Double",
            MoneyType => "Money",
            SmallMoneyType => "SmallMoney",
            BooleanType => "Boolean",
            CharType => "Char",
            VarCharType => "VarChar",
            NCharType => "NChar",
            NVarCharType => "NVarChar",
            TextType => "Text",
            BinaryType => "Binary",
            VarBinaryType => "VarBinary",
            BlobType => "Blob",
            DateType => "Date",
            TimeType => "Time",
            DateTimeType => "DateTime",
            DateTimeOffsetType => "DateTimeOffset",
            UuidType => "Uuid",
            JsonType => "Json",
            XmlType => "Xml",
            RowVersionType => "RowVersion",
            GeometryType => "Geometry",
            GeographyType => "Geography",
            EnumType => "Enum",
            _ => "Text",
        };

    private static int ReadInteger(JsonElement element, string propertyName, int fallback) =>
        element.TryGetProperty(propertyName: propertyName, value: out var property)
            ? property.GetInt32()
            : fallback;
}
