using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Nimblesite.DataProvider.Core;
using Nimblesite.DataProvider.Core.CodeGeneration;
using Nimblesite.Sql.Model;
using Outcome;
using SqlError = Nimblesite.Sql.Model.SqlError;

namespace Nimblesite.DataProvider.SqlServer;

/// <summary>
/// Non-query emission, table operation generation, and grouping/result helpers
/// for <see cref="SqlFileGenerator"/>.
/// </summary>
public sealed partial class SqlFileGenerator
{
    /// <summary>
    /// Emits a non-query (INSERT/UPDATE/DELETE) extension method without any
    /// compile-time database execution, wrapped in the shared source template.
    /// Parameters come exclusively from the official ScriptDom parser.
    /// </summary>
    private void GenerateNonQuery(
        SourceProductionContext context,
        string baseName,
        string sqlText,
        CodeGenerationConfig codeGenConfig
    )
    {
        var parseResult = _parser.ParseSql(sqlText);
        if (parseResult is not Result<SelectStatement, string>.Ok<SelectStatement, string> parsed)
        {
            var message = parseResult
                is Result<SelectStatement, string>.Error<SelectStatement, string> failure
                ? failure.Value
                : "Unknown parse failure";
            context.ReportDiagnostic(
                ErrorDiag($"Failed to parse SQL file '{baseName}': {message}")
            );
            return;
        }

        var methodResult = DataAccessGenerator.GenerateNonQueryMethod(
            $"{baseName}Extensions",
            baseName,
            sqlText,
            parsed.Value.Parameters.ToList().AsReadOnly(),
            codeGenConfig.ConnectionType
        );
        var wrapped = methodResult switch
        {
            Result<string, SqlError>.Ok<string, SqlError> ok => codeGenConfig.GenerateSourceFile(
                codeGenConfig.TargetNamespace,
                string.Empty,
                ok.Value
            ),
            Result<string, SqlError>.Error<string, SqlError> err => err,
        };
        EmitResult(context, baseName, wrapped);
    }

    /// <summary>
    /// Loads and deserializes the grouping config for a SQL file from either an
    /// explicit override file or the base-name lookup. Reports an error on
    /// malformed JSON.
    /// </summary>
    private static GroupingConfig? LoadGroupingFromLookup(
        SourceProductionContext context,
        string baseName,
        ILookup<string, AdditionalText> groupingByBase
    )
    {
        if (!groupingByBase.Contains(baseName))
        {
            return null;
        }
        return LoadGrouping(context, baseName, groupingByBase[baseName].First());
    }

    /// <summary>
    /// Loads and deserializes the grouping config from an explicit grouping file.
    /// Reports an error on malformed JSON.
    /// </summary>
    private static GroupingConfig? LoadGrouping(
        SourceProductionContext context,
        string baseName,
        AdditionalText groupingFile
    )
    {
        var text = groupingFile.GetText()?.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GroupingConfig>(text, JsonOptions);
        }
        catch (JsonException ex)
        {
            context.ReportDiagnostic(
                ErrorDiag($"Failed to parse {baseName}.grouping.json: {ex.Message}")
            );
            return null;
        }
    }

    /// <summary>
    /// Generates INSERT/UPDATE/DELETE operations for each table listed in the
    /// config's Tables section, using SQL Server schema inspection.
    /// </summary>
    private static void GenerateForTables(
        SourceProductionContext context,
        SourceGeneratorDataProviderConfiguration config,
        CodeGenerationConfig codeGenConfig
    )
    {
        foreach (var tableItem in config.Tables)
        {
            // SQL Server default schema
            var schema = string.IsNullOrWhiteSpace(tableItem.Schema) ? "dbo" : tableItem.Schema;
            var tableResult = SqlServerCodeGenerator
                .GetTableMetadataAsync(config.ConnectionString, schema, tableItem.Name)
                .GetAwaiter()
                .GetResult();
            if (
                tableResult is not Result<DatabaseTable, SqlError>.Ok<DatabaseTable, SqlError> table
            )
            {
                var error = tableResult
                    is Result<DatabaseTable, SqlError>.Error<DatabaseTable, SqlError> tableFailure
                    ? tableFailure.Value.Message
                    : "Unknown table metadata failure";
                context.ReportDiagnostic(
                    ErrorDiag($"Failed to get table metadata for {tableItem.Name}: {error}")
                );
                continue;
            }

            if (tableItem.GenerateDelete)
            {
                // No shared Core delete generator exists yet — never silently drop the request.
                context.ReportDiagnostic(
                    ErrorDiag(
                        $"Table '{schema}.{tableItem.Name}': GenerateDelete is not supported by the shared table operation generator (INSERT/UPDATE only)"
                    )
                );
            }

            var filtered = FilterTable(table.Value, tableItem);
            var operationsResult = codeGenConfig.TableOperationGenerator.GenerateTableOperations(
                filtered,
                new TableConfig
                {
                    Schema = schema,
                    Name = tableItem.Name,
                    GenerateInsert = tableItem.GenerateInsert,
                    GenerateUpdate = tableItem.GenerateUpdate,
                }
            );
            EmitResult(context, $"{schema}_{tableItem.Name}Table", operationsResult);
        }
    }

    /// <summary>
    /// Applies ExcludeColumns and PrimaryKeyColumns to the table metadata before
    /// delegation, since the shared table generator ignores those config values.
    /// </summary>
    private static DatabaseTable FilterTable(DatabaseTable table, TableConfigItem item)
    {
        var excluded = item.ExcludeColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pk = item.PrimaryKeyColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var columns = table
            .Columns.Where(c => !excluded.Contains(c.Name))
            .Select(c => pk.Count == 0 ? c : c with { IsPrimaryKey = pk.Contains(c.Name) })
            .ToList()
            .AsReadOnly();
        return table with { Columns = columns };
    }

    /// <summary>
    /// Wraps emitted member code in a full source file and adds it to the
    /// compilation, reporting an error diagnostic on failure.
    /// </summary>
    private static void EmitResult(
        SourceProductionContext context,
        string hintName,
        Result<string, SqlError> result
    )
    {
        switch (result)
        {
            case Result<string, SqlError>.Ok<string, SqlError> success:
                context.AddSource(
                    hintName + ".g.cs",
                    SourceText.From(success.Value, Encoding.UTF8)
                );
                break;
            case Result<string, SqlError>.Error<string, SqlError> failure:
                context.ReportDiagnostic(
                    ErrorDiag($"Failed to generate code for {hintName}: {failure.Value.Message}")
                );
                break;
        }
    }
}
