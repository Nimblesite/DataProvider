namespace Nimblesite.DataProvider.Migration.Core;

public static partial class SchemaDiff
{
    private static bool IsSupportCleanupOperation(SchemaOperation operation) =>
        operation is DropFunctionOperation or RevokePrivilegesOperation;

    private static IEnumerable<SchemaOperation> CalculateRoleDiff(
        SchemaDefinition current,
        SchemaDefinition desired,
        ILogger? logger
    )
    {
        var currentRoles = current.Roles.ToDictionary(
            r => r.Name,
            StringComparer.OrdinalIgnoreCase
        );

        foreach (var desiredRole in desired.Roles)
        {
            if (
                !currentRoles.TryGetValue(desiredRole.Name, out var currentRole)
                || RoleNeedsChange(currentRole, desiredRole)
            )
            {
                logger?.LogDebug("Role {Role} will be created or altered", desiredRole.Name);
                yield return new CreateOrAlterRoleOperation(desiredRole);
            }
        }
    }

    private static bool RoleNeedsChange(
        PostgresRoleDefinition current,
        PostgresRoleDefinition desired
    )
    {
        var currentGrantTo = current.GrantTo.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return current.Login != desired.Login
            || current.BypassRls != desired.BypassRls
            || desired.GrantTo.Any(g => !currentGrantTo.Contains(g));
    }

    private static IEnumerable<SchemaOperation> CalculateFunctionDiff(
        SchemaDefinition current,
        SchemaDefinition desired,
        bool allowDestructive,
        ILogger? logger
    )
    {
        var currentFunctions = current.Functions.ToDictionary(
            FunctionKey,
            StringComparer.OrdinalIgnoreCase
        );

        foreach (var desiredFunction in desired.Functions)
        {
            var key = FunctionKey(desiredFunction);
            if (
                !currentFunctions.TryGetValue(key, out var currentFunction)
                || FunctionNeedsChange(currentFunction, desiredFunction)
            )
            {
                logger?.LogDebug(
                    "Function {Schema}.{Function} will be created or replaced",
                    desiredFunction.Schema,
                    desiredFunction.Name
                );
                yield return new CreateOrReplaceFunctionOperation(desiredFunction);
            }
        }

        if (!allowDestructive)
        {
            yield break;
        }

        var desiredKeys = desired
            .Functions.Select(FunctionKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var currentFunction in current.Functions)
        {
            if (!desiredKeys.Contains(FunctionKey(currentFunction)))
            {
                logger?.LogWarning(
                    "Function {Schema}.{Function} will be DROPPED",
                    currentFunction.Schema,
                    currentFunction.Name
                );
                yield return new DropFunctionOperation(
                    currentFunction.Schema,
                    currentFunction.Name,
                    currentFunction.Arguments.Select(a => a.Type).ToList()
                );
            }
        }
    }

    private static bool FunctionNeedsChange(
        PostgresFunctionDefinition current,
        PostgresFunctionDefinition desired
    )
    {
        var currentExecuteRoles = current.ExecuteRoles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return !SameSqlToken(current.Returns, desired.Returns)
            || !SameSqlToken(current.Language, desired.Language)
            || !SameSqlToken(current.Volatility, desired.Volatility)
            || current.SecurityDefiner != desired.SecurityDefiner
            || current.RevokePublicExecute != desired.RevokePublicExecute
            || FunctionBodyForDiff(current) != FunctionBodyForDiff(desired)
            || !currentExecuteRoles.SetEquals(desired.ExecuteRoles);
    }

    private static string FunctionBodyForDiff(PostgresFunctionDefinition function)
    {
        if (string.IsNullOrWhiteSpace(function.BodyLql))
        {
            return function.Body.Trim();
        }

        var result = LqlFunctionBodyTranspiler.TranslatePostgresBody(
            function.BodyLql,
            $"{function.Schema}.{function.Name}"
        );
        return result switch
        {
            Outcome.Result<string, MigrationError>.Ok<string, MigrationError> ok => ok.Value.Trim(),
            Outcome.Result<string, MigrationError>.Error<string, MigrationError> =>
                function.BodyLql.Trim(),
        };
    }

    private static string FunctionKey(PostgresFunctionDefinition function) =>
        string.Join(
            "|",
            [
                function.Schema.ToLowerInvariant(),
                function.Name.ToLowerInvariant(),
                .. function.Arguments.Select(a => a.Type.Trim().ToLowerInvariant()),
            ]
        );

    private static bool SameSqlToken(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<SchemaOperation> CalculateGrantDiff(
        SchemaDefinition current,
        SchemaDefinition desired,
        bool allowDestructive,
        ILogger? logger
    )
    {
        var currentKeys = ExpandGrantKeys(current.Grants, current.Tables).ToHashSet();

        foreach (var desiredGrant in desired.Grants)
        {
            var desiredKeys = ExpandGrantKeys([desiredGrant], desired.Tables).ToList();
            if (desiredKeys.Count == 0 || desiredKeys.Any(k => !currentKeys.Contains(k)))
            {
                logger?.LogDebug(
                    "Grant on {Schema} {Target} will be applied",
                    desiredGrant.Schema,
                    desiredGrant.Target
                );
                yield return new GrantPrivilegesOperation(desiredGrant);
            }
        }

        if (!allowDestructive)
        {
            yield break;
        }

        var managedRoles = desired
            .Grants.SelectMany(g => g.Roles)
            .Select(r => r.ToLowerInvariant())
            .ToHashSet();
        var desiredGrantKeys = ExpandGrantKeys(desired.Grants, desired.Tables).ToHashSet();

        foreach (var currentKey in currentKeys)
        {
            if (managedRoles.Contains(currentKey.Role) && !desiredGrantKeys.Contains(currentKey))
            {
                logger?.LogWarning(
                    "Grant {Privilege} on {Schema}.{ObjectName} to {Role} will be REVOKED",
                    currentKey.Privilege,
                    currentKey.Schema,
                    currentKey.ObjectName,
                    currentKey.Role
                );
                yield return new RevokePrivilegesOperation(currentKey.ToGrant());
            }
        }
    }

    private static IEnumerable<PostgresGrantKey> ExpandGrantKeys(
        IEnumerable<PostgresGrantDefinition> grants,
        IReadOnlyList<TableDefinition> tables
    )
    {
        foreach (var grant in grants)
        {
            foreach (var privilege in grant.Privileges)
            {
                foreach (var role in grant.Roles)
                {
                    foreach (var key in ExpandGrantKey(grant, tables, privilege, role))
                    {
                        yield return key;
                    }
                }
            }
        }
    }

    private static IEnumerable<PostgresGrantKey> ExpandGrantKey(
        PostgresGrantDefinition grant,
        IReadOnlyList<TableDefinition> tables,
        string privilege,
        string role
    )
    {
        if (grant.Target == PostgresGrantTarget.Schema)
        {
            yield return PostgresGrantKey.Create(
                grant.Schema,
                PostgresGrantTarget.Schema,
                null,
                role,
                privilege
            );
            yield break;
        }

        if (grant.Target == PostgresGrantTarget.Table && grant.ObjectName is string objectName)
        {
            yield return PostgresGrantKey.Create(
                grant.Schema,
                PostgresGrantTarget.Table,
                objectName,
                role,
                privilege
            );
            yield break;
        }

        foreach (var table in tables.Where(t => SameSqlToken(t.Schema, grant.Schema)))
        {
            yield return PostgresGrantKey.Create(
                grant.Schema,
                PostgresGrantTarget.Table,
                table.Name,
                role,
                privilege
            );
        }
    }

    private sealed record PostgresGrantKey(
        string Schema,
        PostgresGrantTarget Target,
        string? ObjectName,
        string Role,
        string Privilege
    )
    {
        public static PostgresGrantKey Create(
            string schema,
            PostgresGrantTarget target,
            string? objectName,
            string role,
            string privilege
        ) =>
            new(
                schema.ToLowerInvariant(),
                target,
                objectName?.ToLowerInvariant(),
                role.ToLowerInvariant(),
                privilege.ToUpperInvariant()
            );

        public PostgresGrantDefinition ToGrant() =>
            new()
            {
                Schema = Schema,
                Target = Target,
                ObjectName = ObjectName,
                Privileges = [Privilege],
                Roles = [Role],
            };
    }
}
