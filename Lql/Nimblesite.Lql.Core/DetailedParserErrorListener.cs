using Antlr4.Runtime;
using Nimblesite.Sql.Model;

namespace Nimblesite.Lql.Core;

/// <summary>
/// Shared ANTLR error listener with symbol-specific position extraction.
/// Implements [LQL-PARSER-SHARED-ERROR-LISTENER].
/// </summary>
internal sealed class DetailedParserErrorListener<TSymbol> : IAntlrErrorListener<TSymbol>
{
    private readonly List<SqlError> _errors = [];
    private readonly string _source;
    private readonly Func<TSymbol, (int Start, int Stop)> _position;

    public DetailedParserErrorListener(string source, Func<TSymbol, (int Start, int Stop)> position)
    {
        _source = source;
        _position = position;
    }

    public IReadOnlyList<SqlError> Errors => _errors;

    public void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        TSymbol offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e
    )
    {
        var (startIndex, stopIndex) = _position(arg: offendingSymbol);

        var error = SqlError.WithDetailedPosition(
            message: $"Syntax error: {msg}",
            line: line,
            column: charPositionInLine,
            startIndex: startIndex,
            stopIndex: stopIndex,
            source: _source
        );

        _errors.Add(error);
    }
}
