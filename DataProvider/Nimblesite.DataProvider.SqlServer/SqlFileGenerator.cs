using Microsoft.CodeAnalysis;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.SqlServer;

/// <summary>
/// SQL Server specific source generator that inherits from the base generator
/// </summary>
[Generator]
public sealed class SqlFileGenerator : SqlFileGeneratorBase
{
    /// <summary>
    /// Initializes a new instance of the SqlFileGenerator class
    /// </summary>
    /// <param name="sqlParser">The SQL parser to use</param>
    public SqlFileGenerator(ISqlParser sqlParser)
        : base(sqlParser) { }

    /// <summary>
    /// Creates code generators for SQL Server
    /// </summary>
    /// <returns>A tuple containing the generate code function, generate code with metadata function, and generate table operations function</returns>
    protected override (
        CodeGenerators.GenerateCodeFunc GenerateCode,
        CodeGenerators.GenerateCodeWithMetadataFunc GenerateCodeWithMetadata,
        CodeGenerators.GenerateTableOperationsFunc GenerateTableOperations
    ) CreateCodeGenerators() =>
        (
            SqlServerCodeGenerator.GenerateCode,
            SqlServerCodeGenerator.GenerateCodeWithMetadata,
            SqlServerCodeGenerator.GenerateTableOperations
        );

    /// <summary>
    /// Gets column metadata for the specified SQL by querying the database.
    /// </summary>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="sql">The SQL text.</param>
    /// <param name="parameters">Parameters used by the SQL statement.</param>
    /// <returns>A result containing the discovered columns, or an error.</returns>
    protected override async Task<
        Result<IReadOnlyList<DatabaseColumn>, SqlError>
    > GetColumnMetadataAsync(
        string connectionString,
        string sql,
        IEnumerable<ParameterInfo> parameters
    ) =>
        await SqlServerCodeGenerator
            .GetColumnMetadataFromSqlAsync(connectionString, sql, parameters)
            .ConfigureAwait(false);
}
