// Generated from /home/user/DataProvider/Lql/Lql/Parsing/Lql.g4 by ANTLR 4.8
// Generated from Lql.g4 by ANTLR 4.8
use super::lqlparser::*;
use antlr_rust::tree::ParseTreeListener;

pub trait LqlListener<'input>: ParseTreeListener<'input, LqlParserContextType> {
    /**
     * Enter a parse tree produced by {@link LqlParser#program}.
     * @param ctx the parse tree
     */
    fn enter_program(&mut self, _ctx: &ProgramContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#program}.
     * @param ctx the parse tree
     */
    fn exit_program(&mut self, _ctx: &ProgramContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#statement}.
     * @param ctx the parse tree
     */
    fn enter_statement(&mut self, _ctx: &StatementContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#statement}.
     * @param ctx the parse tree
     */
    fn exit_statement(&mut self, _ctx: &StatementContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#withStmt}.
     * @param ctx the parse tree
     */
    fn enter_withStmt(&mut self, _ctx: &WithStmtContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#withStmt}.
     * @param ctx the parse tree
     */
    fn exit_withStmt(&mut self, _ctx: &WithStmtContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#cteDef}.
     * @param ctx the parse tree
     */
    fn enter_cteDef(&mut self, _ctx: &CteDefContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#cteDef}.
     * @param ctx the parse tree
     */
    fn exit_cteDef(&mut self, _ctx: &CteDefContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#letStmt}.
     * @param ctx the parse tree
     */
    fn enter_letStmt(&mut self, _ctx: &LetStmtContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#letStmt}.
     * @param ctx the parse tree
     */
    fn exit_letStmt(&mut self, _ctx: &LetStmtContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#pipeExpr}.
     * @param ctx the parse tree
     */
    fn enter_pipeExpr(&mut self, _ctx: &PipeExprContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#pipeExpr}.
     * @param ctx the parse tree
     */
    fn exit_pipeExpr(&mut self, _ctx: &PipeExprContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#expr}.
     * @param ctx the parse tree
     */
    fn enter_expr(&mut self, _ctx: &ExprContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#expr}.
     * @param ctx the parse tree
     */
    fn exit_expr(&mut self, _ctx: &ExprContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#windowSpec}.
     * @param ctx the parse tree
     */
    fn enter_windowSpec(&mut self, _ctx: &WindowSpecContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#windowSpec}.
     * @param ctx the parse tree
     */
    fn exit_windowSpec(&mut self, _ctx: &WindowSpecContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#partitionClause}.
     * @param ctx the parse tree
     */
    fn enter_partitionClause(&mut self, _ctx: &PartitionClauseContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#partitionClause}.
     * @param ctx the parse tree
     */
    fn exit_partitionClause(&mut self, _ctx: &PartitionClauseContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#orderClause}.
     * @param ctx the parse tree
     */
    fn enter_orderClause(&mut self, _ctx: &OrderClauseContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#orderClause}.
     * @param ctx the parse tree
     */
    fn exit_orderClause(&mut self, _ctx: &OrderClauseContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#orderByArgList}.
     * @param ctx the parse tree
     */
    fn enter_orderByArgList(&mut self, _ctx: &OrderByArgListContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#orderByArgList}.
     * @param ctx the parse tree
     */
    fn exit_orderByArgList(&mut self, _ctx: &OrderByArgListContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#orderByArg}.
     * @param ctx the parse tree
     */
    fn enter_orderByArg(&mut self, _ctx: &OrderByArgContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#orderByArg}.
     * @param ctx the parse tree
     */
    fn exit_orderByArg(&mut self, _ctx: &OrderByArgContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#lambdaExpr}.
     * @param ctx the parse tree
     */
    fn enter_lambdaExpr(&mut self, _ctx: &LambdaExprContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#lambdaExpr}.
     * @param ctx the parse tree
     */
    fn exit_lambdaExpr(&mut self, _ctx: &LambdaExprContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#qualifiedIdent}.
     * @param ctx the parse tree
     */
    fn enter_qualifiedIdent(&mut self, _ctx: &QualifiedIdentContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#qualifiedIdent}.
     * @param ctx the parse tree
     */
    fn exit_qualifiedIdent(&mut self, _ctx: &QualifiedIdentContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#argList}.
     * @param ctx the parse tree
     */
    fn enter_argList(&mut self, _ctx: &ArgListContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#argList}.
     * @param ctx the parse tree
     */
    fn exit_argList(&mut self, _ctx: &ArgListContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#arg}.
     * @param ctx the parse tree
     */
    fn enter_arg(&mut self, _ctx: &ArgContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#arg}.
     * @param ctx the parse tree
     */
    fn exit_arg(&mut self, _ctx: &ArgContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#columnAlias}.
     * @param ctx the parse tree
     */
    fn enter_columnAlias(&mut self, _ctx: &ColumnAliasContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#columnAlias}.
     * @param ctx the parse tree
     */
    fn exit_columnAlias(&mut self, _ctx: &ColumnAliasContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#arithmeticExpr}.
     * @param ctx the parse tree
     */
    fn enter_arithmeticExpr(&mut self, _ctx: &ArithmeticExprContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#arithmeticExpr}.
     * @param ctx the parse tree
     */
    fn exit_arithmeticExpr(&mut self, _ctx: &ArithmeticExprContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#arithmeticTerm}.
     * @param ctx the parse tree
     */
    fn enter_arithmeticTerm(&mut self, _ctx: &ArithmeticTermContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#arithmeticTerm}.
     * @param ctx the parse tree
     */
    fn exit_arithmeticTerm(&mut self, _ctx: &ArithmeticTermContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#arithmeticFactor}.
     * @param ctx the parse tree
     */
    fn enter_arithmeticFactor(&mut self, _ctx: &ArithmeticFactorContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#arithmeticFactor}.
     * @param ctx the parse tree
     */
    fn exit_arithmeticFactor(&mut self, _ctx: &ArithmeticFactorContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#functionCall}.
     * @param ctx the parse tree
     */
    fn enter_functionCall(&mut self, _ctx: &FunctionCallContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#functionCall}.
     * @param ctx the parse tree
     */
    fn exit_functionCall(&mut self, _ctx: &FunctionCallContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#namedArg}.
     * @param ctx the parse tree
     */
    fn enter_namedArg(&mut self, _ctx: &NamedArgContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#namedArg}.
     * @param ctx the parse tree
     */
    fn exit_namedArg(&mut self, _ctx: &NamedArgContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#logicalExpr}.
     * @param ctx the parse tree
     */
    fn enter_logicalExpr(&mut self, _ctx: &LogicalExprContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#logicalExpr}.
     * @param ctx the parse tree
     */
    fn exit_logicalExpr(&mut self, _ctx: &LogicalExprContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#andExpr}.
     * @param ctx the parse tree
     */
    fn enter_andExpr(&mut self, _ctx: &AndExprContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#andExpr}.
     * @param ctx the parse tree
     */
    fn exit_andExpr(&mut self, _ctx: &AndExprContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#atomicExpr}.
     * @param ctx the parse tree
     */
    fn enter_atomicExpr(&mut self, _ctx: &AtomicExprContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#atomicExpr}.
     * @param ctx the parse tree
     */
    fn exit_atomicExpr(&mut self, _ctx: &AtomicExprContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#comparison}.
     * @param ctx the parse tree
     */
    fn enter_comparison(&mut self, _ctx: &ComparisonContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#comparison}.
     * @param ctx the parse tree
     */
    fn exit_comparison(&mut self, _ctx: &ComparisonContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#existsExpr}.
     * @param ctx the parse tree
     */
    fn enter_existsExpr(&mut self, _ctx: &ExistsExprContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#existsExpr}.
     * @param ctx the parse tree
     */
    fn exit_existsExpr(&mut self, _ctx: &ExistsExprContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#nullCheckExpr}.
     * @param ctx the parse tree
     */
    fn enter_nullCheckExpr(&mut self, _ctx: &NullCheckExprContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#nullCheckExpr}.
     * @param ctx the parse tree
     */
    fn exit_nullCheckExpr(&mut self, _ctx: &NullCheckExprContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#inExpr}.
     * @param ctx the parse tree
     */
    fn enter_inExpr(&mut self, _ctx: &InExprContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#inExpr}.
     * @param ctx the parse tree
     */
    fn exit_inExpr(&mut self, _ctx: &InExprContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#caseExpr}.
     * @param ctx the parse tree
     */
    fn enter_caseExpr(&mut self, _ctx: &CaseExprContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#caseExpr}.
     * @param ctx the parse tree
     */
    fn exit_caseExpr(&mut self, _ctx: &CaseExprContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#whenClause}.
     * @param ctx the parse tree
     */
    fn enter_whenClause(&mut self, _ctx: &WhenClauseContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#whenClause}.
     * @param ctx the parse tree
     */
    fn exit_whenClause(&mut self, _ctx: &WhenClauseContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#caseResult}.
     * @param ctx the parse tree
     */
    fn enter_caseResult(&mut self, _ctx: &CaseResultContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#caseResult}.
     * @param ctx the parse tree
     */
    fn exit_caseResult(&mut self, _ctx: &CaseResultContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#orderDirection}.
     * @param ctx the parse tree
     */
    fn enter_orderDirection(&mut self, _ctx: &OrderDirectionContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#orderDirection}.
     * @param ctx the parse tree
     */
    fn exit_orderDirection(&mut self, _ctx: &OrderDirectionContext<'input>) {}
    /**
     * Enter a parse tree produced by {@link LqlParser#comparisonOp}.
     * @param ctx the parse tree
     */
    fn enter_comparisonOp(&mut self, _ctx: &ComparisonOpContext<'input>) {}
    /**
     * Exit a parse tree produced by {@link LqlParser#comparisonOp}.
     * @param ctx the parse tree
     */
    fn exit_comparisonOp(&mut self, _ctx: &ComparisonOpContext<'input>) {}
}

antlr_rust::coerce_from! { 'input : LqlListener<'input> }
