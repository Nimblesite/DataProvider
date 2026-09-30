// Generated from /home/user/DataProvider/Lql/Lql/Parsing/Lql.g4 by ANTLR 4.8
// Generated from Lql.g4 by ANTLR 4.8
use super::lqlparser::*;
use antlr_rust::tree::{ParseTreeVisitor, ParseTreeVisitorCompat};

/**
 * This interface defines a complete generic visitor for a parse tree produced
 * by {@link LqlParser}.
 */
pub trait LqlVisitor<'input>: ParseTreeVisitor<'input, LqlParserContextType> {
    /**
     * Visit a parse tree produced by {@link LqlParser#program}.
     * @param ctx the parse tree
     */
    fn visit_program(&mut self, ctx: &ProgramContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#statement}.
     * @param ctx the parse tree
     */
    fn visit_statement(&mut self, ctx: &StatementContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#withStmt}.
     * @param ctx the parse tree
     */
    fn visit_withStmt(&mut self, ctx: &WithStmtContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#cteDef}.
     * @param ctx the parse tree
     */
    fn visit_cteDef(&mut self, ctx: &CteDefContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#letStmt}.
     * @param ctx the parse tree
     */
    fn visit_letStmt(&mut self, ctx: &LetStmtContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#pipeExpr}.
     * @param ctx the parse tree
     */
    fn visit_pipeExpr(&mut self, ctx: &PipeExprContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#expr}.
     * @param ctx the parse tree
     */
    fn visit_expr(&mut self, ctx: &ExprContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#windowSpec}.
     * @param ctx the parse tree
     */
    fn visit_windowSpec(&mut self, ctx: &WindowSpecContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#partitionClause}.
     * @param ctx the parse tree
     */
    fn visit_partitionClause(&mut self, ctx: &PartitionClauseContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#orderClause}.
     * @param ctx the parse tree
     */
    fn visit_orderClause(&mut self, ctx: &OrderClauseContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#orderByArgList}.
     * @param ctx the parse tree
     */
    fn visit_orderByArgList(&mut self, ctx: &OrderByArgListContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#orderByArg}.
     * @param ctx the parse tree
     */
    fn visit_orderByArg(&mut self, ctx: &OrderByArgContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#lambdaExpr}.
     * @param ctx the parse tree
     */
    fn visit_lambdaExpr(&mut self, ctx: &LambdaExprContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#qualifiedIdent}.
     * @param ctx the parse tree
     */
    fn visit_qualifiedIdent(&mut self, ctx: &QualifiedIdentContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#argList}.
     * @param ctx the parse tree
     */
    fn visit_argList(&mut self, ctx: &ArgListContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#arg}.
     * @param ctx the parse tree
     */
    fn visit_arg(&mut self, ctx: &ArgContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#columnAlias}.
     * @param ctx the parse tree
     */
    fn visit_columnAlias(&mut self, ctx: &ColumnAliasContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#arithmeticExpr}.
     * @param ctx the parse tree
     */
    fn visit_arithmeticExpr(&mut self, ctx: &ArithmeticExprContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#arithmeticTerm}.
     * @param ctx the parse tree
     */
    fn visit_arithmeticTerm(&mut self, ctx: &ArithmeticTermContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#arithmeticFactor}.
     * @param ctx the parse tree
     */
    fn visit_arithmeticFactor(&mut self, ctx: &ArithmeticFactorContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#functionCall}.
     * @param ctx the parse tree
     */
    fn visit_functionCall(&mut self, ctx: &FunctionCallContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#namedArg}.
     * @param ctx the parse tree
     */
    fn visit_namedArg(&mut self, ctx: &NamedArgContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#logicalExpr}.
     * @param ctx the parse tree
     */
    fn visit_logicalExpr(&mut self, ctx: &LogicalExprContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#andExpr}.
     * @param ctx the parse tree
     */
    fn visit_andExpr(&mut self, ctx: &AndExprContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#atomicExpr}.
     * @param ctx the parse tree
     */
    fn visit_atomicExpr(&mut self, ctx: &AtomicExprContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#comparison}.
     * @param ctx the parse tree
     */
    fn visit_comparison(&mut self, ctx: &ComparisonContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#existsExpr}.
     * @param ctx the parse tree
     */
    fn visit_existsExpr(&mut self, ctx: &ExistsExprContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#nullCheckExpr}.
     * @param ctx the parse tree
     */
    fn visit_nullCheckExpr(&mut self, ctx: &NullCheckExprContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#inExpr}.
     * @param ctx the parse tree
     */
    fn visit_inExpr(&mut self, ctx: &InExprContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#caseExpr}.
     * @param ctx the parse tree
     */
    fn visit_caseExpr(&mut self, ctx: &CaseExprContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#whenClause}.
     * @param ctx the parse tree
     */
    fn visit_whenClause(&mut self, ctx: &WhenClauseContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#caseResult}.
     * @param ctx the parse tree
     */
    fn visit_caseResult(&mut self, ctx: &CaseResultContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#orderDirection}.
     * @param ctx the parse tree
     */
    fn visit_orderDirection(&mut self, ctx: &OrderDirectionContext<'input>) {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#comparisonOp}.
     * @param ctx the parse tree
     */
    fn visit_comparisonOp(&mut self, ctx: &ComparisonOpContext<'input>) {
        self.visit_children(ctx)
    }
}

pub trait LqlVisitorCompat<'input>:
    ParseTreeVisitorCompat<'input, Node = LqlParserContextType>
{
    /**
     * Visit a parse tree produced by {@link LqlParser#program}.
     * @param ctx the parse tree
     */
    fn visit_program(&mut self, ctx: &ProgramContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#statement}.
     * @param ctx the parse tree
     */
    fn visit_statement(&mut self, ctx: &StatementContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#withStmt}.
     * @param ctx the parse tree
     */
    fn visit_withStmt(&mut self, ctx: &WithStmtContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#cteDef}.
     * @param ctx the parse tree
     */
    fn visit_cteDef(&mut self, ctx: &CteDefContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#letStmt}.
     * @param ctx the parse tree
     */
    fn visit_letStmt(&mut self, ctx: &LetStmtContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#pipeExpr}.
     * @param ctx the parse tree
     */
    fn visit_pipeExpr(&mut self, ctx: &PipeExprContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#expr}.
     * @param ctx the parse tree
     */
    fn visit_expr(&mut self, ctx: &ExprContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#windowSpec}.
     * @param ctx the parse tree
     */
    fn visit_windowSpec(&mut self, ctx: &WindowSpecContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#partitionClause}.
     * @param ctx the parse tree
     */
    fn visit_partitionClause(&mut self, ctx: &PartitionClauseContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#orderClause}.
     * @param ctx the parse tree
     */
    fn visit_orderClause(&mut self, ctx: &OrderClauseContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#orderByArgList}.
     * @param ctx the parse tree
     */
    fn visit_orderByArgList(&mut self, ctx: &OrderByArgListContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#orderByArg}.
     * @param ctx the parse tree
     */
    fn visit_orderByArg(&mut self, ctx: &OrderByArgContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#lambdaExpr}.
     * @param ctx the parse tree
     */
    fn visit_lambdaExpr(&mut self, ctx: &LambdaExprContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#qualifiedIdent}.
     * @param ctx the parse tree
     */
    fn visit_qualifiedIdent(&mut self, ctx: &QualifiedIdentContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#argList}.
     * @param ctx the parse tree
     */
    fn visit_argList(&mut self, ctx: &ArgListContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#arg}.
     * @param ctx the parse tree
     */
    fn visit_arg(&mut self, ctx: &ArgContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#columnAlias}.
     * @param ctx the parse tree
     */
    fn visit_columnAlias(&mut self, ctx: &ColumnAliasContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#arithmeticExpr}.
     * @param ctx the parse tree
     */
    fn visit_arithmeticExpr(&mut self, ctx: &ArithmeticExprContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#arithmeticTerm}.
     * @param ctx the parse tree
     */
    fn visit_arithmeticTerm(&mut self, ctx: &ArithmeticTermContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#arithmeticFactor}.
     * @param ctx the parse tree
     */
    fn visit_arithmeticFactor(&mut self, ctx: &ArithmeticFactorContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#functionCall}.
     * @param ctx the parse tree
     */
    fn visit_functionCall(&mut self, ctx: &FunctionCallContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#namedArg}.
     * @param ctx the parse tree
     */
    fn visit_namedArg(&mut self, ctx: &NamedArgContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#logicalExpr}.
     * @param ctx the parse tree
     */
    fn visit_logicalExpr(&mut self, ctx: &LogicalExprContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#andExpr}.
     * @param ctx the parse tree
     */
    fn visit_andExpr(&mut self, ctx: &AndExprContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#atomicExpr}.
     * @param ctx the parse tree
     */
    fn visit_atomicExpr(&mut self, ctx: &AtomicExprContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#comparison}.
     * @param ctx the parse tree
     */
    fn visit_comparison(&mut self, ctx: &ComparisonContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#existsExpr}.
     * @param ctx the parse tree
     */
    fn visit_existsExpr(&mut self, ctx: &ExistsExprContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#nullCheckExpr}.
     * @param ctx the parse tree
     */
    fn visit_nullCheckExpr(&mut self, ctx: &NullCheckExprContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#inExpr}.
     * @param ctx the parse tree
     */
    fn visit_inExpr(&mut self, ctx: &InExprContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#caseExpr}.
     * @param ctx the parse tree
     */
    fn visit_caseExpr(&mut self, ctx: &CaseExprContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#whenClause}.
     * @param ctx the parse tree
     */
    fn visit_whenClause(&mut self, ctx: &WhenClauseContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#caseResult}.
     * @param ctx the parse tree
     */
    fn visit_caseResult(&mut self, ctx: &CaseResultContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#orderDirection}.
     * @param ctx the parse tree
     */
    fn visit_orderDirection(&mut self, ctx: &OrderDirectionContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }

    /**
     * Visit a parse tree produced by {@link LqlParser#comparisonOp}.
     * @param ctx the parse tree
     */
    fn visit_comparisonOp(&mut self, ctx: &ComparisonOpContext<'input>) -> Self::Return {
        self.visit_children(ctx)
    }
}

impl<'input, T> LqlVisitor<'input> for T
where
    T: LqlVisitorCompat<'input>,
{
    fn visit_program(&mut self, ctx: &ProgramContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_program(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_statement(&mut self, ctx: &StatementContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_statement(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_withStmt(&mut self, ctx: &WithStmtContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_withStmt(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_cteDef(&mut self, ctx: &CteDefContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_cteDef(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_letStmt(&mut self, ctx: &LetStmtContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_letStmt(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_pipeExpr(&mut self, ctx: &PipeExprContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_pipeExpr(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_expr(&mut self, ctx: &ExprContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_expr(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_windowSpec(&mut self, ctx: &WindowSpecContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_windowSpec(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_partitionClause(&mut self, ctx: &PartitionClauseContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_partitionClause(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_orderClause(&mut self, ctx: &OrderClauseContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_orderClause(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_orderByArgList(&mut self, ctx: &OrderByArgListContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_orderByArgList(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_orderByArg(&mut self, ctx: &OrderByArgContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_orderByArg(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_lambdaExpr(&mut self, ctx: &LambdaExprContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_lambdaExpr(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_qualifiedIdent(&mut self, ctx: &QualifiedIdentContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_qualifiedIdent(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_argList(&mut self, ctx: &ArgListContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_argList(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_arg(&mut self, ctx: &ArgContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_arg(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_columnAlias(&mut self, ctx: &ColumnAliasContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_columnAlias(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_arithmeticExpr(&mut self, ctx: &ArithmeticExprContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_arithmeticExpr(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_arithmeticTerm(&mut self, ctx: &ArithmeticTermContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_arithmeticTerm(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_arithmeticFactor(&mut self, ctx: &ArithmeticFactorContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_arithmeticFactor(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_functionCall(&mut self, ctx: &FunctionCallContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_functionCall(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_namedArg(&mut self, ctx: &NamedArgContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_namedArg(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_logicalExpr(&mut self, ctx: &LogicalExprContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_logicalExpr(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_andExpr(&mut self, ctx: &AndExprContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_andExpr(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_atomicExpr(&mut self, ctx: &AtomicExprContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_atomicExpr(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_comparison(&mut self, ctx: &ComparisonContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_comparison(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_existsExpr(&mut self, ctx: &ExistsExprContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_existsExpr(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_nullCheckExpr(&mut self, ctx: &NullCheckExprContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_nullCheckExpr(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_inExpr(&mut self, ctx: &InExprContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_inExpr(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_caseExpr(&mut self, ctx: &CaseExprContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_caseExpr(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_whenClause(&mut self, ctx: &WhenClauseContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_whenClause(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_caseResult(&mut self, ctx: &CaseResultContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_caseResult(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_orderDirection(&mut self, ctx: &OrderDirectionContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_orderDirection(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }

    fn visit_comparisonOp(&mut self, ctx: &ComparisonOpContext<'input>) {
        let result = <Self as LqlVisitorCompat>::visit_comparisonOp(self, ctx);
        *<Self as ParseTreeVisitorCompat>::temp_result(self) = result;
    }
}
