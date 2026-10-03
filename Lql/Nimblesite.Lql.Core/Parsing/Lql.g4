grammar Lql;

program
    : statement* EOF
    ;

statement
    : withStmt
    | letStmt
    | pipeExpr
    ;

// Common table expressions: with name as (pipeline), ... main pipeline
withStmt
    : WITH cteDef (',' cteDef)* pipeExpr
    ;

cteDef
    : IDENT AS '(' pipeExpr ')'
    ;

letStmt
    : 'let' IDENT '=' pipeExpr
    ;

pipeExpr
    : expr ('|>' expr)*
    ;

expr
    : IDENT '(' argList? ')' OVER '(' windowSpec ')'  // Window function
    | IDENT '(' argList? ')'                          // Function call with arguments
    | IDENT                                           // Simple identifier  
    | '(' pipeExpr ')'                                // Parenthesized pipe expression
    | qualifiedIdent                                  // Qualified identifier like table.column
    | lambdaExpr                                      // Lambda expression
    | caseExpr                                        // Case expressions
    | INT                                             // Integer literal
    | DECIMAL                                         // Decimal literal
    | '*'                                             // Asterisk for SELECT * and COUNT(*)
    | STRING                                          // String literal
    | PARAMETER                                       // Parameter like @customerId
    ;

windowSpec
    : partitionClause? orderClause?
    ;

partitionClause
    : PARTITION BY argList
    ;

orderClause
    : ORDER BY orderByArgList
    ;

orderByArgList
    : orderByArg (',' orderByArg)*
    ;

orderByArg
    : (arithmeticExpr | functionCall | qualifiedIdent | IDENT) orderDirection?
    ;

lambdaExpr
    : 'fn' '(' IDENT (',' IDENT)* ')' '=>' logicalExpr  // Multiple params
    ;

qualifiedIdent
    : IDENT ('.' IDENT)+
    ;

argList
    : arg (',' arg)*
    ;

arg
    : columnAlias             // Column with alias: column as alias
    | arithmeticExpr          // Arithmetic expressions
    | functionCall            // Function calls
    | caseExpr                // Case expressions
    | expr                    // Simple expressions
    | namedArg                // Named arguments
    | comparison              // Comparison expressions
    | pipeExpr                // Pipe expressions
    | lambdaExpr              // Lambda expressions
    | '(' pipeExpr ')'        // Allow parenthesized pipelines as arguments
    ;

columnAlias
    : (arithmeticExpr | functionCall | qualifiedIdent | IDENT) (AS IDENT)?
    ;

arithmeticExpr
    : arithmeticTerm (('+'|'-'|'||') arithmeticTerm)*
    ;

arithmeticTerm
    : arithmeticFactor (('*'|'/'|'%') arithmeticFactor)*
    ;

arithmeticFactor
    : qualifiedIdent
    | IDENT
    | INT
    | DECIMAL
    | STRING
    | functionCall
    | caseExpr
    | PARAMETER
    | '(' arithmeticExpr ')'
    ;

functionCall
    : IDENT '(' (DISTINCT? argList)? ')' (OVER '(' windowSpec ')')?
    ;

namedArg
    : (IDENT | ON) '=' (comparison | logicalExpr)
    ;

logicalExpr
    : andExpr (OR andExpr)*
    ;

andExpr
    : atomicExpr (AND atomicExpr)*
    ;

atomicExpr
    : comparison
    | '(' logicalExpr ')'
    ;

comparison
    : arithmeticExpr comparisonOp arithmeticExpr
    | qualifiedIdent comparisonOp (qualifiedIdent | STRING | IDENT | INT | DECIMAL | PARAMETER)
    | IDENT comparisonOp (qualifiedIdent | STRING | IDENT | INT | DECIMAL | PARAMETER)
    | PARAMETER comparisonOp (qualifiedIdent | STRING | IDENT | INT | DECIMAL | PARAMETER)
    | qualifiedIdent (orderDirection)?
    | IDENT (orderDirection)?
    | PARAMETER (orderDirection)?
    | STRING
    | INT
    | DECIMAL
    | expr
    | existsExpr
    | nullCheckExpr
    | inExpr
    ;

existsExpr
    : EXISTS '(' pipeExpr ')'
    ;

nullCheckExpr
    : (qualifiedIdent | IDENT | PARAMETER) (IS NOT? NULL)
    ;

inExpr
    : (qualifiedIdent | IDENT | PARAMETER) IN '(' (pipeExpr | argList) ')'
    ;

caseExpr
    : CASE whenClause+ (ELSE caseResult)? END
    ;

whenClause
    : WHEN comparison THEN caseResult
    ;

caseResult
    : arithmeticExpr
    | comparison
    | qualifiedIdent
    | IDENT
    | INT
    | DECIMAL
    | STRING
    | PARAMETER
    ;

orderDirection
    : ASC
    | DESC
    ;

comparisonOp
    : '='
    | '!='
    | '<>'
    | '<'
    | '>'
    | '<='
    | '>='
    | LIKE
    ;

// Keywords - these must come before IDENT to have priority
ASC: A S C;
DESC: D E S C;
AND: A N D;
OR: O R;
DISTINCT: D I S T I N C T;
EXISTS: E X I S T S;
NULL: N U L L;
IS: I S;
NOT: N O T;
IN: I N;
AS: A S;
CASE: C A S E;
WHEN: W H E N;
THEN: T H E N;
ELSE: E L S E;
END: E N D;
WITH: W I T H;
OVER: O V E R;
PARTITION: P A R T I T I O N;
ORDER: O R D E R;
BY: B Y;
COALESCE: C O A L E S C E;
EXTRACT: E X T R A C T;
FROM: F R O M;
INTERVAL: I N T E R V A L;
CURRENT_DATE: C U R R E N T '_' D A T E;
DATE_TRUNC: D A T E '_' T R U N C;
ON: O N;
LIKE: L I K E;

// Case-insensitive character fragments
fragment A: [aA];
fragment B: [bB];
fragment C: [cC];
fragment D: [dD];
fragment E: [eE];
fragment F: [fF];
fragment G: [gG];
fragment H: [hH];
fragment I: [iI];
fragment J: [jJ];
fragment K: [kK];
fragment L: [lL];
fragment M: [mM];
fragment N: [nN];
fragment O: [oO];
fragment P: [pP];
fragment Q: [qQ];
fragment R: [rR];
fragment S: [sS];
fragment T: [tT];
fragment U: [uU];
fragment V: [vV];
fragment W: [wW];
fragment X: [xX];
fragment Y: [yY];
fragment Z: [zZ];

PARAMETER
    : '@' [a-zA-Z_][a-zA-Z0-9_]*
    ;

IDENT
    : [a-zA-Z_][a-zA-Z0-9_]*
    ;

INT
    : [0-9]+
    ;

DECIMAL
    : [0-9]+ '.' [0-9]+
    ;

STRING
    : '\'' (~['\\] | '\\' .)* '\''
    ;

COMMENT
    : '--' ~[\r\n]* -> skip
    ;

WS
    : [ \t\r\n]+ -> skip
    ;

ASTERISK
    : '*'
    ;
