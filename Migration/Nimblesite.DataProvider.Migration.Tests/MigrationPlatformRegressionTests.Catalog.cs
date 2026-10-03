using System.Collections.Immutable;
using System.Data.Common;
using System.Globalization;

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial record MigrationPlatformRegressionTests
{
    // Implements [MIG-DIFF-FOREIGN-KEY] and [MIG-DIFF-DESTRUCTIVE].
    private static void AssertTableExists(MigrationTarget target, string table, bool expected) =>
        Assert.Equal(expected, CatalogObjectExists(target, table, column: null));

    // Implements [MIG-DIFF-FOREIGN-KEY] and [MIG-DIFF-DESTRUCTIVE].
    private static void AssertColumnExists(
        MigrationTarget target,
        string table,
        string column,
        bool expected
    ) => Assert.Equal(expected, CatalogObjectExists(target, table, column));

    private static bool CatalogObjectExists(MigrationTarget target, string table, string? column)
    {
        using var command = target.Connection.CreateCommand();
        command.CommandText = (target.Provider, column) switch
        {
            ("sqlite", null) =>
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @table",
            ("sqlite", _) => "SELECT COUNT(*) FROM pragma_table_info(@table) WHERE name = @column",
            (_, null) =>
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = @schema AND table_name = @table",
            _ =>
                "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = @schema AND table_name = @table AND column_name = @column",
        };
        if (target.Provider != "sqlite")
        {
            AddParameter(command, "@schema", target.Schema);
        }
        AddParameter(command, "@table", table);
        if (column is not null)
        {
            AddParameter(command, "@column", column);
        }
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
    }

    private static bool ColumnIsNullable(MigrationTarget target, string table, string column)
    {
        using var command = target.Connection.CreateCommand();
        if (target.Provider == "sqlite")
        {
            command.CommandText = $"PRAGMA table_info([{table}])";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (reader.GetString(1) == column)
                {
                    return reader.GetInt32(3) == 0;
                }
            }
            Assert.Fail($"Missing {table}.{column} in {target.Provider}");
            return false;
        }
        command.CommandText =
            "SELECT is_nullable FROM information_schema.columns WHERE table_schema = @schema AND table_name = @table AND column_name = @column";
        AddParameter(command, "@schema", target.Schema);
        AddParameter(command, "@table", table);
        AddParameter(command, "@column", column);
        var value = command.ExecuteScalar();
        Assert.NotNull(value);
        return string.Equals(value.ToString(), "YES", StringComparison.OrdinalIgnoreCase);
    }

    private static ImmutableArray<ForeignKeySnapshot> ReadForeignKeys(
        MigrationTarget target,
        string table
    )
    {
        using var command = target.Connection.CreateCommand();
        command.CommandText = target.Provider switch
        {
            "sqlite" => $"PRAGMA foreign_key_list([{table}])",
            "postgres" => """
                SELECT kcu.column_name, ccu.table_name, ccu.column_name, rc.delete_rule
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu
                  ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
                JOIN information_schema.constraint_column_usage ccu
                  ON tc.constraint_name = ccu.constraint_name AND tc.table_schema = ccu.table_schema
                JOIN information_schema.referential_constraints rc
                  ON tc.constraint_name = rc.constraint_name AND tc.table_schema = rc.constraint_schema
                WHERE tc.table_schema = @schema AND tc.table_name = @table AND tc.constraint_type = 'FOREIGN KEY'
                """,
            "sqlserver" => """
                SELECT source_column.name, referenced_table.name, referenced_column.name,
                       fk.delete_referential_action_desc, fk.is_disabled, fk.is_not_trusted,
                       referenced_schema.name
                FROM sys.foreign_keys fk
                JOIN sys.foreign_key_columns link ON link.constraint_object_id = fk.object_id
                JOIN sys.tables source_table ON source_table.object_id = fk.parent_object_id
                JOIN sys.schemas source_schema ON source_schema.schema_id = source_table.schema_id
                JOIN sys.columns source_column ON source_column.object_id = source_table.object_id AND source_column.column_id = link.parent_column_id
                JOIN sys.tables referenced_table ON referenced_table.object_id = fk.referenced_object_id
                JOIN sys.schemas referenced_schema ON referenced_schema.schema_id = referenced_table.schema_id
                JOIN sys.columns referenced_column ON referenced_column.object_id = referenced_table.object_id AND referenced_column.column_id = link.referenced_column_id
                WHERE source_schema.name = @schema AND source_table.name = @table
                """,
            _ => string.Empty,
        };
        if (target.Provider != "sqlite")
        {
            AddParameter(command, "@schema", target.Schema);
            AddParameter(command, "@table", table);
        }
        using var reader = command.ExecuteReader();
        var foreignKeys = ImmutableArray.CreateBuilder<ForeignKeySnapshot>();
        while (reader.Read())
        {
            if (target.Provider == "sqlserver")
            {
                Assert.False(reader.GetBoolean(4), "Foreign key is disabled");
                Assert.False(reader.GetBoolean(5), "Foreign key is not trusted");
                Assert.Equal(target.Schema, reader.GetString(6));
            }
            foreignKeys.Add(
                target.Provider == "sqlite"
                    ? new ForeignKeySnapshot(
                        reader.GetString(3),
                        reader.GetString(2),
                        reader.GetString(4),
                        reader.GetString(6)
                    )
                    : new ForeignKeySnapshot(
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3)
                    )
            );
        }
        return foreignKeys.ToImmutable();
    }

    // Implements [MIG-DIFF-FOREIGN-KEY].
    private static void AssertEndUserForeignKey(MigrationTarget target)
    {
        var foreignKey = Assert.Single(ReadForeignKeys(target, "usage_events"));
        Assert.Equal("end_user_id", foreignKey.Column);
        Assert.Equal("tenant_end_users", foreignKey.ReferencedTable);
        Assert.Equal("id", foreignKey.ReferencedColumn);
        Assert.Equal("SET NULL", NormalizedDeleteAction(foreignKey.OnDelete));
        Assert.Empty(ReadForeignKeys(target, "tenant_end_users"));
    }

    // Implements [MIG-DIFF-DESTRUCTIVE].
    private static void AssertCleanupForeignKeys(
        MigrationTarget target,
        bool includeTenantForeignKey,
        bool includeProjectForeignKey
    )
    {
        var eventKeys = ReadForeignKeys(target, "usage_events");
        Assert.Equal(
            (includeTenantForeignKey ? 1 : 0) + (includeProjectForeignKey ? 1 : 0),
            eventKeys.Length
        );
        if (includeTenantForeignKey)
        {
            Assert.Contains(eventKeys, fk => MatchesCleanupForeignKey(fk, "tenant_id", "tenants"));
        }
        else
        {
            Assert.DoesNotContain(eventKeys, fk => fk.Column == "tenant_id");
        }
        if (includeProjectForeignKey)
        {
            Assert.Contains(
                eventKeys,
                fk => MatchesCleanupForeignKey(fk, "project_id", "projects")
            );
        }
        else
        {
            Assert.DoesNotContain(eventKeys, fk => fk.Column == "project_id");
        }
        var auditKey = Assert.Single(ReadForeignKeys(target, "audit_events"));
        Assert.True(MatchesCleanupForeignKey(auditKey, "tenant_id", "tenants"));
        Assert.Empty(ReadForeignKeys(target, "tenants"));
        Assert.Empty(ReadForeignKeys(target, "projects"));
    }

    private static bool MatchesCleanupForeignKey(
        ForeignKeySnapshot fk,
        string column,
        string table
    ) =>
        fk.Column == column
        && fk.ReferencedTable == table
        && fk.ReferencedColumn == "id"
        && NormalizedDeleteAction(fk.OnDelete) == "CASCADE";

    private static string NormalizedDeleteAction(string action) =>
        action.Replace('_', ' ').ToUpperInvariant();

    private static void AddParameter(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record ForeignKeySnapshot(
        string Column,
        string ReferencedTable,
        string ReferencedColumn,
        string OnDelete
    );
}
