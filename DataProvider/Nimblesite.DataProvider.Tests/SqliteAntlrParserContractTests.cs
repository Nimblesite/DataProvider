using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Nimblesite.DataProvider.Parsing.Tests;
using Nimblesite.DataProvider.SQLite.Parsing;

namespace Nimblesite.DataProvider.Tests;

// Concrete SQLite binding for the shared SqlParserContractTests. Every
// test in the base class runs against SqliteAntlrParser. The SQLite
// dialect's differences from Postgres are described via the virtual hooks
// below.
public sealed class SqliteAntlrParserContractTests : SqlParserContractTests
{
    protected override ISqlParser CreateParser() => new SqliteAntlrParser();

    protected override IParseTree ParseRawTree(string sql)
    {
        var input = new AntlrInputStream(sql);
        var lexer = new SQLiteLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new SQLiteParser(tokens);
        return parser.parse();
    }

    // The vendored antlr/grammars-v4 SQLite grammar uses snake_case for its
    // rule contexts (unlike the Postgres grammar which is PascalCase).
    protected override string SelectStmtRuleSuffix => "Select_stmtContext";

    protected override string InsertStmtRuleSuffix => "Insert_stmtContext";

    protected override string UpdateStmtRuleSuffix => "Update_stmtContext";

    protected override string DeleteStmtRuleSuffix => "Delete_stmtContext";

    // SQLite has no ILIKE operator. The default, case-insensitive behaviour
    // of LIKE on ASCII text is close enough for the contract test's intent
    // (parameter extraction from a LIKE-flavoured predicate).
    protected override string CaseInsensitiveLikeOperator => "LIKE";

    // SQLite uses SQL-standard CAST(x AS text), not Postgres's `x::text`.
    protected override string TextCastExpression(string column) => $"CAST({column} AS text)";
}
