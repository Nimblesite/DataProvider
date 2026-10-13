using System.Collections.Immutable;
using Antlr4.Runtime;
using Nimblesite.DataProvider.SQLite.Parsing;

namespace Nimblesite.DataProvider.Migration.SQLite;

// Implements [SYNC-HTTP-PROVIDERS]. Preserve CHECK metadata when verifying the shared schema.
internal static class SqliteColumnChecks
{
    internal static (
        IReadOnlyList<ColumnDefinition> Columns,
        IReadOnlyList<CheckConstraintDefinition> Checks
    ) Read(SqliteConnection connection, string tableName, IReadOnlyList<ColumnDefinition> columns)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = @table";
        command.Parameters.AddWithValue(parameterName: "@table", value: tableName);
        var sql = command.ExecuteScalar()?.ToString() ?? string.Empty;
        var parser = new SQLiteParser(
            new CommonTokenStream(new SQLiteLexer(new AntlrInputStream(sql)))
        );
        var table = parser.create_table_stmt();
        return (
            columns
                .Select(column =>
                    ReadColumn(column: column, declarations: table.column_def(), sql: sql)
                )
                .ToImmutableArray(),
            table
                .table_constraint()
                .Where(constraint => constraint.CHECK_() is not null)
                .Select(constraint => new CheckConstraintDefinition
                {
                    Name = constraint.name()?.GetText().Trim('[', ']', '"', '`') ?? string.Empty,
                    Expression = sql[
                        constraint.expr().Start.StartIndex..(constraint.expr().Stop.StopIndex + 1)
                    ],
                })
                .ToImmutableArray()
        );
    }

    private static ColumnDefinition ReadColumn(
        ColumnDefinition column,
        SQLiteParser.Column_defContext[] declarations,
        string sql
    )
    {
        var declaration = declarations.FirstOrDefault(candidate =>
            candidate.column_name().GetText().Trim('[', ']', '"', '`') == column.Name
        );
        var constraint = declaration
            ?.column_constraint()
            .FirstOrDefault(candidate => candidate.CHECK_() is not null);
        var expression = constraint?.expr();
        return expression is null
            ? column
            : column with
            {
                CheckConstraint = sql[expression.Start.StartIndex..(expression.Stop.StopIndex + 1)],
                CheckConstraintName = constraint?.name()?.GetText().Trim('[', ']', '"', '`'),
            };
    }
}
