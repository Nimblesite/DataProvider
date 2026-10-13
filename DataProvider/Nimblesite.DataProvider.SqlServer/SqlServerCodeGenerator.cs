using System.Data;
using Microsoft.Data.SqlClient;
using Nimblesite.DataProvider.Core;
using Nimblesite.DataProvider.Core.CodeGeneration;
using Nimblesite.DataProvider.SqlServer.SchemaInspection;
using Nimblesite.Sql.Model;
using Outcome;
using SqlError = Nimblesite.Sql.Model.SqlError;

namespace Nimblesite.DataProvider.SqlServer;

// Implements [CON-SHARED-CORE]. SQL Server is a thin adapter over the shared
// Core generators: SqlAntlrCodeGenerator orchestrates emission, AdoNetDatabaseEffects
// performs schema discovery through Microsoft.Data.SqlClient, and
// DefaultTableOperationGenerator emits INSERT/UPDATE. The only dialect-specific
// piece is swapping the template's Sqlite using for SqlClient so generated
// files compile against Microsoft.Data.SqlClient.
/// <summary>
/// SQL Server code generation composed from the shared Core generators.
/// </summary>
internal static class SqlServerCodeGenerator
{
    /// <summary>
    /// The connection type name used in generated SQL Server code.
    /// </summary>
    private const string ConnectionType = "SqlConnection";

    /// <summary>
    /// The using directive emitted by the shared Core template that must be
    /// substituted for the SQL Server client namespace.
    /// </summary>
    private const string TemplateSqliteUsing = "using Microsoft.Data.Sqlite;";

    /// <summary>
    /// The using directive required by generated SQL Server code.
    /// </summary>
    private const string SqlServerUsing = "using Microsoft.Data.SqlClient;";

    /// <summary>
    /// Generates C# source for a SQL file using real database metadata.
    /// Delegates to <see cref="SqlAntlrCodeGenerator.GenerateCodeWithMetadata"/>.
    /// </summary>
    /// <param name="fileName">The SQL file name.</param>
    /// <param name="sql">The SQL content.</param>
    /// <param name="statement">The parsed SQL statement metadata.</param>
    /// <param name="connectionString">Unused; metadata is resolved by the caller.</param>
    /// <param name="columnMetadata">Real column metadata from the database.</param>
    /// <param name="hasCustomImplementation">Unused.</param>
    /// <param name="groupingConfig">Optional grouping configuration for parent-child relationships.</param>
    /// <param name="config">Optional code generation configuration with custom functions.</param>
    /// <returns>Result with generated source or an error.</returns>
    public static Result<string, SqlError> GenerateCodeWithMetadata(
        string fileName,
        string sql,
        SelectStatement statement,
        string connectionString,
        IReadOnlyList<DatabaseColumn> columnMetadata,
        bool hasCustomImplementation,
        GroupingConfig? groupingConfig = null,
        CodeGenerationConfig? config = null
    )
    {
        _ = connectionString; // Metadata has already been resolved by the caller
        _ = hasCustomImplementation;

        return SqlAntlrCodeGenerator.GenerateCodeWithMetadata(
            fileName,
            sql,
            statement,
            columnMetadata,
            config ?? CreateSqlServerConfig(),
            groupingConfig
        );
    }

    /// <summary>
    /// Creates the SQL Server <see cref="CodeGenerationConfig"/> composed from
    /// shared Core generators: ADO.NET schema discovery over
    /// <see cref="SqlConnection"/>, table operations, and the shared source
    /// template with the client using substituted.
    /// </summary>
    /// <returns>A ready-to-use code generation configuration for SQL Server.</returns>
    public static CodeGenerationConfig CreateSqlServerConfig()
    {
        var databaseEffects = new AdoNetDatabaseEffects(
            connectionString => new SqlConnection(connectionString),
            MapReaderTypeToCSharpType
        )
        {
            SchemaBehavior = CommandBehavior.SchemaOnly | CommandBehavior.KeyInfo,
        };
        var sharedConfig = new CodeGenerationConfig(
            databaseEffects.GetColumnMetadataFromSqlAsync,
            new DefaultTableOperationGenerator(ConnectionType)
        );
        var sharedSourceFile = sharedConfig.GenerateSourceFile;

        return sharedConfig with
        {
            ConnectionType = ConnectionType,
            TargetNamespace = "Generated",
            GenerateSourceFile = (namespaceName, modelCode, dataAccessCode) =>
                SubstituteClientUsing(sharedSourceFile, namespaceName, modelCode, dataAccessCode),
        };
    }

    /// <summary>
    /// Runs the shared Core source file template, then swaps the template's
    /// Sqlite using for the SQL Server client so generated output compiles.
    /// </summary>
    /// <param name="template">The shared Core source file emitter.</param>
    /// <param name="namespaceName">Target namespace for generated code.</param>
    /// <param name="modelCode">Generated model types.</param>
    /// <param name="dataAccessCode">Generated data access methods.</param>
    /// <returns>Result with SQL Server-ready source or an error.</returns>
    private static Result<string, SqlError> SubstituteClientUsing(
        Func<string, string, string, Result<string, SqlError>> template,
        string namespaceName,
        string modelCode,
        string dataAccessCode
    ) =>
        template(namespaceName, modelCode, dataAccessCode) switch
        {
            Result<string, SqlError>.Ok<string, SqlError> success => new Result<
                string,
                SqlError
            >.Ok<string, SqlError>(
                success.Value.Replace(TemplateSqliteUsing, SqlServerUsing, StringComparison.Ordinal)
            ),
            Result<string, SqlError>.Error<string, SqlError> failure => failure,
        };

    /// <summary>
    /// Maps a SQL Server type name reported by <see cref="SqlConnection"/> to
    /// a C# type literal.
    /// </summary>
    /// <param name="fieldType">The CLR type reported by the reader.</param>
    /// <param name="dataTypeName">The driver-declared SQL type name.</param>
    /// <param name="isNullable">Whether the column is nullable.</param>
    /// <returns>A C# type literal suitable for emission in generated code.</returns>
    private static string MapReaderTypeToCSharpType(
        Type fieldType,
        string dataTypeName,
        bool isNullable
    )
    {
        _ = fieldType;
        return SqlServerSchemaInspector.MapSqlTypeToCSharpType(dataTypeName, isNullable);
    }

    /// <summary>
    /// Gets column metadata by executing the SQL query against the database.
    /// </summary>
    /// <param name="connectionString">Database connection string.</param>
    /// <param name="sql">SQL query to execute.</param>
    /// <param name="parameters">Query parameters.</param>
    /// <returns>Result with the discovered columns or an error.</returns>
    public static Task<
        Result<IReadOnlyList<DatabaseColumn>, SqlError>
    > GetColumnMetadataFromSqlAsync(
        string connectionString,
        string sql,
        IEnumerable<ParameterInfo> parameters
    ) => CreateSqlServerConfig().GetColumnMetadata(connectionString, sql, parameters);

    /// <summary>
    /// Gets table metadata via SQL Server schema inspection.
    /// </summary>
    /// <param name="connectionString">Database connection string.</param>
    /// <param name="schema">Table schema.</param>
    /// <param name="tableName">Table name.</param>
    /// <returns>Result with the table metadata or an error.</returns>
    public static async Task<Result<DatabaseTable, SqlError>> GetTableMetadataAsync(
        string connectionString,
        string schema,
        string tableName
    )
    {
        var inspector = new SqlServerSchemaInspector(connectionString);
        var table = await inspector.GetTableAsync(schema, tableName).ConfigureAwait(false);

        return table is null
            ? new Result<DatabaseTable, SqlError>.Error<DatabaseTable, SqlError>(
                new SqlError($"Table {schema}.{tableName} not found or has no columns")
            )
            : new Result<DatabaseTable, SqlError>.Ok<DatabaseTable, SqlError>(table);
    }
}
