using System.Globalization;
using System.Text;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Core.CodeGeneration;

/// <summary>
/// Static methods for generating model/record types from database metadata
/// </summary>
public static partial class ModelGenerator
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
            return error;

        var sb = new StringBuilder();

        // Generate record with XML docs
        AppendXmlDocHeader(
            builder: sb,
            summary: $"Result row for '{typeName}' query.",
            declaration: $"public record {typeName}"
        );
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
        AppendDatabaseColumnParameters(
            builder: sb,
            signature: $"    public {typeName}(",
            columns: columns,
            indentation: "        "
        );
        sb.AppendLine("    )");
        sb.AppendLine("    {");
        AppendAssignments(builder: sb, columnNames: columns.Select(column => column.Name));
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
        if (
            CodeGenerationGuards.ValidateGroupedRecordTypes(
                parentName: parentName,
                childName: childName,
                parentColumns: parentColumns,
                childColumns: childColumns,
                allColumns: allColumns
            ) is
            { } error
        )
            return error;

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

        AppendGroupedProperties(builder: sb, columnNames: parentColumns, allColumns: allColumns);

        AppendDocumentedProperty(
            builder: sb,
            summary: $"Gets the related '{childCollectionName}'.",
            declaration: $"public IReadOnlyList<{childName}> {childCollectionName} {{ get; init; }}"
        );

        AppendGroupedConstructor(
            builder: sb,
            typeName: parentName,
            columnNames: parentColumns,
            allColumns: allColumns,
            trailingComma: true,
            extraParameterDoc: $"<param name=\"{childCollectionName}\">The related '{childCollectionName}'.</param>",
            extraParameter: $"IReadOnlyList<{childName}> {childCollectionName}",
            extraAssignment: $"this.{childCollectionName} = {childCollectionName};"
        );
        AppendBlockAndLine(
            builder: sb,
            block: EmissionBlockLines11,
            line: $"/// Represents a '{childName}'."
        );
        sb.AppendLine("/// </summary>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"public record {childName}");
        sb.AppendLine("{");

        AppendGroupedProperties(builder: sb, columnNames: childColumns, allColumns: allColumns);

        AppendGroupedConstructor(
            builder: sb,
            typeName: childName,
            columnNames: childColumns,
            allColumns: allColumns,
            trailingComma: false,
            extraParameterDoc: null,
            extraParameter: null,
            extraAssignment: null
        );
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
            return error;

        var sb = new StringBuilder();
        AppendDatabaseColumnParameters(
            builder: sb,
            signature: $"internal record {typeName}(",
            columns: columns,
            indentation: "    "
        );
        sb.AppendLine(");");

        return new Result<string, SqlError>.Ok<string, SqlError>(sb.ToString());
    }

    private static Result<string, SqlError>? ValidateTypeNameAndColumns(
        string typeName,
        IReadOnlyList<DatabaseColumn>? columns
    ) => CodeGenerationGuards.RequireText(typeName) ?? CodeGenerationGuards.RequireItems(columns);

    // Implements [DP-MODEL-SHARED-EMISSION].
    internal static void AppendXmlDocHeader(
        StringBuilder builder,
        string summary,
        string declaration
    )
    {
        builder.AppendLine("/// <summary>");
        builder.AppendLine(CultureInfo.InvariantCulture, $"/// {summary}");
        builder.AppendLine("/// </summary>");
        builder.AppendLine(CultureInfo.InvariantCulture, $"{declaration}");
    }

    // Implements [DP-MODEL-SHARED-EMISSION].
    private static void AppendGroupedProperties(
        StringBuilder builder,
        IReadOnlyList<string> columnNames,
        IReadOnlyList<DatabaseColumn> allColumns
    )
    {
        foreach (var col in columnNames)
        {
            var columnMetadata = allColumns.FirstOrDefault(c => c.Name == col);
            var csharpType = columnMetadata?.CSharpType ?? "object";
            AppendDocumentedProperty(
                builder: builder,
                summary: $"Gets the '{col}'.",
                declaration: $"public {csharpType} {col} {{ get; init; }}"
            );
        }
    }

    // Implements [DP-MODEL-SHARED-EMISSION].
    private static void AppendDocumentedProperty(
        StringBuilder builder,
        string summary,
        string declaration
    )
    {
        builder.AppendLine("    /// <summary>");
        builder.AppendLine(CultureInfo.InvariantCulture, $"    /// {summary}");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine(CultureInfo.InvariantCulture, $"    {declaration}");
    }

    // Implements [DP-MODEL-SHARED-EMISSION].
    private static void AppendDatabaseColumnParameters(
        StringBuilder builder,
        string signature,
        IReadOnlyList<DatabaseColumn> columns,
        string indentation
    )
    {
        builder.AppendLine(CultureInfo.InvariantCulture, $"{signature}");
        AppendParameters(
            builder: builder,
            columns: columns,
            csharpType: column => column.CSharpType,
            columnName: column => column.Name,
            indentation: indentation,
            trailingComma: false
        );
    }

    // Implements [DP-MODEL-SHARED-EMISSION].
    private static void AppendGroupedConstructor(
        StringBuilder builder,
        string typeName,
        IReadOnlyList<string> columnNames,
        IReadOnlyList<DatabaseColumn> allColumns,
        bool trailingComma,
        string? extraParameterDoc,
        string? extraParameter,
        string? extraAssignment
    )
    {
        builder.AppendLine();
        builder.AppendLine("    /// <summary>");
        builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"    /// Initializes a new instance of <see cref=\"{typeName}\"/>."
        );
        builder.AppendLine("    /// </summary>");
        AppendParameterDocumentation(builder: builder, columnNames: columnNames);
        if (extraParameterDoc is not null)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"    /// {extraParameterDoc}");
        }
        builder.AppendLine(CultureInfo.InvariantCulture, $"    public {typeName}(");
        AppendGroupedParameters(
            builder: builder,
            columnNames: columnNames,
            allColumns: allColumns,
            trailingComma: trailingComma
        );
        if (extraParameter is not null)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"        {extraParameter}");
        }
        builder.AppendLine("    )");
        builder.AppendLine("    {");
        AppendAssignments(builder: builder, columnNames: columnNames);
        if (extraAssignment is not null)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"        {extraAssignment}");
        }
    }

    // Implements [DP-MODEL-SHARED-EMISSION].
    internal static void AppendIndexedLines(
        StringBuilder builder,
        int count,
        Func<int, string, string> render
    )
    {
        for (var i = 0; i < count; i++)
        {
            var comma = i == count - 1 ? "" : ",";
            builder.AppendLine(render(i, comma));
        }
    }

    // Implements [DP-MODEL-SHARED-EMISSION].
    private static void AppendAssignments(StringBuilder builder, IEnumerable<string> columnNames)
    {
        foreach (var col in columnNames)
            builder.AppendLine(CultureInfo.InvariantCulture, $"        this.{col} = {col};");
    }

    // Implements [DP-MODEL-SHARED-EMISSION].
    private static void AppendParameterDocumentation(
        StringBuilder builder,
        IReadOnlyList<string> columnNames
    )
    {
        foreach (var col in columnNames)
            builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"    /// <param name=\"{col}\">The '{col}'.</param>"
            );
    }

    // Implements [DP-MODEL-SHARED-EMISSION].
    private static void AppendGroupedParameters(
        StringBuilder builder,
        IReadOnlyList<string> columnNames,
        IReadOnlyList<DatabaseColumn> allColumns,
        bool trailingComma
    )
    {
        AppendParameters(
            builder: builder,
            columns: columnNames,
            csharpType: column =>
                allColumns.FirstOrDefault(c => c.Name == column)?.CSharpType ?? "object",
            columnName: column => column,
            indentation: "        ",
            trailingComma: trailingComma
        );
    }

    // Implements [DP-MODEL-SHARED-EMISSION].
    private static void AppendParameters<TColumn>(
        StringBuilder builder,
        IReadOnlyList<TColumn> columns,
        Func<TColumn, string> csharpType,
        Func<TColumn, string> columnName,
        string indentation,
        bool trailingComma
    )
    {
        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var type = csharpType(arg: column);
            var name = columnName(arg: column);
            var comma = trailingComma || i < columns.Count - 1 ? "," : string.Empty;
            builder.AppendLine(CultureInfo.InvariantCulture, $"{indentation}{type} {name}{comma}");
        }
    }

    // Implements [DP-CLI-SHARED-EMISSION-TEXT].
    internal static void AppendSourceLines(
        StringBuilder builder,
        global::System.Collections.Immutable.ImmutableArray<string> lines
    )
    {
        foreach (var line in lines)
        {
            builder.AppendLine(value: line);
        }
    }

    // Implements [DP-MODEL-SHARED-EMISSION].
    internal static void AppendBlockAndLine(
        StringBuilder builder,
        global::System.Collections.Immutable.ImmutableArray<string> block,
        string line
    )
    {
        AppendSourceLines(builder: builder, lines: block);
        builder.AppendLine(line);
    }

    // Implements [DP-MODEL-SHARED-EMISSION].
    internal static void AppendGroupedConstruction(
        StringBuilder builder,
        string parentName,
        IReadOnlyList<string> parentColumns,
        string childName,
        IReadOnlyList<string> childColumns
    )
    {
        AppendConstructedProperties(
            builder: builder,
            header: $"            var parent = new {parentName}(",
            count: parentColumns.Count,
            render: (i, comma) => $"                firstItem.{parentColumns[i]}{comma}"
        );
        builder.AppendLine("                ,");
        AppendConstructedProperties(
            builder: builder,
            header: $"                group.Select(item => new {childName}(",
            count: childColumns.Count,
            render: (i, comma) => $"                    item.{childColumns[i]}{comma}"
        );
    }

    private static void AppendConstructedProperties(
        StringBuilder builder,
        string header,
        int count,
        Func<int, string, string> render
    )
    {
        builder.AppendLine(header);
        AppendIndexedLines(builder: builder, count: count, render: render);
    }

    // Implements [DP-MODEL-SHARED-EMISSION].
    internal static Result<string, SqlError> CompleteResult(
        StringBuilder builder,
        global::System.Collections.Immutable.ImmutableArray<string> tail
    )
    {
        AppendSourceLines(builder: builder, lines: tail);
        return new Result<string, SqlError>.Ok<string, SqlError>(builder.ToString());
    }
}
