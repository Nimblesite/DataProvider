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
        VerifyExpected(
            expected: desired.Tables,
            find: expected => FindTable(schema: live, expected: expected),
            missing: expected => $"{TablePath(table: expected)}: missing table",
            verify: (actual, expected) =>
            {
                VerifyTable(actual: actual, expected: expected, includeRls: includeRls, mismatches);
            },
            mismatches: mismatches
        );
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
        VerifyExpected(
            expected: expected.Columns,
            find: expectedColumn => FindColumn(table: actual, name: expectedColumn.Name),
            missing: expectedColumn =>
                $"{TablePath(table: expected)}.{expectedColumn.Name}: missing column",
            verify: (actualColumn, expectedColumn) =>
            {
                VerifyColumn(
                    actual: actualColumn,
                    expected: PrimaryKeyNullability.UsesPlatformDefault(expected, expectedColumn)
                        ? expectedColumn with
                        {
                            IsNullable = actualColumn.IsNullable,
                        }
                        : expectedColumn,
                    path: $"{TablePath(table: expected)}.{expectedColumn.Name}",
                    isSqlite: IsSqliteSchema(actual.Schema),
                    mismatches: mismatches
                );
            },
            mismatches: mismatches
        );
    }

    private static void VerifyColumn(
        ColumnDefinition actual,
        ColumnDefinition expected,
        string path,
        bool isSqlite,
        ImmutableArray<string>.Builder mismatches
    )
    {
        AddMismatches(
            mismatches,
            (
                !SamePortableType(actual.Type, expected.Type, isSqlite),
                $"{path}: type expected {expected.Type} but found {actual.Type}"
            ),
            (
                actual.IsNullable != expected.IsNullable,
                $"{path}: nullability expected {Nullability(expected)} but found {Nullability(actual)}"
            ),
            ValueDrift(path, "identity", expected.IsIdentity, actual.IsIdentity)
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
        VerifyExpected(
            expected: expected.ForeignKeys,
            find: expectedFk => FindForeignKey(table: actual, expected: expectedFk),
            missing: expectedFk =>
                $"{TablePath(table: expected)}: missing foreign key {ForeignKeyName(expected.Name, expectedFk)} on ({Format(expectedFk.Columns)})",
            verify: (actualFk, expectedFk) =>
            {
                VerifyForeignKey(
                    actual: actualFk,
                    expected: expectedFk,
                    tablePath: TablePath(expected),
                    mismatches
                );
            },
            mismatches: mismatches
        );
    }

    private static void VerifyForeignKey(
        ForeignKeyDefinition actual,
        ForeignKeyDefinition expected,
        string tablePath,
        ImmutableArray<string>.Builder mismatches
    )
    {
        var name = ForeignKeyName(tablePath, expected);
        AddMismatches(
            mismatches,
            Drift(
                SameIdentifiers(actual.Columns, expected.Columns),
                tablePath,
                $"foreign key {name} columns"
            ),
            Drift(
                SameIdentifier(actual.ReferencedTable, expected.ReferencedTable),
                tablePath,
                $"foreign key {name} referenced table"
            ),
            Drift(
                SameSchema(actual.ReferencedSchema, expected.ReferencedSchema),
                tablePath,
                $"foreign key {name} referenced schema"
            ),
            Drift(
                SameIdentifiers(actual.ReferencedColumns, expected.ReferencedColumns),
                tablePath,
                $"foreign key {name} referenced columns"
            ),
            ValueDrift(
                tablePath,
                $"foreign key {name} on delete",
                expected.OnDelete,
                actual.OnDelete
            ),
            ValueDrift(
                tablePath,
                $"foreign key {name} on update",
                expected.OnUpdate,
                actual.OnUpdate
            )
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
        VerifyExpected(
            expected: expected.Indexes,
            find: expectedIndex =>
                actual.Indexes.FirstOrDefault(i => SameIdentifier(i.Name, expectedIndex.Name)),
            missing: expectedIndex =>
                $"{TablePath(table: expected)}: missing index {expectedIndex.Name}",
            verify: (actualIndex, expectedIndex) =>
            {
                VerifyIndex(
                    actual: actualIndex,
                    expected: expectedIndex,
                    tablePath: TablePath(expected),
                    mismatches
                );
            },
            mismatches: mismatches
        );
    }

    private static void VerifyIndex(
        IndexDefinition actual,
        IndexDefinition expected,
        string tablePath,
        ImmutableArray<string>.Builder mismatches
    )
    {
        var name = expected.Name;
        AddMismatches(
            mismatches,
            Drift(actual.IsUnique == expected.IsUnique, tablePath, $"index {name} uniqueness"),
            Drift(
                SameIdentifiers(actual.Columns, expected.Columns),
                tablePath,
                $"index {name} columns"
            ),
            Drift(
                SameSqlSequence(actual.Expressions, expected.Expressions),
                tablePath,
                $"index {name} expressions"
            ),
            Drift(SameSql(actual.Filter, expected.Filter), tablePath, $"index {name} filter")
        );
    }

    private static void VerifyCheckConstraints(
        TableDefinition actual,
        TableDefinition expected,
        ImmutableArray<string>.Builder mismatches
    )
    {
        VerifyExpected(
            expected: expected.CheckConstraints,
            find: expectedCheck =>
                actual.CheckConstraints.FirstOrDefault(c =>
                    SameIdentifier(c.Name, expectedCheck.Name)
                ),
            missing: expectedCheck =>
                $"{TablePath(table: expected)}: missing check constraint {expectedCheck.Name}",
            verify: (actualCheck, expectedCheck) =>
            {
                AddIf(
                    !SameSql(actualCheck.Expression, expectedCheck.Expression),
                    $"{TablePath(table: expected)}: check constraint {expectedCheck.Name} expression drifted",
                    mismatches
                );
            },
            mismatches: mismatches
        );
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
        var tablePath = TablePath(table: expected);
        AddMismatches(
            mismatches,
            ValueDrift(
                tablePath,
                "row-level security",
                expected.RowLevelSecurity.Enabled,
                actual.RowLevelSecurity?.Enabled ?? false
            ),
            ValueDrift(
                tablePath,
                "forced row-level security",
                expected.RowLevelSecurity.Forced,
                actual.RowLevelSecurity?.Forced ?? false
            )
        );
        VerifyRlsPolicies(actual: actual, expected: expected, mismatches: mismatches);
    }

    private static void VerifyRlsPolicies(
        TableDefinition actual,
        TableDefinition expected,
        ImmutableArray<string>.Builder mismatches
    )
    {
        VerifyExpected(
            expected: expected.RowLevelSecurity?.Policies ?? [],
            find: expectedPolicy =>
                actual.RowLevelSecurity?.Policies.FirstOrDefault(p =>
                    SameIdentifier(p.Name, expectedPolicy.Name)
                ),
            missing: expectedPolicy =>
                $"{TablePath(table: expected)}: missing row-level security policy {expectedPolicy.Name}",
            verify: (actualPolicy, expectedPolicy) =>
            {
                if (IsSqliteSchema(actual.Schema))
                {
                    return;
                }
                var tablePath = TablePath(table: expected);
                AddMismatches(
                    mismatches,
                    Drift(
                        RlsPolicyPredicates.SameUsing(actualPolicy, expectedPolicy),
                        tablePath,
                        $"policy {expectedPolicy.Name} USING predicate"
                    ),
                    Drift(
                        RlsPolicyPredicates.SameWithCheck(actualPolicy, expectedPolicy),
                        tablePath,
                        $"policy {expectedPolicy.Name} WITH CHECK predicate"
                    ),
                    Drift(
                        RlsPolicyPredicates.SameScope(actualPolicy, expectedPolicy),
                        tablePath,
                        $"policy {expectedPolicy.Name} scope"
                    )
                );
            },
            mismatches: mismatches
        );
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
        VerifyExpected(
            expected: desired.Roles,
            find: expectedRole =>
                live.Roles.FirstOrDefault(r => SameIdentifier(r.Name, expectedRole.Name)),
            missing: expectedRole => $"role {expectedRole.Name}: missing role",
            verify: (actualRole, expectedRole) =>
                AddMismatches(
                    mismatches,
                    Drift(
                        actualRole.Login == expectedRole.Login,
                        $"role {expectedRole.Name}",
                        "login"
                    ),
                    Drift(
                        actualRole.BypassRls == expectedRole.BypassRls,
                        $"role {expectedRole.Name}",
                        "bypassRls"
                    )
                ),
            mismatches: mismatches
        );
    }

    private static void VerifyFunctions(
        SchemaDefinition live,
        SchemaDefinition desired,
        ImmutableArray<string>.Builder mismatches
    )
    {
        VerifyExpected(
            expected: desired.Functions,
            find: expectedFunction => FindFunction(schema: live, expected: expectedFunction),
            missing: expectedFunction =>
                $"{expectedFunction.Schema}.{expectedFunction.Name}: missing function",
            verify: (actualFunction, expectedFunction) =>
            {
                VerifyFunction(
                    actual: actualFunction,
                    expected: expectedFunction,
                    mismatches: mismatches
                );
            },
            mismatches: mismatches
        );
    }

    private static void VerifyFunction(
        PostgresFunctionDefinition actual,
        PostgresFunctionDefinition expected,
        ImmutableArray<string>.Builder mismatches
    )
    {
        var path = FunctionPath(function: expected);
        AddMismatches(
            mismatches,
            IdentifierDrift(path, "returns", expected.Returns, actual.Returns),
            IdentifierDrift(path, "language", expected.Language, actual.Language)
        );
        VerifyFunctionBehavior(
            actual: actual,
            expected: expected,
            path: path,
            mismatches: mismatches
        );
    }

    private static void VerifyFunctionBehavior(
        PostgresFunctionDefinition actual,
        PostgresFunctionDefinition expected,
        string path,
        ImmutableArray<string>.Builder mismatches
    )
    {
        AddMismatches(
            mismatches,
            IdentifierDrift(path, "volatility", expected.Volatility, actual.Volatility),
            ValueDrift(path, "security definer", expected.SecurityDefiner, actual.SecurityDefiner),
            Drift(SameFunctionBody(actual: actual, expected: expected), path, "function body")
        );
    }

    private static void VerifyGrants(
        SchemaDefinition live,
        SchemaDefinition desired,
        ImmutableArray<string>.Builder mismatches
    )
    {
        foreach (var expectedGrant in desired.Grants)
        {
            // Implements [MIG-VERIFY-DEFAULTS-AND-GRANTS] (#59 Bugs 2 & 3):
            // an inspector may report grants in normalised atomic form — one row per
            // (object, role) — while a desired schema can declare grants over many
            // roles or every table in a schema. Expand each desired grant into the
            // same atomic units before searching the live set.
            foreach (var atomic in ExpandDesiredGrant(grant: expectedGrant, desired: desired))
            {
                var actualGrant = live.Grants.FirstOrDefault(g =>
                    SameGrant(actual: g, expected: atomic)
                );
                if (actualGrant is null)
                {
                    mismatches.Add(
                        $"grant {expectedGrant.Target} {expectedGrant.ObjectName ?? expectedGrant.Schema}: missing grant"
                    );
                    break;
                }
            }
        }
    }

    private static IEnumerable<PostgresGrantDefinition> ExpandDesiredGrant(
        PostgresGrantDefinition grant,
        SchemaDefinition desired
    )
    {
        var roles = grant.Roles.Count == 0 ? [string.Empty] : grant.Roles;
        if (grant.Target == PostgresGrantTarget.AllTablesInSchema)
        {
            var tablesInSchema = desired
                .Tables.Where(t => SameSchema(t.Schema, grant.Schema))
                .Select(t => t.Name)
                .ToList();
            foreach (var role in roles)
            {
                foreach (var table in tablesInSchema)
                {
                    yield return grant with
                    {
                        Target = PostgresGrantTarget.Table,
                        ObjectName = table,
                        Roles = [role],
                    };
                }
            }
            yield break;
        }
        foreach (var role in roles)
        {
            yield return grant with
            {
                Roles = [role],
            };
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
        && CoversPrivileges(actual: actual.Privileges, expected: expected.Privileges)
        && SameIdentifierSet(actual.Roles, expected.Roles);

    // Implements [MIG-VERIFY-DEFAULTS-AND-GRANTS] (#59 Bugs 2 & 3):
    // privilege comparison must be order-insensitive — different inspectors
    // emit privileges in different orders, and YAML order is arbitrary — and
    // the live grant must cover at least every privilege expected.
    private static bool CoversPrivileges(
        IReadOnlyList<string> actual,
        IReadOnlyList<string> expected
    )
    {
        var actualSet = actual.Select(p => p.ToUpperInvariant()).ToHashSet();
        return expected.All(p => actualSet.Contains(p.ToUpperInvariant()));
    }

    private static bool SameIdentifierSet(
        IReadOnlyList<string> actual,
        IReadOnlyList<string> expected
    ) =>
        actual.Count == expected.Count
        && actual
            .Select(a => a.ToLowerInvariant())
            .OrderBy(a => a, StringComparer.Ordinal)
            .SequenceEqual(
                expected.Select(e => e.ToLowerInvariant()).OrderBy(e => e, StringComparer.Ordinal)
            );

    private static bool SameFunctionBody(
        PostgresFunctionDefinition actual,
        PostgresFunctionDefinition expected
    ) =>
        string.IsNullOrWhiteSpace(expected.Body)
        || SameSql(actual: actual.Body, expected: expected.Body);

    private static bool SameIdentifiers(
        IReadOnlyList<string> actual,
        IReadOnlyList<string> expected
    ) => SameSequence(actual, expected, SameIdentifier);

    private static bool SameIdentifier(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static bool SameSchema(string actual, string expected) =>
        SameIdentifier(actual: actual, expected: expected)
        || (IsDefaultSchema(actual) && IsDefaultSchema(expected));

    private static bool IsDefaultSchema(string schema) =>
        string.IsNullOrWhiteSpace(schema)
        || SameIdentifier(actual: schema, expected: "main")
        || SameIdentifier(actual: schema, expected: "public")
        || SameIdentifier(actual: schema, expected: "dbo");

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
    ) => SameSequence(actual, expected, (a, b) => SameSql(a, b));

    private static bool SameSequence(
        IReadOnlyList<string> actual,
        IReadOnlyList<string> expected,
        Func<string, string, bool> same
    ) =>
        actual.Count == expected.Count
        && actual.Zip(expected).All(pair => same(pair.First, pair.Second));

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
        var normalized = builder.ToString().Trim().TrimEnd(';').Trim();
        return SqlExpressionText.StripOuterParens(
            StripTrailingTypeCast(SqlExpressionText.StripOuterParens(normalized))
        );
    }

    // Implements [MIG-VERIFY-DEFAULTS-AND-GRANTS] (#59 Bug 1):
    // some platforms store a string default like 'pending' with an explicit type
    // suffix such as 'pending'::text when read back from the catalog. Strip a
    // trailing ::<typename> cast on a quoted string literal so the verifier
    // treats 'pending' and 'pending'::text as equal across providers.
    private static string StripTrailingTypeCast(string normalized)
    {
        var castIndex = normalized.LastIndexOf("::", StringComparison.Ordinal);
        if (castIndex <= 0)
        {
            return normalized;
        }
        var before = normalized[..castIndex];
        if (!before.EndsWith('\''))
        {
            return normalized;
        }
        var after = normalized[(castIndex + 2)..];
        if (after.Length == 0 || !after.All(IsTypeNameChar))
        {
            return normalized;
        }
        return before;
    }

    private static bool IsTypeNameChar(char ch) =>
        char.IsLetterOrDigit(ch) || ch == '_' || ch == ' ';

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

    // Implements [MIG-VERIFY-SHARED-OBJECT-MATCHING]: one comparison/message shape
    // for the "<field> drifted" checks so every verifier stays consistent.
    private static (bool Condition, string Message) Drift(
        bool same,
        string path,
        string description
    ) => (!same, $"{path}: {description} drifted");

    private static (bool Condition, string Message) IdentifierDrift(
        string path,
        string field,
        string expected,
        string actual
    ) =>
        (
            !SameIdentifier(actual: actual, expected: expected),
            $"{path}: {field} expected {expected} but found {actual}"
        );

    private static (bool Condition, string Message) ValueDrift<T>(
        string path,
        string field,
        T expected,
        T actual
    ) =>
        (
            !EqualityComparer<T>.Default.Equals(x: actual, y: expected),
            $"{path}: {field} expected {expected} but found {actual}"
        );

    private static void AddMismatches(
        ImmutableArray<string>.Builder mismatches,
        params (bool Condition, string Message)[] checks
    )
    {
        foreach (var (condition, message) in checks)
        {
            AddIf(condition, message, mismatches);
        }
    }

    private static string TablePath(TableDefinition table) => $"{table.Schema}.{table.Name}";

    private static string FunctionPath(PostgresFunctionDefinition function) =>
        $"{function.Schema}.{function.Name}({Format(function.Arguments.Select(a => a.Type).ToArray())})";

    private static string Nullability(ColumnDefinition column) =>
        column.IsNullable ? "NULL" : "NOT NULL";

    private static string Format(IReadOnlyList<string> values) => string.Join(", ", values);

    private static string UniqueConstraintName(
        string tableName,
        UniqueConstraintDefinition unique
    ) => SchemaConstraintName("UQ", tableName, unique.Name, unique.Columns);

    private static string ForeignKeyName(string tableName, ForeignKeyDefinition foreignKey) =>
        SchemaConstraintName("FK", tableName, foreignKey.Name, foreignKey.Columns);

    // Implements [MIG-VERIFY-SHARED-OBJECT-MATCHING]: unnamed constraints get the
    // same platform-independent name the DDL generators assign.
    private static string SchemaConstraintName(
        string prefix,
        string tableName,
        string? name,
        IReadOnlyList<string> columns
    ) =>
        string.IsNullOrWhiteSpace(name)
            ? $"{prefix}_{tableName}_{string.Join("_", columns)}"
            : name;

    // Implements [MIG-VERIFY-SHARED-OBJECT-MATCHING].
    private static void VerifyExpected<T>(
        IEnumerable<T> expected,
        Func<T, T?> find,
        Func<T, string> missing,
        Action<T, T> verify,
        ImmutableArray<string>.Builder mismatches
    )
        where T : class
    {
        foreach (var item in expected)
        {
            var actual = find(item);
            if (actual is null)
                mismatches.Add(missing(item));
            else
                verify(actual, item);
        }
    }
}
