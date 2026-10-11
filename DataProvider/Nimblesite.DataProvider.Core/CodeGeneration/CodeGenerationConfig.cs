using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Core.CodeGeneration;

/// <summary>
/// Configuration for code generation with customizable functions
/// </summary>
public record CodeGenerationConfig
{
    /// <summary>
    /// Function to generate model types from database columns
    /// </summary>
    public Func<
        string,
        IReadOnlyList<DatabaseColumn>,
        Result<string, SqlError>
    > GenerateModelType { get; init; } = ModelGenerator.GenerateRecordType;

    /// <summary>
    /// Function to generate data access methods
    /// </summary>
    public Func<
        string,
        string,
        string,
        IReadOnlyList<ParameterInfo>,
        IReadOnlyList<DatabaseColumn>,
        string,
        Result<string, SqlError>
    > GenerateDataAccessMethod { get; init; } =
        (className, methodName, sql, parameters, columns, connectionType) =>
            DataAccessGenerator.GenerateQueryMethod(
                className,
                methodName,
                methodName,
                sql,
                parameters,
                columns,
                connectionType
            );

    /// <summary>
    /// Function to generate complete source files
    /// </summary>
    public Func<
        string,
        string,
        string,
        Result<string, SqlError>
    > GenerateSourceFile { get; init; } = (namespaceName, model, methods) => GenerateSourceForConnection(
        namespaceName: namespaceName, modelCode: model, dataAccessCode: methods, connectionNamespace: "Microsoft.Data.Sqlite");

    /// <summary>
    /// Function to generate grouped model types
    /// </summary>
    public Func<
        string,
        string,
        IReadOnlyList<string>,
        IReadOnlyList<string>,
        IReadOnlyList<DatabaseColumn>,
        Result<string, SqlError>
    > GenerateGroupedModels { get; init; } = ModelGenerator.GenerateGroupedRecordTypes;

    /// <summary>
    /// Function to generate raw record types for grouping
    /// </summary>
    public Func<
        string,
        IReadOnlyList<DatabaseColumn>,
        Result<string, SqlError>
    > GenerateRawRecordType { get; init; } = ModelGenerator.GenerateRawRecordType;

    /// <summary>
    /// Function to generate grouped query methods
    /// </summary>
    public Func<
        string,
        string,
        string,
        IReadOnlyList<ParameterInfo>,
        IReadOnlyList<DatabaseColumn>,
        GroupingConfig,
        string,
        Result<string, SqlError>
    > GenerateGroupedQueryMethod { get; init; } =
        GroupingTransformations.GenerateGroupedQueryMethod;

    /// <summary>
    /// Function to get column metadata from SQL
    /// </summary>
    public Func<
        string,
        string,
        IEnumerable<ParameterInfo>,
        Task<Result<IReadOnlyList<DatabaseColumn>, SqlError>>
    > GetColumnMetadata { get; init; }

    /// <summary>
    /// Table operation generator
    /// </summary>
    public ITableOperationGenerator TableOperationGenerator { get; init; }

    /// <summary>
    /// Target namespace for generated code
    /// </summary>
    public string TargetNamespace { get; init; } = "Generated";

    /// <summary>
    /// Database connection type name
    /// </summary>
    public string ConnectionType { get; init; } = "SqliteConnection";

    /// <summary>
    /// Initializes a new instance with required dependencies
    /// </summary>
    public CodeGenerationConfig(
        Func<
            string,
            string,
            IEnumerable<ParameterInfo>,
            Task<Result<IReadOnlyList<DatabaseColumn>, SqlError>>
        > getColumnMetadata,
        ITableOperationGenerator? tableOperationGenerator = null
    )
    {
        GetColumnMetadata =
            getColumnMetadata ?? throw new ArgumentNullException(nameof(getColumnMetadata));
        TableOperationGenerator = tableOperationGenerator ?? new DefaultTableOperationGenerator();
    }

    /// <summary>
    /// Default source file generator
    /// </summary>
    /// <summary>Emits a shared source file with the selected database driver's namespace.</summary>
    public static Result<string, SqlError> GenerateSourceForConnection(
        string namespaceName,
        string modelCode,
        string dataAccessCode,
        string connectionNamespace
    )
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("namespaceName cannot be null or empty")
            );

        if (string.IsNullOrWhiteSpace(modelCode) && string.IsNullOrWhiteSpace(dataAccessCode))
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("At least one of modelCode or dataAccessCode must be provided")
            );

        var sb = new System.Text.StringBuilder();

        // Generate using statements
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Collections.Immutable;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"using {connectionNamespace};");
        sb.AppendLine("using Outcome;");
        sb.AppendLine("using Nimblesite.Sql.Model;");
        sb.AppendLine();

        // Generate namespace
        sb.AppendLine(
            System.Globalization.CultureInfo.InvariantCulture,
            $"namespace {namespaceName};"
        );
        sb.AppendLine();

        // Add data access code if provided
        if (!string.IsNullOrWhiteSpace(dataAccessCode))
        {
            sb.Append(dataAccessCode);
            sb.AppendLine();
        }

        // Add model code if provided
        if (!string.IsNullOrWhiteSpace(modelCode))
        {
            sb.Append(modelCode);
        }

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }
}
