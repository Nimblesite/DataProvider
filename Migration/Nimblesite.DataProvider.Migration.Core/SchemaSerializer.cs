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
                root.TryGetProperty("precision", out var p) ? p.GetInt32() : 18,
                root.TryGetProperty("scale", out var s) ? s.GetInt32() : 2
            ),
            "Float" => new FloatType(),
            "Double" => new DoubleType(),
            "Money" => new MoneyType(),
            "SmallMoney" => new SmallMoneyType(),
            "Boolean" => new BooleanType(),
            "Char" => new CharType(root.TryGetProperty("length", out var cl) ? cl.GetInt32() : 1),
            "VarChar" => new VarCharType(
                root.TryGetProperty("length", out var vl) ? vl.GetInt32() : 255
            ),
            "NChar" => new NCharType(
                root.TryGetProperty("length", out var ncl) ? ncl.GetInt32() : 1
            ),
            "NVarChar" => new NVarCharType(
                root.TryGetProperty("length", out var nvl) ? nvl.GetInt32() : 255
            ),
            "Text" => new TextType(),
            "Binary" => new BinaryType(
                root.TryGetProperty("length", out var bl) ? bl.GetInt32() : 1
            ),
            "VarBinary" => new VarBinaryType(
                root.TryGetProperty("length", out var vbl) ? vbl.GetInt32() : 255
            ),
            "Blob" => new BlobType(),
            "Date" => new DateType(),
            "Time" => new TimeType(
                root.TryGetProperty("precision", out var tp) ? tp.GetInt32() : 7
            ),
            "DateTime" => new DateTimeType(
                root.TryGetProperty("precision", out var dtp) ? dtp.GetInt32() : 3
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
                root.TryGetProperty("srid", out var ggs) ? ggs.GetInt32() : 4326
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

        switch (value)
        {
            case TinyIntType:
                writer.WriteString("type", "TinyInt");
                break;
            case SmallIntType:
                writer.WriteString("type", "SmallInt");
                break;
            case IntType:
                writer.WriteString("type", "Int");
                break;
            case BigIntType:
                writer.WriteString("type", "BigInt");
                break;
            case DecimalType d:
                writer.WriteString("type", "Decimal");
                writer.WriteNumber("precision", d.Precision);
                writer.WriteNumber("scale", d.Scale);
                break;
            case FloatType:
                writer.WriteString("type", "Float");
                break;
            case DoubleType:
                writer.WriteString("type", "Double");
                break;
            case MoneyType:
                writer.WriteString("type", "Money");
                break;
            case SmallMoneyType:
                writer.WriteString("type", "SmallMoney");
                break;
            case BooleanType:
                writer.WriteString("type", "Boolean");
                break;
            case CharType c:
                writer.WriteString("type", "Char");
                writer.WriteNumber("length", c.Length);
                break;
            case VarCharType v:
                writer.WriteString("type", "VarChar");
                writer.WriteNumber("length", v.MaxLength);
                break;
            case NCharType nc:
                writer.WriteString("type", "NChar");
                writer.WriteNumber("length", nc.Length);
                break;
            case NVarCharType nv:
                writer.WriteString("type", "NVarChar");
                writer.WriteNumber("length", nv.MaxLength);
                break;
            case TextType:
                writer.WriteString("type", "Text");
                break;
            case BinaryType b:
                writer.WriteString("type", "Binary");
                writer.WriteNumber("length", b.Length);
                break;
            case VarBinaryType vb:
                writer.WriteString("type", "VarBinary");
                writer.WriteNumber("length", vb.MaxLength);
                break;
            case BlobType:
                writer.WriteString("type", "Blob");
                break;
            case DateType:
                writer.WriteString("type", "Date");
                break;
            case TimeType t:
                writer.WriteString("type", "Time");
                writer.WriteNumber("precision", t.Precision);
                break;
            case DateTimeType dt:
                writer.WriteString("type", "DateTime");
                writer.WriteNumber("precision", dt.Precision);
                break;
            case DateTimeOffsetType:
                writer.WriteString("type", "DateTimeOffset");
                break;
            case UuidType:
                writer.WriteString("type", "Uuid");
                break;
            case JsonType:
                writer.WriteString("type", "Json");
                break;
            case XmlType:
                writer.WriteString("type", "Xml");
                break;
            case RowVersionType:
                writer.WriteString("type", "RowVersion");
                break;
            case GeometryType g:
                writer.WriteString("type", "Geometry");
                if (g.Srid.HasValue)
                {
                    writer.WriteNumber("srid", g.Srid.Value);
                }
                break;
            case GeographyType geo:
                writer.WriteString("type", "Geography");
                writer.WriteNumber("srid", geo.Srid);
                break;
            case EnumType e:
                writer.WriteString("type", "Enum");
                writer.WriteString("name", e.Name);
                writer.WriteStartArray("values");
                foreach (var val in e.Values)
                {
                    writer.WriteStringValue(val);
                }
                writer.WriteEndArray();
                break;
            default:
                writer.WriteString("type", "Text");
                break;
        }

        writer.WriteEndObject();
    }
}
