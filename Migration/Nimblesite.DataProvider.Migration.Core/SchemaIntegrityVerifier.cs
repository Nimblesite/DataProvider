using System.Text;

namespace Nimblesite.DataProvider.Migration.Core;

/// <summary>
/// Verifies that an inspected live schema satisfies a declared schema.
/// </summary>
public static class SchemaIntegrityVerifier
{
    /// <summary>
    /// Compare the live schema against the desired schema and return every drift mismatch.
    /// </summary>
    public static SchemaIntegrityResult Verify(
        SchemaDefinition live,
        SchemaDefinition desired,
        bool includeSupportObjects = true,
        bool includeRls = true,
        ILogger? logger = null
    )
    {
        try
        {
            var mismatches = ImmutableArray.CreateBuilder<string>();
            VerifyTables(live: live, desired: desired, includeRls: includeRls, mismatches);
            if (includeSupportObjects)
            {
                VerifySupportObjects(live: live, desired: desired, mismatches: mismatches);
            }
            return new SchemaIntegrityResult.Ok<ImmutableArray<string>, MigrationError>(
                mismatches.ToImmutable()
            );
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Schema integrity verification failed");
            return new SchemaIntegrityResult.Error<ImmutableArray<string>, MigrationError>(
                MigrationError.FromException(ex)
            );
        }
    }

    private static void VerifyTables(
        SchemaDefinition live,
        SchemaDefinition desired,
        bool includeRls,
        ImmutableArray<string>.Builder mismatches
    )
    {
        foreach (var expected in desired.Tables)
        {
            var actual = FindTable(schema: live, expected: expected);
            if (actual is null)
            {
                mismatches.Add($"{TablePath(table: expected)}: missing table");
                continue;
            }
            VerifyTable(actual: actual, expected: expected, includeRls: includeRls, mismatches);
        }
    }

    private static void VerifyTable(
        TableDefinition actual,
        TableDefinition expected,
        bool includeRls,
        ImmutableArray<string>.Builder mismatches
    )
    {
        VerifyColumns(actual: actual, expected: expected, mismatches: mismatches);
        VerifyPrimaryKey(actual: actual, expected: expected, mismatches: mismatches);
        VerifyForeignKeys(actual: actual, expected: expected, mismatches: mismatches);
        VerifyUniqueConstraints(actual: actual, expected: expected, mismatches: mismatches);
        VerifyIndexes(actual: actual, expected: expected, mismatches: mismatches);
        VerifyCheckConstraints(actual: actual, expected: expected, mismatches: mismatches);
        if (includeRls)
        {
            VerifyRls(actual: actual, expected: expected, mismatches: mismatches);
        }
    }

    private static void VerifyColumns(
        TableDefinition actual,
        TableDefinition expected,
        ImmutableArray<string>.Builder mismatches
    )
    {
        foreach (var expectedColumn in expected.Columns)
        {
            var actualColumn = FindColumn(table: actual, name: expectedColumn.Name);
            if (actualColumn is null)
            {
                mismatches.Add(
                    $"{TablePath(table: expected)}.{expectedColumn.Name}: missing column"
                );
                continue;
            }
            VerifyColumn(
                actual: actualColumn,
                expected: expectedColumn,
                path: $"{TablePath(table: expected)}.{expectedColumn.Name}",
                isSqlite: IsSqliteSchema(actual.Schema),
                mismatches: mismatches
            );
        }
    }

    private static void VerifyColumn(
        ColumnDefinition actual,
        ColumnDefinition expected,
        string path,
        bool isSqlite,
        ImmutableArray<string>.Builder mismatches
    )
    {
        AddIf(
            condition: !SamePortableType(actual.Type, expected.Type, isSqlite),
            $"{path}: type expected {expected.Type} but found {actual.Type}",
            mismatches
        );
        AddIf(
            condition: actual.IsNullable != expected.IsNullable,
            $"{path}: nullability expected {Nullability(expected)} but found {Nullability(actual)}",
            mismatches
        );
        AddIf(
            condition: actual.IsIdentity != expected.IsIdentity,
            $"{path}: identity expected {expected.IsIdentity} but found {actual.IsIdentity}",
            mismatches
        );
        VerifyDefault(actual: actual, expected: expected, path: path, mismatches: mismatches);
    }

    private static void VerifyDefault(
        ColumnDefinition actual,
        ColumnDefinition expected,
        string path,
        ImmutableArray<string>.Builder mismatches
    )
    {
        if (
            expected.DefaultValue is not null
            && !SameSql(actual.DefaultValue, expected.DefaultValue)
        )
        {
            mismatches.Add(
                $"{path}: default expected {expected.DefaultValue} but found {actual.DefaultValue ?? "<none>"}"
            );
        }
        if (
            expected.DefaultLqlExpression is not null
            && string.IsNullOrWhiteSpace(actual.DefaultValue)
        )
        {
            mismatches.Add(
                $"{path}: default expected {expected.DefaultLqlExpression} but found <none>"
            );
        }
    }

    private static void VerifyPrimaryKey(
        TableDefinition actual,
        TableDefinition expected,
        ImmutableArray<string>.Builder mismatches
    )
    {
        if (expected.PrimaryKey is null)
        {
            return;
        }
        if (actual.PrimaryKey is null)
        {
            mismatches.Add($"{TablePath(table: expected)}: missing primary key");
            return;
        }
        AddIf(
            condition: !SameIdentifiers(actual.PrimaryKey.Columns, expected.PrimaryKey.Columns),
            $"{TablePath(table: expected)}: primary key columns expected ({Format(expected.PrimaryKey.Columns)}) but found ({Format(actual.PrimaryKey.Columns)})",
            mismatches
        );
    }

    private static void VerifyForeignKeys(
        TableDefinition actual,
        TableDefinition expected,
        ImmutableArray<string>.Builder mismatches
    )
    {
        foreach (var expectedFk in expected.ForeignKeys)
        {
            var actualFk = FindForeignKey(table: actual, expected: expectedFk);
            if (actualFk is null)
            {
                mismatches.Add(
                    $"{TablePath(table: expected)}: missing foreign key {ForeignKeyName(expected.Name, expectedFk)} on ({Format(expectedFk.Columns)})"
                );
                continue;
            }
            VerifyForeignKey(
                actual: actualFk,
                expected: expectedFk,
                tablePath: TablePath(expected),
                mismatches
            );
        }
    }

    private static void VerifyForeignKey(
        ForeignKeyDefinition actual,
        ForeignKeyDefinition expected,
        string tablePath,
        ImmutableArray<string>.Builder mismatches
    )
    {
        var name = ForeignKeyName(tablePath, expected);
        AddIf(
            !SameIdentifiers(actual.Columns, expected.Columns),
            $"{tablePath}: foreign key {name} columns drifted",
            mismatches
        );
        AddIf(
            !SameIdentifier(actual.ReferencedTable, expected.ReferencedTable),
            $"{tablePath}: foreign key {name} referenced table drifted",
            mismatches
        );
        AddIf(
            !SameIdentifiers(actual.ReferencedColumns, expected.ReferencedColumns),
            $"{tablePath}: foreign key {name} referenced columns drifted",
            mismatches
        );
        AddIf(
            actual.OnDelete != expected.OnDelete,
            $"{tablePath}: foreign key {name} on delete expected {expected.OnDelete} but found {actual.OnDelete}",
            mismatches
        );
        AddIf(
            actual.OnUpdate != expected.OnUpdate,
            $"{tablePath}: foreign key {name} on update expected {expected.OnUpdate} but found {actual.OnUpdate}",
            mismatches
        );
    }

    private static void VerifyUniqueConstraints(
        TableDefinition actual,
        TableDefinition expected,
        ImmutableArray<string>.Builder mismatches
    )
    {
        foreach (var expectedUnique in expected.UniqueConstraints)
        {
            var name = UniqueConstraintName(tableName: expected.Name, unique: expectedUnique);
            var actualUnique = actual.UniqueConstraints.FirstOrDefault(uc =>
                SameIdentifier(UniqueConstraintName(actual.Name, uc), name)
            );
            if (actualUnique is null)
            {
                mismatches.Add(
                    $"{TablePath(table: expected)}: missing unique constraint {name} on ({Format(expectedUnique.Columns)})"
                );
                continue;
            }
            AddIf(
                condition: !SameIdentifiers(actualUnique.Columns, expectedUnique.Columns),
                $"{TablePath(table: expected)}: unique constraint {name} columns expected ({Format(expectedUnique.Columns)}) but found ({Format(actualUnique.Columns)})",
                mismatches
            );
        }
    }

    private static void VerifyIndexes(
        TableDefinition actual,
        TableDefinition expected,
        ImmutableArray<string>.Builder mismatches
    )
    {
        foreach (var expectedIndex in expected.Indexes)
        {
            var actualIndex = actual.Indexes.FirstOrDefault(i =>
                SameIdentifier(i.Name, expectedIndex.Name)
            );
            if (actualIndex is null)
            {
                mismatches.Add($"{TablePath(table: expected)}: missing index {expectedIndex.Name}");
                continue;
            }
            VerifyIndex(
                actual: actualIndex,
                expected: expectedIndex,
                tablePath: TablePath(expected),
                mismatches
            );
        }
    }

    private static void VerifyIndex(
        IndexDefinition actual,
        IndexDefinition expected,
        string tablePath,
        ImmutableArray<string>.Builder mismatches
    )
    {
        AddIf(
            actual.IsUnique != expected.IsUnique,
            $"{tablePath}: index {expected.Name} uniqueness drifted",
            mismatches
        );
        AddIf(
            !SameIdentifiers(actual.Columns, expected.Columns),
            $"{tablePath}: index {expected.Name} columns drifted",
            mismatches
        );
        AddIf(
            !SameSqlSequence(actual.Expressions, expected.Expressions),
            $"{tablePath}: index {expected.Name} expressions drifted",
            mismatches
        );
        AddIf(
            !SameSql(actual.Filter, expected.Filter),
            $"{tablePath}: index {expected.Name} filter drifted",
            mismatches
        );
    }

    private static void VerifyCheckConstraints(
        TableDefinition actual,
        TableDefinition expected,
        ImmutableArray<string>.Builder mismatches
    )
    {
        foreach (var expectedCheck in expected.CheckConstraints)
        {
            var actualCheck = actual.CheckConstraints.FirstOrDefault(c =>
                SameIdentifier(c.Name, expectedCheck.Name)
            );
            if (actualCheck is null)
            {
                mismatches.Add(
                    $"{TablePath(table: expected)}: missing check constraint {expectedCheck.Name}"
                );
                continue;
            }
            AddIf(
                !SameSql(actualCheck.Expression, expectedCheck.Expression),
                $"{TablePath(table: expected)}: check constraint {expectedCheck.Name} expression drifted",
                mismatches
            );
        }
        VerifyColumnCheckConstraints(actual: actual, expected: expected, mismatches: mismatches);
    }

    private static void VerifyColumnCheckConstraints(
        TableDefinition actual,
        TableDefinition expected,
        ImmutableArray<string>.Builder mismatches
    )
    {
        foreach (var expectedColumn in expected.Columns.Where(c => c.CheckConstraint is not null))
        {
            var actualColumn = FindColumn(table: actual, name: expectedColumn.Name);
            if (actualColumn?.CheckConstraint is null)
            {
                mismatches.Add(
                    $"{TablePath(table: expected)}.{expectedColumn.Name}: missing check constraint"
                );
            }
        }
    }

    private static void VerifyRls(
        TableDefinition actual,
        TableDefinition expected,
        ImmutableArray<string>.Builder mismatches
    )
    {
        if (expected.RowLevelSecurity is null)
        {
            return;
        }
        AddIf(
            actual.RowLevelSecurity?.Enabled != expected.RowLevelSecurity.Enabled,
            $"{TablePath(table: expected)}: row-level security expected {expected.RowLevelSecurity.Enabled} but found {actual.RowLevelSecurity?.Enabled ?? false}",
            mismatches
        );
        AddIf(
            actual.RowLevelSecurity?.Forced != expected.RowLevelSecurity.Forced,
            $"{TablePath(table: expected)}: forced row-level security expected {expected.RowLevelSecurity.Forced} but found {actual.RowLevelSecurity?.Forced ?? false}",
            mismatches
        );
        VerifyRlsPolicies(actual: actual, expected: expected, mismatches: mismatches);
    }

    private static void VerifyRlsPolicies(
        TableDefinition actual,
        TableDefinition expected,
        ImmutableArray<string>.Builder mismatches
    )
    {
        foreach (var expectedPolicy in expected.RowLevelSecurity?.Policies ?? [])
        {
            var actualPolicy = actual.RowLevelSecurity?.Policies.FirstOrDefault(p =>
                SameIdentifier(p.Name, expectedPolicy.Name)
            );
            if (actualPolicy is null)
            {
                mismatches.Add(
                    $"{TablePath(table: expected)}: missing row-level security policy {expectedPolicy.Name}"
                );
            }
        }
    }

    private static void VerifySupportObjects(
        SchemaDefinition live,
        SchemaDefinition desired,
        ImmutableArray<string>.Builder mismatches
    )
    {
        VerifyRoles(live: live, desired: desired, mismatches: mismatches);
        VerifyFunctions(live: live, desired: desired, mismatches: mismatches);
        VerifyGrants(live: live, desired: desired, mismatches: mismatches);
    }

    private static void VerifyRoles(
        SchemaDefinition live,
        SchemaDefinition desired,
        ImmutableArray<string>.Builder mismatches
    )
    {
        foreach (var expectedRole in desired.Roles)
        {
            var actualRole = live.Roles.FirstOrDefault(r =>
                SameIdentifier(r.Name, expectedRole.Name)
            );
            if (actualRole is null)
            {
                mismatches.Add($"role {expectedRole.Name}: missing role");
                continue;
            }
            AddIf(
                actualRole.Login != expectedRole.Login,
                $"role {expectedRole.Name}: login drifted",
                mismatches
            );
            AddIf(
                actualRole.BypassRls != expectedRole.BypassRls,
                $"role {expectedRole.Name}: bypassRls drifted",
                mismatches
            );
        }
    }

    private static void VerifyFunctions(
        SchemaDefinition live,
        SchemaDefinition desired,
        ImmutableArray<string>.Builder mismatches
    )
    {
        foreach (var expectedFunction in desired.Functions)
        {
            var actualFunction = FindFunction(schema: live, expected: expectedFunction);
            if (actualFunction is null)
            {
                mismatches.Add(
                    $"{expectedFunction.Schema}.{expectedFunction.Name}: missing function"
                );
            }
        }
    }

    private static void VerifyGrants(
        SchemaDefinition live,
        SchemaDefinition desired,
        ImmutableArray<string>.Builder mismatches
    )
    {
        foreach (var expectedGrant in desired.Grants)
        {
            var actualGrant = live.Grants.FirstOrDefault(g =>
                SameGrant(actual: g, expected: expectedGrant)
            );
            if (actualGrant is null)
            {
                mismatches.Add(
                    $"grant {expectedGrant.Target} {expectedGrant.ObjectName ?? expectedGrant.Schema}: missing grant"
                );
            }
        }
    }

    private static TableDefinition? FindTable(SchemaDefinition schema, TableDefinition expected) =>
        schema.Tables.FirstOrDefault(t =>
            SameSchema(t.Schema, expected.Schema) && SameIdentifier(t.Name, expected.Name)
        );

    private static ColumnDefinition? FindColumn(TableDefinition table, string name) =>
        table.Columns.FirstOrDefault(c => SameIdentifier(c.Name, name));

    private static ForeignKeyDefinition? FindForeignKey(
        TableDefinition table,
        ForeignKeyDefinition expected
    )
    {
        if (expected.Name is not null)
        {
            return table.ForeignKeys.FirstOrDefault(fk =>
                SameIdentifier(fk.Name ?? string.Empty, expected.Name)
            );
        }
        return table.ForeignKeys.FirstOrDefault(fk =>
            SameIdentifiers(fk.Columns, expected.Columns)
        );
    }

    private static PostgresFunctionDefinition? FindFunction(
        SchemaDefinition schema,
        PostgresFunctionDefinition expected
    ) =>
        schema.Functions.FirstOrDefault(f =>
            SameIdentifier(f.Schema, expected.Schema)
            && SameIdentifier(f.Name, expected.Name)
            && SameIdentifiers(
                f.Arguments.Select(a => a.Type).ToArray(),
                expected.Arguments.Select(a => a.Type).ToArray()
            )
        );

    private static bool SameGrant(
        PostgresGrantDefinition actual,
        PostgresGrantDefinition expected
    ) =>
        actual.Target == expected.Target
        && SameIdentifier(actual.Schema, expected.Schema)
        && SameIdentifier(actual.ObjectName ?? string.Empty, expected.ObjectName ?? string.Empty)
        && SameIdentifiers(actual.Privileges, expected.Privileges)
        && SameIdentifiers(actual.Roles, expected.Roles);

    private static bool SameIdentifiers(
        IReadOnlyList<string> actual,
        IReadOnlyList<string> expected
    ) =>
        actual.Count == expected.Count
        && actual.Zip(expected).All(pair => SameIdentifier(pair.First, pair.Second));

    private static bool SameIdentifier(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static bool SameSchema(string actual, string expected) =>
        SameIdentifier(actual: actual, expected: expected)
        || (IsDefaultSchema(actual) && IsDefaultSchema(expected));

    private static bool IsDefaultSchema(string schema) =>
        string.IsNullOrWhiteSpace(schema)
        || SameIdentifier(actual: schema, expected: "main")
        || SameIdentifier(actual: schema, expected: "public");

    private static bool IsSqliteSchema(string schema) =>
        SameIdentifier(actual: schema, expected: "main");

    private static bool SamePortableType(PortableType actual, PortableType expected, bool isSqlite)
    {
        if (Equals(actual, expected))
        {
            return true;
        }
        return isSqlite && IsIntegerType(actual) && IsIntegerType(expected);
    }

    private static bool IsIntegerType(PortableType type) =>
        type is TinyIntType or SmallIntType or IntType or BigIntType;

    private static bool SameSqlSequence(
        IReadOnlyList<string> actual,
        IReadOnlyList<string> expected
    ) =>
        actual.Count == expected.Count
        && actual.Zip(expected).All(pair => SameSql(pair.First, pair.Second));

    private static bool SameSql(string? actual, string? expected) =>
        string.Equals(NormalizeSql(actual), NormalizeSql(expected), StringComparison.Ordinal);

    private static string NormalizeSql(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }
        var builder = new StringBuilder();
        AppendNormalizedSql(value: value, builder: builder);
        return builder.ToString().Trim().TrimEnd(';').Trim();
    }

    private static void AppendNormalizedSql(string value, StringBuilder builder)
    {
        var pendingWhitespace = false;
        foreach (var ch in value.Trim())
        {
            if (char.IsWhiteSpace(ch))
            {
                pendingWhitespace = builder.Length > 0;
                continue;
            }
            AppendPendingWhitespace(builder: builder, pendingWhitespace: pendingWhitespace);
            builder.Append(char.ToLowerInvariant(ch));
            pendingWhitespace = false;
        }
    }

    private static void AppendPendingWhitespace(StringBuilder builder, bool pendingWhitespace)
    {
        if (pendingWhitespace)
        {
            builder.Append(' ');
        }
    }

    private static void AddIf(
        bool condition,
        string message,
        ImmutableArray<string>.Builder mismatches
    )
    {
        if (condition)
        {
            mismatches.Add(message);
        }
    }

    private static string TablePath(TableDefinition table) => $"{table.Schema}.{table.Name}";

    private static string Nullability(ColumnDefinition column) =>
        column.IsNullable ? "NULL" : "NOT NULL";

    private static string Format(IReadOnlyList<string> values) => string.Join(", ", values);

    private static string UniqueConstraintName(
        string tableName,
        UniqueConstraintDefinition unique
    ) =>
        string.IsNullOrWhiteSpace(unique.Name)
            ? $"UQ_{tableName}_{string.Join("_", unique.Columns)}"
            : unique.Name;

    private static string ForeignKeyName(string tableName, ForeignKeyDefinition foreignKey) =>
        string.IsNullOrWhiteSpace(foreignKey.Name)
            ? $"FK_{tableName}_{string.Join("_", foreignKey.Columns)}"
            : foreignKey.Name;
}
