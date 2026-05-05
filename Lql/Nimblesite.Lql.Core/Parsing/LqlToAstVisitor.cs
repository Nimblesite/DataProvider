using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Nimblesite.Sql.Model;

namespace Nimblesite.Lql.Core.Parsing;

/// <summary>
/// Visitor that converts ANTLR parse tree to transpiler AST nodes.
/// </summary>
internal sealed class LqlToAstVisitor : LqlBaseVisitor<INode>
{
    private readonly Dictionary<string, INode> _variables = [];
    private HashSet<string>? _lambdaScope;
    private readonly string _sourceCode;

    /// <summary>
    /// Initializes a new instance of the <see cref="LqlToAstVisitor"/> class.
    /// </summary>
    /// <param name="sourceCode">Optional LQL source code used to enrich error positions.</param>
    public LqlToAstVisitor(string sourceCode = "")
    {
        _sourceCode = sourceCode;
    }

    /// <summary>
    /// Creates a SqlError with position information from an ANTLR context
    /// </summary>
    private SqlError CreateSqlError(string message, ParserRuleContext context)
    {
        var start = context.Start;
        var stop = context.Stop;

        return SqlError.WithDetailedPosition(
            $"Syntax error: {message}",
            start.Line,
            start.Column,
            start.StartIndex,
            stop?.StopIndex ?? start.StartIndex,
            _sourceCode
        );
    }

    /// <summary>
    /// Creates a SqlError with position information from an ANTLR context (static version)
    /// </summary>
    private static SqlError CreateSqlErrorStatic(string message, ParserRuleContext context)
    {
        var start = context.Start;
        var stop = context.Stop;

        return SqlError.WithDetailedPosition(
            $"Syntax error: {message}",
            start.Line,
            start.Column,
            start.StartIndex,
            stop?.StopIndex ?? start.StartIndex,
            null // No source code available in static context
        );
    }

    /// <summary>
    /// Visits a program and returns the last statement result.
    /// </summary>
    /// <param name="context">The program context.</param>
    /// <returns>The last statement's AST node.</returns>
    public override INode VisitProgram([NotNull] LqlParser.ProgramContext context)
    {
        INode? lastResult = null;

        foreach (var statement in context.statement())
        {
            lastResult = Visit(statement);
        }

        var result = lastResult ?? new Identifier("empty");
        return result;
    }

    /// <summary>
    /// Visits a statement.
    /// </summary>
    /// <param name="context">The statement context.</param>
    /// <returns>The statement's AST node.</returns>
    public override INode VisitStatement([NotNull] LqlParser.StatementContext context)
    {
        if (context.letStmt() != null)
        {
            return Visit(context.letStmt());
        }

        if (context.pipeExpr() != null)
        {
            return Visit(context.pipeExpr());
        }

        throw new SqlErrorException(CreateSqlError("Unknown statement type", context));
    }

    /// <summary>
    /// Visits a let statement and stores the variable.
    /// </summary>
    /// <param name="context">The let statement context.</param>
    /// <returns>The pipe expression's AST node.</returns>
    public override INode VisitLetStmt([NotNull] LqlParser.LetStmtContext context)
    {
        string varName = context.IDENT().GetText();

        INode value = Visit(context.pipeExpr());

        _variables[varName] = value;
        return value;
    }

    /// <summary>
    /// Visits a pipe expression and builds a pipeline.
    /// </summary>
    /// <param name="context">The pipe expression context.</param>
    /// <returns>The pipeline AST node.</returns>
    public override INode VisitPipeExpr([NotNull] LqlParser.PipeExprContext context)
    {
        var expressions = context.expr();

        if (expressions.Length == 1)
        {
            var singleResult = Visit(expressions[0]);
            return singleResult;
        }

        var pipeline = new Pipeline();

        // First expression is the base
        INode baseNode = Visit(expressions[0]);
        pipeline.Steps.Add(new IdentityStep { Base = baseNode });

        // Subsequent expressions are pipeline operations
        for (int i = 1; i < expressions.Length; i++)
        {
            var expr = expressions[i];

            var step = ConvertToStep(baseNode, expr);

            pipeline.Steps.Add(step);
            baseNode = new Identifier("temp"); // placeholder for chained operations
        }

        return pipeline;
    }

    /// <summary>
    /// Visits an expression.
    /// </summary>
    /// <param name="context">The expression context.</param>
    /// <returns>The expression's AST node.</returns>
    public override INode VisitExpr([NotNull] LqlParser.ExprContext context)
    {
        if (context.IDENT() != null && context.windowSpec() != null)
        {
            // Window function - create window function identifier
            string functionCall = ExtractWindowFunction(context);
            return new Identifier(functionCall);
        }

        if (context.IDENT() != null && context.argList() == null)
        {
            // Simple identifier - check if it's a variable first
            string name = context.IDENT().GetText();
            if (_variables.TryGetValue(name, out INode? variable))
            {
                return variable;
            }
            return new Identifier(name);
        }

        if (context.IDENT() != null && context.argList() != null)
        {
            // Function call - this will be converted to a step later
            string name = context.IDENT().GetText();
            return new Identifier(name);
        }

        if (context.lambdaExpr() != null)
        {
            // Lambda function expression
            return Visit(context.lambdaExpr());
        }

        if (context.pipeExpr() != null)
        {
            // Parenthesized pipe expression
            return Visit(context.pipeExpr());
        }

        if (context.qualifiedIdent() != null)
        {
            // Qualified identifier like table.column
            return new Identifier(ProcessQualifiedIdentifierToSql(context.qualifiedIdent(), null));
        }

        if (context.INT() != null)
        {
            // Integer literal
            return new Identifier(context.INT().GetText());
        }

        if (context.ASTERISK() != null)
        {
            // Asterisk for SELECT * or COUNT(*)
            return new Identifier("*");
        }

        if (context.STRING() != null)
        {
            // String literal
            return new Identifier(context.STRING().GetText());
        }

        if (context.caseExpr() != null)
        {
            // CASE expression - process with proper formatting
            var caseExpressionText = ProcessCaseExpressionToSql(context.caseExpr(), _lambdaScope);
            return new Identifier(caseExpressionText);
        }

        // No fallback - fail hard if expression type is not handled
        throw new SqlErrorException(
            CreateSqlError($"Unsupported expression type: {context.GetType().Name}", context)
        );
    }

    /// <summary>
    /// Visits a CASE expression and returns the properly formatted SQL.
    /// </summary>
    /// <param name="context">The CASE expression context.</param>
    /// <returns>An identifier containing the formatted CASE statement.</returns>
    public override INode VisitCaseExpr([NotNull] LqlParser.CaseExprContext context)
    {
        var caseExpressionText = ProcessCaseExpressionToSql(context, _lambdaScope);
        return new Identifier(caseExpressionText);
    }

    /// <summary>
    /// Visits a lambda expression and returns the logical expression with variable scope handled.
    /// </summary>
    /// <param name="context">The lambda expression context.</param>
    /// <returns>The lambda expression as an identifier with proper variable scope.</returns>
    public override INode VisitLambdaExpr([NotNull] LqlParser.LambdaExprContext context)
    {
        // Extract the lambda variable names
        var parameters = context.IDENT().Select(ident => ident.GetText()).ToList();

        // Get the logical expression inside the lambda
        var logicalExpr = context.logicalExpr();
        if (logicalExpr != null)
        {
            // Process the logical expression with lambda variable scope
            string conditionText = ProcessLambdaLogicalExpr(logicalExpr, parameters);
            return new Identifier(conditionText);
        }

        // No fallback - fail hard if no logical expression
        throw new NotSupportedException(
            $"Lambda expression must contain a logical expression: {context.GetType().Name}"
        );
    }

    /// <summary>
    /// Processes a logical expression within lambda scope, handling lambda variable scope correctly.
    /// </summary>
    /// <param name="logicalExpr">The logical expression context.</param>
    /// <param name="lambdaVariables">The lambda variable names.</param>
    /// <returns>The processed condition text.</returns>
    private static string ProcessLambdaLogicalExpr(
        LqlParser.LogicalExprContext logicalExpr,
        List<string> lambdaVariables
    )
    {
        // Create a new visitor instance with lambda variable scope
        var visitor = new LqlToAstVisitor();
        visitor._lambdaScope = new HashSet<string>(
            lambdaVariables,
            StringComparer.OrdinalIgnoreCase
        );

        // Process the logical expression and return the text representation
        return ProcessLogicalExpressionToSql(logicalExpr, visitor._lambdaScope);
    }

    /// <summary>
    /// Processes a logical expression to SQL text, respecting lambda variable scope.
    /// </summary>
    /// <param name="logicalExpr">The logical expression context.</param>
    /// <param name="lambdaScope">The lambda variables in scope.</param>
    /// <returns>The SQL condition text.</returns>
    private static string ProcessLogicalExpressionToSql(
        LqlParser.LogicalExprContext logicalExpr,
        HashSet<string>? lambdaScope
    )
    {
        // Process OR expressions
        var andExprs = logicalExpr.andExpr();
        var orParts = new List<string>();

        foreach (var andExpr in andExprs)
        {
            var atomicExprs = andExpr.atomicExpr();
            var andParts = new List<string>();

            foreach (var atomicExpr in atomicExprs)
            {
                if (atomicExpr.comparison() != null)
                {
                    // Process all comparisons through ProcessComparisonToSql
                    // which already handles caseExpr properly
                    andParts.Add(ProcessComparisonToSql(atomicExpr.comparison(), lambdaScope));
                }
                else if (atomicExpr.logicalExpr() != null)
                {
                    andParts.Add(
                        $"({ProcessLogicalExpressionToSql(atomicExpr.logicalExpr(), lambdaScope)})"
                    );
                }
                else
                {
                    // No fallback - fail hard if atomic expression type is not handled
                    throw new NotSupportedException(
                        $"Unsupported atomic expression type: {atomicExpr.GetType().Name}"
                    );
                }
            }

            orParts.Add(string.Join(" AND ", andParts));
        }

        return string.Join(" OR ", orParts);
    }

    /// <summary>
    /// Processes a comparison expression to SQL text, respecting lambda variable scope.
    /// </summary>
    /// <param name="comparison">The comparison context.</param>
    /// <param name="lambdaScope">The lambda variables in scope.</param>
    /// <returns>The SQL comparison text.</returns>
    private static string ProcessComparisonToSql(
        LqlParser.ComparisonContext comparison,
        HashSet<string>? lambdaScope
    )
    {
        // Handle expr which can include caseExpr
        if (comparison.expr() != null)
        {
            var expr = comparison.expr();
            if (expr.caseExpr() != null)
            {
                return ProcessCaseExpressionToSql(expr.caseExpr(), lambdaScope);
            }

            // expr matched the `IDENT '(' argList? ')'` branch — a bare
            // function call (e.g. is_member(u, t)) used as a boolean predicate
            // inside a lambda body without an explicit comparison operator.
            // GitHub issue #40: NAP needs SECURITY DEFINER fn calls to
            // survive LQL transpilation; rewriting via exists() loses
            // SECURITY DEFINER semantics. Emit the call verbatim so RLS
            // policy bodies can call user-defined Postgres functions.
            if (expr.IDENT() != null && expr.ChildCount >= 3)
            {
                return ProcessFnCallExprToSql(expr, lambdaScope);
            }

            // No fallback - fail hard if expr type is not handled
            throw new SqlErrorException(
                CreateSqlErrorStatic(
                    $"Unsupported expr type in comparison: {expr.GetType().Name}",
                    expr as ParserRuleContext
                        ?? throw new ArgumentException("Context must be ParserRuleContext")
                )
            );
        }

        // Handle arithmetic expression comparisons
        if (comparison.arithmeticExpr().Length == 2)
        {
            var left = ProcessArithmeticExpressionToSql(comparison.arithmeticExpr(0), lambdaScope);
            var right = ProcessArithmeticExpressionToSql(comparison.arithmeticExpr(1), lambdaScope);
            var op = comparison.comparisonOp().GetText().ToUpperInvariant();
            return $"{left} {op} {right}";
        }

        // Handle qualified identifier comparisons
        if (comparison.qualifiedIdent().Length > 0)
        {
            var parts = new List<string>();

            foreach (var qualifiedIdent in comparison.qualifiedIdent())
            {
                parts.Add(ProcessQualifiedIdentifierToSql(qualifiedIdent, lambdaScope));
            }

            if (comparison.comparisonOp() != null && parts.Count >= 2)
            {
                var op = comparison.comparisonOp().GetText().ToUpperInvariant();
                return $"{parts[0]} {op} {parts[1]}";
            }

            return string.Join(" ", parts);
        }

        // Handle IS [NOT] NULL checks
        if (comparison.nullCheckExpr() != null)
        {
            var nullCheck = comparison.nullCheckExpr();
            string left;
            if (nullCheck.qualifiedIdent() != null)
            {
                left = ProcessQualifiedIdentifierToSql(nullCheck.qualifiedIdent(), lambdaScope);
            }
            else if (nullCheck.IDENT() != null)
            {
                left = nullCheck.IDENT().GetText();
            }
            else if (nullCheck.PARAMETER() != null)
            {
                left = nullCheck.PARAMETER().GetText();
            }
            else
            {
                // Fallback to raw text if unexpected shape
                left = nullCheck.GetText();
            }

            var isNot = nullCheck.NOT() != null;
            return $"{left} IS {(isNot ? "NOT " : string.Empty)}NULL";
        }

        // No fallback - fail hard if comparison type is not handled
        throw new SqlErrorException(
            CreateSqlErrorStatic(
                $"Unsupported comparison type: {comparison.GetType().Name}",
                comparison
            )
        );
    }

    /// <summary>
    /// Processes an arithmetic expression to SQL text, respecting lambda variable scope.
    /// </summary>
    /// <param name="arithmeticExpr">The arithmetic expression context.</param>
    /// <param name="lambdaScope">The lambda variables in scope.</param>
    /// <returns>The SQL arithmetic expression text.</returns>
    private static string ProcessArithmeticExpressionToSql(
        LqlParser.ArithmeticExprContext arithmeticExpr,
        HashSet<string>? lambdaScope
    )
    {
        // Process arithmetic terms
        var terms = arithmeticExpr.arithmeticTerm();
        var results = new List<string>();

        for (int i = 0; i < terms.Length; i++)
        {
            if (i > 0)
            {
                // Extract actual operator from context
                // The operator is between the terms, so we look at child nodes
                var operatorIndex = (i * 2) - 1; // Operators are at odd indices: term op term op term
                if (operatorIndex < arithmeticExpr.ChildCount)
                {
                    var operatorNode = arithmeticExpr.GetChild(operatorIndex);
                    if (operatorNode is ITerminalNode terminalNode)
                    {
                        results.Add($" {terminalNode.GetText()} ");
                    }
                    else
                    {
                        results.Add(" + "); // Fallback to plus if we can't extract operator
                    }
                }
                else
                {
                    results.Add(" + "); // Fallback to plus if index is out of bounds
                }
            }
            results.Add(ProcessArithmeticTermToSql(terms[i], lambdaScope));
        }

        return string.Join("", results);
    }

    /// <summary>
    /// Processes an arithmetic term to SQL text, respecting lambda variable scope.
    /// </summary>
    /// <param name="arithmeticTerm">The arithmetic term context.</param>
    /// <param name="lambdaScope">The lambda variables in scope.</param>
    /// <returns>The SQL arithmetic term text.</returns>
    private static string ProcessArithmeticTermToSql(
        LqlParser.ArithmeticTermContext arithmeticTerm,
        HashSet<string>? lambdaScope
    )
    {
        // Process arithmetic factors
        var factors = arithmeticTerm.arithmeticFactor();
        var results = new List<string>();

        for (int i = 0; i < factors.Length; i++)
        {
            if (i > 0)
            {
                // Extract actual operator from context
                // The operator is between the factors, so we look at child nodes
                var operatorIndex = (i * 2) - 1; // Operators are at odd indices: factor op factor op factor
                if (operatorIndex < arithmeticTerm.ChildCount)
                {
                    var operatorNode = arithmeticTerm.GetChild(operatorIndex);
                    if (operatorNode is ITerminalNode terminalNode)
                    {
                        results.Add($" {terminalNode.GetText()} ");
                    }
                    else
                    {
                        results.Add(" * "); // Fallback to multiply if we can't extract operator
                    }
                }
                else
                {
                    results.Add(" * "); // Fallback to multiply if index is out of bounds
                }
            }
            results.Add(ProcessArithmeticFactorToSql(factors[i], lambdaScope));
        }

        return string.Join("", results);
    }

    /// <summary>
    /// Processes an arithmetic factor to SQL text, respecting lambda variable scope.
    /// </summary>
    /// <param name="arithmeticFactor">The arithmetic factor context.</param>
    /// <param name="lambdaScope">The lambda variables in scope.</param>
    /// <returns>The SQL arithmetic factor text.</returns>
    private static string ProcessArithmeticFactorToSql(
        LqlParser.ArithmeticFactorContext arithmeticFactor,
        HashSet<string>? lambdaScope
    )
    {
        if (arithmeticFactor.qualifiedIdent() != null)
        {
            return ProcessQualifiedIdentifierToSql(arithmeticFactor.qualifiedIdent(), lambdaScope);
        }

        if (arithmeticFactor.IDENT() != null)
        {
            return arithmeticFactor.IDENT().GetText();
        }

        if (arithmeticFactor.arithmeticExpr() != null)
        {
            return $"({ProcessArithmeticExpressionToSql(arithmeticFactor.arithmeticExpr(), lambdaScope)})";
        }

        if (arithmeticFactor.caseExpr() != null)
        {
            return ProcessCaseExpressionToSql(arithmeticFactor.caseExpr(), lambdaScope);
        }

        if (arithmeticFactor.functionCall() != null)
        {
            return ExtractFunctionCall(arithmeticFactor.functionCall());
        }

        // Handle basic tokens
        if (arithmeticFactor.INT() != null)
        {
            return arithmeticFactor.INT().GetText();
        }

        if (arithmeticFactor.DECIMAL() != null)
        {
            return arithmeticFactor.DECIMAL().GetText();
        }

        if (arithmeticFactor.STRING() != null)
        {
            return arithmeticFactor.STRING().GetText();
        }

        // Handle parameter tokens
        if (arithmeticFactor.PARAMETER() != null)
        {
            return arithmeticFactor.PARAMETER().GetText();
        }

        // No fallback - fail hard if factor type is not handled
        throw new SqlErrorException(
            CreateSqlErrorStatic(
                $"Unsupported arithmetic factor type: {arithmeticFactor.GetType().Name}",
                arithmeticFactor
            )
        );
    }

    /// <summary>
    /// Processes a CASE expression to properly formatted SQL text.
    /// </summary>
    /// <param name="caseExpr">The CASE expression context.</param>
    /// <param name="lambdaScope">The lambda variables in scope.</param>
    /// <returns>The properly formatted SQL CASE statement.</returns>
    private static string ProcessCaseExpressionToSql(
        LqlParser.CaseExprContext caseExpr,
        HashSet<string>? lambdaScope
    )
    {
        var result = new StringBuilder();
        result.Append("CASE");

        // Process all WHEN clauses
        foreach (var whenClause in caseExpr.whenClause())
        {
            result.Append(" WHEN ");

            // Process the condition using the comparison context
            var comparison = whenClause.comparison();
            if (comparison != null)
            {
                var comparisonSql = ProcessComparisonToSql(comparison, lambdaScope);
                result.Append(comparisonSql);
            }

            result.Append(" THEN ");

            // Process the THEN result using the caseResult context
            var thenResult = whenClause.caseResult();
            if (thenResult != null)
            {
                var thenSql = ProcessExpressionToSql(thenResult, lambdaScope);
                result.Append(thenSql);
            }
        }

        // Process ELSE clause if present
        if (caseExpr.ELSE() != null && caseExpr.caseResult() != null)
        {
            result.Append(" ELSE ");
            var elseSql = ProcessExpressionToSql(caseExpr.caseResult(), lambdaScope);
            result.Append(elseSql);
        }

        result.Append(" END");
        return result.ToString();
    }

    /// <summary>
    /// Processes any expression context to SQL text.
    /// </summary>
    /// <param name="context">The parser rule context.</param>
    /// <param name="lambdaScope">The lambda variables in scope.</param>
    /// <returns>The SQL text.</returns>
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    private static string ProcessExpressionToSql(IParseTree context, HashSet<string>? lambdaScope)
#pragma warning restore CA1859
    {
        // Handle different types of expressions
        if (context is LqlParser.ArithmeticExprContext arithmeticExpr)
        {
            return ProcessArithmeticExpressionToSql(arithmeticExpr, lambdaScope);
        }

        if (context is LqlParser.QualifiedIdentContext qualifiedIdent)
        {
            return ProcessQualifiedIdentifierToSql(qualifiedIdent, lambdaScope);
        }
        // Handle CASE expressions properly
        if (context is LqlParser.CaseExprContext caseExpr)
        {
            return ProcessCaseExpressionToSql(caseExpr, lambdaScope);
        }

        if (context is LqlParser.CaseResultContext caseResult)
        {
            // Process the case result based on its content
            if (caseResult.arithmeticExpr() != null)
            {
                return ProcessArithmeticExpressionToSql(caseResult.arithmeticExpr(), lambdaScope);
            }
            if (caseResult.comparison() != null)
            {
                return ProcessComparisonToSql(caseResult.comparison(), lambdaScope);
            }
            if (caseResult.qualifiedIdent() != null)
            {
                return ProcessQualifiedIdentifierToSql(caseResult.qualifiedIdent(), lambdaScope);
            }
            // Handle basic tokens
            if (caseResult.IDENT() != null)
            {
                return caseResult.IDENT().GetText();
            }
            if (caseResult.INT() != null)
            {
                return caseResult.INT().GetText();
            }
            if (caseResult.DECIMAL() != null)
            {
                return caseResult.DECIMAL().GetText();
            }
            if (caseResult.STRING() != null)
            {
                return caseResult.STRING().GetText();
            }
        }

        // No fallback - fail hard if expression type is not handled
        throw new SqlErrorException(
            CreateSqlErrorStatic(
                $"Unsupported expression type: {context.GetType().Name}",
                context as ParserRuleContext
                    ?? throw new ArgumentException("Context must be ParserRuleContext")
            )
        );
    }

    /// <summary>
    /// Process an expr that matched the <c>IDENT '(' argList? ')'</c> branch
    /// (a function call) into SQL text. Lambda-scope-aware so qualified
    /// idents like <c>p.tenant_id</c> strip the <c>p.</c> prefix when
    /// <c>p</c> is bound. Implements GitHub issue #40 — RLS policies must
    /// be able to call user-defined SECURITY DEFINER functions verbatim.
    /// </summary>
    private static string ProcessFnCallExprToSql(
        LqlParser.ExprContext expr,
        HashSet<string>? lambdaScope
    )
    {
        var fnName = expr.IDENT().GetText();
        var args = expr.argList();
        if (args == null)
        {
            return $"{fnName}()";
        }
        var argTexts = args.arg().Select(a => ProcessFnCallArgToSql(a, lambdaScope)).ToList();
        return $"{fnName}({string.Join(", ", argTexts)})";
    }

    private static string ProcessFnCallArgToSql(
        LqlParser.ArgContext arg,
        HashSet<string>? lambdaScope
    )
    {
        if (arg.arithmeticExpr() != null)
        {
            return ProcessArithmeticExpressionToSql(arg.arithmeticExpr(), lambdaScope);
        }
        if (arg.functionCall() != null)
        {
            return ExtractFunctionCall(arg.functionCall());
        }
        if (arg.expr() != null)
        {
            var inner = arg.expr();
            if (inner.IDENT() != null && inner.ChildCount >= 3)
            {
                return ProcessFnCallExprToSql(inner, lambdaScope);
            }
            if (inner.qualifiedIdent() != null)
            {
                return ProcessQualifiedIdentifierToSql(inner.qualifiedIdent(), lambdaScope);
            }
            if (inner.IDENT() != null)
            {
                return inner.IDENT().GetText();
            }
            if (inner.STRING() != null)
            {
                return inner.STRING().GetText();
            }
            if (inner.INT() != null)
            {
                return inner.INT().GetText();
            }
            if (inner.DECIMAL() != null)
            {
                return inner.DECIMAL().GetText();
            }
        }
        if (arg.comparison() != null)
        {
            return ProcessComparisonToSql(arg.comparison(), lambdaScope);
        }
        return ExtractIdentifier(arg);
    }

    /// <summary>
    /// Processes a qualified identifier to SQL text, removing lambda variable prefixes.
    /// </summary>
    /// <param name="qualifiedIdent">The qualified identifier context.</param>
    /// <param name="lambdaScope">The lambda variables in scope.</param>
    /// <returns>The SQL identifier text.</returns>
    private static string ProcessQualifiedIdentifierToSql(
        LqlParser.QualifiedIdentContext qualifiedIdent,
        HashSet<string>? lambdaScope
    )
    {
        var identifierParts = qualifiedIdent.IDENT().Select(i => i.GetText()).ToList();

        // If we have lambda scope and this identifier starts with a lambda variable, remove it
        if (
            lambdaScope != null
            && identifierParts.Count >= 2
            && lambdaScope.Contains(identifierParts[0])
        )
        {
            // Remove the lambda variable prefix
            return string.Join(".", identifierParts.Skip(1));
        }

        // Otherwise, return as-is
        return string.Join(".", identifierParts);
    }

    /// <summary>
    /// Converts an expression to a pipeline step.
    /// </summary>
    /// <param name="baseNode">The base node for the step.</param>
    /// <param name="expr">The expression context.</param>
    /// <returns>The pipeline step.</returns>
    private IStep ConvertToStep(INode baseNode, LqlParser.ExprContext expr)
    {
        if (expr.IDENT() == null)
        {
            throw new SqlErrorException(
                CreateSqlError("Pipeline operations must be function calls", expr)
            );
        }

        string functionName = expr.IDENT().GetText();
        var args = expr.argList()?.arg() ?? [];

        return functionName switch
        {
            "join" => CreateJoinStepWithType(baseNode, args, "INNER JOIN", expr),
            "left_join" => CreateJoinStepWithType(baseNode, args, "LEFT JOIN", expr),
            "cross_join" => CreateJoinStepWithType(baseNode, args, "CROSS JOIN", expr),
            "filter" => CreateFilterStep(baseNode, args),
            "select" => CreateSelectStep(baseNode, args),
            "select_distinct" => CreateSelectDistinctStep(baseNode, args),
            "group_by" => CreateGroupByStep(baseNode, args),
            "order_by" => CreateOrderByStep(baseNode, args),
            "having" => CreateHavingStep(baseNode, args),
            "limit" => CreateLimitStep(baseNode, args),
            "offset" => CreateOffsetStep(baseNode, args),
            "union" => CreateUnionStep(baseNode, args),
            "union_all" => CreateUnionAllStep(baseNode, args),
            "insert" => CreateInsertStep(baseNode, args),
            _ => throw new SqlErrorException(
                CreateSqlError($"Unknown function: {functionName}", expr)
            ),
        };
    }

    /// <summary>
    /// Creates a JOIN step with the specified join type.
    /// </summary>
    /// <param name="baseNode">The base node.</param>
    /// <param name="args">The arguments.</param>
    /// <param name="joinType">The type of join (INNER JOIN, LEFT JOIN, CROSS JOIN, etc.).</param>
    /// <param name="context">The expression context for error reporting.</param>
    /// <returns>The JOIN step.</returns>
    private JoinStep CreateJoinStepWithType(
        INode baseNode,
        LqlParser.ArgContext[] args,
        string joinType,
        LqlParser.ExprContext context
    )
    {
        if (args.Length < 1)
        {
            throw new SqlErrorException(CreateSqlError("join requires table", context));
        }

        string rightTable = ExtractIdentifier(args[0]);
        string? onCondition = null;

        // For CROSS JOIN, no ON condition is needed
        if (joinType != "CROSS JOIN")
        {
            onCondition =
                ExtractNamedArgValue(args, "on")
                ?? throw new SqlErrorException(
                    CreateSqlError($"{joinType} requires 'on' parameter", context)
                );
        }

        // For now, we'll use empty string as leftTable - this gets resolved later in pipeline processing
        var joinRelationship = new JoinRelationship("", rightTable, onCondition ?? "", joinType);

        return new JoinStep { Base = baseNode, JoinRelationship = joinRelationship };
    }

    /// <summary>
    /// Creates a filter step.
    /// </summary>
    /// <param name="baseNode">The base node.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The filter step.</returns>
    private static FilterStep CreateFilterStep(INode baseNode, LqlParser.ArgContext[] args)
    {
        WhereCondition condition;

        if (args.Length > 0)
        {
            // Try to extract condition from arguments and convert to typed WhereCondition
            var conditionText = ExtractConditionFromLambda(args[0]);
            condition = WhereCondition.FromExpression(conditionText);
        }
        else
        {
            // No arguments found, provide safe default
            // TODO: Fix the ANTLR grammar to properly parse lambda expressions
            condition = WhereCondition.FromExpression("TRUE"); // Safe default that doesn't filter anything
        }

        return new FilterStep { Base = baseNode, Condition = condition };
    }

    /// <summary>
    /// Creates a SELECT step.
    /// </summary>
    /// <param name="baseNode">The base node.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The SELECT step.</returns>
    private static SelectStep CreateSelectStep(INode baseNode, LqlParser.ArgContext[] args)
    {
        var columns = args.Select(MapArgToColumnInfo).ToList();
        return new SelectStep(columns) { Base = baseNode };
    }

    /// <summary>
    /// Maps an ANTLR argument context to a ColumnInfo object in a platform-agnostic way.
    /// This handles the abstraction of SQL column selections without platform-specific logic.
    /// </summary>
    /// <param name="arg">The argument context from ANTLR parsing.</param>
    /// <returns>A ColumnInfo representing the column selection.</returns>
    private static ColumnInfo MapArgToColumnInfo(LqlParser.ArgContext arg)
    {
        // Handle column alias first (expressions with "as" keyword)
        if (arg.columnAlias() != null)
        {
            return MapColumnAliasToColumnInfo(arg.columnAlias());
        }

        // Handle function calls (COUNT(*), SUM(column), etc.)
        if (arg.functionCall() != null)
        {
            var functionText = ExtractFunctionCall(arg.functionCall());
            return ColumnInfo.FromExpression(functionText);
        }

        // Handle arithmetic expressions (quantity * price, etc.)
        if (arg.arithmeticExpr() != null)
        {
            var expressionText = ExtractArithmeticExpression(arg.arithmeticExpr());
            return ColumnInfo.FromExpression(expressionText);
        }

        // Handle subqueries (pipeline expressions)
        if (arg.pipeExpr() != null)
        {
            // For subqueries, we create an expression column with the raw text
            // The platform-specific SQL generator will handle proper subquery formatting
            throw new SqlErrorException(
                CreateSqlErrorStatic("Subqueries in column mapping are not supported", arg)
            );
        }

        // Handle simple expressions
        if (arg.expr() != null)
        {
            // Check for CASE expressions
            if (arg.expr().caseExpr() != null)
            {
                var caseExpressionText = ProcessCaseExpressionToSql(arg.expr().caseExpr(), null);
                return ColumnInfo.FromExpression(caseExpressionText);
            }

            // Check for window functions
            if (arg.expr().windowSpec() != null)
            {
                var windowFunction = ExtractWindowFunction(arg.expr());
                return ColumnInfo.FromExpression(windowFunction);
            }

            // Check for asterisk (SELECT *)
            if (arg.expr().ASTERISK() != null)
            {
                return ColumnInfo.Wildcard();
            }

            // Check for qualified identifiers (table.column)
            if (arg.expr().qualifiedIdent() != null)
            {
                var qualifiedIdent = ProcessQualifiedIdentifierToSql(
                    arg.expr().qualifiedIdent(),
                    null
                );
                var parts = qualifiedIdent.Split('.');
                if (parts.Length == 2)
                {
                    return ColumnInfo.Named(parts[1], parts[0]);
                }
                return ColumnInfo.Named(qualifiedIdent);
            }

            // Simple identifier or literal
            throw new SqlErrorException(
                CreateSqlErrorStatic("Unhandled expression type in column selection", arg)
            );
        }

        // No fallback - fail hard if argument type is not handled
        throw new SqlErrorException(
            CreateSqlErrorStatic(
                $"Unsupported argument type in column selection: {arg.GetType().Name}",
                arg
            )
        );
    }

    /// <summary>
    /// Maps a column alias context to a ColumnInfo object.
    /// </summary>
    /// <param name="columnAlias">The column alias context.</param>
    /// <returns>A ColumnInfo with proper column and alias information.</returns>
    private static ColumnInfo MapColumnAliasToColumnInfo(LqlParser.ColumnAliasContext columnAlias)
    {
        string? alias = null;

        // Extract alias if AS keyword is present
        if (columnAlias.AS() != null)
        {
            var allIdents = columnAlias.IDENT();
            if (allIdents != null && allIdents.Length > 0)
            {
                alias = allIdents[^1].GetText(); // Last identifier is the alias
            }
        }

        // Extract the main column/expression
        if (columnAlias.arithmeticExpr() != null)
        {
            var expressionText = ExtractArithmeticExpression(columnAlias.arithmeticExpr());
            return ColumnInfo.FromExpression(expressionText, alias);
        }
        else if (columnAlias.functionCall() != null)
        {
            var functionText = ExtractFunctionCall(columnAlias.functionCall());
            return ColumnInfo.FromExpression(functionText, alias);
        }
        else if (columnAlias.qualifiedIdent() != null)
        {
            var qualifiedIdent = ProcessQualifiedIdentifierToSql(
                columnAlias.qualifiedIdent(),
                null
            );
            var parts = qualifiedIdent.Split('.');
            if (parts.Length == 2)
            {
                return ColumnInfo.Named(parts[1], parts[0], alias);
            }
            return ColumnInfo.Named(qualifiedIdent, null, alias);
        }
        else if (columnAlias.IDENT() != null && columnAlias.IDENT().Length > 0)
        {
            // Get the first IDENT (main column)
            var columnName = columnAlias.IDENT()[0].GetText();
            return ColumnInfo.Named(columnName, null, alias);
        }

        // Fallback - use the full text as expression
        throw new SqlErrorException(
            CreateSqlErrorStatic(
                "Unhandled column alias type in MapColumnAliasToColumnInfo",
                columnAlias
            )
        );
    }

    /// <summary>
    /// Creates a UNION step.
    /// </summary>
    /// <param name="baseNode">The base node.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The UNION step.</returns>
    private UnionStep CreateUnionStep(INode baseNode, LqlParser.ArgContext[] args)
    {
        string otherQuery;

        if (args.Length > 0)
        {
            // Extract the union query from the arguments
            otherQuery = ExtractUnionQuery(args[0]);
        }
        else
        {
            // If no arguments, provide a placeholder that indicates the issue
            otherQuery = "-- UNION query not found in arguments";
        }

        return new UnionStep { Base = baseNode, OtherQuery = otherQuery };
    }

    /// <summary>
    /// Creates an INSERT step.
    /// </summary>
    /// <param name="baseNode">The base node.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The INSERT step.</returns>
    private static InsertStep CreateInsertStep(INode baseNode, LqlParser.ArgContext[] args)
    {
        string table;

        if (args.Length > 0)
        {
            table = ExtractIdentifier(args[0]);
        }
        else
        {
            // If no arguments found, provide a placeholder table name
            table = "unknown_table";
        }

        // Don't specify columns - let the INSERT use the same columns as the SELECT
        var columns = new List<string>(); // Empty list means use SELECT columns

        var insertStep = new InsertStep(table, columns) { Base = baseNode };
        return insertStep;
    }

    /// <summary>
    /// Extracts condition from lambda or other argument types.
    /// </summary>
    /// <param name="arg">The argument context.</param>
    /// <returns>The condition string.</returns>
    private static string ExtractConditionFromLambda(LqlParser.ArgContext arg)
    {
        // Check if this is a lambda expression directly
        if (arg.lambdaExpr() != null)
        {
            return ExtractLambdaCondition(arg.lambdaExpr());
        }

        // Check if this is a lambda function expression
        if (arg.expr() != null)
        {
            var exprContext = arg.expr();
            if (exprContext.lambdaExpr() != null)
            {
                return ExtractLambdaCondition(exprContext.lambdaExpr());
            }

            throw new SqlErrorException(
                CreateSqlErrorStatic(
                    $"Unsupported expression type in ExtractConditionFromLambda: {exprContext.GetType().Name}",
                    exprContext
                )
            );
        }

        // Check if this is a comparison directly
        if (arg.comparison() != null)
        {
            return ProcessComparisonToSql(arg.comparison(), null);
        }

        // Check if this is a pipeExpr that might contain a condition
        if (arg.pipeExpr() != null)
        {
            throw new SqlErrorException(
                CreateSqlErrorStatic(
                    "PipeExpr conditions not yet supported in ExtractConditionFromLambda",
                    arg
                )
            );
        }

        // Check if this is a lambda expression directly
        if (arg.lambdaExpr() != null)
        {
            return ExtractLambdaCondition(arg.lambdaExpr());
        }

        throw new SqlErrorException(
            CreateSqlErrorStatic(
                $"Unsupported argument type in ExtractConditionFromLambda: {arg.GetType().Name}",
                arg
            )
        );
    }

    /// <summary>
    /// Extracts condition from a lambda expression.
    /// </summary>
    /// <param name="lambdaExpr">The lambda expression context.</param>
    /// <returns>The condition string.</returns>
    private static string ExtractLambdaCondition(LqlParser.LambdaExprContext lambdaExpr)
    {
        // Extract the lambda variable names
        var parameters = lambdaExpr.IDENT().Select(ident => ident.GetText()).ToList();

        var logicalExpr = lambdaExpr.logicalExpr();
        if (logicalExpr != null)
        {
            // Use the proper ANTLR visitor to process the logical expression
            return ProcessLambdaLogicalExpr(logicalExpr, parameters);
        }

        throw new SqlErrorException(
            CreateSqlErrorStatic(
                $"Lambda expression must contain a logical expression: {lambdaExpr.GetType().Name}",
                lambdaExpr
            )
        );
    }

    /// <summary>
    /// Extracts a named argument value.
    /// </summary>
    /// <param name="args">The arguments array.</param>
    /// <param name="name">The parameter name.</param>
    /// <returns>The parameter value or null if not found.</returns>
    private static string? ExtractNamedArgValue(LqlParser.ArgContext[] args, string name)
    {
        foreach (var arg in args)
        {
            var namedArg = arg.namedArg();
            if (namedArg != null)
            {
                // Check for IDENT first
                if (namedArg.IDENT()?.GetText() == name)
                {
                    var comparisonText =
                        namedArg.comparison() != null
                            ? ProcessComparisonToSql(namedArg.comparison(), null)
                            : null;
                    var logicalText =
                        namedArg.logicalExpr() != null
                            ? ProcessLogicalExpressionToSql(namedArg.logicalExpr(), null)
                            : null;
                    return comparisonText ?? logicalText;
                }

                // Check for ON keyword
                if (name == "on" && namedArg.ON() != null)
                {
                    var comparisonText =
                        namedArg.comparison() != null
                            ? ProcessComparisonToSql(namedArg.comparison(), null)
                            : null;
                    var logicalText =
                        namedArg.logicalExpr() != null
                            ? ProcessLogicalExpressionToSql(namedArg.logicalExpr(), null)
                            : null;
                    return comparisonText ?? logicalText;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Extracts an identifier from an argument.
    /// </summary>
    /// <param name="arg">The argument context.</param>
    /// <returns>The identifier text.</returns>
    private static string ExtractIdentifier(LqlParser.ArgContext arg)
    {
        // Debug: print what we're processing
        //var argText = arg.GetText();
        //System.Console.WriteLine(
        //    $"ExtractIdentifier processing: {argText[..Math.Min(50, argText.Length)]}..."
        //);

        // Check for columnAlias first (handles expressions with "as" keyword)
        if (arg.columnAlias() != null)
        {
            return ExtractColumnAlias(arg.columnAlias());
        }

        // Check for functionCall (handles count(), sum(), etc.)
        if (arg.functionCall() != null)
        {
            return ExtractFunctionCall(arg.functionCall());
        }

        // Check for arithmeticExpr (handles expressions like quantity * price)
        if (arg.arithmeticExpr() != null)
        {
            return ExtractArithmeticExpression(arg.arithmeticExpr());
        }

        // Check for pipeExpr BEFORE expr - handle subqueries
        if (arg.pipeExpr() != null)
        {
            // For subqueries, we create an expression column with the raw text
            // The platform-specific SQL generator will handle proper subquery formatting
            throw new SqlErrorException(
                CreateSqlErrorStatic("Pipe expressions not supported as identifiers", arg)
            );
        }

        // Check for parenthesized pipeline expressions
        var argText = arg.GetText();
        if (argText.StartsWith("(") && argText.EndsWith(")"))
        {
            throw new NotSupportedException(
                "Parenthesized pipeline expressions not yet supported in ExtractIdentifier"
            );
        }

        // Try to extract from the full expression text (backwards compatibility)
        if (arg.expr() != null)
        {
            // Check if this is a window function
            if (arg.expr().windowSpec() != null)
            {
                return ExtractWindowFunction(arg.expr());
            }
            throw new NotSupportedException(
                $"Unsupported expr type in ExtractIdentifier: {arg.expr().GetType().Name}"
            );
        }

        // Try to extract from comparison if it's a simple comparison
        if (arg.comparison() != null)
        {
            return ProcessComparisonToSql(arg.comparison(), null);
        }

        // No fallback - fail hard if argument type is not handled
        throw new SqlErrorException(
            CreateSqlErrorStatic(
                $"Unsupported argument type in ExtractIdentifier: {arg.GetType().Name}",
                arg
            )
        );
    }

    /// <summary>
    /// Extracts a column alias (e.g., "column as alias" or just "column").
    /// </summary>
    /// <param name="columnAlias">The column alias context.</param>
    /// <returns>The formatted column alias text.</returns>
    private static string ExtractColumnAlias(LqlParser.ColumnAliasContext columnAlias)
    {
        // Extract the main column/expression part
        string columnPart = "";

        if (columnAlias.arithmeticExpr() != null)
        {
            columnPart = ExtractArithmeticExpression(columnAlias.arithmeticExpr());
        }
        else if (columnAlias.functionCall() != null)
        {
            columnPart = ExtractFunctionCall(columnAlias.functionCall());
        }
        else if (columnAlias.qualifiedIdent() != null)
        {
            columnPart = ProcessQualifiedIdentifierToSql(columnAlias.qualifiedIdent(), null);
        }
        else if (columnAlias.IDENT() != null)
        {
            // Get the first IDENT (main column)
            columnPart = columnAlias.IDENT()[0].GetText();
        }

        // Check if there's an AS keyword and alias
        if (columnAlias.AS() != null)
        {
            // Look for the alias identifier - it should be the last IDENT in the context
            var allIdents = columnAlias.IDENT();
            if (allIdents != null && allIdents.Length > 0)
            {
                // The alias is typically the last IDENT in the context
                string alias = allIdents[^1].GetText();

                // Make sure the alias is different from function names in the columnPart
                if (!columnPart.Contains(alias, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{columnPart} AS {alias}";
                }
            }
        }

        // If we can't find the alias through grammar parsing, fail hard
        // No more GetText() fallbacks - proper grammar parsing only

        return columnPart;
    }

    /// <summary>
    /// Extracts a function call (e.g., "count(*)" or "count(distinct column)").
    /// </summary>
    /// <param name="functionCall">The function call context.</param>
    /// <returns>The formatted function call text.</returns>
    private static string ExtractFunctionCall(LqlParser.FunctionCallContext functionCall)
    {
        // Build the function call properly using the grammar structure
        string functionName = functionCall.IDENT().GetText();

        if (functionCall.argList() == null)
        {
            return $"{functionName.ToUpperInvariant()}()";
        }

        // Handle DISTINCT keyword
        string distinctPrefix = functionCall.DISTINCT() != null ? "DISTINCT " : "";

        // Special case for COUNT(*) - check if it's a single asterisk argument
        var argContexts = functionCall.argList().arg();

        // Handle special cases first - check for asterisk
        if (argContexts.Length == 1)
        {
            // Check if it's an asterisk expression
            if (argContexts[0].expr() != null && argContexts[0].expr().ASTERISK() != null)
            {
                return $"{functionName.ToUpperInvariant()}(*)";
            }
            string argText = ExtractIdentifier(argContexts[0]);

            if (argText == "*")
            {
                return $"{functionName.ToUpperInvariant()}(*)";
            }
        }

        // Extract arguments normally for other cases, but handle asterisk specially
        var args = new List<string>();
        foreach (var argContext in argContexts)
        {
            // Check for asterisk in expr
            if (argContext.expr() != null && argContext.expr().ASTERISK() != null)
            {
                args.Add("*");
            }
            else
            {
                args.Add(ExtractIdentifier(argContext));
            }
        }

        return $"{functionName.ToUpperInvariant()}({distinctPrefix}{string.Join(", ", args)})";
    }

    /// <summary>
    /// Extracts an arithmetic expression (e.g., "quantity * price").
    /// </summary>
    /// <param name="arithmeticExpr">The arithmetic expression context.</param>
    /// <returns>The formatted arithmetic expression text.</returns>
    private static string ExtractArithmeticExpression(
        LqlParser.ArithmeticExprContext arithmeticExpr
    ) =>
        // Process arithmetic expression properly using the grammar structure
        ProcessArithmeticExpressionToSql(arithmeticExpr, null);

    /// <summary>
    /// Extracts a window function (e.g., "ROW_NUMBER() OVER (PARTITION BY column ORDER BY column)").
    /// </summary>
    /// <param name="context">The expression context containing the window function.</param>
    /// <returns>The formatted window function text.</returns>
    private static string ExtractWindowFunction(LqlParser.ExprContext context)
    {
        string functionName = context.IDENT().GetText().ToUpperInvariant();

        // Build function call part
        string functionCall = functionName + "()";
        if (context.argList() != null)
        {
            var args = context.argList().arg().Select(ExtractIdentifier).ToArray();
            functionCall = $"{functionName}({string.Join(", ", args)})";
        }

        // Build window specification
        var windowSpec = context.windowSpec();
        string windowClause = "OVER (";

        if (windowSpec.partitionClause() != null)
        {
            var partitionArgs = windowSpec
                .partitionClause()
                .argList()
                .arg()
                .Select(arg => StripTablePrefix(ExtractIdentifier(arg)))
                .ToArray();
            windowClause += $"PARTITION BY {string.Join(", ", partitionArgs)}";
        }

        if (windowSpec.orderClause() != null)
        {
            if (windowSpec.partitionClause() != null)
                windowClause += " ";

            var orderArgs = windowSpec
                .orderClause()
                .argList()
                .arg()
                .Select(ProcessWindowOrderItem)
                .ToArray();
            windowClause += $"ORDER BY {string.Join(", ", orderArgs)}";
        }

        windowClause += ")";

        return $"{functionCall} {windowClause}";
    }

    /// <summary>
    /// TODO: WRONG!
    /// ColumnRef should not be a string to start with! It should be a strong type that
    /// can represent star etc.
    /// </summary>
    /// <param name="columnRef">The column reference (e.g., "orders.total").</param>
    /// <returns>The column name without table prefix (e.g., "total").</returns>
    private static string StripTablePrefix(
        //TODO: this should be a proper type, not a string
        string columnRef
    )
    {
        if (columnRef.Contains('.', StringComparison.Ordinal))
        {
            var parts = columnRef.Split('.');
            if (parts.Length == 2)
            {
                // For window functions, we typically want just the column name
                // unless it's a complex expression
                return parts[1];
            }
        }
        return columnRef;
    }

    /// <summary>
    /// Processes an order item for window functions, handling direction (ASC/DESC) properly.
    /// </summary>
    /// <param name="arg">The argument context containing the order item.</param>
    /// <returns>The formatted order item with proper spacing.</returns>
    private static string ProcessWindowOrderItem(LqlParser.ArgContext arg)
    {
        // Check if it's a comparison with orderDirection
        if (arg.comparison() != null)
        {
            var comparison = arg.comparison();

            // Check if it has an orderDirection
            if (comparison.orderDirection() != null)
            {
                var direction = comparison.orderDirection().ASC() != null ? "ASC" : "DESC";

                // Extract the column name (could be qualifiedIdent or IDENT)
                var columnName =
                    comparison.qualifiedIdent(0) != null
                        ? ProcessQualifiedIdentifierToSql(comparison.qualifiedIdent(0), null)
                        : comparison.IDENT(0)?.GetText()
                            ?? throw new NotSupportedException(
                                "Unknown comparison type in ExtractWindowFunction"
                            );

                return $"{StripTablePrefix(columnName)} {direction}";
            }

            // If no direction specified, just get the column name
            var colName =
                comparison.qualifiedIdent(0) != null
                    ? ProcessQualifiedIdentifierToSql(comparison.qualifiedIdent(0), null)
                    : comparison.IDENT(0)?.GetText()
                        ?? throw new NotSupportedException(
                            "Unsupported comparison type in ExtractWindowFunction"
                        );
            return StripTablePrefix(colName);
        }

        // Fallback to simple extraction
        return StripTablePrefix(ExtractIdentifier(arg));
    }

    /// <summary>
    /// Extracts union query and resolves variable references.
    /// </summary>
    /// <param name="arg">The argument context.</param>
    /// <returns>The union query string.</returns>
    private string ExtractUnionQuery(LqlParser.ArgContext arg)
    {
        // Try to process the argument as a pipe expression first
        if (arg.pipeExpr() != null)
        {
            // Parse the pipe expression as an AST node
            INode pipeNode = VisitPipeExpr(arg.pipeExpr());
            return ConvertVariableToSql(pipeNode);
        }

        // Check if this is a simple variable reference
        if (arg.expr() != null)
        {
            // Process the expression properly instead of using GetText()
            INode exprNode = VisitExpr(arg.expr());
            if (exprNode is Identifier identifier)
            {
                string queryText = identifier.Name;
                if (_variables.TryGetValue(queryText, out INode? variable))
                {
                    // Convert the variable's AST node to SQL
                    return ConvertVariableToSql(variable);
                }
            }

            // If not a variable, convert the expression node to SQL
            return ConvertVariableToSql(exprNode);
        }

        // No fallback - fail hard if argument type is not handled
        throw new NotSupportedException(
            $"Unsupported argument type in ExtractUnionQuery: {arg.GetType().Name}"
        );
    }

    /// <summary>
    /// Converts a variable's AST node to SQL for UNION operations.
    /// </summary>
    /// <param name="variable">The variable's AST node.</param>
    /// <returns>The SQL representation.</returns>
    private static string ConvertVariableToSql(INode variable) =>
        // Convert the AST node to SQL using the same logic as the main conversion
        variable switch
        {
            Pipeline pipeline => ConvertPipelineToSql(pipeline),
            Identifier identifier => $"SELECT *\nFROM {identifier.Name}",
            _ => $"-- Unknown variable type: {variable.GetType().Name}",
        };

    /// <summary>
    /// Converts a pipeline to SQL for UNION operations.
    /// </summary>
    /// <param name="pipeline">The pipeline to convert.</param>
    /// <returns>The SQL representation.</returns>
    private static string ConvertPipelineToSql(Pipeline pipeline)
    {
        if (pipeline.Steps.Count == 0)
        {
            return "-- Empty pipeline";
        }

        // Check if this pipeline contains union operations
        var unionSteps = pipeline.Steps.OfType<UnionStep>().ToList();
        var unionAllSteps = pipeline.Steps.OfType<UnionAllStep>().ToList();

        if (unionSteps.Count > 0 || unionAllSteps.Count > 0)
        {
            // This pipeline contains unions - we need to generate the main query and union parts
            var mainQuery = GenerateMainQueryFromPipeline(pipeline);
            var unionQueries = new List<string>();

            // Add union queries
            foreach (var unionStep in unionSteps)
            {
                unionQueries.Add($"UNION\n{unionStep.OtherQuery}");
            }

            foreach (var unionAllStep in unionAllSteps)
            {
                unionQueries.Add($"UNION ALL\n{unionAllStep.OtherQuery}");
            }

            return $"{mainQuery}\n{string.Join("\n", unionQueries)}";
        }

        // No unions - generate basic SELECT statement
        return GenerateMainQueryFromPipeline(pipeline);
    }

    /// <summary>
    /// Generates the main query part from a pipeline (without unions).
    /// </summary>
    /// <param name="pipeline">The pipeline to convert.</param>
    /// <returns>The main query SQL.</returns>
    private static string GenerateMainQueryFromPipeline(Pipeline pipeline)
    {
        // Look for the base table in the pipeline
        var identityStep = pipeline.Steps.OfType<IdentityStep>().FirstOrDefault();
        if (identityStep?.Base is Identifier baseTable)
        {
            var selectStep = pipeline.Steps.OfType<SelectStep>().FirstOrDefault();
            if (selectStep != null)
            {
                // Generate a simple SELECT statement
                var alias = baseTable.Name[0].ToString().ToLowerInvariant();

                // Process columns - extract just the column names (without table prefixes)
                var processedColumns = selectStep
                    .Columns.Select(col =>
                        col switch
                        {
                            NamedColumn n => n.Name,
                            WildcardColumn => "*",
                            ExpressionColumn e => e.Expression,
                            SubQueryColumn s => $"({s.SubQuery})",
                            _ => "/*UNKNOWN_COLUMN*/",
                        }
                    )
                    .ToList();

                return $"SELECT {alias}.{string.Join($", {alias}.", processedColumns)}\nFROM {baseTable.Name} {alias}";
            }
        }

        return "-- Complex pipeline conversion not implemented";
    }

    /// <summary>
    /// Creates a SELECT DISTINCT step.
    /// </summary>
    /// <param name="baseNode">The base node.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The SELECT DISTINCT step.</returns>
    private static SelectDistinctStep CreateSelectDistinctStep(
        INode baseNode,
        LqlParser.ArgContext[] args
    )
    {
        var columns = args.Select(MapArgToColumnInfo).ToList();
        return new SelectDistinctStep(columns) { Base = baseNode };
    }

    /// <summary>
    /// Creates a GROUP BY step.
    /// </summary>
    /// <param name="baseNode">The base node.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The GROUP BY step.</returns>
    private static GroupByStep CreateGroupByStep(INode baseNode, LqlParser.ArgContext[] args)
    {
        var columns = args.Select(ExtractIdentifier).ToList();
        return new GroupByStep(columns) { Base = baseNode };
    }

    /// <summary>
    /// Creates an ORDER BY step.
    /// </summary>
    /// <param name="baseNode">The base node.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The ORDER BY step.</returns>
    private static OrderByStep CreateOrderByStep(INode baseNode, LqlParser.ArgContext[] args)
    {
        var orderItems = args.Select(ExtractOrderItem).ToList();
        return new OrderByStep(orderItems) { Base = baseNode };
    }

    /// <summary>
    /// Extracts order item (column and direction) from an argument.
    /// </summary>
    /// <param name="arg">The argument context.</param>
    /// <returns>Tuple of column name and direction.</returns>
    private static (string Column, string Direction) ExtractOrderItem(LqlParser.ArgContext arg)
    {
        // Check if it's a comparison with orderDirection
        if (arg.comparison() != null)
        {
            var comparison = arg.comparison();

            // Check if it has an orderDirection
            if (comparison.orderDirection() != null)
            {
                var direction = comparison.orderDirection().ASC() != null ? "ASC" : "DESC";

                // Extract the column name (could be qualifiedIdent or IDENT)
                string columnName;
                if (comparison.qualifiedIdent(0) != null)
                {
                    columnName = ProcessQualifiedIdentifierToSql(
                        comparison.qualifiedIdent(0),
                        null
                    );
                }
                else if (comparison.IDENT(0) != null)
                {
                    columnName = comparison.IDENT(0).GetText();
                }
                else
                {
                    throw new SqlErrorException(
                        CreateSqlErrorStatic(
                            "Unsupported column type in order direction",
                            comparison
                        )
                    );
                }

                return (columnName, direction);
            }

            // If no direction specified, return empty direction
            string colName;
            if (comparison.qualifiedIdent(0) != null)
            {
                colName = ProcessQualifiedIdentifierToSql(comparison.qualifiedIdent(0), null);
            }
            else if (comparison.IDENT(0) != null)
            {
                colName = comparison.IDENT(0).GetText();
            }
            else
            {
                throw new SqlErrorException(
                    CreateSqlErrorStatic("Unsupported column type in order by", comparison)
                );
            }
            return (colName, "");
        }

        // Fallback - just extract the identifier directly using ANTLR grammar
        var text = ExtractIdentifier(arg);

        // The ANTLR grammar should have already parsed ASC/DESC properly,
        // so we don't need regex. Just check for simple string endings as fallback
        if (text.EndsWith("desc", StringComparison.OrdinalIgnoreCase))
        {
            var column = text[..^4].Trim();
            return (column, "DESC");
        }
        if (text.EndsWith("asc", StringComparison.OrdinalIgnoreCase))
        {
            var column = text[..^3].Trim();
            return (column, "ASC");
        }
        return (text, "");
    }

    /// <summary>
    /// Creates a HAVING step.
    /// </summary>
    /// <param name="baseNode">The base node.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The HAVING step.</returns>
    private static HavingStep CreateHavingStep(INode baseNode, LqlParser.ArgContext[] args)
    {
        string condition = args.Length > 0 ? ExtractConditionFromLambda(args[0]) : "true";
        return new HavingStep { Base = baseNode, Condition = condition };
    }

    /// <summary>
    /// Creates a LIMIT step.
    /// </summary>
    /// <param name="baseNode">The base node.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The LIMIT step.</returns>
    private static LimitStep CreateLimitStep(INode baseNode, LqlParser.ArgContext[] args)
    {
        string limit = args.Length > 0 ? ExtractIdentifier(args[0]) : "10";
        return new LimitStep { Base = baseNode, Count = limit };
    }

    /// <summary>
    /// Creates an OFFSET step.
    /// </summary>
    /// <param name="baseNode">The base node.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The OFFSET step.</returns>
    private static OffsetStep CreateOffsetStep(INode baseNode, LqlParser.ArgContext[] args)
    {
        string offset = args.Length > 0 ? ExtractIdentifier(args[0]) : "0";
        return new OffsetStep { Base = baseNode, Count = offset };
    }

    /// <summary>
    /// Creates a UNION ALL step.
    /// </summary>
    /// <param name="baseNode">The base node.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>The UNION ALL step.</returns>
    private UnionAllStep CreateUnionAllStep(INode baseNode, LqlParser.ArgContext[] args)
    {
        string otherQuery =
            args.Length > 0 ? ExtractUnionQuery(args[0]) : "-- UNION ALL query not found";
        return new UnionAllStep { Base = baseNode, OtherQuery = otherQuery };
    }
}
