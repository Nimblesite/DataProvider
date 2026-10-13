// Simplified LQL Language Server
// This is a basic implementation that can be extended with proper LSP dependencies

// Basic types for language server functionality
interface Position {
    line: number;
    character: number;
}

interface Range {
    start: Position;
    end: Position;
}

interface Diagnostic {
    range: Range;
    message: string;
    severity: number;
}

interface TextDocument {
    uri: string;
    getText(): string;
    lineCount: number;
    lineAt(line: number): { text: string };
}

interface CompletionItem {
    label: string;
    kind: number;
    detail?: string;
    documentation?: string;
}

/** Build a single-line range from column 0 to endCharacter. */
function lineRange(line: number, endCharacter: number): Range {
    return {
        start: { line, character: 0 },
        end: { line, character: endCharacter }
    };
}

// Diagnostic severity levels
const DiagnosticSeverity = {
    Error: 1,
    Warning: 2,
    Information: 3,
    Hint: 4
};

// Completion item kinds
const CompletionItemKind = {
    Text: 1,
    Method: 2,
    Function: 3,
    Constructor: 4,
    Field: 5,
    Variable: 6,
    Class: 7,
    Interface: 8,
    Module: 9,
    Property: 10,
    Unit: 11,
    Value: 12,
    Enum: 13,
    Keyword: 14,
    Snippet: 15,
    Color: 16,
    File: 17,
    Reference: 18,
    Folder: 19,
    EnumMember: 20,
    Constant: 21,
    Struct: 22,
    Event: 23,
    Operator: 24,
    TypeParameter: 25
};

// LQL Language Server class
class LqlLanguageServer {
    private documents: Map<string, TextDocument> = new Map();
    private settings = {
        maxNumberOfProblems: 1000,
        validation: { enabled: true },
        formatting: { enabled: true }
    };

    // Validate LQL document
    validateDocument(document: TextDocument): Diagnostic[] {
        if (!this.settings.validation.enabled) {
            return [];
        }

        const diagnostics: Diagnostic[] = [];
        const text = document.getText();
        const lines = text.split(/\r?\n/g);

        for (let i = 0; i < lines.length; i++) {
            const line = lines[i];
            const trimmedLine = line.trim();

            // Skip empty lines and comments
            if (trimmedLine === '' || trimmedLine.startsWith('--')) {
                continue;
            }

            // Check for pipeline operator spacing
            const pipeIndex = line.indexOf('|>');
            if (pipeIndex > 0 && line[pipeIndex - 1] !== ' ') {
                diagnostics.push({
                    range: {
                        start: { line: i, character: pipeIndex },
                        end: { line: i, character: pipeIndex + 2 }
                    },
                    message: 'Pipeline operator should be preceded by a space',
                    severity: DiagnosticSeverity.Warning
                });
            }

            // Check for unmatched brackets
            const openBrackets = (line.match(/[\{\[\(]/g) || []).length;
            const closeBrackets = (line.match(/[\}\]\)]/g) || []).length;
            if (openBrackets !== closeBrackets) {
                diagnostics.push({
                    range: lineRange(i, line.length),
                    message: 'Unmatched brackets detected',
                    severity: DiagnosticSeverity.Error
                });
            }

            // Check for unknown functions
            const knownFunctions = [
                'select', 'filter', 'join', 'group_by', 'order_by', 'having', 'limit', 'offset',
                'union', 'union_all', 'insert', 'update', 'delete',
                'count', 'sum', 'avg', 'min', 'max', 'first', 'last',
                'concat', 'substring', 'length', 'trim', 'upper', 'lower', 'replace',
                'round', 'floor', 'ceil', 'abs', 'sqrt', 'power', 'mod',
                'now', 'today', 'year', 'month', 'day', 'hour', 'minute', 'second',
                'coalesce', 'nullif', 'isnull', 'isnotnull'
            ];

            const functionMatch = line.match(/\b([a-zA-Z_][a-zA-Z0-9_]*)\s*\(/g);
            if (functionMatch) {
                for (const match of functionMatch) {
                    const funcName = match.replace(/\s*\($/, '');
                    if (!knownFunctions.includes(funcName)) {
                        const index = line.indexOf(match);
                        diagnostics.push({
                            range: {
                                start: { line: i, character: index },
                                end: { line: i, character: index + funcName.length }
                            },
                            message: `Unknown function: ${funcName}`,
                            severity: DiagnosticSeverity.Information
                        });
                    }
                }
            }
        }

        return diagnostics;
    }

    // Provide completion items
    getCompletionItems(): CompletionItem[] {
        return [
            // Query operations
            {
                label: 'select',
                kind: CompletionItemKind.Function,
                detail: 'Project columns',
                documentation: 'Projects specified columns from the input data'
            },
            {
                label: 'filter',
                kind: CompletionItemKind.Function,
                detail: 'Filter rows',
                documentation: 'Filters rows based on a condition'
            },
            {
                label: 'join',
                kind: CompletionItemKind.Function,
                detail: 'Join tables',
                documentation: 'Joins two tables based on a condition'
            },
            {
                label: 'group_by',
                kind: CompletionItemKind.Function,
                detail: 'Group rows',
                documentation: 'Groups rows by specified columns'
            },
            {
                label: 'order_by',
                kind: CompletionItemKind.Function,
                detail: 'Order rows',
                documentation: 'Orders rows by specified columns'
            },
            {
                label: 'having',
                kind: CompletionItemKind.Function,
                detail: 'Filter groups',
                documentation: 'Filters groups based on aggregate conditions'
            },
            {
                label: 'limit',
                kind: CompletionItemKind.Function,
                detail: 'Limit results',
                documentation: 'Limits the number of returned rows'
            },
            // Aggregate functions
            {
                label: 'count',
                kind: CompletionItemKind.Function,
                detail: 'Count rows',
                documentation: 'Counts the number of rows'
            },
            {
                label: 'sum',
                kind: CompletionItemKind.Function,
                detail: 'Sum values',
                documentation: 'Calculates the sum of numeric values'
            },
            {
                label: 'avg',
                kind: CompletionItemKind.Function,
                detail: 'Average values',
                documentation: 'Calculates the average of numeric values'
            },
            {
                label: 'max',
                kind: CompletionItemKind.Function,
                detail: 'Maximum value',
                documentation: 'Finds the maximum value'
            },
            {
                label: 'min',
                kind: CompletionItemKind.Function,
                detail: 'Minimum value',
                documentation: 'Finds the minimum value'
            },
            // Pipeline operator
            {
                label: '|>',
                kind: CompletionItemKind.Operator,
                detail: 'Pipeline operator',
                documentation: 'Pipes data from one operation to the next'
            },
            // Keywords
            {
                label: 'let',
                kind: CompletionItemKind.Keyword,
                detail: 'Variable binding',
                documentation: 'Binds a value to a variable'
            },
            {
                label: 'fn',
                kind: CompletionItemKind.Keyword,
                detail: 'Function definition',
                documentation: 'Defines a lambda function'
            }
        ];
    }

    // Get hover information
    getHoverInfo(word: string): string | undefined {
        const functionDocs: { [key: string]: string } = {
            'select': 'Projects specified columns from the input data.\n\n**Syntax:** `|> select column1, column2, ...`',
            'filter': 'Filters rows based on a condition.\n\n**Syntax:** `|> filter (condition)`',
            'join': 'Joins two tables based on a condition.\n\n**Syntax:** `|> join table2 on condition`',
            'group_by': 'Groups rows by specified columns.\n\n**Syntax:** `|> group_by column1, column2, ...`',
            'order_by': 'Orders rows by specified columns.\n\n**Syntax:** `|> order_by column [asc|desc]`',
            'count': 'Counts the number of rows.\n\n**Syntax:** `count(*)` or `count(column)`',
            'sum': 'Calculates the sum of numeric values.\n\n**Syntax:** `sum(column)`',
            'avg': 'Calculates the average of numeric values.\n\n**Syntax:** `avg(column)`',
            'max': 'Finds the maximum value.\n\n**Syntax:** `max(column)`',
            'min': 'Finds the minimum value.\n\n**Syntax:** `min(column)`'
        };

        return functionDocs[word];
    }

    // Format document
    formatDocument(document: TextDocument): { range: Range; newText: string }[] {
        if (!this.settings.formatting.enabled) {
            return [];
        }

        const edits: { range: Range; newText: string }[] = [];
        const text = document.getText();
        const lines = text.split('\n');
        let indentLevel = 0;
        const indentSize = 4;

        for (let i = 0; i < lines.length; i++) {
            const line = lines[i];
            const trimmedLine = line.trim();
            
            if (trimmedLine === '') continue;

            // Decrease indent for closing brackets
            if (trimmedLine.match(/^[\}\]\)]/)) {
                indentLevel = Math.max(0, indentLevel - 1);
            }

            const expectedIndent = ' '.repeat(indentLevel * indentSize);
            const currentIndent = line.match(/^\s*/)?.[0] || '';

            if (currentIndent !== expectedIndent) {
                const range: Range = lineRange(i, currentIndent.length);
                edits.push({ range, newText: expectedIndent });
            }

            // Increase indent for opening brackets and certain keywords
            if (trimmedLine.match(/[\{\[\(]$/) || 
                trimmedLine.match(/^(let|fn|if|case)\b/)) {
                indentLevel++;
            }
        }

        return edits;
    }
}

// Export the language server
export const lqlLanguageServer = new LqlLanguageServer();

// Main server entry point (placeholder for actual LSP implementation)
// In a real implementation, this would set up the LSP connection
console.log('LQL Language Server module loaded');

export default lqlLanguageServer;