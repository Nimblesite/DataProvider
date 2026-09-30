using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// YAML type converter for the <see cref="TriggerTiming"/> and
/// <see cref="TriggerEvent"/> enums. Implements [MIG-TRIGGER-YAML]
/// (GitHub issue 82).
/// </summary>
internal sealed class TriggerEnumYamlConverter : IYamlTypeConverter
{
    /// <inheritdoc />
    public bool Accepts(Type type) => type == typeof(TriggerTiming) || type == typeof(TriggerEvent);

    /// <inheritdoc />
    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var scalar = parser.Consume<Scalar>();
        return type == typeof(TriggerTiming) ? ParseTiming(scalar.Value) : ParseEvent(scalar.Value);
    }

    /// <inheritdoc />
    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        emitter.Emit(new Scalar(value?.ToString() ?? string.Empty));
    }

    private static object ParseTiming(string value) =>
        value.ToUpperInvariant() switch
        {
            "AFTER" => TriggerTiming.After,
            _ => TriggerTiming.Before,
        };

    private static object ParseEvent(string value) =>
        value.ToUpperInvariant() switch
        {
            "UPDATE" => TriggerEvent.Update,
            "DELETE" => TriggerEvent.Delete,
            _ => TriggerEvent.Insert,
        };
}
