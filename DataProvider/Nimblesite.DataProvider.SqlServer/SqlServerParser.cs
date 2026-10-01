using Nimblesite.DataProvider.SqlServer.SqlParsing;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.SqlServer;

/// <summary>
/// SQL Server specific parser implementation using SqlParserCS
/// </summary>
internal sealed class SqlServerParser : ISqlParser
{
    private readonly SqlParserCsImplementation _parser = new();

    /// <summary>
    /// Parses the specified SQL into a Result containing either a SelectStatement or an error.
    /// </summary>
    /// <param name="sql">The SQL text to parse.</param>
    /// <returns>A Result containing either a SelectStatement or an error message.</returns>
    public Result<SelectStatement, string> ParseSql(string sql) =>
        // Use the SqlParserCS library which supports multiple SQL dialects
        _parser.ParseSql(sql);
}
