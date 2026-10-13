/*
PostgreSQL grammar.
The MIT License (MIT).
Copyright (c) 2021-2023, Oleksii Kovalov (Oleksii.Kovalov@outlook.com).
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;

namespace Nimblesite.DataProvider.Postgres.Parsing;

// Shared error-dispatch body for the lexer and parser listeners: forwards each
// syntax error to the recognizer's own error listeners through a proxy.
class DispatchingErrorListener<T> : IAntlrErrorListener<T>
{
    private readonly IEnumerable<IAntlrErrorListener<T>> _listeners;

    protected DispatchingErrorListener(IEnumerable<IAntlrErrorListener<T>> listeners) =>
        _listeners = listeners;

    public void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        T offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e
    )
    {
        var foo = new ProxyErrorListener<T>(_listeners);
        foo.SyntaxError(output, recognizer, offendingSymbol, line, charPositionInLine, msg, e);
    }
}
