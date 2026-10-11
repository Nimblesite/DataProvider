using System.Globalization;
using System.Text;
using Nimblesite.Sql.Model;
using Outcome;
using GenerationResult = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>;

namespace Nimblesite.DataProvider.Core.CodeGeneration;

/// <summary>
/// Default implementation for generating table operations
/// </summary>
public partial class DefaultTableOperationGenerator : ITableOperationGenerator
{
    private readonly string _connectionType;

    /// <summary>
    /// Initializes a new instance of DefaultTableOperationGenerator
    /// </summary>
    /// <param name="connectionType">Database connection type (e.g., SqliteConnection)</param>
    public DefaultTableOperationGenerator(string connectionType = "SqliteConnection")
    {
        _connectionType = connectionType;
    }

    /// <summary>
    /// Generates INSERT and UPDATE operations for a database table
    /// </summary>
    public virtual Result<string, SqlError> GenerateTableOperations(
        DatabaseTable table,
        TableConfig config
    )
    {
        if (table == null)
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("table cannot be null")
            );

        if (config == null)
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("config cannot be null")
            );

        var sb = new StringBuilder();
        global::Nimblesite.DataProvider.Core.CodeGeneration.ModelGenerator.AppendSourceLines(
            builder: sb,
            lines: EmissionBlockLines13
        );
        sb.AppendLine(CultureInfo.InvariantCulture, $"using {GetConnectionNamespace()};");
        sb.AppendLine("using Outcome;");
        sb.AppendLine("using Nimblesite.Sql.Model;");
        if (GetConnectionNamespace() == "Microsoft.Data.SqlClient")
            sb.AppendLine("using SqlError = Nimblesite.Sql.Model.SqlError;");
        global::Nimblesite.DataProvider.Core.CodeGeneration.ModelGenerator.AppendSourceLines(
            builder: sb,
            lines: EmissionBlockLines14
        );

        var className = string.Create(CultureInfo.InvariantCulture, $"{table.Name}Extensions");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Extension methods for table operations on {table.Name}"
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    public static partial class {className}");
        sb.AppendLine("    {");

        if (
            config.GenerateInsert
            && AppendGeneratedMethod(
                builder: sb,
                generate: () => GenerateInsertMethod(table: table)
            )
                is { } GenerateInsertFailure
        )
            return GenerateInsertFailure;

        if (
            config.GenerateUpdate
            && AppendGeneratedMethod(
                builder: sb,
                generate: () => GenerateUpdateMethod(table: table)
            )
                is { } GenerateUpdateFailure
        )
            return GenerateUpdateFailure;

        if (
            config.GenerateDelete
            && AppendGeneratedMethod(
                builder: sb,
                generate: () =>
                    DataAccessGenerator.GenerateDeleteMethod(
                        table: table,
                        connectionType: _connectionType
                    )
            )
                is { } GenerateDeleteFailure
        )
            return GenerateDeleteFailure;

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    /// <summary>
    /// Generates an INSERT method for a database table
    /// </summary>
    public virtual Result<string, SqlError> GenerateInsertMethod(DatabaseTable table) =>
        DataAccessGenerator.GenerateInsertMethod(table, _connectionType);

    /// <summary>
    /// Generates an UPDATE method for a database table
    /// </summary>
    public virtual Result<string, SqlError> GenerateUpdateMethod(DatabaseTable table) =>
        DataAccessGenerator.GenerateUpdateMethod(table, _connectionType);

    /// <summary>
    /// Gets the namespace for the connection type
    /// </summary>
    protected virtual string GetConnectionNamespace() =>
        ResolveConnectionNamespace(connectionType: _connectionType);

    internal static string ResolveConnectionNamespace(string connectionType) =>
        connectionType.Split('.').Last() switch
        {
            "SqliteConnection" => "Microsoft.Data.Sqlite",
            "SqlConnection" => "Microsoft.Data.SqlClient",
            "NpgsqlConnection" => "Npgsql",
            _ => "System.Data.Common",
        };

    // Implements [DP-MODEL-SHARED-EMISSION].
    private static GenerationResult? AppendGeneratedMethod(
        StringBuilder builder,
        Func<GenerationResult> generate
    )
    {
        var result = generate();
        if (result is GenerationResult.Ok<string, SqlError> success)
            builder.AppendLine(success.Value);
        return result is GenerationResult.Error<string, SqlError> ? result : null;
    }
}
