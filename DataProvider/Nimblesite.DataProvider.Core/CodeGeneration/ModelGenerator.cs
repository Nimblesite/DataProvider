using System.Globalization;
using System.Text;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Core.CodeGeneration;

/// <summary>
/// Static methods for generating model/record types from database metadata
/// </summary>
public static class ModelGenerator
{
    /// <summary>
    /// Generates a C# record type from database column metadata
    /// </summary>
    /// <param name="typeName">Name of the record type</param>
    /// <param name="columns">Database column metadata</param>
    /// <returns>Generated C# record definition</returns>
    public static Result<string, SqlError> GenerateRecordType(
        string typeName,
        IReadOnlyList<DatabaseColumn> columns
    )
    {
        if (ValidateTypeNameAndColumns(typeName, columns) is { } error)
            return new Result<string, SqlError>.Error<string, SqlError>(error);

        var sb = new StringBuilder();

        // Generate record with XML docs
        sb.AppendLine("/// <summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"/// Result row for '{typeName}' query.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"public record {typeName}");
        sb.AppendLine("{");

        // Generate properties
        foreach (var column in columns)
        {
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    /// <summary>Column '{column.Name}'.</summary>"
            );
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    public {column.CSharpType} {column.Name} {{ get; init; }}"
            );
            sb.AppendLine();
        }

        // Generate constructor with XML docs
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// <summary>Initializes a new instance of {typeName}.</summary>"
        );

        // Constructor signature
        sb.AppendLine(CultureInfo.InvariantCulture, $"    public {typeName}(");
        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var isLast = i == columns.Count - 1;
            var comma = isLast ? "" : ",";
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"        {column.CSharpType} {column.Name}{comma}"
            );
        }
        sb.AppendLine("    )");
        sb.AppendLine("    {");
        foreach (var column in columns)
        {
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"        this.{column.Name} = {column.Name};"
            );
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    /// <summary>
    /// Generates parent and child record types for grouped queries
    /// </summary>
    /// <param name="parentName">Name of the parent entity</param>
    /// <param name="childName">Name of the child entity</param>
    /// <param name="parentColumns">Parent entity columns</param>
    /// <param name="childColumns">Child entity columns</param>
    /// <param name="allColumns">All columns from the query for type resolution</param>
    /// <returns>Generated C# code for parent and child records</returns>
    public static Result<string, SqlError> GenerateGroupedRecordTypes(
        string parentName,
        string childName,
        IReadOnlyList<string> parentColumns,
        IReadOnlyList<string> childColumns,
        IReadOnlyList<DatabaseColumn> allColumns
    )
    {
        if (string.IsNullOrWhiteSpace(parentName))
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("parentName cannot be null or empty")
            );

        if (string.IsNullOrWhiteSpace(childName))
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("childName cannot be null or empty")
            );

        if (parentColumns == null || parentColumns.Count == 0)
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("parentColumns cannot be null or empty")
            );

        if (childColumns == null || childColumns.Count == 0)
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("childColumns cannot be null or empty")
            );

        if (allColumns == null || allColumns.Count == 0)
            return new Result<string, SqlError>.Error<string, SqlError>(
                new SqlError("allColumns cannot be null or empty")
            );

        var sb = new StringBuilder();
        var childCollectionName = string.Create(CultureInfo.InvariantCulture, $"{childName}s");

        // Generate parent entity
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"/// Represents a '{parentName}' with '{childCollectionName}'."
        );
        sb.AppendLine("/// </summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"public record {parentName}");
        sb.AppendLine("{");

        foreach (var col in parentColumns)
        {
            var columnMetadata = allColumns.FirstOrDefault(c => c.Name == col);
            var csharpType = columnMetadata?.CSharpType ?? "object";
            sb.AppendLine("    /// <summary>");
            sb.AppendLine(CultureInfo.InvariantCulture, $"    /// Gets the '{col}'.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    public {csharpType} {col} {{ get; init; }}"
            );
        }

        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Gets the related '{childCollectionName}'."
        );
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public IReadOnlyList<{childName}> {childCollectionName} {{ get; init; }}"
        );
        sb.AppendLine();

        // Parent constructor
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Initializes a new instance of <see cref=\"{parentName}\"/>."
        );
        sb.AppendLine("    /// </summary>");
        foreach (var col in parentColumns)
        {
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    /// <param name=\"{col}\">The '{col}'.</param>"
            );
        }
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// <param name=\"{childCollectionName}\">The related '{childCollectionName}'.</param>"
        );
        sb.AppendLine(CultureInfo.InvariantCulture, $"    public {parentName}(");
        foreach (var column in parentColumns)
        {
            var columnMetadata = allColumns.FirstOrDefault(c => c.Name == column);
            var csharpType = columnMetadata?.CSharpType ?? "object";
            sb.AppendLine(CultureInfo.InvariantCulture, $"        {csharpType} {column},");
        }
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        IReadOnlyList<{childName}> {childCollectionName}"
        );
        sb.AppendLine("    )");
        sb.AppendLine("    {");
        foreach (var col in parentColumns)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"        this.{col} = {col};");
        }
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"        this.{childCollectionName} = {childCollectionName};"
        );
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Generate child entity
        sb.AppendLine("/// <summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"/// Represents a '{childName}'.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"public record {childName}");
        sb.AppendLine("{");

        foreach (var col in childColumns)
        {
            var columnMetadata = allColumns.FirstOrDefault(c => c.Name == col);
            var csharpType = columnMetadata?.CSharpType ?? "object";
            sb.AppendLine("    /// <summary>");
            sb.AppendLine(CultureInfo.InvariantCulture, $"    /// Gets the '{col}'.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    public {csharpType} {col} {{ get; init; }}"
            );
        }
        sb.AppendLine();

        // Child constructor
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Initializes a new instance of <see cref=\"{childName}\"/>."
        );
        sb.AppendLine("    /// </summary>");
        foreach (var col in childColumns)
        {
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    /// <param name=\"{col}\">The '{col}'.</param>"
            );
        }
        sb.AppendLine(CultureInfo.InvariantCulture, $"    public {childName}(");
        for (int i = 0; i < childColumns.Count; i++)
        {
            var column = childColumns[i];
            var columnMetadata = allColumns.FirstOrDefault(c => c.Name == column);
            var csharpType = columnMetadata?.CSharpType ?? "object";
            var comma = i == childColumns.Count - 1 ? string.Empty : ",";
            sb.AppendLine(CultureInfo.InvariantCulture, $"        {csharpType} {column}{comma}");
        }
        sb.AppendLine("    )");
        sb.AppendLine("    {");
        foreach (var col in childColumns)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"        this.{col} = {col};");
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    /// <summary>
    /// Generates a raw data record for intermediate grouping operations
    /// </summary>
    /// <param name="typeName">Name of the raw record type</param>
    /// <param name="columns">Database column metadata</param>
    /// <returns>Generated C# record definition</returns>
    public static Result<string, SqlError> GenerateRawRecordType(
        string typeName,
        IReadOnlyList<DatabaseColumn> columns
    )
    {
        if (ValidateTypeNameAndColumns(typeName, columns) is { } error)
            return new Result<string, SqlError>.Error<string, SqlError>(error);

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"internal record {typeName}(");
        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var isLast = i == columns.Count - 1;
            var comma = isLast ? "" : ",";
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"    {column.CSharpType} {column.Name}{comma}"
            );
        }
        sb.AppendLine(");");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    private static SqlError? ValidateTypeNameAndColumns(
        string typeName,
        IReadOnlyList<DatabaseColumn>? columns
    ) =>
        string.IsNullOrWhiteSpace(typeName) ? new SqlError("typeName cannot be null or empty")
        : columns == null || columns.Count == 0 ? new SqlError("columns cannot be null or empty")
        : null;
}
