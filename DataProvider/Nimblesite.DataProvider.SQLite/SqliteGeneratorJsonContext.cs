using System.Text.Json.Serialization;

namespace Nimblesite.DataProvider.SQLite;

/// <summary>
/// Source-generated JSON metadata for the SQLite generator's configuration files.
/// Implements [MIG-AOT-JSON]: the Native AOT migrate binary compiles this assembly
/// for its SQLite parser, so its JSON reads must be reflection-free.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(SourceGeneratorDataProviderConfiguration))]
[JsonSerializable(typeof(GroupingConfig))]
internal sealed partial class SqliteGeneratorJsonContext : JsonSerializerContext { }
