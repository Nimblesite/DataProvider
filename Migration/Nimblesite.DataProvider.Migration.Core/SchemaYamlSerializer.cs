namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Serializes and deserializes schema definitions to/from YAML.
/// Used for storing schema definitions as portable configuration files.
/// Implements [MIG-AOT-YAML]: read/write are fully hand-written and
/// reflection-free (Native AOT safe) via <see cref="SchemaYamlWriter" /> and
/// <see cref="SchemaYamlReader" />.
/// </summary>
public static class SchemaYamlSerializer
{
    /// <summary>
    /// Serialize a schema definition to YAML string.
    /// </summary>
    /// <param name="schema">Schema to serialize.</param>
    /// <returns>YAML representation of the schema.</returns>
    public static string ToYaml(SchemaDefinition schema)
    {
        ValidateSupportFunctionBodies(schema);
        return SchemaYamlWriter.Write(schema);
    }

    /// <summary>
    /// Deserialize a schema definition from YAML string.
    /// </summary>
    /// <param name="yaml">YAML string.</param>
    /// <returns>Deserialized schema definition.</returns>
    public static SchemaDefinition FromYaml(string yaml)
    {
        var schema = SchemaYamlReader.Read(yaml);
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
