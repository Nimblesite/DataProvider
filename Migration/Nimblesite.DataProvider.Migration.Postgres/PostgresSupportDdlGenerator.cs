using System.Globalization;

namespace Nimblesite.DataProvider.Migration.Postgres;

public static partial class PostgresDdlGenerator
{
    private static string GenerateCreateOrAlterRole(CreateOrAlterRoleOperation op)
    {
        var roleName = QuoteIdent(op.Role.Name);
        var roleLiteral = QuoteLiteral(op.Role.Name);
        var login = op.Role.Login ? "LOGIN" : "NOLOGIN";
        var bypass = op.Role.BypassRls ? "BYPASSRLS" : "NOBYPASSRLS";
        var sb = new StringBuilder();

        sb.AppendLine("DO $$");
        sb.AppendLine("BEGIN");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = {roleLiteral}) THEN"
        );
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    CREATE ROLE {roleName} {login} {bypass};"
        );
        sb.AppendLine("  END IF;");
        sb.AppendLine("END $$;");
        sb.Append(CultureInfo.InvariantCulture, $"ALTER ROLE {roleName} {login} {bypass}");

        foreach (var grantee in op.Role.GrantTo)
        {
            sb.AppendLine(";");
            sb.Append(CultureInfo.InvariantCulture, $"GRANT {roleName} TO {QuoteIdent(grantee)}");
        }

        return sb.ToString();
    }

    private static string GenerateCreateOrReplaceFunction(CreateOrReplaceFunctionOperation op)
    {
        var function = op.Function;
        var functionName = $"{QuoteIdent(function.Schema)}.{QuoteIdent(function.Name)}";
        var argumentDeclarations = string.Join(
            ", ",
            function.Arguments.Select(ArgumentDeclaration)
        );
        var signature = FunctionSignature(function);
        var sb = new StringBuilder();

        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"CREATE OR REPLACE FUNCTION {functionName}({argumentDeclarations})"
        );
        sb.AppendLine(CultureInfo.InvariantCulture, $"RETURNS {function.Returns}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"LANGUAGE {function.Language}");
        sb.AppendLine(function.Volatility.ToUpperInvariant());
        if (function.SecurityDefiner)
        {
            sb.AppendLine("SECURITY DEFINER");
        }
        sb.AppendLine("AS $function$");
        sb.AppendLine(function.Body.Trim());
        sb.Append("$function$");

        if (function.RevokePublicExecute)
        {
            sb.AppendLine(";");
            sb.Append(
                CultureInfo.InvariantCulture,
                $"REVOKE EXECUTE ON FUNCTION {signature} FROM PUBLIC"
            );
        }

        if (function.ExecuteRoles.Count > 0)
        {
            sb.AppendLine(";");
            sb.Append(
                CultureInfo.InvariantCulture,
                $"GRANT EXECUTE ON FUNCTION {signature} TO {QuoteIdentList(function.ExecuteRoles)}"
            );
        }

        return sb.ToString();
    }

    private static string GenerateGrantPrivileges(PostgresGrantDefinition grant) =>
        $"GRANT {PrivilegeList(grant.Privileges)} ON {GrantTarget(grant)} TO {QuoteIdentList(grant.Roles)}";

    private static string GenerateRevokePrivileges(PostgresGrantDefinition grant) =>
        $"REVOKE {PrivilegeList(grant.Privileges)} ON {GrantTarget(grant)} FROM {QuoteIdentList(grant.Roles)}";

    private static string GenerateDropFunction(DropFunctionOperation op) =>
        $"DROP FUNCTION IF EXISTS {QuoteIdent(op.Schema)}.{QuoteIdent(op.Name)}({string.Join(", ", op.ArgumentTypes)})";

    private static string ArgumentDeclaration(PostgresFunctionArgumentDefinition argument) =>
        string.IsNullOrWhiteSpace(argument.Name)
            ? argument.Type
            : $"{QuoteIdent(argument.Name)} {argument.Type}";

    private static string FunctionSignature(PostgresFunctionDefinition function) =>
        $"{QuoteIdent(function.Schema)}.{QuoteIdent(function.Name)}({string.Join(", ", function.Arguments.Select(a => a.Type))})";

    private static string GrantTarget(PostgresGrantDefinition grant) =>
        grant.Target switch
        {
            PostgresGrantTarget.Schema => $"SCHEMA {QuoteIdent(grant.Schema)}",
            PostgresGrantTarget.Table when grant.ObjectName is string objectName =>
                $"TABLE {QuoteIdent(grant.Schema)}.{QuoteIdent(objectName)}",
            PostgresGrantTarget.AllTablesInSchema =>
                $"ALL TABLES IN SCHEMA {QuoteIdent(grant.Schema)}",
            _ => $"TABLE {QuoteIdent(grant.Schema)}.{QuoteIdent(string.Empty)}",
        };

    private static string PrivilegeList(IReadOnlyList<string> privileges) =>
        string.Join(", ", privileges.Select(p => p.Trim().ToUpperInvariant()));

    private static string QuoteIdentList(IReadOnlyList<string> identifiers) =>
        string.Join(", ", identifiers.Select(QuoteIdent));

    private static string QuoteIdent(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string QuoteLiteral(string value) =>
        $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}
