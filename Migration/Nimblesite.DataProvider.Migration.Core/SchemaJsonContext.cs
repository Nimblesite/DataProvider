using System.Text.Json.Serialization;

namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Source-generated <see cref="System.Text.Json.Serialization.JsonSerializerContext" />
/// for <see cref="SchemaDefinition" /> and its graph. Implements [MIG-AOT-JSON]:
/// provides reflection-free JSON metadata so <see cref="SchemaSerializer" /> is
/// Native AOT and trim compatible. <see cref="PortableType" /> is intentionally
/// not listed — it is handled by <see cref="PortableTypeJsonConverter" /> registered
/// on the serializer options.
/// </summary>
[JsonSerializable(typeof(SchemaDefinition))]
internal sealed partial class SchemaJsonContext : JsonSerializerContext { }
