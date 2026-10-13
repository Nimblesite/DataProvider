using System.Globalization;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Core.CodeGeneration;

/// <summary>
/// Default implementation of the code generation template
/// </summary>
public class DefaultCodeTemplate : ICodeTemplate
{
    /// <summary>
    /// Generates the model/record type definition
    /// </summary>
    public virtual Result<string, SqlError> GenerateModelType(
        string typeName,
        IReadOnlyList<DatabaseColumn> columns
    ) => ModelGenerator.GenerateRecordType(typeName, columns);

    /// <summary>
    /// Generates the data access extension method
    /// </summary>
    public virtual Result<string, SqlError> GenerateDataAccessMethod(
        string methodName,
        string returnTypeName,
        string sql,
        IReadOnlyList<ParameterInfo> parameters,
        IReadOnlyList<DatabaseColumn> columns
    )
    {
        var className = string.Create(CultureInfo.InvariantCulture, $"{methodName}Extensions");
        return DataAccessGenerator.GenerateQueryMethod(
            className,
            methodName,
            returnTypeName,
            sql,
            parameters,
            columns,
            "SqliteConnection"
        );
    }

    /// <summary>
    /// Generates the complete source file with usings, namespace, and all generated code
    /// </summary>
    public virtual Result<string, SqlError> GenerateSourceFile(
        string namespaceName,
        string modelCode,
        string dataAccessCode
    ) =>
        CodeGenerationConfig.GenerateSourceForConnection(
            namespaceName: namespaceName,
            modelCode: modelCode,
            dataAccessCode: dataAccessCode,
            connectionNamespace: "Microsoft.Data.Sqlite"
        );

    /// <summary>
    /// Generates grouped parent-child model types
    /// </summary>
    public virtual Result<string, SqlError> GenerateGroupedModels(
        GroupingConfig groupingConfig,
        IReadOnlyList<DatabaseColumn> columns
    )
    {
        if (groupingConfig == null)
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("groupingConfig cannot be null")
            );

        if (columns == null || columns.Count == 0)
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("columns cannot be null or empty")
            );

        return ModelGenerator.GenerateGroupedRecordTypes(
            groupingConfig.ParentEntity.Name,
            groupingConfig.ChildEntity.Name,
            groupingConfig.ParentEntity.Columns,
            groupingConfig.ChildEntity.Columns,
            columns
        );
    }
}
