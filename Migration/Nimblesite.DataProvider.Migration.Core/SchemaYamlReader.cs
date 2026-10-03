using System.Globalization;
using YamlDotNet.RepresentationModel;

namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Reflection-free, Native-AOT-safe deserializer that maps a YAML string to a
/// <see cref="SchemaDefinition"/> using only the YamlDotNet representation model.
/// </summary>
internal static class SchemaYamlReader
{
    /// <summary>Parse a YAML document into a <see cref="SchemaDefinition"/>.</summary>
    public static SchemaDefinition Read(string yaml)
    {
        var root = LoadRootMapping(yaml);
        if (root is null)
        {
            return new SchemaDefinition { Name = string.Empty, Tables = [] };
        }

        return new SchemaDefinition
        {
            Name = ReadString(root, "name") ?? string.Empty,
            Tables = ReadList(root, "tables", ReadTable),
            Roles = ReadList(root, "roles", ReadRole),
            Functions = ReadList(root, "functions", ReadFunction),
            Grants = ReadList(root, "grants", ReadGrant),
        };
    }

    private static YamlMappingNode? LoadRootMapping(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return null;
        }

        var stream = new YamlStream();
        using var reader = new StringReader(yaml);
        stream.Load(reader);

        if (stream.Documents.Count == 0)
        {
            return null;
        }

        return stream.Documents[0].RootNode as YamlMappingNode;
    }

    private static TableDefinition ReadTable(YamlNode node)
    {
        var map = AsMapping(node);
        return new TableDefinition
        {
            Schema = ReadString(map, "schema") ?? "public",
            Name = ReadString(map, "name") ?? string.Empty,
            Columns = ReadList(map, "columns", ReadColumn),
            Indexes = ReadList(map, "indexes", ReadIndex),
            ForeignKeys = ReadList(map, "foreignKeys", ReadForeignKey),
            PrimaryKey = ReadOptional(map, "primaryKey", ReadPrimaryKey),
            UniqueConstraints = ReadList(map, "uniqueConstraints", ReadUniqueConstraint),
            CheckConstraints = ReadList(map, "checkConstraints", ReadCheckConstraint),
            Comment = ReadString(map, "comment"),
            RowLevelSecurity = ReadOptional(map, "rowLevelSecurity", ReadRlsPolicySet),
            Triggers = ReadList(map, "triggers", ReadTrigger),
        };
    }

    private static TriggerDefinition ReadTrigger(YamlNode node)
    {
        var map = AsMapping(node);
        return new TriggerDefinition
        {
            Name = ReadString(map, "name") ?? string.Empty,
            Timing =
                ReadString(map, "timing")?.ToUpperInvariant() == "AFTER"
                    ? TriggerTiming.After
                    : TriggerTiming.Before,
            Events = ReadList(map, "events", ReadTriggerEvent),
            ForEachRow = ReadBool(map, "forEachRow") ?? true,
            RaiseWhenLql = ReadString(map, "raiseWhen"),
            ErrorMessage = ReadString(map, "errorMessage"),
        };
    }

    private static TriggerEvent ReadTriggerEvent(YamlNode node) =>
        (node as YamlScalarNode)?.Value?.ToUpperInvariant() switch
        {
            "UPDATE" => TriggerEvent.Update,
            "DELETE" => TriggerEvent.Delete,
            _ => TriggerEvent.Insert,
        };

    private static ColumnDefinition ReadColumn(YamlNode node)
    {
        var map = AsMapping(node);
        return new ColumnDefinition
        {
            Name = ReadString(map, "name") ?? string.Empty,
            Type = ReadPortableType(map, "type"),
            IsNullable = ReadBool(map, "isNullable") ?? true,
            DefaultValue = ReadString(map, "defaultValue"),
            DefaultLqlExpression = ReadString(map, "defaultLqlExpression"),
            IsIdentity = ReadBool(map, "isIdentity") ?? false,
            IdentitySeed = ReadLong(map, "identitySeed") ?? 1L,
            IdentityIncrement = ReadLong(map, "identityIncrement") ?? 1L,
            ComputedExpression = ReadString(map, "computedExpression"),
            IsComputedPersisted = ReadBool(map, "isComputedPersisted") ?? false,
            Collation = ReadString(map, "collation"),
            CheckConstraint = ReadString(map, "checkConstraint"),
            CheckConstraintName = ReadString(map, "checkConstraintName"),
            Comment = ReadString(map, "comment"),
        };
    }

    private static IndexDefinition ReadIndex(YamlNode node)
    {
        var map = AsMapping(node);
        return new IndexDefinition
        {
            Name = ReadString(map, "name") ?? string.Empty,
            Columns = ReadStringList(map, "columns"),
            Expressions = ReadStringList(map, "expressions"),
            IsUnique = ReadBool(map, "isUnique") ?? false,
            Filter = ReadString(map, "filter"),
        };
    }

    private static ForeignKeyDefinition ReadForeignKey(YamlNode node)
    {
        var map = AsMapping(node);
        return new ForeignKeyDefinition
        {
            Name = ReadString(map, "name"),
            Columns = ReadStringList(map, "columns"),
            ReferencedTable = ReadString(map, "referencedTable") ?? string.Empty,
            ReferencedSchema = ReadString(map, "referencedSchema") ?? "public",
            ReferencedColumns = ReadStringList(map, "referencedColumns"),
            OnDelete = ReadForeignKeyAction(map, "onDelete"),
            OnUpdate = ReadForeignKeyAction(map, "onUpdate"),
        };
    }

    private static PrimaryKeyDefinition ReadPrimaryKey(YamlNode node)
    {
        var map = AsMapping(node);
        return new PrimaryKeyDefinition
        {
            Name = ReadString(map, "name"),
            Columns = ReadStringList(map, "columns"),
        };
    }

    private static UniqueConstraintDefinition ReadUniqueConstraint(YamlNode node)
    {
        var map = AsMapping(node);
        return new UniqueConstraintDefinition
        {
            Name = ReadString(map, "name"),
            Columns = ReadStringList(map, "columns"),
        };
    }

    private static CheckConstraintDefinition ReadCheckConstraint(YamlNode node)
    {
        var map = AsMapping(node);
        return new CheckConstraintDefinition
        {
            Name = ReadString(map, "name") ?? string.Empty,
            Expression = ReadString(map, "expression") ?? string.Empty,
        };
    }

    private static RlsPolicySetDefinition ReadRlsPolicySet(YamlNode node)
    {
        var map = AsMapping(node);
        return new RlsPolicySetDefinition
        {
            Enabled = ReadBool(map, "enabled") ?? true,
            Policies = ReadList(map, "policies", ReadRlsPolicy),
            Forced = ReadBool(map, "forced") ?? false,
        };
    }

    private static RlsPolicyDefinition ReadRlsPolicy(YamlNode node)
    {
        var map = AsMapping(node);
        var operationsNode = FindValue(map, "operations");
        return new RlsPolicyDefinition
        {
            Name = ReadString(map, "name") ?? string.Empty,
            IsPermissive = ReadBool(map, "permissive") ?? true,
            Operations = operationsNode is null
                ? [RlsOperation.All]
                : ReadSequenceItems(operationsNode, ReadRlsOperationNode),
            Roles = ReadStringList(map, "roles"),
            UsingLql = ReadString(map, "using"),
            WithCheckLql = ReadString(map, "withCheck"),
            UsingSql = ReadString(map, "usingSql"),
            WithCheckSql = ReadString(map, "withCheckSql"),
        };
    }

    private static PostgresRoleDefinition ReadRole(YamlNode node)
    {
        var map = AsMapping(node);
        return new PostgresRoleDefinition
        {
            Name = ReadString(map, "name") ?? string.Empty,
            Login = ReadBool(map, "login") ?? false,
            BypassRls = ReadBool(map, "bypassRls") ?? false,
            GrantTo = ReadStringList(map, "grantTo"),
        };
    }

    private static PostgresFunctionDefinition ReadFunction(YamlNode node)
    {
        var map = AsMapping(node);
        return new PostgresFunctionDefinition
        {
            Schema = ReadString(map, "schema") ?? "public",
            Name = ReadString(map, "name") ?? string.Empty,
            Arguments = ReadList(map, "arguments", ReadFunctionArgument),
            Returns = ReadString(map, "returns") ?? "void",
            Language = ReadString(map, "language") ?? "sql",
            Volatility = ReadString(map, "volatility") ?? "stable",
            SecurityDefiner = ReadBool(map, "securityDefiner") ?? false,
            Body = ReadString(map, "body") ?? string.Empty,
            BodyLql = ReadString(map, "bodyLql"),
            ExecuteRoles = ReadStringList(map, "executeRoles"),
            RevokePublicExecute = ReadBool(map, "revokePublicExecute") ?? true,
        };
    }

    private static PostgresFunctionArgumentDefinition ReadFunctionArgument(YamlNode node)
    {
        var map = AsMapping(node);
        return new PostgresFunctionArgumentDefinition
        {
            Name = ReadString(map, "name") ?? string.Empty,
            Type = ReadString(map, "type") ?? string.Empty,
        };
    }

    private static PostgresGrantDefinition ReadGrant(YamlNode node)
    {
        var map = AsMapping(node);
        return new PostgresGrantDefinition
        {
            Schema = ReadString(map, "schema") ?? "public",
            Target = ReadGrantTarget(map, "target"),
            ObjectName = ReadString(map, "objectName"),
            Privileges = ReadStringList(map, "privileges"),
            Roles = ReadStringList(map, "roles"),
            RunAs = ReadString(map, "runAs"),
        };
    }

    private static PortableType ReadPortableType(YamlMappingNode map, string key) =>
        FindValue(map, key) is YamlScalarNode s
            ? SchemaYamlScalars.ParseType(s.Value ?? string.Empty)
            : new TextType();

    private static ForeignKeyAction ReadForeignKeyAction(YamlMappingNode map, string key) =>
        FindValue(map, key) is YamlScalarNode { Value: { } v }
            ? SchemaYamlScalars.ParseForeignKeyAction(v)
            : ForeignKeyAction.NoAction;

    private static RlsOperation ReadRlsOperationNode(YamlNode node) =>
        node is YamlScalarNode { Value: { } v }
            ? SchemaYamlScalars.ParseRlsOperation(v)
            : RlsOperation.All;

    private static PostgresGrantTarget ReadGrantTarget(YamlMappingNode map, string key) =>
        FindValue(map, key) is YamlScalarNode { Value: { } v }
            ? SchemaYamlScalars.ParseGrantTarget(v)
            : PostgresGrantTarget.Table;

    private static YamlMappingNode AsMapping(YamlNode node) =>
        node as YamlMappingNode ?? new YamlMappingNode();

    private static YamlNode? FindValue(YamlMappingNode map, string key) =>
        map.Children.TryGetValue(new YamlScalarNode(key), out var value) ? value : null;

    private static string? ReadString(YamlMappingNode map, string key) =>
        FindValue(map, key) is YamlScalarNode { Value: { } v } ? v : null;

    private static bool? ReadBool(YamlMappingNode map, string key) =>
        FindValue(map, key) is YamlScalarNode { Value: { } v }
            ? v.Trim().ToUpperInvariant() switch
            {
                "TRUE" or "YES" or "ON" or "1" => true,
                "FALSE" or "NO" or "OFF" or "0" => false,
                _ => bool.TryParse(v, out var b) ? b : (bool?)null,
            }
            : null;

    private static long? ReadLong(YamlMappingNode map, string key) =>
        FindValue(map, key) is YamlScalarNode { Value: { } v }
        && long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : (long?)null;

    private static System.Collections.Immutable.ImmutableArray<T> ReadList<T>(
        YamlMappingNode map,
        string key,
        Func<YamlNode, T> readItem
    ) => FindValue(map, key) is YamlSequenceNode seq ? ReadSequenceItems(seq, readItem) : [];

    private static System.Collections.Immutable.ImmutableArray<T> ReadSequenceItems<T>(
        YamlNode node,
        Func<YamlNode, T> readItem
    )
    {
        if (node is not YamlSequenceNode seq)
        {
            return [];
        }

        var builder = System.Collections.Immutable.ImmutableArray.CreateBuilder<T>(
            seq.Children.Count
        );
        foreach (var child in seq.Children)
        {
            builder.Add(readItem(child));
        }

        return builder.ToImmutable();
    }

    private static System.Collections.Immutable.ImmutableArray<string> ReadStringList(
        YamlMappingNode map,
        string key
    ) =>
        FindValue(map, key) is YamlSequenceNode seq
            ? ReadSequenceItems(seq, n => n is YamlScalarNode { Value: { } v } ? v : string.Empty)
            : [];

    private static T? ReadOptional<T>(YamlMappingNode map, string key, Func<YamlNode, T> read)
        where T : class => FindValue(map, key) is { } node ? read(node) : null;
}
