// Generated from /home/user/DataProvider/Lql/Lql/Parsing/Lql.g4 by ANTLR 4.8
#![allow(dead_code)]
#![allow(non_snake_case)]
#![allow(non_upper_case_globals)]
#![allow(nonstandard_style)]
#![allow(unused_imports)]
#![allow(unused_mut)]
#![allow(unused_braces)]
use super::lqllistener::*;
use super::lqlvisitor::*;
use antlr_rust::atn::{ATN, INVALID_ALT};
use antlr_rust::atn_deserializer::ATNDeserializer;
use antlr_rust::dfa::DFA;
use antlr_rust::error_strategy::{DefaultErrorStrategy, ErrorStrategy};
use antlr_rust::errors::*;
use antlr_rust::int_stream::EOF;
use antlr_rust::parser::{BaseParser, Parser, ParserNodeType, ParserRecog};
use antlr_rust::parser_atn_simulator::ParserATNSimulator;
use antlr_rust::parser_rule_context::{cast, cast_mut, BaseParserRuleContext, ParserRuleContext};
use antlr_rust::recognizer::{Actions, Recognizer};
use antlr_rust::rule_context::{BaseRuleContext, CustomRuleContext, RuleContext};
use antlr_rust::token::{OwningToken, Token, TOKEN_EOF};
use antlr_rust::token_factory::{CommonTokenFactory, TokenAware, TokenFactory};
use antlr_rust::token_stream::TokenStream;
use antlr_rust::tree::*;
use antlr_rust::vocabulary::{Vocabulary, VocabularyImpl};
use antlr_rust::PredictionContextCache;
use antlr_rust::TokenSource;

use antlr_rust::lazy_static;
use antlr_rust::{TidAble, TidExt};

use std::any::{Any, TypeId};
use std::borrow::{Borrow, BorrowMut};
use std::cell::RefCell;
use std::convert::TryFrom;
use std::marker::PhantomData;
use std::ops::{Deref, DerefMut};
use std::rc::Rc;
use std::sync::Arc;

pub const T__0: isize = 1;
pub const T__1: isize = 2;
pub const T__2: isize = 3;
pub const T__3: isize = 4;
pub const T__4: isize = 5;
pub const T__5: isize = 6;
pub const T__6: isize = 7;
pub const T__7: isize = 8;
pub const T__8: isize = 9;
pub const T__9: isize = 10;
pub const T__10: isize = 11;
pub const T__11: isize = 12;
pub const T__12: isize = 13;
pub const T__13: isize = 14;
pub const T__14: isize = 15;
pub const T__15: isize = 16;
pub const T__16: isize = 17;
pub const T__17: isize = 18;
pub const T__18: isize = 19;
pub const T__19: isize = 20;
pub const ASC: isize = 21;
pub const DESC: isize = 22;
pub const AND: isize = 23;
pub const OR: isize = 24;
pub const DISTINCT: isize = 25;
pub const EXISTS: isize = 26;
pub const NULL: isize = 27;
pub const IS: isize = 28;
pub const NOT: isize = 29;
pub const IN: isize = 30;
pub const AS: isize = 31;
pub const CASE: isize = 32;
pub const WHEN: isize = 33;
pub const THEN: isize = 34;
pub const ELSE: isize = 35;
pub const END: isize = 36;
pub const WITH: isize = 37;
pub const OVER: isize = 38;
pub const PARTITION: isize = 39;
pub const ORDER: isize = 40;
pub const BY: isize = 41;
pub const COALESCE: isize = 42;
pub const EXTRACT: isize = 43;
pub const FROM: isize = 44;
pub const INTERVAL: isize = 45;
pub const CURRENT_DATE: isize = 46;
pub const DATE_TRUNC: isize = 47;
pub const ON: isize = 48;
pub const LIKE: isize = 49;
pub const PARAMETER: isize = 50;
pub const IDENT: isize = 51;
pub const INT: isize = 52;
pub const DECIMAL: isize = 53;
pub const STRING: isize = 54;
pub const COMMENT: isize = 55;
pub const WS: isize = 56;
pub const ASTERISK: isize = 57;
pub const RULE_program: usize = 0;
pub const RULE_statement: usize = 1;
pub const RULE_withStmt: usize = 2;
pub const RULE_cteDef: usize = 3;
pub const RULE_letStmt: usize = 4;
pub const RULE_pipeExpr: usize = 5;
pub const RULE_expr: usize = 6;
pub const RULE_windowSpec: usize = 7;
pub const RULE_partitionClause: usize = 8;
pub const RULE_orderClause: usize = 9;
pub const RULE_orderByArgList: usize = 10;
pub const RULE_orderByArg: usize = 11;
pub const RULE_lambdaExpr: usize = 12;
pub const RULE_qualifiedIdent: usize = 13;
pub const RULE_argList: usize = 14;
pub const RULE_arg: usize = 15;
pub const RULE_columnAlias: usize = 16;
pub const RULE_arithmeticExpr: usize = 17;
pub const RULE_arithmeticTerm: usize = 18;
pub const RULE_arithmeticFactor: usize = 19;
pub const RULE_functionCall: usize = 20;
pub const RULE_namedArg: usize = 21;
pub const RULE_logicalExpr: usize = 22;
pub const RULE_andExpr: usize = 23;
pub const RULE_atomicExpr: usize = 24;
pub const RULE_comparison: usize = 25;
pub const RULE_existsExpr: usize = 26;
pub const RULE_nullCheckExpr: usize = 27;
pub const RULE_inExpr: usize = 28;
pub const RULE_caseExpr: usize = 29;
pub const RULE_whenClause: usize = 30;
pub const RULE_caseResult: usize = 31;
pub const RULE_orderDirection: usize = 32;
pub const RULE_comparisonOp: usize = 33;
pub const ruleNames: [&'static str; 34] = [
    "program",
    "statement",
    "withStmt",
    "cteDef",
    "letStmt",
    "pipeExpr",
    "expr",
    "windowSpec",
    "partitionClause",
    "orderClause",
    "orderByArgList",
    "orderByArg",
    "lambdaExpr",
    "qualifiedIdent",
    "argList",
    "arg",
    "columnAlias",
    "arithmeticExpr",
    "arithmeticTerm",
    "arithmeticFactor",
    "functionCall",
    "namedArg",
    "logicalExpr",
    "andExpr",
    "atomicExpr",
    "comparison",
    "existsExpr",
    "nullCheckExpr",
    "inExpr",
    "caseExpr",
    "whenClause",
    "caseResult",
    "orderDirection",
    "comparisonOp",
];

pub const _LITERAL_NAMES: [Option<&'static str>; 58] = [
    None,
    Some("','"),
    Some("'('"),
    Some("')'"),
    Some("'let'"),
    Some("'='"),
    Some("'|>'"),
    Some("'fn'"),
    Some("'=>'"),
    Some("'.'"),
    Some("'+'"),
    Some("'-'"),
    Some("'||'"),
    Some("'/'"),
    Some("'%'"),
    Some("'!='"),
    Some("'<>'"),
    Some("'<'"),
    Some("'>'"),
    Some("'<='"),
    Some("'>='"),
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    Some("'*'"),
];
pub const _SYMBOLIC_NAMES: [Option<&'static str>; 58] = [
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    None,
    Some("ASC"),
    Some("DESC"),
    Some("AND"),
    Some("OR"),
    Some("DISTINCT"),
    Some("EXISTS"),
    Some("NULL"),
    Some("IS"),
    Some("NOT"),
    Some("IN"),
    Some("AS"),
    Some("CASE"),
    Some("WHEN"),
    Some("THEN"),
    Some("ELSE"),
    Some("END"),
    Some("WITH"),
    Some("OVER"),
    Some("PARTITION"),
    Some("ORDER"),
    Some("BY"),
    Some("COALESCE"),
    Some("EXTRACT"),
    Some("FROM"),
    Some("INTERVAL"),
    Some("CURRENT_DATE"),
    Some("DATE_TRUNC"),
    Some("ON"),
    Some("LIKE"),
    Some("PARAMETER"),
    Some("IDENT"),
    Some("INT"),
    Some("DECIMAL"),
    Some("STRING"),
    Some("COMMENT"),
    Some("WS"),
    Some("ASTERISK"),
];
lazy_static! {
    static ref _shared_context_cache: Arc<PredictionContextCache> =
        Arc::new(PredictionContextCache::new());
    static ref VOCABULARY: Box<dyn Vocabulary> = Box::new(VocabularyImpl::new(
        _LITERAL_NAMES.iter(),
        _SYMBOLIC_NAMES.iter(),
        None
    ));
}

type BaseParserType<'input, I> = BaseParser<
    'input,
    LqlParserExt<'input>,
    I,
    LqlParserContextType,
    dyn LqlListener<'input> + 'input,
>;

type TokenType<'input> = <LocalTokenFactory<'input> as TokenFactory<'input>>::Tok;
pub type LocalTokenFactory<'input> = CommonTokenFactory;

pub type LqlTreeWalker<'input, 'a> =
    ParseTreeWalker<'input, 'a, LqlParserContextType, dyn LqlListener<'input> + 'a>;

/// Parser for Lql grammar
pub struct LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    base: BaseParserType<'input, I>,
    interpreter: Arc<ParserATNSimulator>,
    _shared_context_cache: Box<PredictionContextCache>,
    pub err_handler: H,
}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn get_serialized_atn() -> &'static str {
        _serializedATN
    }

    pub fn set_error_strategy(&mut self, strategy: H) {
        self.err_handler = strategy
    }

    pub fn with_strategy(input: I, strategy: H) -> Self {
        antlr_rust::recognizer::check_version("0", "3");
        let interpreter = Arc::new(ParserATNSimulator::new(
            _ATN.clone(),
            _decision_to_DFA.clone(),
            _shared_context_cache.clone(),
        ));
        Self {
            base: BaseParser::new_base_parser(
                input,
                Arc::clone(&interpreter),
                LqlParserExt {
                    _pd: Default::default(),
                },
            ),
            interpreter,
            _shared_context_cache: Box::new(PredictionContextCache::new()),
            err_handler: strategy,
        }
    }
}

type DynStrategy<'input, I> = Box<dyn ErrorStrategy<'input, BaseParserType<'input, I>> + 'input>;

impl<'input, I> LqlParser<'input, I, DynStrategy<'input, I>>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
{
    pub fn with_dyn_strategy(input: I) -> Self {
        Self::with_strategy(input, Box::new(DefaultErrorStrategy::new()))
    }
}

impl<'input, I> LqlParser<'input, I, DefaultErrorStrategy<'input, LqlParserContextType>>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
{
    pub fn new(input: I) -> Self {
        Self::with_strategy(input, DefaultErrorStrategy::new())
    }
}

/// Trait for monomorphized trait object that corresponds to the nodes of parse tree generated for LqlParser
pub trait LqlParserContext<'input>:
    for<'x> Listenable<dyn LqlListener<'input> + 'x>
    + for<'x> Visitable<dyn LqlVisitor<'input> + 'x>
    + ParserRuleContext<'input, TF = LocalTokenFactory<'input>, Ctx = LqlParserContextType>
{
}

antlr_rust::coerce_from! { 'input : LqlParserContext<'input> }

impl<'input, 'x, T> VisitableDyn<T> for dyn LqlParserContext<'input> + 'input
where
    T: LqlVisitor<'input> + 'x,
{
    fn accept_dyn(&self, visitor: &mut T) {
        self.accept(visitor as &mut (dyn LqlVisitor<'input> + 'x))
    }
}

impl<'input> LqlParserContext<'input> for TerminalNode<'input, LqlParserContextType> {}
impl<'input> LqlParserContext<'input> for ErrorNode<'input, LqlParserContextType> {}

antlr_rust::tid! { impl<'input> TidAble<'input> for dyn LqlParserContext<'input> + 'input }

antlr_rust::tid! { impl<'input> TidAble<'input> for dyn LqlListener<'input> + 'input }

pub struct LqlParserContextType;
antlr_rust::tid! {LqlParserContextType}

impl<'input> ParserNodeType<'input> for LqlParserContextType {
    type TF = LocalTokenFactory<'input>;
    type Type = dyn LqlParserContext<'input> + 'input;
}

impl<'input, I, H> Deref for LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    type Target = BaseParserType<'input, I>;

    fn deref(&self) -> &Self::Target {
        &self.base
    }
}

impl<'input, I, H> DerefMut for LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    fn deref_mut(&mut self) -> &mut Self::Target {
        &mut self.base
    }
}

pub struct LqlParserExt<'input> {
    _pd: PhantomData<&'input str>,
}

impl<'input> LqlParserExt<'input> {}
antlr_rust::tid! { LqlParserExt<'a> }

impl<'input> TokenAware<'input> for LqlParserExt<'input> {
    type TF = LocalTokenFactory<'input>;
}

impl<'input, I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>>
    ParserRecog<'input, BaseParserType<'input, I>> for LqlParserExt<'input>
{
}

impl<'input, I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>>
    Actions<'input, BaseParserType<'input, I>> for LqlParserExt<'input>
{
    fn get_grammar_file_name(&self) -> &str {
        "Lql.g4"
    }

    fn get_rule_names(&self) -> &[&str] {
        &ruleNames
    }

    fn get_vocabulary(&self) -> &dyn Vocabulary {
        &**VOCABULARY
    }
}
//------------------- program ----------------
pub type ProgramContextAll<'input> = ProgramContext<'input>;

pub type ProgramContext<'input> = BaseParserRuleContext<'input, ProgramContextExt<'input>>;

#[derive(Clone)]
pub struct ProgramContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for ProgramContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for ProgramContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_program(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_program(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for ProgramContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_program(self);
    }
}

impl<'input> CustomRuleContext<'input> for ProgramContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_program
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_program }
}
antlr_rust::tid! {ProgramContextExt<'a>}

impl<'input> ProgramContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<ProgramContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            ProgramContextExt { ph: PhantomData },
        ))
    }
}

pub trait ProgramContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<ProgramContextExt<'input>>
{
    /// Retrieves first TerminalNode corresponding to token EOF
    /// Returns `None` if there is no child corresponding to token EOF
    fn EOF(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(EOF, 0)
    }
    fn statement_all(&self) -> Vec<Rc<StatementContextAll<'input>>>
    where
        Self: Sized,
    {
        self.children_of_type()
    }
    fn statement(&self, i: usize) -> Option<Rc<StatementContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(i)
    }
}

impl<'input> ProgramContextAttrs<'input> for ProgramContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn program(&mut self) -> Result<Rc<ProgramContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = ProgramContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog.base.enter_rule(_localctx.clone(), 0, RULE_program);
        let mut _localctx: Rc<ProgramContextAll> = _localctx;
        let mut _la: isize = -1;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                recog.base.set_state(71);
                recog.err_handler.sync(&mut recog.base)?;
                _la = recog.base.input.la(1);
                while (((_la) & !0x3f) == 0
                    && ((1usize << _la) & ((1usize << T__1) | (1usize << T__3) | (1usize << T__6)))
                        != 0)
                    || (((_la - 32) & !0x3f) == 0
                        && ((1usize << (_la - 32))
                            & ((1usize << (CASE - 32))
                                | (1usize << (WITH - 32))
                                | (1usize << (PARAMETER - 32))
                                | (1usize << (IDENT - 32))
                                | (1usize << (INT - 32))
                                | (1usize << (DECIMAL - 32))
                                | (1usize << (STRING - 32))
                                | (1usize << (ASTERISK - 32))))
                            != 0)
                {
                    {
                        {
                            /*InvokeRule statement*/
                            recog.base.set_state(68);
                            recog.statement()?;
                        }
                    }
                    recog.base.set_state(73);
                    recog.err_handler.sync(&mut recog.base)?;
                    _la = recog.base.input.la(1);
                }
                recog.base.set_state(74);
                recog.base.match_token(EOF, &mut recog.err_handler)?;
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- statement ----------------
pub type StatementContextAll<'input> = StatementContext<'input>;

pub type StatementContext<'input> = BaseParserRuleContext<'input, StatementContextExt<'input>>;

#[derive(Clone)]
pub struct StatementContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for StatementContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for StatementContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_statement(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_statement(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for StatementContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_statement(self);
    }
}

impl<'input> CustomRuleContext<'input> for StatementContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_statement
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_statement }
}
antlr_rust::tid! {StatementContextExt<'a>}

impl<'input> StatementContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<StatementContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            StatementContextExt { ph: PhantomData },
        ))
    }
}

pub trait StatementContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<StatementContextExt<'input>>
{
    fn withStmt(&self) -> Option<Rc<WithStmtContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn letStmt(&self) -> Option<Rc<LetStmtContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn pipeExpr(&self) -> Option<Rc<PipeExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
}

impl<'input> StatementContextAttrs<'input> for StatementContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn statement(&mut self) -> Result<Rc<StatementContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = StatementContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog.base.enter_rule(_localctx.clone(), 2, RULE_statement);
        let mut _localctx: Rc<StatementContextAll> = _localctx;
        let result: Result<(), ANTLRError> = (|| {
            recog.base.set_state(79);
            recog.err_handler.sync(&mut recog.base)?;
            match recog.base.input.la(1) {
                WITH => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 1);
                    recog.base.enter_outer_alt(None, 1);
                    {
                        /*InvokeRule withStmt*/
                        recog.base.set_state(76);
                        recog.withStmt()?;
                    }
                }

                T__3 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 2);
                    recog.base.enter_outer_alt(None, 2);
                    {
                        /*InvokeRule letStmt*/
                        recog.base.set_state(77);
                        recog.letStmt()?;
                    }
                }

                T__1 | T__6 | CASE | PARAMETER | IDENT | INT | DECIMAL | STRING | ASTERISK => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 3);
                    recog.base.enter_outer_alt(None, 3);
                    {
                        /*InvokeRule pipeExpr*/
                        recog.base.set_state(78);
                        recog.pipeExpr()?;
                    }
                }

                _ => Err(ANTLRError::NoAltError(NoViableAltError::new(
                    &mut recog.base,
                )))?,
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- withStmt ----------------
pub type WithStmtContextAll<'input> = WithStmtContext<'input>;

pub type WithStmtContext<'input> = BaseParserRuleContext<'input, WithStmtContextExt<'input>>;

#[derive(Clone)]
pub struct WithStmtContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for WithStmtContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for WithStmtContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_withStmt(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_withStmt(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for WithStmtContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_withStmt(self);
    }
}

impl<'input> CustomRuleContext<'input> for WithStmtContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_withStmt
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_withStmt }
}
antlr_rust::tid! {WithStmtContextExt<'a>}

impl<'input> WithStmtContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<WithStmtContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            WithStmtContextExt { ph: PhantomData },
        ))
    }
}

pub trait WithStmtContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<WithStmtContextExt<'input>>
{
    /// Retrieves first TerminalNode corresponding to token WITH
    /// Returns `None` if there is no child corresponding to token WITH
    fn WITH(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(WITH, 0)
    }
    fn cteDef_all(&self) -> Vec<Rc<CteDefContextAll<'input>>>
    where
        Self: Sized,
    {
        self.children_of_type()
    }
    fn cteDef(&self, i: usize) -> Option<Rc<CteDefContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(i)
    }
    fn pipeExpr(&self) -> Option<Rc<PipeExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
}

impl<'input> WithStmtContextAttrs<'input> for WithStmtContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn withStmt(&mut self) -> Result<Rc<WithStmtContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = WithStmtContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog.base.enter_rule(_localctx.clone(), 4, RULE_withStmt);
        let mut _localctx: Rc<WithStmtContextAll> = _localctx;
        let mut _la: isize = -1;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                recog.base.set_state(81);
                recog.base.match_token(WITH, &mut recog.err_handler)?;

                /*InvokeRule cteDef*/
                recog.base.set_state(82);
                recog.cteDef()?;

                recog.base.set_state(87);
                recog.err_handler.sync(&mut recog.base)?;
                _la = recog.base.input.la(1);
                while _la == T__0 {
                    {
                        {
                            recog.base.set_state(83);
                            recog.base.match_token(T__0, &mut recog.err_handler)?;

                            /*InvokeRule cteDef*/
                            recog.base.set_state(84);
                            recog.cteDef()?;
                        }
                    }
                    recog.base.set_state(89);
                    recog.err_handler.sync(&mut recog.base)?;
                    _la = recog.base.input.la(1);
                }
                /*InvokeRule pipeExpr*/
                recog.base.set_state(90);
                recog.pipeExpr()?;
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- cteDef ----------------
pub type CteDefContextAll<'input> = CteDefContext<'input>;

pub type CteDefContext<'input> = BaseParserRuleContext<'input, CteDefContextExt<'input>>;

#[derive(Clone)]
pub struct CteDefContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for CteDefContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for CteDefContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_cteDef(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_cteDef(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for CteDefContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_cteDef(self);
    }
}

impl<'input> CustomRuleContext<'input> for CteDefContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_cteDef
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_cteDef }
}
antlr_rust::tid! {CteDefContextExt<'a>}

impl<'input> CteDefContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<CteDefContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            CteDefContextExt { ph: PhantomData },
        ))
    }
}

pub trait CteDefContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<CteDefContextExt<'input>>
{
    /// Retrieves first TerminalNode corresponding to token IDENT
    /// Returns `None` if there is no child corresponding to token IDENT
    fn IDENT(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(IDENT, 0)
    }
    /// Retrieves first TerminalNode corresponding to token AS
    /// Returns `None` if there is no child corresponding to token AS
    fn AS(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(AS, 0)
    }
    fn pipeExpr(&self) -> Option<Rc<PipeExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
}

impl<'input> CteDefContextAttrs<'input> for CteDefContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn cteDef(&mut self) -> Result<Rc<CteDefContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = CteDefContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog.base.enter_rule(_localctx.clone(), 6, RULE_cteDef);
        let mut _localctx: Rc<CteDefContextAll> = _localctx;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                recog.base.set_state(92);
                recog.base.match_token(IDENT, &mut recog.err_handler)?;

                recog.base.set_state(93);
                recog.base.match_token(AS, &mut recog.err_handler)?;

                recog.base.set_state(94);
                recog.base.match_token(T__1, &mut recog.err_handler)?;

                /*InvokeRule pipeExpr*/
                recog.base.set_state(95);
                recog.pipeExpr()?;

                recog.base.set_state(96);
                recog.base.match_token(T__2, &mut recog.err_handler)?;
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- letStmt ----------------
pub type LetStmtContextAll<'input> = LetStmtContext<'input>;

pub type LetStmtContext<'input> = BaseParserRuleContext<'input, LetStmtContextExt<'input>>;

#[derive(Clone)]
pub struct LetStmtContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for LetStmtContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for LetStmtContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_letStmt(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_letStmt(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for LetStmtContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_letStmt(self);
    }
}

impl<'input> CustomRuleContext<'input> for LetStmtContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_letStmt
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_letStmt }
}
antlr_rust::tid! {LetStmtContextExt<'a>}

impl<'input> LetStmtContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<LetStmtContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            LetStmtContextExt { ph: PhantomData },
        ))
    }
}

pub trait LetStmtContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<LetStmtContextExt<'input>>
{
    /// Retrieves first TerminalNode corresponding to token IDENT
    /// Returns `None` if there is no child corresponding to token IDENT
    fn IDENT(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(IDENT, 0)
    }
    fn pipeExpr(&self) -> Option<Rc<PipeExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
}

impl<'input> LetStmtContextAttrs<'input> for LetStmtContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn letStmt(&mut self) -> Result<Rc<LetStmtContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = LetStmtContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog.base.enter_rule(_localctx.clone(), 8, RULE_letStmt);
        let mut _localctx: Rc<LetStmtContextAll> = _localctx;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                recog.base.set_state(98);
                recog.base.match_token(T__3, &mut recog.err_handler)?;

                recog.base.set_state(99);
                recog.base.match_token(IDENT, &mut recog.err_handler)?;

                recog.base.set_state(100);
                recog.base.match_token(T__4, &mut recog.err_handler)?;

                /*InvokeRule pipeExpr*/
                recog.base.set_state(101);
                recog.pipeExpr()?;
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- pipeExpr ----------------
pub type PipeExprContextAll<'input> = PipeExprContext<'input>;

pub type PipeExprContext<'input> = BaseParserRuleContext<'input, PipeExprContextExt<'input>>;

#[derive(Clone)]
pub struct PipeExprContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for PipeExprContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for PipeExprContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_pipeExpr(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_pipeExpr(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for PipeExprContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_pipeExpr(self);
    }
}

impl<'input> CustomRuleContext<'input> for PipeExprContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_pipeExpr
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_pipeExpr }
}
antlr_rust::tid! {PipeExprContextExt<'a>}

impl<'input> PipeExprContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<PipeExprContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            PipeExprContextExt { ph: PhantomData },
        ))
    }
}

pub trait PipeExprContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<PipeExprContextExt<'input>>
{
    fn expr_all(&self) -> Vec<Rc<ExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.children_of_type()
    }
    fn expr(&self, i: usize) -> Option<Rc<ExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(i)
    }
}

impl<'input> PipeExprContextAttrs<'input> for PipeExprContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn pipeExpr(&mut self) -> Result<Rc<PipeExprContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = PipeExprContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog.base.enter_rule(_localctx.clone(), 10, RULE_pipeExpr);
        let mut _localctx: Rc<PipeExprContextAll> = _localctx;
        let mut _la: isize = -1;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                /*InvokeRule expr*/
                recog.base.set_state(103);
                recog.expr()?;

                recog.base.set_state(108);
                recog.err_handler.sync(&mut recog.base)?;
                _la = recog.base.input.la(1);
                while _la == T__5 {
                    {
                        {
                            recog.base.set_state(104);
                            recog.base.match_token(T__5, &mut recog.err_handler)?;

                            /*InvokeRule expr*/
                            recog.base.set_state(105);
                            recog.expr()?;
                        }
                    }
                    recog.base.set_state(110);
                    recog.err_handler.sync(&mut recog.base)?;
                    _la = recog.base.input.la(1);
                }
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- expr ----------------
pub type ExprContextAll<'input> = ExprContext<'input>;

pub type ExprContext<'input> = BaseParserRuleContext<'input, ExprContextExt<'input>>;

#[derive(Clone)]
pub struct ExprContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for ExprContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for ExprContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_expr(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_expr(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for ExprContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_expr(self);
    }
}

impl<'input> CustomRuleContext<'input> for ExprContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_expr
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_expr }
}
antlr_rust::tid! {ExprContextExt<'a>}

impl<'input> ExprContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<ExprContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            ExprContextExt { ph: PhantomData },
        ))
    }
}

pub trait ExprContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<ExprContextExt<'input>>
{
    /// Retrieves first TerminalNode corresponding to token IDENT
    /// Returns `None` if there is no child corresponding to token IDENT
    fn IDENT(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(IDENT, 0)
    }
    /// Retrieves first TerminalNode corresponding to token OVER
    /// Returns `None` if there is no child corresponding to token OVER
    fn OVER(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(OVER, 0)
    }
    fn windowSpec(&self) -> Option<Rc<WindowSpecContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn argList(&self) -> Option<Rc<ArgListContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn pipeExpr(&self) -> Option<Rc<PipeExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn qualifiedIdent(&self) -> Option<Rc<QualifiedIdentContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn lambdaExpr(&self) -> Option<Rc<LambdaExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn caseExpr(&self) -> Option<Rc<CaseExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    /// Retrieves first TerminalNode corresponding to token INT
    /// Returns `None` if there is no child corresponding to token INT
    fn INT(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(INT, 0)
    }
    /// Retrieves first TerminalNode corresponding to token DECIMAL
    /// Returns `None` if there is no child corresponding to token DECIMAL
    fn DECIMAL(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(DECIMAL, 0)
    }
    /// Retrieves first TerminalNode corresponding to token ASTERISK
    /// Returns `None` if there is no child corresponding to token ASTERISK
    fn ASTERISK(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(ASTERISK, 0)
    }
    /// Retrieves first TerminalNode corresponding to token STRING
    /// Returns `None` if there is no child corresponding to token STRING
    fn STRING(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(STRING, 0)
    }
    /// Retrieves first TerminalNode corresponding to token PARAMETER
    /// Returns `None` if there is no child corresponding to token PARAMETER
    fn PARAMETER(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(PARAMETER, 0)
    }
}

impl<'input> ExprContextAttrs<'input> for ExprContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn expr(&mut self) -> Result<Rc<ExprContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = ExprContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog.base.enter_rule(_localctx.clone(), 12, RULE_expr);
        let mut _localctx: Rc<ExprContextAll> = _localctx;
        let mut _la: isize = -1;
        let result: Result<(), ANTLRError> = (|| {
            recog.base.set_state(141);
            recog.err_handler.sync(&mut recog.base)?;
            match recog.interpreter.adaptive_predict(6, &mut recog.base)? {
                1 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 1);
                    recog.base.enter_outer_alt(None, 1);
                    {
                        recog.base.set_state(111);
                        recog.base.match_token(IDENT, &mut recog.err_handler)?;

                        recog.base.set_state(112);
                        recog.base.match_token(T__1, &mut recog.err_handler)?;

                        recog.base.set_state(114);
                        recog.err_handler.sync(&mut recog.base)?;
                        _la = recog.base.input.la(1);
                        if (((_la) & !0x3f) == 0
                            && ((1usize << _la)
                                & ((1usize << T__1) | (1usize << T__6) | (1usize << EXISTS)))
                                != 0)
                            || (((_la - 32) & !0x3f) == 0
                                && ((1usize << (_la - 32))
                                    & ((1usize << (CASE - 32))
                                        | (1usize << (ON - 32))
                                        | (1usize << (PARAMETER - 32))
                                        | (1usize << (IDENT - 32))
                                        | (1usize << (INT - 32))
                                        | (1usize << (DECIMAL - 32))
                                        | (1usize << (STRING - 32))
                                        | (1usize << (ASTERISK - 32))))
                                    != 0)
                        {
                            {
                                /*InvokeRule argList*/
                                recog.base.set_state(113);
                                recog.argList()?;
                            }
                        }

                        recog.base.set_state(116);
                        recog.base.match_token(T__2, &mut recog.err_handler)?;

                        recog.base.set_state(117);
                        recog.base.match_token(OVER, &mut recog.err_handler)?;

                        recog.base.set_state(118);
                        recog.base.match_token(T__1, &mut recog.err_handler)?;

                        /*InvokeRule windowSpec*/
                        recog.base.set_state(119);
                        recog.windowSpec()?;

                        recog.base.set_state(120);
                        recog.base.match_token(T__2, &mut recog.err_handler)?;
                    }
                }
                2 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 2);
                    recog.base.enter_outer_alt(None, 2);
                    {
                        recog.base.set_state(122);
                        recog.base.match_token(IDENT, &mut recog.err_handler)?;

                        recog.base.set_state(123);
                        recog.base.match_token(T__1, &mut recog.err_handler)?;

                        recog.base.set_state(125);
                        recog.err_handler.sync(&mut recog.base)?;
                        _la = recog.base.input.la(1);
                        if (((_la) & !0x3f) == 0
                            && ((1usize << _la)
                                & ((1usize << T__1) | (1usize << T__6) | (1usize << EXISTS)))
                                != 0)
                            || (((_la - 32) & !0x3f) == 0
                                && ((1usize << (_la - 32))
                                    & ((1usize << (CASE - 32))
                                        | (1usize << (ON - 32))
                                        | (1usize << (PARAMETER - 32))
                                        | (1usize << (IDENT - 32))
                                        | (1usize << (INT - 32))
                                        | (1usize << (DECIMAL - 32))
                                        | (1usize << (STRING - 32))
                                        | (1usize << (ASTERISK - 32))))
                                    != 0)
                        {
                            {
                                /*InvokeRule argList*/
                                recog.base.set_state(124);
                                recog.argList()?;
                            }
                        }

                        recog.base.set_state(127);
                        recog.base.match_token(T__2, &mut recog.err_handler)?;
                    }
                }
                3 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 3);
                    recog.base.enter_outer_alt(None, 3);
                    {
                        recog.base.set_state(128);
                        recog.base.match_token(IDENT, &mut recog.err_handler)?;
                    }
                }
                4 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 4);
                    recog.base.enter_outer_alt(None, 4);
                    {
                        recog.base.set_state(129);
                        recog.base.match_token(T__1, &mut recog.err_handler)?;

                        /*InvokeRule pipeExpr*/
                        recog.base.set_state(130);
                        recog.pipeExpr()?;

                        recog.base.set_state(131);
                        recog.base.match_token(T__2, &mut recog.err_handler)?;
                    }
                }
                5 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 5);
                    recog.base.enter_outer_alt(None, 5);
                    {
                        /*InvokeRule qualifiedIdent*/
                        recog.base.set_state(133);
                        recog.qualifiedIdent()?;
                    }
                }
                6 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 6);
                    recog.base.enter_outer_alt(None, 6);
                    {
                        /*InvokeRule lambdaExpr*/
                        recog.base.set_state(134);
                        recog.lambdaExpr()?;
                    }
                }
                7 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 7);
                    recog.base.enter_outer_alt(None, 7);
                    {
                        /*InvokeRule caseExpr*/
                        recog.base.set_state(135);
                        recog.caseExpr()?;
                    }
                }
                8 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 8);
                    recog.base.enter_outer_alt(None, 8);
                    {
                        recog.base.set_state(136);
                        recog.base.match_token(INT, &mut recog.err_handler)?;
                    }
                }
                9 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 9);
                    recog.base.enter_outer_alt(None, 9);
                    {
                        recog.base.set_state(137);
                        recog.base.match_token(DECIMAL, &mut recog.err_handler)?;
                    }
                }
                10 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 10);
                    recog.base.enter_outer_alt(None, 10);
                    {
                        recog.base.set_state(138);
                        recog.base.match_token(ASTERISK, &mut recog.err_handler)?;
                    }
                }
                11 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 11);
                    recog.base.enter_outer_alt(None, 11);
                    {
                        recog.base.set_state(139);
                        recog.base.match_token(STRING, &mut recog.err_handler)?;
                    }
                }
                12 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 12);
                    recog.base.enter_outer_alt(None, 12);
                    {
                        recog.base.set_state(140);
                        recog.base.match_token(PARAMETER, &mut recog.err_handler)?;
                    }
                }

                _ => {}
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- windowSpec ----------------
pub type WindowSpecContextAll<'input> = WindowSpecContext<'input>;

pub type WindowSpecContext<'input> = BaseParserRuleContext<'input, WindowSpecContextExt<'input>>;

#[derive(Clone)]
pub struct WindowSpecContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for WindowSpecContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for WindowSpecContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_windowSpec(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_windowSpec(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for WindowSpecContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_windowSpec(self);
    }
}

impl<'input> CustomRuleContext<'input> for WindowSpecContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_windowSpec
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_windowSpec }
}
antlr_rust::tid! {WindowSpecContextExt<'a>}

impl<'input> WindowSpecContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<WindowSpecContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            WindowSpecContextExt { ph: PhantomData },
        ))
    }
}

pub trait WindowSpecContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<WindowSpecContextExt<'input>>
{
    fn partitionClause(&self) -> Option<Rc<PartitionClauseContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn orderClause(&self) -> Option<Rc<OrderClauseContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
}

impl<'input> WindowSpecContextAttrs<'input> for WindowSpecContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn windowSpec(&mut self) -> Result<Rc<WindowSpecContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = WindowSpecContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog
            .base
            .enter_rule(_localctx.clone(), 14, RULE_windowSpec);
        let mut _localctx: Rc<WindowSpecContextAll> = _localctx;
        let mut _la: isize = -1;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                recog.base.set_state(144);
                recog.err_handler.sync(&mut recog.base)?;
                _la = recog.base.input.la(1);
                if _la == PARTITION {
                    {
                        /*InvokeRule partitionClause*/
                        recog.base.set_state(143);
                        recog.partitionClause()?;
                    }
                }

                recog.base.set_state(147);
                recog.err_handler.sync(&mut recog.base)?;
                _la = recog.base.input.la(1);
                if _la == ORDER {
                    {
                        /*InvokeRule orderClause*/
                        recog.base.set_state(146);
                        recog.orderClause()?;
                    }
                }
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- partitionClause ----------------
pub type PartitionClauseContextAll<'input> = PartitionClauseContext<'input>;

pub type PartitionClauseContext<'input> =
    BaseParserRuleContext<'input, PartitionClauseContextExt<'input>>;

#[derive(Clone)]
pub struct PartitionClauseContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for PartitionClauseContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for PartitionClauseContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_partitionClause(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_partitionClause(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for PartitionClauseContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_partitionClause(self);
    }
}

impl<'input> CustomRuleContext<'input> for PartitionClauseContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_partitionClause
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_partitionClause }
}
antlr_rust::tid! {PartitionClauseContextExt<'a>}

impl<'input> PartitionClauseContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<PartitionClauseContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            PartitionClauseContextExt { ph: PhantomData },
        ))
    }
}

pub trait PartitionClauseContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<PartitionClauseContextExt<'input>>
{
    /// Retrieves first TerminalNode corresponding to token PARTITION
    /// Returns `None` if there is no child corresponding to token PARTITION
    fn PARTITION(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(PARTITION, 0)
    }
    /// Retrieves first TerminalNode corresponding to token BY
    /// Returns `None` if there is no child corresponding to token BY
    fn BY(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(BY, 0)
    }
    fn argList(&self) -> Option<Rc<ArgListContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
}

impl<'input> PartitionClauseContextAttrs<'input> for PartitionClauseContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn partitionClause(&mut self) -> Result<Rc<PartitionClauseContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx =
            PartitionClauseContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog
            .base
            .enter_rule(_localctx.clone(), 16, RULE_partitionClause);
        let mut _localctx: Rc<PartitionClauseContextAll> = _localctx;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                recog.base.set_state(149);
                recog.base.match_token(PARTITION, &mut recog.err_handler)?;

                recog.base.set_state(150);
                recog.base.match_token(BY, &mut recog.err_handler)?;

                /*InvokeRule argList*/
                recog.base.set_state(151);
                recog.argList()?;
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- orderClause ----------------
pub type OrderClauseContextAll<'input> = OrderClauseContext<'input>;

pub type OrderClauseContext<'input> = BaseParserRuleContext<'input, OrderClauseContextExt<'input>>;

#[derive(Clone)]
pub struct OrderClauseContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for OrderClauseContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for OrderClauseContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_orderClause(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_orderClause(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for OrderClauseContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_orderClause(self);
    }
}

impl<'input> CustomRuleContext<'input> for OrderClauseContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_orderClause
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_orderClause }
}
antlr_rust::tid! {OrderClauseContextExt<'a>}

impl<'input> OrderClauseContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<OrderClauseContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            OrderClauseContextExt { ph: PhantomData },
        ))
    }
}

pub trait OrderClauseContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<OrderClauseContextExt<'input>>
{
    /// Retrieves first TerminalNode corresponding to token ORDER
    /// Returns `None` if there is no child corresponding to token ORDER
    fn ORDER(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(ORDER, 0)
    }
    /// Retrieves first TerminalNode corresponding to token BY
    /// Returns `None` if there is no child corresponding to token BY
    fn BY(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(BY, 0)
    }
    fn orderByArgList(&self) -> Option<Rc<OrderByArgListContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
}

impl<'input> OrderClauseContextAttrs<'input> for OrderClauseContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn orderClause(&mut self) -> Result<Rc<OrderClauseContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = OrderClauseContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog
            .base
            .enter_rule(_localctx.clone(), 18, RULE_orderClause);
        let mut _localctx: Rc<OrderClauseContextAll> = _localctx;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                recog.base.set_state(153);
                recog.base.match_token(ORDER, &mut recog.err_handler)?;

                recog.base.set_state(154);
                recog.base.match_token(BY, &mut recog.err_handler)?;

                /*InvokeRule orderByArgList*/
                recog.base.set_state(155);
                recog.orderByArgList()?;
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- orderByArgList ----------------
pub type OrderByArgListContextAll<'input> = OrderByArgListContext<'input>;

pub type OrderByArgListContext<'input> =
    BaseParserRuleContext<'input, OrderByArgListContextExt<'input>>;

#[derive(Clone)]
pub struct OrderByArgListContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for OrderByArgListContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for OrderByArgListContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_orderByArgList(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_orderByArgList(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for OrderByArgListContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_orderByArgList(self);
    }
}

impl<'input> CustomRuleContext<'input> for OrderByArgListContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_orderByArgList
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_orderByArgList }
}
antlr_rust::tid! {OrderByArgListContextExt<'a>}

impl<'input> OrderByArgListContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<OrderByArgListContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            OrderByArgListContextExt { ph: PhantomData },
        ))
    }
}

pub trait OrderByArgListContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<OrderByArgListContextExt<'input>>
{
    fn orderByArg_all(&self) -> Vec<Rc<OrderByArgContextAll<'input>>>
    where
        Self: Sized,
    {
        self.children_of_type()
    }
    fn orderByArg(&self, i: usize) -> Option<Rc<OrderByArgContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(i)
    }
}

impl<'input> OrderByArgListContextAttrs<'input> for OrderByArgListContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn orderByArgList(&mut self) -> Result<Rc<OrderByArgListContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx =
            OrderByArgListContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog
            .base
            .enter_rule(_localctx.clone(), 20, RULE_orderByArgList);
        let mut _localctx: Rc<OrderByArgListContextAll> = _localctx;
        let mut _la: isize = -1;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                /*InvokeRule orderByArg*/
                recog.base.set_state(157);
                recog.orderByArg()?;

                recog.base.set_state(162);
                recog.err_handler.sync(&mut recog.base)?;
                _la = recog.base.input.la(1);
                while _la == T__0 {
                    {
                        {
                            recog.base.set_state(158);
                            recog.base.match_token(T__0, &mut recog.err_handler)?;

                            /*InvokeRule orderByArg*/
                            recog.base.set_state(159);
                            recog.orderByArg()?;
                        }
                    }
                    recog.base.set_state(164);
                    recog.err_handler.sync(&mut recog.base)?;
                    _la = recog.base.input.la(1);
                }
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- orderByArg ----------------
pub type OrderByArgContextAll<'input> = OrderByArgContext<'input>;

pub type OrderByArgContext<'input> = BaseParserRuleContext<'input, OrderByArgContextExt<'input>>;

#[derive(Clone)]
pub struct OrderByArgContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for OrderByArgContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for OrderByArgContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_orderByArg(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_orderByArg(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for OrderByArgContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_orderByArg(self);
    }
}

impl<'input> CustomRuleContext<'input> for OrderByArgContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_orderByArg
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_orderByArg }
}
antlr_rust::tid! {OrderByArgContextExt<'a>}

impl<'input> OrderByArgContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<OrderByArgContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            OrderByArgContextExt { ph: PhantomData },
        ))
    }
}

pub trait OrderByArgContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<OrderByArgContextExt<'input>>
{
    fn arithmeticExpr(&self) -> Option<Rc<ArithmeticExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn functionCall(&self) -> Option<Rc<FunctionCallContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn qualifiedIdent(&self) -> Option<Rc<QualifiedIdentContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    /// Retrieves first TerminalNode corresponding to token IDENT
    /// Returns `None` if there is no child corresponding to token IDENT
    fn IDENT(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(IDENT, 0)
    }
    fn orderDirection(&self) -> Option<Rc<OrderDirectionContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
}

impl<'input> OrderByArgContextAttrs<'input> for OrderByArgContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn orderByArg(&mut self) -> Result<Rc<OrderByArgContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = OrderByArgContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog
            .base
            .enter_rule(_localctx.clone(), 22, RULE_orderByArg);
        let mut _localctx: Rc<OrderByArgContextAll> = _localctx;
        let mut _la: isize = -1;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                recog.base.set_state(169);
                recog.err_handler.sync(&mut recog.base)?;
                match recog.interpreter.adaptive_predict(10, &mut recog.base)? {
                    1 => {
                        {
                            /*InvokeRule arithmeticExpr*/
                            recog.base.set_state(165);
                            recog.arithmeticExpr()?;
                        }
                    }
                    2 => {
                        {
                            /*InvokeRule functionCall*/
                            recog.base.set_state(166);
                            recog.functionCall()?;
                        }
                    }
                    3 => {
                        {
                            /*InvokeRule qualifiedIdent*/
                            recog.base.set_state(167);
                            recog.qualifiedIdent()?;
                        }
                    }
                    4 => {
                        recog.base.set_state(168);
                        recog.base.match_token(IDENT, &mut recog.err_handler)?;
                    }

                    _ => {}
                }
                recog.base.set_state(172);
                recog.err_handler.sync(&mut recog.base)?;
                _la = recog.base.input.la(1);
                if _la == ASC || _la == DESC {
                    {
                        /*InvokeRule orderDirection*/
                        recog.base.set_state(171);
                        recog.orderDirection()?;
                    }
                }
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- lambdaExpr ----------------
pub type LambdaExprContextAll<'input> = LambdaExprContext<'input>;

pub type LambdaExprContext<'input> = BaseParserRuleContext<'input, LambdaExprContextExt<'input>>;

#[derive(Clone)]
pub struct LambdaExprContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for LambdaExprContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for LambdaExprContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_lambdaExpr(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_lambdaExpr(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for LambdaExprContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_lambdaExpr(self);
    }
}

impl<'input> CustomRuleContext<'input> for LambdaExprContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_lambdaExpr
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_lambdaExpr }
}
antlr_rust::tid! {LambdaExprContextExt<'a>}

impl<'input> LambdaExprContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<LambdaExprContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            LambdaExprContextExt { ph: PhantomData },
        ))
    }
}

pub trait LambdaExprContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<LambdaExprContextExt<'input>>
{
    /// Retrieves all `TerminalNode`s corresponding to token IDENT in current rule
    fn IDENT_all(&self) -> Vec<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.children_of_type()
    }
    /// Retrieves 'i's TerminalNode corresponding to token IDENT, starting from 0.
    /// Returns `None` if number of children corresponding to token IDENT is less or equal than `i`.
    fn IDENT(&self, i: usize) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(IDENT, i)
    }
    fn logicalExpr(&self) -> Option<Rc<LogicalExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
}

impl<'input> LambdaExprContextAttrs<'input> for LambdaExprContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn lambdaExpr(&mut self) -> Result<Rc<LambdaExprContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = LambdaExprContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog
            .base
            .enter_rule(_localctx.clone(), 24, RULE_lambdaExpr);
        let mut _localctx: Rc<LambdaExprContextAll> = _localctx;
        let mut _la: isize = -1;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                recog.base.set_state(174);
                recog.base.match_token(T__6, &mut recog.err_handler)?;

                recog.base.set_state(175);
                recog.base.match_token(T__1, &mut recog.err_handler)?;

                recog.base.set_state(176);
                recog.base.match_token(IDENT, &mut recog.err_handler)?;

                recog.base.set_state(181);
                recog.err_handler.sync(&mut recog.base)?;
                _la = recog.base.input.la(1);
                while _la == T__0 {
                    {
                        {
                            recog.base.set_state(177);
                            recog.base.match_token(T__0, &mut recog.err_handler)?;

                            recog.base.set_state(178);
                            recog.base.match_token(IDENT, &mut recog.err_handler)?;
                        }
                    }
                    recog.base.set_state(183);
                    recog.err_handler.sync(&mut recog.base)?;
                    _la = recog.base.input.la(1);
                }
                recog.base.set_state(184);
                recog.base.match_token(T__2, &mut recog.err_handler)?;

                recog.base.set_state(185);
                recog.base.match_token(T__7, &mut recog.err_handler)?;

                /*InvokeRule logicalExpr*/
                recog.base.set_state(186);
                recog.logicalExpr()?;
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- qualifiedIdent ----------------
pub type QualifiedIdentContextAll<'input> = QualifiedIdentContext<'input>;

pub type QualifiedIdentContext<'input> =
    BaseParserRuleContext<'input, QualifiedIdentContextExt<'input>>;

#[derive(Clone)]
pub struct QualifiedIdentContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for QualifiedIdentContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for QualifiedIdentContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_qualifiedIdent(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_qualifiedIdent(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for QualifiedIdentContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_qualifiedIdent(self);
    }
}

impl<'input> CustomRuleContext<'input> for QualifiedIdentContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_qualifiedIdent
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_qualifiedIdent }
}
antlr_rust::tid! {QualifiedIdentContextExt<'a>}

impl<'input> QualifiedIdentContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<QualifiedIdentContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            QualifiedIdentContextExt { ph: PhantomData },
        ))
    }
}

pub trait QualifiedIdentContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<QualifiedIdentContextExt<'input>>
{
    /// Retrieves all `TerminalNode`s corresponding to token IDENT in current rule
    fn IDENT_all(&self) -> Vec<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.children_of_type()
    }
    /// Retrieves 'i's TerminalNode corresponding to token IDENT, starting from 0.
    /// Returns `None` if number of children corresponding to token IDENT is less or equal than `i`.
    fn IDENT(&self, i: usize) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(IDENT, i)
    }
}

impl<'input> QualifiedIdentContextAttrs<'input> for QualifiedIdentContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn qualifiedIdent(&mut self) -> Result<Rc<QualifiedIdentContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx =
            QualifiedIdentContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog
            .base
            .enter_rule(_localctx.clone(), 26, RULE_qualifiedIdent);
        let mut _localctx: Rc<QualifiedIdentContextAll> = _localctx;
        let mut _la: isize = -1;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                recog.base.set_state(188);
                recog.base.match_token(IDENT, &mut recog.err_handler)?;

                recog.base.set_state(191);
                recog.err_handler.sync(&mut recog.base)?;
                _la = recog.base.input.la(1);
                loop {
                    {
                        {
                            recog.base.set_state(189);
                            recog.base.match_token(T__8, &mut recog.err_handler)?;

                            recog.base.set_state(190);
                            recog.base.match_token(IDENT, &mut recog.err_handler)?;
                        }
                    }
                    recog.base.set_state(193);
                    recog.err_handler.sync(&mut recog.base)?;
                    _la = recog.base.input.la(1);
                    if !(_la == T__8) {
                        break;
                    }
                }
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- argList ----------------
pub type ArgListContextAll<'input> = ArgListContext<'input>;

pub type ArgListContext<'input> = BaseParserRuleContext<'input, ArgListContextExt<'input>>;

#[derive(Clone)]
pub struct ArgListContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for ArgListContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for ArgListContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_argList(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_argList(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for ArgListContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_argList(self);
    }
}

impl<'input> CustomRuleContext<'input> for ArgListContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_argList
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_argList }
}
antlr_rust::tid! {ArgListContextExt<'a>}

impl<'input> ArgListContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<ArgListContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            ArgListContextExt { ph: PhantomData },
        ))
    }
}

pub trait ArgListContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<ArgListContextExt<'input>>
{
    fn arg_all(&self) -> Vec<Rc<ArgContextAll<'input>>>
    where
        Self: Sized,
    {
        self.children_of_type()
    }
    fn arg(&self, i: usize) -> Option<Rc<ArgContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(i)
    }
}

impl<'input> ArgListContextAttrs<'input> for ArgListContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn argList(&mut self) -> Result<Rc<ArgListContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = ArgListContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog.base.enter_rule(_localctx.clone(), 28, RULE_argList);
        let mut _localctx: Rc<ArgListContextAll> = _localctx;
        let mut _la: isize = -1;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                /*InvokeRule arg*/
                recog.base.set_state(195);
                recog.arg()?;

                recog.base.set_state(200);
                recog.err_handler.sync(&mut recog.base)?;
                _la = recog.base.input.la(1);
                while _la == T__0 {
                    {
                        {
                            recog.base.set_state(196);
                            recog.base.match_token(T__0, &mut recog.err_handler)?;

                            /*InvokeRule arg*/
                            recog.base.set_state(197);
                            recog.arg()?;
                        }
                    }
                    recog.base.set_state(202);
                    recog.err_handler.sync(&mut recog.base)?;
                    _la = recog.base.input.la(1);
                }
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- arg ----------------
pub type ArgContextAll<'input> = ArgContext<'input>;

pub type ArgContext<'input> = BaseParserRuleContext<'input, ArgContextExt<'input>>;

#[derive(Clone)]
pub struct ArgContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for ArgContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for ArgContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_arg(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_arg(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for ArgContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_arg(self);
    }
}

impl<'input> CustomRuleContext<'input> for ArgContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_arg
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_arg }
}
antlr_rust::tid! {ArgContextExt<'a>}

impl<'input> ArgContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<ArgContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            ArgContextExt { ph: PhantomData },
        ))
    }
}

pub trait ArgContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<ArgContextExt<'input>>
{
    fn columnAlias(&self) -> Option<Rc<ColumnAliasContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn arithmeticExpr(&self) -> Option<Rc<ArithmeticExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn functionCall(&self) -> Option<Rc<FunctionCallContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn caseExpr(&self) -> Option<Rc<CaseExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn expr(&self) -> Option<Rc<ExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn namedArg(&self) -> Option<Rc<NamedArgContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn comparison(&self) -> Option<Rc<ComparisonContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn pipeExpr(&self) -> Option<Rc<PipeExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn lambdaExpr(&self) -> Option<Rc<LambdaExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
}

impl<'input> ArgContextAttrs<'input> for ArgContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn arg(&mut self) -> Result<Rc<ArgContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = ArgContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog.base.enter_rule(_localctx.clone(), 30, RULE_arg);
        let mut _localctx: Rc<ArgContextAll> = _localctx;
        let result: Result<(), ANTLRError> = (|| {
            recog.base.set_state(216);
            recog.err_handler.sync(&mut recog.base)?;
            match recog.interpreter.adaptive_predict(15, &mut recog.base)? {
                1 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 1);
                    recog.base.enter_outer_alt(None, 1);
                    {
                        /*InvokeRule columnAlias*/
                        recog.base.set_state(203);
                        recog.columnAlias()?;
                    }
                }
                2 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 2);
                    recog.base.enter_outer_alt(None, 2);
                    {
                        /*InvokeRule arithmeticExpr*/
                        recog.base.set_state(204);
                        recog.arithmeticExpr()?;
                    }
                }
                3 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 3);
                    recog.base.enter_outer_alt(None, 3);
                    {
                        /*InvokeRule functionCall*/
                        recog.base.set_state(205);
                        recog.functionCall()?;
                    }
                }
                4 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 4);
                    recog.base.enter_outer_alt(None, 4);
                    {
                        /*InvokeRule caseExpr*/
                        recog.base.set_state(206);
                        recog.caseExpr()?;
                    }
                }
                5 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 5);
                    recog.base.enter_outer_alt(None, 5);
                    {
                        /*InvokeRule expr*/
                        recog.base.set_state(207);
                        recog.expr()?;
                    }
                }
                6 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 6);
                    recog.base.enter_outer_alt(None, 6);
                    {
                        /*InvokeRule namedArg*/
                        recog.base.set_state(208);
                        recog.namedArg()?;
                    }
                }
                7 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 7);
                    recog.base.enter_outer_alt(None, 7);
                    {
                        /*InvokeRule comparison*/
                        recog.base.set_state(209);
                        recog.comparison()?;
                    }
                }
                8 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 8);
                    recog.base.enter_outer_alt(None, 8);
                    {
                        /*InvokeRule pipeExpr*/
                        recog.base.set_state(210);
                        recog.pipeExpr()?;
                    }
                }
                9 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 9);
                    recog.base.enter_outer_alt(None, 9);
                    {
                        /*InvokeRule lambdaExpr*/
                        recog.base.set_state(211);
                        recog.lambdaExpr()?;
                    }
                }
                10 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 10);
                    recog.base.enter_outer_alt(None, 10);
                    {
                        recog.base.set_state(212);
                        recog.base.match_token(T__1, &mut recog.err_handler)?;

                        /*InvokeRule pipeExpr*/
                        recog.base.set_state(213);
                        recog.pipeExpr()?;

                        recog.base.set_state(214);
                        recog.base.match_token(T__2, &mut recog.err_handler)?;
                    }
                }

                _ => {}
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- columnAlias ----------------
pub type ColumnAliasContextAll<'input> = ColumnAliasContext<'input>;

pub type ColumnAliasContext<'input> = BaseParserRuleContext<'input, ColumnAliasContextExt<'input>>;

#[derive(Clone)]
pub struct ColumnAliasContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for ColumnAliasContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for ColumnAliasContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_columnAlias(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_columnAlias(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for ColumnAliasContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_columnAlias(self);
    }
}

impl<'input> CustomRuleContext<'input> for ColumnAliasContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_columnAlias
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_columnAlias }
}
antlr_rust::tid! {ColumnAliasContextExt<'a>}

impl<'input> ColumnAliasContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<ColumnAliasContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            ColumnAliasContextExt { ph: PhantomData },
        ))
    }
}

pub trait ColumnAliasContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<ColumnAliasContextExt<'input>>
{
    fn arithmeticExpr(&self) -> Option<Rc<ArithmeticExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn functionCall(&self) -> Option<Rc<FunctionCallContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn qualifiedIdent(&self) -> Option<Rc<QualifiedIdentContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    /// Retrieves all `TerminalNode`s corresponding to token IDENT in current rule
    fn IDENT_all(&self) -> Vec<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.children_of_type()
    }
    /// Retrieves 'i's TerminalNode corresponding to token IDENT, starting from 0.
    /// Returns `None` if number of children corresponding to token IDENT is less or equal than `i`.
    fn IDENT(&self, i: usize) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(IDENT, i)
    }
    /// Retrieves first TerminalNode corresponding to token AS
    /// Returns `None` if there is no child corresponding to token AS
    fn AS(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(AS, 0)
    }
}

impl<'input> ColumnAliasContextAttrs<'input> for ColumnAliasContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn columnAlias(&mut self) -> Result<Rc<ColumnAliasContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = ColumnAliasContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog
            .base
            .enter_rule(_localctx.clone(), 32, RULE_columnAlias);
        let mut _localctx: Rc<ColumnAliasContextAll> = _localctx;
        let mut _la: isize = -1;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                recog.base.set_state(222);
                recog.err_handler.sync(&mut recog.base)?;
                match recog.interpreter.adaptive_predict(16, &mut recog.base)? {
                    1 => {
                        {
                            /*InvokeRule arithmeticExpr*/
                            recog.base.set_state(218);
                            recog.arithmeticExpr()?;
                        }
                    }
                    2 => {
                        {
                            /*InvokeRule functionCall*/
                            recog.base.set_state(219);
                            recog.functionCall()?;
                        }
                    }
                    3 => {
                        {
                            /*InvokeRule qualifiedIdent*/
                            recog.base.set_state(220);
                            recog.qualifiedIdent()?;
                        }
                    }
                    4 => {
                        recog.base.set_state(221);
                        recog.base.match_token(IDENT, &mut recog.err_handler)?;
                    }

                    _ => {}
                }
                recog.base.set_state(226);
                recog.err_handler.sync(&mut recog.base)?;
                _la = recog.base.input.la(1);
                if _la == AS {
                    {
                        recog.base.set_state(224);
                        recog.base.match_token(AS, &mut recog.err_handler)?;

                        recog.base.set_state(225);
                        recog.base.match_token(IDENT, &mut recog.err_handler)?;
                    }
                }
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- arithmeticExpr ----------------
pub type ArithmeticExprContextAll<'input> = ArithmeticExprContext<'input>;

pub type ArithmeticExprContext<'input> =
    BaseParserRuleContext<'input, ArithmeticExprContextExt<'input>>;

#[derive(Clone)]
pub struct ArithmeticExprContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for ArithmeticExprContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for ArithmeticExprContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_arithmeticExpr(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_arithmeticExpr(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for ArithmeticExprContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_arithmeticExpr(self);
    }
}

impl<'input> CustomRuleContext<'input> for ArithmeticExprContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_arithmeticExpr
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_arithmeticExpr }
}
antlr_rust::tid! {ArithmeticExprContextExt<'a>}

impl<'input> ArithmeticExprContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<ArithmeticExprContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            ArithmeticExprContextExt { ph: PhantomData },
        ))
    }
}

pub trait ArithmeticExprContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<ArithmeticExprContextExt<'input>>
{
    fn arithmeticTerm_all(&self) -> Vec<Rc<ArithmeticTermContextAll<'input>>>
    where
        Self: Sized,
    {
        self.children_of_type()
    }
    fn arithmeticTerm(&self, i: usize) -> Option<Rc<ArithmeticTermContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(i)
    }
}

impl<'input> ArithmeticExprContextAttrs<'input> for ArithmeticExprContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn arithmeticExpr(&mut self) -> Result<Rc<ArithmeticExprContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx =
            ArithmeticExprContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog
            .base
            .enter_rule(_localctx.clone(), 34, RULE_arithmeticExpr);
        let mut _localctx: Rc<ArithmeticExprContextAll> = _localctx;
        let mut _la: isize = -1;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                /*InvokeRule arithmeticTerm*/
                recog.base.set_state(228);
                recog.arithmeticTerm()?;

                recog.base.set_state(233);
                recog.err_handler.sync(&mut recog.base)?;
                _la = recog.base.input.la(1);
                while (((_la) & !0x3f) == 0
                    && ((1usize << _la)
                        & ((1usize << T__9) | (1usize << T__10) | (1usize << T__11)))
                        != 0)
                {
                    {
                        {
                            recog.base.set_state(229);
                            _la = recog.base.input.la(1);
                            if {
                                !(((_la) & !0x3f) == 0
                                    && ((1usize << _la)
                                        & ((1usize << T__9)
                                            | (1usize << T__10)
                                            | (1usize << T__11)))
                                        != 0)
                            } {
                                recog.err_handler.recover_inline(&mut recog.base)?;
                            } else {
                                if recog.base.input.la(1) == TOKEN_EOF {
                                    recog.base.matched_eof = true
                                };
                                recog.err_handler.report_match(&mut recog.base);
                                recog.base.consume(&mut recog.err_handler);
                            }
                            /*InvokeRule arithmeticTerm*/
                            recog.base.set_state(230);
                            recog.arithmeticTerm()?;
                        }
                    }
                    recog.base.set_state(235);
                    recog.err_handler.sync(&mut recog.base)?;
                    _la = recog.base.input.la(1);
                }
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- arithmeticTerm ----------------
pub type ArithmeticTermContextAll<'input> = ArithmeticTermContext<'input>;

pub type ArithmeticTermContext<'input> =
    BaseParserRuleContext<'input, ArithmeticTermContextExt<'input>>;

#[derive(Clone)]
pub struct ArithmeticTermContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for ArithmeticTermContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for ArithmeticTermContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_arithmeticTerm(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_arithmeticTerm(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for ArithmeticTermContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_arithmeticTerm(self);
    }
}

impl<'input> CustomRuleContext<'input> for ArithmeticTermContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_arithmeticTerm
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_arithmeticTerm }
}
antlr_rust::tid! {ArithmeticTermContextExt<'a>}

impl<'input> ArithmeticTermContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<ArithmeticTermContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            ArithmeticTermContextExt { ph: PhantomData },
        ))
    }
}

pub trait ArithmeticTermContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<ArithmeticTermContextExt<'input>>
{
    fn arithmeticFactor_all(&self) -> Vec<Rc<ArithmeticFactorContextAll<'input>>>
    where
        Self: Sized,
    {
        self.children_of_type()
    }
    fn arithmeticFactor(&self, i: usize) -> Option<Rc<ArithmeticFactorContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(i)
    }
    /// Retrieves all `TerminalNode`s corresponding to token ASTERISK in current rule
    fn ASTERISK_all(&self) -> Vec<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.children_of_type()
    }
    /// Retrieves 'i's TerminalNode corresponding to token ASTERISK, starting from 0.
    /// Returns `None` if number of children corresponding to token ASTERISK is less or equal than `i`.
    fn ASTERISK(&self, i: usize) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(ASTERISK, i)
    }
}

impl<'input> ArithmeticTermContextAttrs<'input> for ArithmeticTermContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn arithmeticTerm(&mut self) -> Result<Rc<ArithmeticTermContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx =
            ArithmeticTermContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog
            .base
            .enter_rule(_localctx.clone(), 36, RULE_arithmeticTerm);
        let mut _localctx: Rc<ArithmeticTermContextAll> = _localctx;
        let mut _la: isize = -1;
        let result: Result<(), ANTLRError> = (|| {
            let mut _alt: isize;
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                /*InvokeRule arithmeticFactor*/
                recog.base.set_state(236);
                recog.arithmeticFactor()?;

                recog.base.set_state(241);
                recog.err_handler.sync(&mut recog.base)?;
                _alt = recog.interpreter.adaptive_predict(19, &mut recog.base)?;
                while { _alt != 2 && _alt != INVALID_ALT } {
                    if _alt == 1 {
                        {
                            {
                                recog.base.set_state(237);
                                _la = recog.base.input.la(1);
                                if { !(_la == T__12 || _la == T__13 || _la == ASTERISK) } {
                                    recog.err_handler.recover_inline(&mut recog.base)?;
                                } else {
                                    if recog.base.input.la(1) == TOKEN_EOF {
                                        recog.base.matched_eof = true
                                    };
                                    recog.err_handler.report_match(&mut recog.base);
                                    recog.base.consume(&mut recog.err_handler);
                                }
                                /*InvokeRule arithmeticFactor*/
                                recog.base.set_state(238);
                                recog.arithmeticFactor()?;
                            }
                        }
                    }
                    recog.base.set_state(243);
                    recog.err_handler.sync(&mut recog.base)?;
                    _alt = recog.interpreter.adaptive_predict(19, &mut recog.base)?;
                }
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- arithmeticFactor ----------------
pub type ArithmeticFactorContextAll<'input> = ArithmeticFactorContext<'input>;

pub type ArithmeticFactorContext<'input> =
    BaseParserRuleContext<'input, ArithmeticFactorContextExt<'input>>;

#[derive(Clone)]
pub struct ArithmeticFactorContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for ArithmeticFactorContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for ArithmeticFactorContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_arithmeticFactor(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_arithmeticFactor(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for ArithmeticFactorContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_arithmeticFactor(self);
    }
}

impl<'input> CustomRuleContext<'input> for ArithmeticFactorContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_arithmeticFactor
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_arithmeticFactor }
}
antlr_rust::tid! {ArithmeticFactorContextExt<'a>}

impl<'input> ArithmeticFactorContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<ArithmeticFactorContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            ArithmeticFactorContextExt { ph: PhantomData },
        ))
    }
}

pub trait ArithmeticFactorContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<ArithmeticFactorContextExt<'input>>
{
    fn qualifiedIdent(&self) -> Option<Rc<QualifiedIdentContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    /// Retrieves first TerminalNode corresponding to token IDENT
    /// Returns `None` if there is no child corresponding to token IDENT
    fn IDENT(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(IDENT, 0)
    }
    /// Retrieves first TerminalNode corresponding to token INT
    /// Returns `None` if there is no child corresponding to token INT
    fn INT(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(INT, 0)
    }
    /// Retrieves first TerminalNode corresponding to token DECIMAL
    /// Returns `None` if there is no child corresponding to token DECIMAL
    fn DECIMAL(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(DECIMAL, 0)
    }
    /// Retrieves first TerminalNode corresponding to token STRING
    /// Returns `None` if there is no child corresponding to token STRING
    fn STRING(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(STRING, 0)
    }
    fn functionCall(&self) -> Option<Rc<FunctionCallContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn caseExpr(&self) -> Option<Rc<CaseExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    /// Retrieves first TerminalNode corresponding to token PARAMETER
    /// Returns `None` if there is no child corresponding to token PARAMETER
    fn PARAMETER(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(PARAMETER, 0)
    }
    fn arithmeticExpr(&self) -> Option<Rc<ArithmeticExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
}

impl<'input> ArithmeticFactorContextAttrs<'input> for ArithmeticFactorContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn arithmeticFactor(
        &mut self,
    ) -> Result<Rc<ArithmeticFactorContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx =
            ArithmeticFactorContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog
            .base
            .enter_rule(_localctx.clone(), 38, RULE_arithmeticFactor);
        let mut _localctx: Rc<ArithmeticFactorContextAll> = _localctx;
        let result: Result<(), ANTLRError> = (|| {
            recog.base.set_state(256);
            recog.err_handler.sync(&mut recog.base)?;
            match recog.interpreter.adaptive_predict(20, &mut recog.base)? {
                1 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 1);
                    recog.base.enter_outer_alt(None, 1);
                    {
                        /*InvokeRule qualifiedIdent*/
                        recog.base.set_state(244);
                        recog.qualifiedIdent()?;
                    }
                }
                2 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 2);
                    recog.base.enter_outer_alt(None, 2);
                    {
                        recog.base.set_state(245);
                        recog.base.match_token(IDENT, &mut recog.err_handler)?;
                    }
                }
                3 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 3);
                    recog.base.enter_outer_alt(None, 3);
                    {
                        recog.base.set_state(246);
                        recog.base.match_token(INT, &mut recog.err_handler)?;
                    }
                }
                4 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 4);
                    recog.base.enter_outer_alt(None, 4);
                    {
                        recog.base.set_state(247);
                        recog.base.match_token(DECIMAL, &mut recog.err_handler)?;
                    }
                }
                5 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 5);
                    recog.base.enter_outer_alt(None, 5);
                    {
                        recog.base.set_state(248);
                        recog.base.match_token(STRING, &mut recog.err_handler)?;
                    }
                }
                6 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 6);
                    recog.base.enter_outer_alt(None, 6);
                    {
                        /*InvokeRule functionCall*/
                        recog.base.set_state(249);
                        recog.functionCall()?;
                    }
                }
                7 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 7);
                    recog.base.enter_outer_alt(None, 7);
                    {
                        /*InvokeRule caseExpr*/
                        recog.base.set_state(250);
                        recog.caseExpr()?;
                    }
                }
                8 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 8);
                    recog.base.enter_outer_alt(None, 8);
                    {
                        recog.base.set_state(251);
                        recog.base.match_token(PARAMETER, &mut recog.err_handler)?;
                    }
                }
                9 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 9);
                    recog.base.enter_outer_alt(None, 9);
                    {
                        recog.base.set_state(252);
                        recog.base.match_token(T__1, &mut recog.err_handler)?;

                        /*InvokeRule arithmeticExpr*/
                        recog.base.set_state(253);
                        recog.arithmeticExpr()?;

                        recog.base.set_state(254);
                        recog.base.match_token(T__2, &mut recog.err_handler)?;
                    }
                }

                _ => {}
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- functionCall ----------------
pub type FunctionCallContextAll<'input> = FunctionCallContext<'input>;

pub type FunctionCallContext<'input> =
    BaseParserRuleContext<'input, FunctionCallContextExt<'input>>;

#[derive(Clone)]
pub struct FunctionCallContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for FunctionCallContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for FunctionCallContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_functionCall(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_functionCall(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for FunctionCallContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_functionCall(self);
    }
}

impl<'input> CustomRuleContext<'input> for FunctionCallContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_functionCall
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_functionCall }
}
antlr_rust::tid! {FunctionCallContextExt<'a>}

impl<'input> FunctionCallContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<FunctionCallContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            FunctionCallContextExt { ph: PhantomData },
        ))
    }
}

pub trait FunctionCallContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<FunctionCallContextExt<'input>>
{
    /// Retrieves first TerminalNode corresponding to token IDENT
    /// Returns `None` if there is no child corresponding to token IDENT
    fn IDENT(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(IDENT, 0)
    }
    fn argList(&self) -> Option<Rc<ArgListContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    /// Retrieves first TerminalNode corresponding to token OVER
    /// Returns `None` if there is no child corresponding to token OVER
    fn OVER(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(OVER, 0)
    }
    fn windowSpec(&self) -> Option<Rc<WindowSpecContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    /// Retrieves first TerminalNode corresponding to token DISTINCT
    /// Returns `None` if there is no child corresponding to token DISTINCT
    fn DISTINCT(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(DISTINCT, 0)
    }
}

impl<'input> FunctionCallContextAttrs<'input> for FunctionCallContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn functionCall(&mut self) -> Result<Rc<FunctionCallContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = FunctionCallContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog
            .base
            .enter_rule(_localctx.clone(), 40, RULE_functionCall);
        let mut _localctx: Rc<FunctionCallContextAll> = _localctx;
        let mut _la: isize = -1;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                recog.base.set_state(258);
                recog.base.match_token(IDENT, &mut recog.err_handler)?;

                recog.base.set_state(259);
                recog.base.match_token(T__1, &mut recog.err_handler)?;

                recog.base.set_state(264);
                recog.err_handler.sync(&mut recog.base)?;
                _la = recog.base.input.la(1);
                if (((_la) & !0x3f) == 0
                    && ((1usize << _la)
                        & ((1usize << T__1)
                            | (1usize << T__6)
                            | (1usize << DISTINCT)
                            | (1usize << EXISTS)))
                        != 0)
                    || (((_la - 32) & !0x3f) == 0
                        && ((1usize << (_la - 32))
                            & ((1usize << (CASE - 32))
                                | (1usize << (ON - 32))
                                | (1usize << (PARAMETER - 32))
                                | (1usize << (IDENT - 32))
                                | (1usize << (INT - 32))
                                | (1usize << (DECIMAL - 32))
                                | (1usize << (STRING - 32))
                                | (1usize << (ASTERISK - 32))))
                            != 0)
                {
                    {
                        recog.base.set_state(261);
                        recog.err_handler.sync(&mut recog.base)?;
                        _la = recog.base.input.la(1);
                        if _la == DISTINCT {
                            {
                                recog.base.set_state(260);
                                recog.base.match_token(DISTINCT, &mut recog.err_handler)?;
                            }
                        }

                        /*InvokeRule argList*/
                        recog.base.set_state(263);
                        recog.argList()?;
                    }
                }

                recog.base.set_state(266);
                recog.base.match_token(T__2, &mut recog.err_handler)?;

                recog.base.set_state(272);
                recog.err_handler.sync(&mut recog.base)?;
                _la = recog.base.input.la(1);
                if _la == OVER {
                    {
                        recog.base.set_state(267);
                        recog.base.match_token(OVER, &mut recog.err_handler)?;

                        recog.base.set_state(268);
                        recog.base.match_token(T__1, &mut recog.err_handler)?;

                        /*InvokeRule windowSpec*/
                        recog.base.set_state(269);
                        recog.windowSpec()?;

                        recog.base.set_state(270);
                        recog.base.match_token(T__2, &mut recog.err_handler)?;
                    }
                }
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- namedArg ----------------
pub type NamedArgContextAll<'input> = NamedArgContext<'input>;

pub type NamedArgContext<'input> = BaseParserRuleContext<'input, NamedArgContextExt<'input>>;

#[derive(Clone)]
pub struct NamedArgContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for NamedArgContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for NamedArgContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_namedArg(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_namedArg(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for NamedArgContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_namedArg(self);
    }
}

impl<'input> CustomRuleContext<'input> for NamedArgContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_namedArg
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_namedArg }
}
antlr_rust::tid! {NamedArgContextExt<'a>}

impl<'input> NamedArgContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<NamedArgContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            NamedArgContextExt { ph: PhantomData },
        ))
    }
}

pub trait NamedArgContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<NamedArgContextExt<'input>>
{
    /// Retrieves first TerminalNode corresponding to token IDENT
    /// Returns `None` if there is no child corresponding to token IDENT
    fn IDENT(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(IDENT, 0)
    }
    /// Retrieves first TerminalNode corresponding to token ON
    /// Returns `None` if there is no child corresponding to token ON
    fn ON(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(ON, 0)
    }
    fn comparison(&self) -> Option<Rc<ComparisonContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn logicalExpr(&self) -> Option<Rc<LogicalExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
}

impl<'input> NamedArgContextAttrs<'input> for NamedArgContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn namedArg(&mut self) -> Result<Rc<NamedArgContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = NamedArgContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog.base.enter_rule(_localctx.clone(), 42, RULE_namedArg);
        let mut _localctx: Rc<NamedArgContextAll> = _localctx;
        let mut _la: isize = -1;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                recog.base.set_state(274);
                _la = recog.base.input.la(1);
                if { !(_la == ON || _la == IDENT) } {
                    recog.err_handler.recover_inline(&mut recog.base)?;
                } else {
                    if recog.base.input.la(1) == TOKEN_EOF {
                        recog.base.matched_eof = true
                    };
                    recog.err_handler.report_match(&mut recog.base);
                    recog.base.consume(&mut recog.err_handler);
                }
                recog.base.set_state(275);
                recog.base.match_token(T__4, &mut recog.err_handler)?;

                recog.base.set_state(278);
                recog.err_handler.sync(&mut recog.base)?;
                match recog.interpreter.adaptive_predict(24, &mut recog.base)? {
                    1 => {
                        {
                            /*InvokeRule comparison*/
                            recog.base.set_state(276);
                            recog.comparison()?;
                        }
                    }
                    2 => {
                        {
                            /*InvokeRule logicalExpr*/
                            recog.base.set_state(277);
                            recog.logicalExpr()?;
                        }
                    }

                    _ => {}
                }
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- logicalExpr ----------------
pub type LogicalExprContextAll<'input> = LogicalExprContext<'input>;

pub type LogicalExprContext<'input> = BaseParserRuleContext<'input, LogicalExprContextExt<'input>>;

#[derive(Clone)]
pub struct LogicalExprContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for LogicalExprContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for LogicalExprContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_logicalExpr(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_logicalExpr(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for LogicalExprContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_logicalExpr(self);
    }
}

impl<'input> CustomRuleContext<'input> for LogicalExprContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_logicalExpr
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_logicalExpr }
}
antlr_rust::tid! {LogicalExprContextExt<'a>}

impl<'input> LogicalExprContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<LogicalExprContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            LogicalExprContextExt { ph: PhantomData },
        ))
    }
}

pub trait LogicalExprContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<LogicalExprContextExt<'input>>
{
    fn andExpr_all(&self) -> Vec<Rc<AndExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.children_of_type()
    }
    fn andExpr(&self, i: usize) -> Option<Rc<AndExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(i)
    }
    /// Retrieves all `TerminalNode`s corresponding to token OR in current rule
    fn OR_all(&self) -> Vec<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.children_of_type()
    }
    /// Retrieves 'i's TerminalNode corresponding to token OR, starting from 0.
    /// Returns `None` if number of children corresponding to token OR is less or equal than `i`.
    fn OR(&self, i: usize) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(OR, i)
    }
}

impl<'input> LogicalExprContextAttrs<'input> for LogicalExprContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn logicalExpr(&mut self) -> Result<Rc<LogicalExprContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = LogicalExprContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog
            .base
            .enter_rule(_localctx.clone(), 44, RULE_logicalExpr);
        let mut _localctx: Rc<LogicalExprContextAll> = _localctx;
        let result: Result<(), ANTLRError> = (|| {
            let mut _alt: isize;
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                /*InvokeRule andExpr*/
                recog.base.set_state(280);
                recog.andExpr()?;

                recog.base.set_state(285);
                recog.err_handler.sync(&mut recog.base)?;
                _alt = recog.interpreter.adaptive_predict(25, &mut recog.base)?;
                while { _alt != 2 && _alt != INVALID_ALT } {
                    if _alt == 1 {
                        {
                            {
                                recog.base.set_state(281);
                                recog.base.match_token(OR, &mut recog.err_handler)?;

                                /*InvokeRule andExpr*/
                                recog.base.set_state(282);
                                recog.andExpr()?;
                            }
                        }
                    }
                    recog.base.set_state(287);
                    recog.err_handler.sync(&mut recog.base)?;
                    _alt = recog.interpreter.adaptive_predict(25, &mut recog.base)?;
                }
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- andExpr ----------------
pub type AndExprContextAll<'input> = AndExprContext<'input>;

pub type AndExprContext<'input> = BaseParserRuleContext<'input, AndExprContextExt<'input>>;

#[derive(Clone)]
pub struct AndExprContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for AndExprContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for AndExprContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_andExpr(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_andExpr(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for AndExprContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_andExpr(self);
    }
}

impl<'input> CustomRuleContext<'input> for AndExprContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_andExpr
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_andExpr }
}
antlr_rust::tid! {AndExprContextExt<'a>}

impl<'input> AndExprContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<AndExprContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            AndExprContextExt { ph: PhantomData },
        ))
    }
}

pub trait AndExprContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<AndExprContextExt<'input>>
{
    fn atomicExpr_all(&self) -> Vec<Rc<AtomicExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.children_of_type()
    }
    fn atomicExpr(&self, i: usize) -> Option<Rc<AtomicExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(i)
    }
    /// Retrieves all `TerminalNode`s corresponding to token AND in current rule
    fn AND_all(&self) -> Vec<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.children_of_type()
    }
    /// Retrieves 'i's TerminalNode corresponding to token AND, starting from 0.
    /// Returns `None` if number of children corresponding to token AND is less or equal than `i`.
    fn AND(&self, i: usize) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(AND, i)
    }
}

impl<'input> AndExprContextAttrs<'input> for AndExprContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn andExpr(&mut self) -> Result<Rc<AndExprContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = AndExprContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog.base.enter_rule(_localctx.clone(), 46, RULE_andExpr);
        let mut _localctx: Rc<AndExprContextAll> = _localctx;
        let result: Result<(), ANTLRError> = (|| {
            let mut _alt: isize;
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                /*InvokeRule atomicExpr*/
                recog.base.set_state(288);
                recog.atomicExpr()?;

                recog.base.set_state(293);
                recog.err_handler.sync(&mut recog.base)?;
                _alt = recog.interpreter.adaptive_predict(26, &mut recog.base)?;
                while { _alt != 2 && _alt != INVALID_ALT } {
                    if _alt == 1 {
                        {
                            {
                                recog.base.set_state(289);
                                recog.base.match_token(AND, &mut recog.err_handler)?;

                                /*InvokeRule atomicExpr*/
                                recog.base.set_state(290);
                                recog.atomicExpr()?;
                            }
                        }
                    }
                    recog.base.set_state(295);
                    recog.err_handler.sync(&mut recog.base)?;
                    _alt = recog.interpreter.adaptive_predict(26, &mut recog.base)?;
                }
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- atomicExpr ----------------
pub type AtomicExprContextAll<'input> = AtomicExprContext<'input>;

pub type AtomicExprContext<'input> = BaseParserRuleContext<'input, AtomicExprContextExt<'input>>;

#[derive(Clone)]
pub struct AtomicExprContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for AtomicExprContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for AtomicExprContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_atomicExpr(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_atomicExpr(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for AtomicExprContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_atomicExpr(self);
    }
}

impl<'input> CustomRuleContext<'input> for AtomicExprContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_atomicExpr
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_atomicExpr }
}
antlr_rust::tid! {AtomicExprContextExt<'a>}

impl<'input> AtomicExprContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<AtomicExprContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            AtomicExprContextExt { ph: PhantomData },
        ))
    }
}

pub trait AtomicExprContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<AtomicExprContextExt<'input>>
{
    fn comparison(&self) -> Option<Rc<ComparisonContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn logicalExpr(&self) -> Option<Rc<LogicalExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
}

impl<'input> AtomicExprContextAttrs<'input> for AtomicExprContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn atomicExpr(&mut self) -> Result<Rc<AtomicExprContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = AtomicExprContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog
            .base
            .enter_rule(_localctx.clone(), 48, RULE_atomicExpr);
        let mut _localctx: Rc<AtomicExprContextAll> = _localctx;
        let result: Result<(), ANTLRError> = (|| {
            recog.base.set_state(301);
            recog.err_handler.sync(&mut recog.base)?;
            match recog.interpreter.adaptive_predict(27, &mut recog.base)? {
                1 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 1);
                    recog.base.enter_outer_alt(None, 1);
                    {
                        /*InvokeRule comparison*/
                        recog.base.set_state(296);
                        recog.comparison()?;
                    }
                }
                2 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 2);
                    recog.base.enter_outer_alt(None, 2);
                    {
                        recog.base.set_state(297);
                        recog.base.match_token(T__1, &mut recog.err_handler)?;

                        /*InvokeRule logicalExpr*/
                        recog.base.set_state(298);
                        recog.logicalExpr()?;

                        recog.base.set_state(299);
                        recog.base.match_token(T__2, &mut recog.err_handler)?;
                    }
                }

                _ => {}
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- comparison ----------------
pub type ComparisonContextAll<'input> = ComparisonContext<'input>;

pub type ComparisonContext<'input> = BaseParserRuleContext<'input, ComparisonContextExt<'input>>;

#[derive(Clone)]
pub struct ComparisonContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for ComparisonContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for ComparisonContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_comparison(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_comparison(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for ComparisonContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_comparison(self);
    }
}

impl<'input> CustomRuleContext<'input> for ComparisonContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_comparison
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_comparison }
}
antlr_rust::tid! {ComparisonContextExt<'a>}

impl<'input> ComparisonContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<ComparisonContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            ComparisonContextExt { ph: PhantomData },
        ))
    }
}

pub trait ComparisonContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<ComparisonContextExt<'input>>
{
    fn arithmeticExpr_all(&self) -> Vec<Rc<ArithmeticExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.children_of_type()
    }
    fn arithmeticExpr(&self, i: usize) -> Option<Rc<ArithmeticExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(i)
    }
    fn comparisonOp(&self) -> Option<Rc<ComparisonOpContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn qualifiedIdent_all(&self) -> Vec<Rc<QualifiedIdentContextAll<'input>>>
    where
        Self: Sized,
    {
        self.children_of_type()
    }
    fn qualifiedIdent(&self, i: usize) -> Option<Rc<QualifiedIdentContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(i)
    }
    /// Retrieves first TerminalNode corresponding to token STRING
    /// Returns `None` if there is no child corresponding to token STRING
    fn STRING(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(STRING, 0)
    }
    /// Retrieves all `TerminalNode`s corresponding to token IDENT in current rule
    fn IDENT_all(&self) -> Vec<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.children_of_type()
    }
    /// Retrieves 'i's TerminalNode corresponding to token IDENT, starting from 0.
    /// Returns `None` if number of children corresponding to token IDENT is less or equal than `i`.
    fn IDENT(&self, i: usize) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(IDENT, i)
    }
    /// Retrieves first TerminalNode corresponding to token INT
    /// Returns `None` if there is no child corresponding to token INT
    fn INT(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(INT, 0)
    }
    /// Retrieves first TerminalNode corresponding to token DECIMAL
    /// Returns `None` if there is no child corresponding to token DECIMAL
    fn DECIMAL(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(DECIMAL, 0)
    }
    /// Retrieves all `TerminalNode`s corresponding to token PARAMETER in current rule
    fn PARAMETER_all(&self) -> Vec<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.children_of_type()
    }
    /// Retrieves 'i's TerminalNode corresponding to token PARAMETER, starting from 0.
    /// Returns `None` if number of children corresponding to token PARAMETER is less or equal than `i`.
    fn PARAMETER(&self, i: usize) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(PARAMETER, i)
    }
    fn orderDirection(&self) -> Option<Rc<OrderDirectionContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn expr(&self) -> Option<Rc<ExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn existsExpr(&self) -> Option<Rc<ExistsExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn nullCheckExpr(&self) -> Option<Rc<NullCheckExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn inExpr(&self) -> Option<Rc<InExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
}

impl<'input> ComparisonContextAttrs<'input> for ComparisonContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn comparison(&mut self) -> Result<Rc<ComparisonContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = ComparisonContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog
            .base
            .enter_rule(_localctx.clone(), 50, RULE_comparison);
        let mut _localctx: Rc<ComparisonContextAll> = _localctx;
        let mut _la: isize = -1;
        let result: Result<(), ANTLRError> = (|| {
            recog.base.set_state(356);
            recog.err_handler.sync(&mut recog.base)?;
            match recog.interpreter.adaptive_predict(34, &mut recog.base)? {
                1 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 1);
                    recog.base.enter_outer_alt(None, 1);
                    {
                        /*InvokeRule arithmeticExpr*/
                        recog.base.set_state(303);
                        recog.arithmeticExpr()?;

                        /*InvokeRule comparisonOp*/
                        recog.base.set_state(304);
                        recog.comparisonOp()?;

                        /*InvokeRule arithmeticExpr*/
                        recog.base.set_state(305);
                        recog.arithmeticExpr()?;
                    }
                }
                2 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 2);
                    recog.base.enter_outer_alt(None, 2);
                    {
                        /*InvokeRule qualifiedIdent*/
                        recog.base.set_state(307);
                        recog.qualifiedIdent()?;

                        /*InvokeRule comparisonOp*/
                        recog.base.set_state(308);
                        recog.comparisonOp()?;

                        recog.base.set_state(315);
                        recog.err_handler.sync(&mut recog.base)?;
                        match recog.interpreter.adaptive_predict(28, &mut recog.base)? {
                            1 => {
                                {
                                    /*InvokeRule qualifiedIdent*/
                                    recog.base.set_state(309);
                                    recog.qualifiedIdent()?;
                                }
                            }
                            2 => {
                                recog.base.set_state(310);
                                recog.base.match_token(STRING, &mut recog.err_handler)?;
                            }
                            3 => {
                                recog.base.set_state(311);
                                recog.base.match_token(IDENT, &mut recog.err_handler)?;
                            }
                            4 => {
                                recog.base.set_state(312);
                                recog.base.match_token(INT, &mut recog.err_handler)?;
                            }
                            5 => {
                                recog.base.set_state(313);
                                recog.base.match_token(DECIMAL, &mut recog.err_handler)?;
                            }
                            6 => {
                                recog.base.set_state(314);
                                recog.base.match_token(PARAMETER, &mut recog.err_handler)?;
                            }

                            _ => {}
                        }
                    }
                }
                3 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 3);
                    recog.base.enter_outer_alt(None, 3);
                    {
                        recog.base.set_state(317);
                        recog.base.match_token(IDENT, &mut recog.err_handler)?;

                        /*InvokeRule comparisonOp*/
                        recog.base.set_state(318);
                        recog.comparisonOp()?;

                        recog.base.set_state(325);
                        recog.err_handler.sync(&mut recog.base)?;
                        match recog.interpreter.adaptive_predict(29, &mut recog.base)? {
                            1 => {
                                {
                                    /*InvokeRule qualifiedIdent*/
                                    recog.base.set_state(319);
                                    recog.qualifiedIdent()?;
                                }
                            }
                            2 => {
                                recog.base.set_state(320);
                                recog.base.match_token(STRING, &mut recog.err_handler)?;
                            }
                            3 => {
                                recog.base.set_state(321);
                                recog.base.match_token(IDENT, &mut recog.err_handler)?;
                            }
                            4 => {
                                recog.base.set_state(322);
                                recog.base.match_token(INT, &mut recog.err_handler)?;
                            }
                            5 => {
                                recog.base.set_state(323);
                                recog.base.match_token(DECIMAL, &mut recog.err_handler)?;
                            }
                            6 => {
                                recog.base.set_state(324);
                                recog.base.match_token(PARAMETER, &mut recog.err_handler)?;
                            }

                            _ => {}
                        }
                    }
                }
                4 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 4);
                    recog.base.enter_outer_alt(None, 4);
                    {
                        recog.base.set_state(327);
                        recog.base.match_token(PARAMETER, &mut recog.err_handler)?;

                        /*InvokeRule comparisonOp*/
                        recog.base.set_state(328);
                        recog.comparisonOp()?;

                        recog.base.set_state(335);
                        recog.err_handler.sync(&mut recog.base)?;
                        match recog.interpreter.adaptive_predict(30, &mut recog.base)? {
                            1 => {
                                {
                                    /*InvokeRule qualifiedIdent*/
                                    recog.base.set_state(329);
                                    recog.qualifiedIdent()?;
                                }
                            }
                            2 => {
                                recog.base.set_state(330);
                                recog.base.match_token(STRING, &mut recog.err_handler)?;
                            }
                            3 => {
                                recog.base.set_state(331);
                                recog.base.match_token(IDENT, &mut recog.err_handler)?;
                            }
                            4 => {
                                recog.base.set_state(332);
                                recog.base.match_token(INT, &mut recog.err_handler)?;
                            }
                            5 => {
                                recog.base.set_state(333);
                                recog.base.match_token(DECIMAL, &mut recog.err_handler)?;
                            }
                            6 => {
                                recog.base.set_state(334);
                                recog.base.match_token(PARAMETER, &mut recog.err_handler)?;
                            }

                            _ => {}
                        }
                    }
                }
                5 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 5);
                    recog.base.enter_outer_alt(None, 5);
                    {
                        /*InvokeRule qualifiedIdent*/
                        recog.base.set_state(337);
                        recog.qualifiedIdent()?;

                        recog.base.set_state(339);
                        recog.err_handler.sync(&mut recog.base)?;
                        _la = recog.base.input.la(1);
                        if _la == ASC || _la == DESC {
                            {
                                /*InvokeRule orderDirection*/
                                recog.base.set_state(338);
                                recog.orderDirection()?;
                            }
                        }
                    }
                }
                6 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 6);
                    recog.base.enter_outer_alt(None, 6);
                    {
                        recog.base.set_state(341);
                        recog.base.match_token(IDENT, &mut recog.err_handler)?;

                        recog.base.set_state(343);
                        recog.err_handler.sync(&mut recog.base)?;
                        _la = recog.base.input.la(1);
                        if _la == ASC || _la == DESC {
                            {
                                /*InvokeRule orderDirection*/
                                recog.base.set_state(342);
                                recog.orderDirection()?;
                            }
                        }
                    }
                }
                7 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 7);
                    recog.base.enter_outer_alt(None, 7);
                    {
                        recog.base.set_state(345);
                        recog.base.match_token(PARAMETER, &mut recog.err_handler)?;

                        recog.base.set_state(347);
                        recog.err_handler.sync(&mut recog.base)?;
                        _la = recog.base.input.la(1);
                        if _la == ASC || _la == DESC {
                            {
                                /*InvokeRule orderDirection*/
                                recog.base.set_state(346);
                                recog.orderDirection()?;
                            }
                        }
                    }
                }
                8 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 8);
                    recog.base.enter_outer_alt(None, 8);
                    {
                        recog.base.set_state(349);
                        recog.base.match_token(STRING, &mut recog.err_handler)?;
                    }
                }
                9 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 9);
                    recog.base.enter_outer_alt(None, 9);
                    {
                        recog.base.set_state(350);
                        recog.base.match_token(INT, &mut recog.err_handler)?;
                    }
                }
                10 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 10);
                    recog.base.enter_outer_alt(None, 10);
                    {
                        recog.base.set_state(351);
                        recog.base.match_token(DECIMAL, &mut recog.err_handler)?;
                    }
                }
                11 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 11);
                    recog.base.enter_outer_alt(None, 11);
                    {
                        /*InvokeRule expr*/
                        recog.base.set_state(352);
                        recog.expr()?;
                    }
                }
                12 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 12);
                    recog.base.enter_outer_alt(None, 12);
                    {
                        /*InvokeRule existsExpr*/
                        recog.base.set_state(353);
                        recog.existsExpr()?;
                    }
                }
                13 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 13);
                    recog.base.enter_outer_alt(None, 13);
                    {
                        /*InvokeRule nullCheckExpr*/
                        recog.base.set_state(354);
                        recog.nullCheckExpr()?;
                    }
                }
                14 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 14);
                    recog.base.enter_outer_alt(None, 14);
                    {
                        /*InvokeRule inExpr*/
                        recog.base.set_state(355);
                        recog.inExpr()?;
                    }
                }

                _ => {}
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- existsExpr ----------------
pub type ExistsExprContextAll<'input> = ExistsExprContext<'input>;

pub type ExistsExprContext<'input> = BaseParserRuleContext<'input, ExistsExprContextExt<'input>>;

#[derive(Clone)]
pub struct ExistsExprContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for ExistsExprContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for ExistsExprContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_existsExpr(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_existsExpr(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for ExistsExprContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_existsExpr(self);
    }
}

impl<'input> CustomRuleContext<'input> for ExistsExprContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_existsExpr
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_existsExpr }
}
antlr_rust::tid! {ExistsExprContextExt<'a>}

impl<'input> ExistsExprContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<ExistsExprContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            ExistsExprContextExt { ph: PhantomData },
        ))
    }
}

pub trait ExistsExprContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<ExistsExprContextExt<'input>>
{
    /// Retrieves first TerminalNode corresponding to token EXISTS
    /// Returns `None` if there is no child corresponding to token EXISTS
    fn EXISTS(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(EXISTS, 0)
    }
    fn pipeExpr(&self) -> Option<Rc<PipeExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
}

impl<'input> ExistsExprContextAttrs<'input> for ExistsExprContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn existsExpr(&mut self) -> Result<Rc<ExistsExprContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = ExistsExprContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog
            .base
            .enter_rule(_localctx.clone(), 52, RULE_existsExpr);
        let mut _localctx: Rc<ExistsExprContextAll> = _localctx;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                recog.base.set_state(358);
                recog.base.match_token(EXISTS, &mut recog.err_handler)?;

                recog.base.set_state(359);
                recog.base.match_token(T__1, &mut recog.err_handler)?;

                /*InvokeRule pipeExpr*/
                recog.base.set_state(360);
                recog.pipeExpr()?;

                recog.base.set_state(361);
                recog.base.match_token(T__2, &mut recog.err_handler)?;
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- nullCheckExpr ----------------
pub type NullCheckExprContextAll<'input> = NullCheckExprContext<'input>;

pub type NullCheckExprContext<'input> =
    BaseParserRuleContext<'input, NullCheckExprContextExt<'input>>;

#[derive(Clone)]
pub struct NullCheckExprContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for NullCheckExprContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for NullCheckExprContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_nullCheckExpr(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_nullCheckExpr(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for NullCheckExprContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_nullCheckExpr(self);
    }
}

impl<'input> CustomRuleContext<'input> for NullCheckExprContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_nullCheckExpr
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_nullCheckExpr }
}
antlr_rust::tid! {NullCheckExprContextExt<'a>}

impl<'input> NullCheckExprContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<NullCheckExprContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            NullCheckExprContextExt { ph: PhantomData },
        ))
    }
}

pub trait NullCheckExprContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<NullCheckExprContextExt<'input>>
{
    fn qualifiedIdent(&self) -> Option<Rc<QualifiedIdentContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    /// Retrieves first TerminalNode corresponding to token IDENT
    /// Returns `None` if there is no child corresponding to token IDENT
    fn IDENT(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(IDENT, 0)
    }
    /// Retrieves first TerminalNode corresponding to token PARAMETER
    /// Returns `None` if there is no child corresponding to token PARAMETER
    fn PARAMETER(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(PARAMETER, 0)
    }
    /// Retrieves first TerminalNode corresponding to token IS
    /// Returns `None` if there is no child corresponding to token IS
    fn IS(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(IS, 0)
    }
    /// Retrieves first TerminalNode corresponding to token NULL
    /// Returns `None` if there is no child corresponding to token NULL
    fn NULL(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(NULL, 0)
    }
    /// Retrieves first TerminalNode corresponding to token NOT
    /// Returns `None` if there is no child corresponding to token NOT
    fn NOT(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(NOT, 0)
    }
}

impl<'input> NullCheckExprContextAttrs<'input> for NullCheckExprContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn nullCheckExpr(&mut self) -> Result<Rc<NullCheckExprContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx =
            NullCheckExprContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog
            .base
            .enter_rule(_localctx.clone(), 54, RULE_nullCheckExpr);
        let mut _localctx: Rc<NullCheckExprContextAll> = _localctx;
        let mut _la: isize = -1;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                recog.base.set_state(366);
                recog.err_handler.sync(&mut recog.base)?;
                match recog.interpreter.adaptive_predict(35, &mut recog.base)? {
                    1 => {
                        {
                            /*InvokeRule qualifiedIdent*/
                            recog.base.set_state(363);
                            recog.qualifiedIdent()?;
                        }
                    }
                    2 => {
                        recog.base.set_state(364);
                        recog.base.match_token(IDENT, &mut recog.err_handler)?;
                    }
                    3 => {
                        recog.base.set_state(365);
                        recog.base.match_token(PARAMETER, &mut recog.err_handler)?;
                    }

                    _ => {}
                }
                {
                    recog.base.set_state(368);
                    recog.base.match_token(IS, &mut recog.err_handler)?;

                    recog.base.set_state(370);
                    recog.err_handler.sync(&mut recog.base)?;
                    _la = recog.base.input.la(1);
                    if _la == NOT {
                        {
                            recog.base.set_state(369);
                            recog.base.match_token(NOT, &mut recog.err_handler)?;
                        }
                    }

                    recog.base.set_state(372);
                    recog.base.match_token(NULL, &mut recog.err_handler)?;
                }
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- inExpr ----------------
pub type InExprContextAll<'input> = InExprContext<'input>;

pub type InExprContext<'input> = BaseParserRuleContext<'input, InExprContextExt<'input>>;

#[derive(Clone)]
pub struct InExprContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for InExprContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for InExprContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_inExpr(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_inExpr(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for InExprContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_inExpr(self);
    }
}

impl<'input> CustomRuleContext<'input> for InExprContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_inExpr
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_inExpr }
}
antlr_rust::tid! {InExprContextExt<'a>}

impl<'input> InExprContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<InExprContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            InExprContextExt { ph: PhantomData },
        ))
    }
}

pub trait InExprContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<InExprContextExt<'input>>
{
    /// Retrieves first TerminalNode corresponding to token IN
    /// Returns `None` if there is no child corresponding to token IN
    fn IN(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(IN, 0)
    }
    fn qualifiedIdent(&self) -> Option<Rc<QualifiedIdentContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    /// Retrieves first TerminalNode corresponding to token IDENT
    /// Returns `None` if there is no child corresponding to token IDENT
    fn IDENT(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(IDENT, 0)
    }
    /// Retrieves first TerminalNode corresponding to token PARAMETER
    /// Returns `None` if there is no child corresponding to token PARAMETER
    fn PARAMETER(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(PARAMETER, 0)
    }
    fn pipeExpr(&self) -> Option<Rc<PipeExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn argList(&self) -> Option<Rc<ArgListContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
}

impl<'input> InExprContextAttrs<'input> for InExprContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn inExpr(&mut self) -> Result<Rc<InExprContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = InExprContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog.base.enter_rule(_localctx.clone(), 56, RULE_inExpr);
        let mut _localctx: Rc<InExprContextAll> = _localctx;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                recog.base.set_state(377);
                recog.err_handler.sync(&mut recog.base)?;
                match recog.interpreter.adaptive_predict(37, &mut recog.base)? {
                    1 => {
                        {
                            /*InvokeRule qualifiedIdent*/
                            recog.base.set_state(374);
                            recog.qualifiedIdent()?;
                        }
                    }
                    2 => {
                        recog.base.set_state(375);
                        recog.base.match_token(IDENT, &mut recog.err_handler)?;
                    }
                    3 => {
                        recog.base.set_state(376);
                        recog.base.match_token(PARAMETER, &mut recog.err_handler)?;
                    }

                    _ => {}
                }
                recog.base.set_state(379);
                recog.base.match_token(IN, &mut recog.err_handler)?;

                recog.base.set_state(380);
                recog.base.match_token(T__1, &mut recog.err_handler)?;

                recog.base.set_state(383);
                recog.err_handler.sync(&mut recog.base)?;
                match recog.interpreter.adaptive_predict(38, &mut recog.base)? {
                    1 => {
                        {
                            /*InvokeRule pipeExpr*/
                            recog.base.set_state(381);
                            recog.pipeExpr()?;
                        }
                    }
                    2 => {
                        {
                            /*InvokeRule argList*/
                            recog.base.set_state(382);
                            recog.argList()?;
                        }
                    }

                    _ => {}
                }
                recog.base.set_state(385);
                recog.base.match_token(T__2, &mut recog.err_handler)?;
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- caseExpr ----------------
pub type CaseExprContextAll<'input> = CaseExprContext<'input>;

pub type CaseExprContext<'input> = BaseParserRuleContext<'input, CaseExprContextExt<'input>>;

#[derive(Clone)]
pub struct CaseExprContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for CaseExprContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for CaseExprContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_caseExpr(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_caseExpr(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for CaseExprContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_caseExpr(self);
    }
}

impl<'input> CustomRuleContext<'input> for CaseExprContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_caseExpr
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_caseExpr }
}
antlr_rust::tid! {CaseExprContextExt<'a>}

impl<'input> CaseExprContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<CaseExprContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            CaseExprContextExt { ph: PhantomData },
        ))
    }
}

pub trait CaseExprContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<CaseExprContextExt<'input>>
{
    /// Retrieves first TerminalNode corresponding to token CASE
    /// Returns `None` if there is no child corresponding to token CASE
    fn CASE(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(CASE, 0)
    }
    /// Retrieves first TerminalNode corresponding to token END
    /// Returns `None` if there is no child corresponding to token END
    fn END(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(END, 0)
    }
    fn whenClause_all(&self) -> Vec<Rc<WhenClauseContextAll<'input>>>
    where
        Self: Sized,
    {
        self.children_of_type()
    }
    fn whenClause(&self, i: usize) -> Option<Rc<WhenClauseContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(i)
    }
    /// Retrieves first TerminalNode corresponding to token ELSE
    /// Returns `None` if there is no child corresponding to token ELSE
    fn ELSE(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(ELSE, 0)
    }
    fn caseResult(&self) -> Option<Rc<CaseResultContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
}

impl<'input> CaseExprContextAttrs<'input> for CaseExprContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn caseExpr(&mut self) -> Result<Rc<CaseExprContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = CaseExprContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog.base.enter_rule(_localctx.clone(), 58, RULE_caseExpr);
        let mut _localctx: Rc<CaseExprContextAll> = _localctx;
        let mut _la: isize = -1;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                recog.base.set_state(387);
                recog.base.match_token(CASE, &mut recog.err_handler)?;

                recog.base.set_state(389);
                recog.err_handler.sync(&mut recog.base)?;
                _la = recog.base.input.la(1);
                loop {
                    {
                        {
                            /*InvokeRule whenClause*/
                            recog.base.set_state(388);
                            recog.whenClause()?;
                        }
                    }
                    recog.base.set_state(391);
                    recog.err_handler.sync(&mut recog.base)?;
                    _la = recog.base.input.la(1);
                    if !(_la == WHEN) {
                        break;
                    }
                }
                recog.base.set_state(395);
                recog.err_handler.sync(&mut recog.base)?;
                _la = recog.base.input.la(1);
                if _la == ELSE {
                    {
                        recog.base.set_state(393);
                        recog.base.match_token(ELSE, &mut recog.err_handler)?;

                        /*InvokeRule caseResult*/
                        recog.base.set_state(394);
                        recog.caseResult()?;
                    }
                }

                recog.base.set_state(397);
                recog.base.match_token(END, &mut recog.err_handler)?;
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- whenClause ----------------
pub type WhenClauseContextAll<'input> = WhenClauseContext<'input>;

pub type WhenClauseContext<'input> = BaseParserRuleContext<'input, WhenClauseContextExt<'input>>;

#[derive(Clone)]
pub struct WhenClauseContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for WhenClauseContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for WhenClauseContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_whenClause(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_whenClause(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for WhenClauseContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_whenClause(self);
    }
}

impl<'input> CustomRuleContext<'input> for WhenClauseContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_whenClause
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_whenClause }
}
antlr_rust::tid! {WhenClauseContextExt<'a>}

impl<'input> WhenClauseContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<WhenClauseContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            WhenClauseContextExt { ph: PhantomData },
        ))
    }
}

pub trait WhenClauseContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<WhenClauseContextExt<'input>>
{
    /// Retrieves first TerminalNode corresponding to token WHEN
    /// Returns `None` if there is no child corresponding to token WHEN
    fn WHEN(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(WHEN, 0)
    }
    fn comparison(&self) -> Option<Rc<ComparisonContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    /// Retrieves first TerminalNode corresponding to token THEN
    /// Returns `None` if there is no child corresponding to token THEN
    fn THEN(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(THEN, 0)
    }
    fn caseResult(&self) -> Option<Rc<CaseResultContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
}

impl<'input> WhenClauseContextAttrs<'input> for WhenClauseContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn whenClause(&mut self) -> Result<Rc<WhenClauseContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = WhenClauseContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog
            .base
            .enter_rule(_localctx.clone(), 60, RULE_whenClause);
        let mut _localctx: Rc<WhenClauseContextAll> = _localctx;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                recog.base.set_state(399);
                recog.base.match_token(WHEN, &mut recog.err_handler)?;

                /*InvokeRule comparison*/
                recog.base.set_state(400);
                recog.comparison()?;

                recog.base.set_state(401);
                recog.base.match_token(THEN, &mut recog.err_handler)?;

                /*InvokeRule caseResult*/
                recog.base.set_state(402);
                recog.caseResult()?;
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- caseResult ----------------
pub type CaseResultContextAll<'input> = CaseResultContext<'input>;

pub type CaseResultContext<'input> = BaseParserRuleContext<'input, CaseResultContextExt<'input>>;

#[derive(Clone)]
pub struct CaseResultContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for CaseResultContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for CaseResultContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_caseResult(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_caseResult(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for CaseResultContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_caseResult(self);
    }
}

impl<'input> CustomRuleContext<'input> for CaseResultContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_caseResult
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_caseResult }
}
antlr_rust::tid! {CaseResultContextExt<'a>}

impl<'input> CaseResultContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<CaseResultContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            CaseResultContextExt { ph: PhantomData },
        ))
    }
}

pub trait CaseResultContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<CaseResultContextExt<'input>>
{
    fn arithmeticExpr(&self) -> Option<Rc<ArithmeticExprContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn comparison(&self) -> Option<Rc<ComparisonContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    fn qualifiedIdent(&self) -> Option<Rc<QualifiedIdentContextAll<'input>>>
    where
        Self: Sized,
    {
        self.child_of_type(0)
    }
    /// Retrieves first TerminalNode corresponding to token IDENT
    /// Returns `None` if there is no child corresponding to token IDENT
    fn IDENT(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(IDENT, 0)
    }
    /// Retrieves first TerminalNode corresponding to token INT
    /// Returns `None` if there is no child corresponding to token INT
    fn INT(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(INT, 0)
    }
    /// Retrieves first TerminalNode corresponding to token DECIMAL
    /// Returns `None` if there is no child corresponding to token DECIMAL
    fn DECIMAL(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(DECIMAL, 0)
    }
    /// Retrieves first TerminalNode corresponding to token STRING
    /// Returns `None` if there is no child corresponding to token STRING
    fn STRING(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(STRING, 0)
    }
    /// Retrieves first TerminalNode corresponding to token PARAMETER
    /// Returns `None` if there is no child corresponding to token PARAMETER
    fn PARAMETER(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(PARAMETER, 0)
    }
}

impl<'input> CaseResultContextAttrs<'input> for CaseResultContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn caseResult(&mut self) -> Result<Rc<CaseResultContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = CaseResultContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog
            .base
            .enter_rule(_localctx.clone(), 62, RULE_caseResult);
        let mut _localctx: Rc<CaseResultContextAll> = _localctx;
        let result: Result<(), ANTLRError> = (|| {
            recog.base.set_state(412);
            recog.err_handler.sync(&mut recog.base)?;
            match recog.interpreter.adaptive_predict(41, &mut recog.base)? {
                1 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 1);
                    recog.base.enter_outer_alt(None, 1);
                    {
                        /*InvokeRule arithmeticExpr*/
                        recog.base.set_state(404);
                        recog.arithmeticExpr()?;
                    }
                }
                2 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 2);
                    recog.base.enter_outer_alt(None, 2);
                    {
                        /*InvokeRule comparison*/
                        recog.base.set_state(405);
                        recog.comparison()?;
                    }
                }
                3 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 3);
                    recog.base.enter_outer_alt(None, 3);
                    {
                        /*InvokeRule qualifiedIdent*/
                        recog.base.set_state(406);
                        recog.qualifiedIdent()?;
                    }
                }
                4 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 4);
                    recog.base.enter_outer_alt(None, 4);
                    {
                        recog.base.set_state(407);
                        recog.base.match_token(IDENT, &mut recog.err_handler)?;
                    }
                }
                5 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 5);
                    recog.base.enter_outer_alt(None, 5);
                    {
                        recog.base.set_state(408);
                        recog.base.match_token(INT, &mut recog.err_handler)?;
                    }
                }
                6 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 6);
                    recog.base.enter_outer_alt(None, 6);
                    {
                        recog.base.set_state(409);
                        recog.base.match_token(DECIMAL, &mut recog.err_handler)?;
                    }
                }
                7 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 7);
                    recog.base.enter_outer_alt(None, 7);
                    {
                        recog.base.set_state(410);
                        recog.base.match_token(STRING, &mut recog.err_handler)?;
                    }
                }
                8 => {
                    //recog.base.enter_outer_alt(_localctx.clone(), 8);
                    recog.base.enter_outer_alt(None, 8);
                    {
                        recog.base.set_state(411);
                        recog.base.match_token(PARAMETER, &mut recog.err_handler)?;
                    }
                }

                _ => {}
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- orderDirection ----------------
pub type OrderDirectionContextAll<'input> = OrderDirectionContext<'input>;

pub type OrderDirectionContext<'input> =
    BaseParserRuleContext<'input, OrderDirectionContextExt<'input>>;

#[derive(Clone)]
pub struct OrderDirectionContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for OrderDirectionContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for OrderDirectionContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_orderDirection(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_orderDirection(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for OrderDirectionContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_orderDirection(self);
    }
}

impl<'input> CustomRuleContext<'input> for OrderDirectionContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_orderDirection
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_orderDirection }
}
antlr_rust::tid! {OrderDirectionContextExt<'a>}

impl<'input> OrderDirectionContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<OrderDirectionContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            OrderDirectionContextExt { ph: PhantomData },
        ))
    }
}

pub trait OrderDirectionContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<OrderDirectionContextExt<'input>>
{
    /// Retrieves first TerminalNode corresponding to token ASC
    /// Returns `None` if there is no child corresponding to token ASC
    fn ASC(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(ASC, 0)
    }
    /// Retrieves first TerminalNode corresponding to token DESC
    /// Returns `None` if there is no child corresponding to token DESC
    fn DESC(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(DESC, 0)
    }
}

impl<'input> OrderDirectionContextAttrs<'input> for OrderDirectionContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn orderDirection(&mut self) -> Result<Rc<OrderDirectionContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx =
            OrderDirectionContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog
            .base
            .enter_rule(_localctx.clone(), 64, RULE_orderDirection);
        let mut _localctx: Rc<OrderDirectionContextAll> = _localctx;
        let mut _la: isize = -1;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                recog.base.set_state(414);
                _la = recog.base.input.la(1);
                if { !(_la == ASC || _la == DESC) } {
                    recog.err_handler.recover_inline(&mut recog.base)?;
                } else {
                    if recog.base.input.la(1) == TOKEN_EOF {
                        recog.base.matched_eof = true
                    };
                    recog.err_handler.report_match(&mut recog.base);
                    recog.base.consume(&mut recog.err_handler);
                }
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}
//------------------- comparisonOp ----------------
pub type ComparisonOpContextAll<'input> = ComparisonOpContext<'input>;

pub type ComparisonOpContext<'input> =
    BaseParserRuleContext<'input, ComparisonOpContextExt<'input>>;

#[derive(Clone)]
pub struct ComparisonOpContextExt<'input> {
    ph: PhantomData<&'input str>,
}

impl<'input> LqlParserContext<'input> for ComparisonOpContext<'input> {}

impl<'input, 'a> Listenable<dyn LqlListener<'input> + 'a> for ComparisonOpContext<'input> {
    fn enter(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.enter_every_rule(self);
        listener.enter_comparisonOp(self);
    }
    fn exit(&self, listener: &mut (dyn LqlListener<'input> + 'a)) {
        listener.exit_comparisonOp(self);
        listener.exit_every_rule(self);
    }
}

impl<'input, 'a> Visitable<dyn LqlVisitor<'input> + 'a> for ComparisonOpContext<'input> {
    fn accept(&self, visitor: &mut (dyn LqlVisitor<'input> + 'a)) {
        visitor.visit_comparisonOp(self);
    }
}

impl<'input> CustomRuleContext<'input> for ComparisonOpContextExt<'input> {
    type TF = LocalTokenFactory<'input>;
    type Ctx = LqlParserContextType;
    fn get_rule_index(&self) -> usize {
        RULE_comparisonOp
    }
    //fn type_rule_index() -> usize where Self: Sized { RULE_comparisonOp }
}
antlr_rust::tid! {ComparisonOpContextExt<'a>}

impl<'input> ComparisonOpContextExt<'input> {
    fn new(
        parent: Option<Rc<dyn LqlParserContext<'input> + 'input>>,
        invoking_state: isize,
    ) -> Rc<ComparisonOpContextAll<'input>> {
        Rc::new(BaseParserRuleContext::new_parser_ctx(
            parent,
            invoking_state,
            ComparisonOpContextExt { ph: PhantomData },
        ))
    }
}

pub trait ComparisonOpContextAttrs<'input>:
    LqlParserContext<'input> + BorrowMut<ComparisonOpContextExt<'input>>
{
    /// Retrieves first TerminalNode corresponding to token LIKE
    /// Returns `None` if there is no child corresponding to token LIKE
    fn LIKE(&self) -> Option<Rc<TerminalNode<'input, LqlParserContextType>>>
    where
        Self: Sized,
    {
        self.get_token(LIKE, 0)
    }
}

impl<'input> ComparisonOpContextAttrs<'input> for ComparisonOpContext<'input> {}

impl<'input, I, H> LqlParser<'input, I, H>
where
    I: TokenStream<'input, TF = LocalTokenFactory<'input>> + TidAble<'input>,
    H: ErrorStrategy<'input, BaseParserType<'input, I>>,
{
    pub fn comparisonOp(&mut self) -> Result<Rc<ComparisonOpContextAll<'input>>, ANTLRError> {
        let mut recog = self;
        let _parentctx = recog.ctx.take();
        let mut _localctx = ComparisonOpContextExt::new(_parentctx.clone(), recog.base.get_state());
        recog
            .base
            .enter_rule(_localctx.clone(), 66, RULE_comparisonOp);
        let mut _localctx: Rc<ComparisonOpContextAll> = _localctx;
        let mut _la: isize = -1;
        let result: Result<(), ANTLRError> = (|| {
            //recog.base.enter_outer_alt(_localctx.clone(), 1);
            recog.base.enter_outer_alt(None, 1);
            {
                recog.base.set_state(416);
                _la = recog.base.input.la(1);
                if {
                    !((((_la) & !0x3f) == 0
                        && ((1usize << _la)
                            & ((1usize << T__4)
                                | (1usize << T__14)
                                | (1usize << T__15)
                                | (1usize << T__16)
                                | (1usize << T__17)
                                | (1usize << T__18)
                                | (1usize << T__19)))
                            != 0)
                        || _la == LIKE)
                } {
                    recog.err_handler.recover_inline(&mut recog.base)?;
                } else {
                    if recog.base.input.la(1) == TOKEN_EOF {
                        recog.base.matched_eof = true
                    };
                    recog.err_handler.report_match(&mut recog.base);
                    recog.base.consume(&mut recog.err_handler);
                }
            }
            Ok(())
        })();
        match result {
            Ok(_) => {}
            Err(e @ ANTLRError::FallThrough(_)) => return Err(e),
            Err(ref re) => {
                //_localctx.exception = re;
                recog.err_handler.report_error(&mut recog.base, re);
                recog.err_handler.recover(&mut recog.base, re)?;
            }
        }
        recog.base.exit_rule();

        Ok(_localctx)
    }
}

lazy_static! {
    static ref _ATN: Arc<ATN> =
        Arc::new(ATNDeserializer::new(None).deserialize(_serializedATN.chars()));
    static ref _decision_to_DFA: Arc<Vec<antlr_rust::RwLock<DFA>>> = {
        let mut dfa = Vec::new();
        let size = _ATN.decision_to_state.len();
        for i in 0..size {
            dfa.push(DFA::new(_ATN.clone(), _ATN.get_decision_state(i), i as isize).into())
        }
        Arc::new(dfa)
    };
}

const _serializedATN: &'static str =
    "\x03\u{608b}\u{a72a}\u{8133}\u{b9ed}\u{417c}\u{3be7}\u{7786}\u{5964}\x03\
	\x3b\u{1a5}\x04\x02\x09\x02\x04\x03\x09\x03\x04\x04\x09\x04\x04\x05\x09\
	\x05\x04\x06\x09\x06\x04\x07\x09\x07\x04\x08\x09\x08\x04\x09\x09\x09\x04\
	\x0a\x09\x0a\x04\x0b\x09\x0b\x04\x0c\x09\x0c\x04\x0d\x09\x0d\x04\x0e\x09\
	\x0e\x04\x0f\x09\x0f\x04\x10\x09\x10\x04\x11\x09\x11\x04\x12\x09\x12\x04\
	\x13\x09\x13\x04\x14\x09\x14\x04\x15\x09\x15\x04\x16\x09\x16\x04\x17\x09\
	\x17\x04\x18\x09\x18\x04\x19\x09\x19\x04\x1a\x09\x1a\x04\x1b\x09\x1b\x04\
	\x1c\x09\x1c\x04\x1d\x09\x1d\x04\x1e\x09\x1e\x04\x1f\x09\x1f\x04\x20\x09\
	\x20\x04\x21\x09\x21\x04\x22\x09\x22\x04\x23\x09\x23\x03\x02\x07\x02\x48\
	\x0a\x02\x0c\x02\x0e\x02\x4b\x0b\x02\x03\x02\x03\x02\x03\x03\x03\x03\x03\
	\x03\x05\x03\x52\x0a\x03\x03\x04\x03\x04\x03\x04\x03\x04\x07\x04\x58\x0a\
	\x04\x0c\x04\x0e\x04\x5b\x0b\x04\x03\x04\x03\x04\x03\x05\x03\x05\x03\x05\
	\x03\x05\x03\x05\x03\x05\x03\x06\x03\x06\x03\x06\x03\x06\x03\x06\x03\x07\
	\x03\x07\x03\x07\x07\x07\x6d\x0a\x07\x0c\x07\x0e\x07\x70\x0b\x07\x03\x08\
	\x03\x08\x03\x08\x05\x08\x75\x0a\x08\x03\x08\x03\x08\x03\x08\x03\x08\x03\
	\x08\x03\x08\x03\x08\x03\x08\x03\x08\x05\x08\u{80}\x0a\x08\x03\x08\x03\x08\
	\x03\x08\x03\x08\x03\x08\x03\x08\x03\x08\x03\x08\x03\x08\x03\x08\x03\x08\
	\x03\x08\x03\x08\x03\x08\x05\x08\u{90}\x0a\x08\x03\x09\x05\x09\u{93}\x0a\
	\x09\x03\x09\x05\x09\u{96}\x0a\x09\x03\x0a\x03\x0a\x03\x0a\x03\x0a\x03\x0b\
	\x03\x0b\x03\x0b\x03\x0b\x03\x0c\x03\x0c\x03\x0c\x07\x0c\u{a3}\x0a\x0c\x0c\
	\x0c\x0e\x0c\u{a6}\x0b\x0c\x03\x0d\x03\x0d\x03\x0d\x03\x0d\x05\x0d\u{ac}\
	\x0a\x0d\x03\x0d\x05\x0d\u{af}\x0a\x0d\x03\x0e\x03\x0e\x03\x0e\x03\x0e\x03\
	\x0e\x07\x0e\u{b6}\x0a\x0e\x0c\x0e\x0e\x0e\u{b9}\x0b\x0e\x03\x0e\x03\x0e\
	\x03\x0e\x03\x0e\x03\x0f\x03\x0f\x03\x0f\x06\x0f\u{c2}\x0a\x0f\x0d\x0f\x0e\
	\x0f\u{c3}\x03\x10\x03\x10\x03\x10\x07\x10\u{c9}\x0a\x10\x0c\x10\x0e\x10\
	\u{cc}\x0b\x10\x03\x11\x03\x11\x03\x11\x03\x11\x03\x11\x03\x11\x03\x11\x03\
	\x11\x03\x11\x03\x11\x03\x11\x03\x11\x03\x11\x05\x11\u{db}\x0a\x11\x03\x12\
	\x03\x12\x03\x12\x03\x12\x05\x12\u{e1}\x0a\x12\x03\x12\x03\x12\x05\x12\u{e5}\
	\x0a\x12\x03\x13\x03\x13\x03\x13\x07\x13\u{ea}\x0a\x13\x0c\x13\x0e\x13\u{ed}\
	\x0b\x13\x03\x14\x03\x14\x03\x14\x07\x14\u{f2}\x0a\x14\x0c\x14\x0e\x14\u{f5}\
	\x0b\x14\x03\x15\x03\x15\x03\x15\x03\x15\x03\x15\x03\x15\x03\x15\x03\x15\
	\x03\x15\x03\x15\x03\x15\x03\x15\x05\x15\u{103}\x0a\x15\x03\x16\x03\x16\
	\x03\x16\x05\x16\u{108}\x0a\x16\x03\x16\x05\x16\u{10b}\x0a\x16\x03\x16\x03\
	\x16\x03\x16\x03\x16\x03\x16\x03\x16\x05\x16\u{113}\x0a\x16\x03\x17\x03\
	\x17\x03\x17\x03\x17\x05\x17\u{119}\x0a\x17\x03\x18\x03\x18\x03\x18\x07\
	\x18\u{11e}\x0a\x18\x0c\x18\x0e\x18\u{121}\x0b\x18\x03\x19\x03\x19\x03\x19\
	\x07\x19\u{126}\x0a\x19\x0c\x19\x0e\x19\u{129}\x0b\x19\x03\x1a\x03\x1a\x03\
	\x1a\x03\x1a\x03\x1a\x05\x1a\u{130}\x0a\x1a\x03\x1b\x03\x1b\x03\x1b\x03\
	\x1b\x03\x1b\x03\x1b\x03\x1b\x03\x1b\x03\x1b\x03\x1b\x03\x1b\x03\x1b\x05\
	\x1b\u{13e}\x0a\x1b\x03\x1b\x03\x1b\x03\x1b\x03\x1b\x03\x1b\x03\x1b\x03\
	\x1b\x03\x1b\x05\x1b\u{148}\x0a\x1b\x03\x1b\x03\x1b\x03\x1b\x03\x1b\x03\
	\x1b\x03\x1b\x03\x1b\x03\x1b\x05\x1b\u{152}\x0a\x1b\x03\x1b\x03\x1b\x05\
	\x1b\u{156}\x0a\x1b\x03\x1b\x03\x1b\x05\x1b\u{15a}\x0a\x1b\x03\x1b\x03\x1b\
	\x05\x1b\u{15e}\x0a\x1b\x03\x1b\x03\x1b\x03\x1b\x03\x1b\x03\x1b\x03\x1b\
	\x03\x1b\x05\x1b\u{167}\x0a\x1b\x03\x1c\x03\x1c\x03\x1c\x03\x1c\x03\x1c\
	\x03\x1d\x03\x1d\x03\x1d\x05\x1d\u{171}\x0a\x1d\x03\x1d\x03\x1d\x05\x1d\
	\u{175}\x0a\x1d\x03\x1d\x03\x1d\x03\x1e\x03\x1e\x03\x1e\x05\x1e\u{17c}\x0a\
	\x1e\x03\x1e\x03\x1e\x03\x1e\x03\x1e\x05\x1e\u{182}\x0a\x1e\x03\x1e\x03\
	\x1e\x03\x1f\x03\x1f\x06\x1f\u{188}\x0a\x1f\x0d\x1f\x0e\x1f\u{189}\x03\x1f\
	\x03\x1f\x05\x1f\u{18e}\x0a\x1f\x03\x1f\x03\x1f\x03\x20\x03\x20\x03\x20\
	\x03\x20\x03\x20\x03\x21\x03\x21\x03\x21\x03\x21\x03\x21\x03\x21\x03\x21\
	\x03\x21\x05\x21\u{19f}\x0a\x21\x03\x22\x03\x22\x03\x23\x03\x23\x03\x23\
	\x02\x02\x24\x02\x04\x06\x08\x0a\x0c\x0e\x10\x12\x14\x16\x18\x1a\x1c\x1e\
	\x20\x22\x24\x26\x28\x2a\x2c\x2e\x30\x32\x34\x36\x38\x3a\x3c\x3e\x40\x42\
	\x44\x02\x07\x03\x02\x0c\x0e\x04\x02\x0f\x10\x3b\x3b\x04\x02\x32\x32\x35\
	\x35\x03\x02\x17\x18\x05\x02\x07\x07\x11\x16\x33\x33\x02\u{1ea}\x02\x49\
	\x03\x02\x02\x02\x04\x51\x03\x02\x02\x02\x06\x53\x03\x02\x02\x02\x08\x5e\
	\x03\x02\x02\x02\x0a\x64\x03\x02\x02\x02\x0c\x69\x03\x02\x02\x02\x0e\u{8f}\
	\x03\x02\x02\x02\x10\u{92}\x03\x02\x02\x02\x12\u{97}\x03\x02\x02\x02\x14\
	\u{9b}\x03\x02\x02\x02\x16\u{9f}\x03\x02\x02\x02\x18\u{ab}\x03\x02\x02\x02\
	\x1a\u{b0}\x03\x02\x02\x02\x1c\u{be}\x03\x02\x02\x02\x1e\u{c5}\x03\x02\x02\
	\x02\x20\u{da}\x03\x02\x02\x02\x22\u{e0}\x03\x02\x02\x02\x24\u{e6}\x03\x02\
	\x02\x02\x26\u{ee}\x03\x02\x02\x02\x28\u{102}\x03\x02\x02\x02\x2a\u{104}\
	\x03\x02\x02\x02\x2c\u{114}\x03\x02\x02\x02\x2e\u{11a}\x03\x02\x02\x02\x30\
	\u{122}\x03\x02\x02\x02\x32\u{12f}\x03\x02\x02\x02\x34\u{166}\x03\x02\x02\
	\x02\x36\u{168}\x03\x02\x02\x02\x38\u{170}\x03\x02\x02\x02\x3a\u{17b}\x03\
	\x02\x02\x02\x3c\u{185}\x03\x02\x02\x02\x3e\u{191}\x03\x02\x02\x02\x40\u{19e}\
	\x03\x02\x02\x02\x42\u{1a0}\x03\x02\x02\x02\x44\u{1a2}\x03\x02\x02\x02\x46\
	\x48\x05\x04\x03\x02\x47\x46\x03\x02\x02\x02\x48\x4b\x03\x02\x02\x02\x49\
	\x47\x03\x02\x02\x02\x49\x4a\x03\x02\x02\x02\x4a\x4c\x03\x02\x02\x02\x4b\
	\x49\x03\x02\x02\x02\x4c\x4d\x07\x02\x02\x03\x4d\x03\x03\x02\x02\x02\x4e\
	\x52\x05\x06\x04\x02\x4f\x52\x05\x0a\x06\x02\x50\x52\x05\x0c\x07\x02\x51\
	\x4e\x03\x02\x02\x02\x51\x4f\x03\x02\x02\x02\x51\x50\x03\x02\x02\x02\x52\
	\x05\x03\x02\x02\x02\x53\x54\x07\x27\x02\x02\x54\x59\x05\x08\x05\x02\x55\
	\x56\x07\x03\x02\x02\x56\x58\x05\x08\x05\x02\x57\x55\x03\x02\x02\x02\x58\
	\x5b\x03\x02\x02\x02\x59\x57\x03\x02\x02\x02\x59\x5a\x03\x02\x02\x02\x5a\
	\x5c\x03\x02\x02\x02\x5b\x59\x03\x02\x02\x02\x5c\x5d\x05\x0c\x07\x02\x5d\
	\x07\x03\x02\x02\x02\x5e\x5f\x07\x35\x02\x02\x5f\x60\x07\x21\x02\x02\x60\
	\x61\x07\x04\x02\x02\x61\x62\x05\x0c\x07\x02\x62\x63\x07\x05\x02\x02\x63\
	\x09\x03\x02\x02\x02\x64\x65\x07\x06\x02\x02\x65\x66\x07\x35\x02\x02\x66\
	\x67\x07\x07\x02\x02\x67\x68\x05\x0c\x07\x02\x68\x0b\x03\x02\x02\x02\x69\
	\x6e\x05\x0e\x08\x02\x6a\x6b\x07\x08\x02\x02\x6b\x6d\x05\x0e\x08\x02\x6c\
	\x6a\x03\x02\x02\x02\x6d\x70\x03\x02\x02\x02\x6e\x6c\x03\x02\x02\x02\x6e\
	\x6f\x03\x02\x02\x02\x6f\x0d\x03\x02\x02\x02\x70\x6e\x03\x02\x02\x02\x71\
	\x72\x07\x35\x02\x02\x72\x74\x07\x04\x02\x02\x73\x75\x05\x1e\x10\x02\x74\
	\x73\x03\x02\x02\x02\x74\x75\x03\x02\x02\x02\x75\x76\x03\x02\x02\x02\x76\
	\x77\x07\x05\x02\x02\x77\x78\x07\x28\x02\x02\x78\x79\x07\x04\x02\x02\x79\
	\x7a\x05\x10\x09\x02\x7a\x7b\x07\x05\x02\x02\x7b\u{90}\x03\x02\x02\x02\x7c\
	\x7d\x07\x35\x02\x02\x7d\x7f\x07\x04\x02\x02\x7e\u{80}\x05\x1e\x10\x02\x7f\
	\x7e\x03\x02\x02\x02\x7f\u{80}\x03\x02\x02\x02\u{80}\u{81}\x03\x02\x02\x02\
	\u{81}\u{90}\x07\x05\x02\x02\u{82}\u{90}\x07\x35\x02\x02\u{83}\u{84}\x07\
	\x04\x02\x02\u{84}\u{85}\x05\x0c\x07\x02\u{85}\u{86}\x07\x05\x02\x02\u{86}\
	\u{90}\x03\x02\x02\x02\u{87}\u{90}\x05\x1c\x0f\x02\u{88}\u{90}\x05\x1a\x0e\
	\x02\u{89}\u{90}\x05\x3c\x1f\x02\u{8a}\u{90}\x07\x36\x02\x02\u{8b}\u{90}\
	\x07\x37\x02\x02\u{8c}\u{90}\x07\x3b\x02\x02\u{8d}\u{90}\x07\x38\x02\x02\
	\u{8e}\u{90}\x07\x34\x02\x02\u{8f}\x71\x03\x02\x02\x02\u{8f}\x7c\x03\x02\
	\x02\x02\u{8f}\u{82}\x03\x02\x02\x02\u{8f}\u{83}\x03\x02\x02\x02\u{8f}\u{87}\
	\x03\x02\x02\x02\u{8f}\u{88}\x03\x02\x02\x02\u{8f}\u{89}\x03\x02\x02\x02\
	\u{8f}\u{8a}\x03\x02\x02\x02\u{8f}\u{8b}\x03\x02\x02\x02\u{8f}\u{8c}\x03\
	\x02\x02\x02\u{8f}\u{8d}\x03\x02\x02\x02\u{8f}\u{8e}\x03\x02\x02\x02\u{90}\
	\x0f\x03\x02\x02\x02\u{91}\u{93}\x05\x12\x0a\x02\u{92}\u{91}\x03\x02\x02\
	\x02\u{92}\u{93}\x03\x02\x02\x02\u{93}\u{95}\x03\x02\x02\x02\u{94}\u{96}\
	\x05\x14\x0b\x02\u{95}\u{94}\x03\x02\x02\x02\u{95}\u{96}\x03\x02\x02\x02\
	\u{96}\x11\x03\x02\x02\x02\u{97}\u{98}\x07\x29\x02\x02\u{98}\u{99}\x07\x2b\
	\x02\x02\u{99}\u{9a}\x05\x1e\x10\x02\u{9a}\x13\x03\x02\x02\x02\u{9b}\u{9c}\
	\x07\x2a\x02\x02\u{9c}\u{9d}\x07\x2b\x02\x02\u{9d}\u{9e}\x05\x16\x0c\x02\
	\u{9e}\x15\x03\x02\x02\x02\u{9f}\u{a4}\x05\x18\x0d\x02\u{a0}\u{a1}\x07\x03\
	\x02\x02\u{a1}\u{a3}\x05\x18\x0d\x02\u{a2}\u{a0}\x03\x02\x02\x02\u{a3}\u{a6}\
	\x03\x02\x02\x02\u{a4}\u{a2}\x03\x02\x02\x02\u{a4}\u{a5}\x03\x02\x02\x02\
	\u{a5}\x17\x03\x02\x02\x02\u{a6}\u{a4}\x03\x02\x02\x02\u{a7}\u{ac}\x05\x24\
	\x13\x02\u{a8}\u{ac}\x05\x2a\x16\x02\u{a9}\u{ac}\x05\x1c\x0f\x02\u{aa}\u{ac}\
	\x07\x35\x02\x02\u{ab}\u{a7}\x03\x02\x02\x02\u{ab}\u{a8}\x03\x02\x02\x02\
	\u{ab}\u{a9}\x03\x02\x02\x02\u{ab}\u{aa}\x03\x02\x02\x02\u{ac}\u{ae}\x03\
	\x02\x02\x02\u{ad}\u{af}\x05\x42\x22\x02\u{ae}\u{ad}\x03\x02\x02\x02\u{ae}\
	\u{af}\x03\x02\x02\x02\u{af}\x19\x03\x02\x02\x02\u{b0}\u{b1}\x07\x09\x02\
	\x02\u{b1}\u{b2}\x07\x04\x02\x02\u{b2}\u{b7}\x07\x35\x02\x02\u{b3}\u{b4}\
	\x07\x03\x02\x02\u{b4}\u{b6}\x07\x35\x02\x02\u{b5}\u{b3}\x03\x02\x02\x02\
	\u{b6}\u{b9}\x03\x02\x02\x02\u{b7}\u{b5}\x03\x02\x02\x02\u{b7}\u{b8}\x03\
	\x02\x02\x02\u{b8}\u{ba}\x03\x02\x02\x02\u{b9}\u{b7}\x03\x02\x02\x02\u{ba}\
	\u{bb}\x07\x05\x02\x02\u{bb}\u{bc}\x07\x0a\x02\x02\u{bc}\u{bd}\x05\x2e\x18\
	\x02\u{bd}\x1b\x03\x02\x02\x02\u{be}\u{c1}\x07\x35\x02\x02\u{bf}\u{c0}\x07\
	\x0b\x02\x02\u{c0}\u{c2}\x07\x35\x02\x02\u{c1}\u{bf}\x03\x02\x02\x02\u{c2}\
	\u{c3}\x03\x02\x02\x02\u{c3}\u{c1}\x03\x02\x02\x02\u{c3}\u{c4}\x03\x02\x02\
	\x02\u{c4}\x1d\x03\x02\x02\x02\u{c5}\u{ca}\x05\x20\x11\x02\u{c6}\u{c7}\x07\
	\x03\x02\x02\u{c7}\u{c9}\x05\x20\x11\x02\u{c8}\u{c6}\x03\x02\x02\x02\u{c9}\
	\u{cc}\x03\x02\x02\x02\u{ca}\u{c8}\x03\x02\x02\x02\u{ca}\u{cb}\x03\x02\x02\
	\x02\u{cb}\x1f\x03\x02\x02\x02\u{cc}\u{ca}\x03\x02\x02\x02\u{cd}\u{db}\x05\
	\x22\x12\x02\u{ce}\u{db}\x05\x24\x13\x02\u{cf}\u{db}\x05\x2a\x16\x02\u{d0}\
	\u{db}\x05\x3c\x1f\x02\u{d1}\u{db}\x05\x0e\x08\x02\u{d2}\u{db}\x05\x2c\x17\
	\x02\u{d3}\u{db}\x05\x34\x1b\x02\u{d4}\u{db}\x05\x0c\x07\x02\u{d5}\u{db}\
	\x05\x1a\x0e\x02\u{d6}\u{d7}\x07\x04\x02\x02\u{d7}\u{d8}\x05\x0c\x07\x02\
	\u{d8}\u{d9}\x07\x05\x02\x02\u{d9}\u{db}\x03\x02\x02\x02\u{da}\u{cd}\x03\
	\x02\x02\x02\u{da}\u{ce}\x03\x02\x02\x02\u{da}\u{cf}\x03\x02\x02\x02\u{da}\
	\u{d0}\x03\x02\x02\x02\u{da}\u{d1}\x03\x02\x02\x02\u{da}\u{d2}\x03\x02\x02\
	\x02\u{da}\u{d3}\x03\x02\x02\x02\u{da}\u{d4}\x03\x02\x02\x02\u{da}\u{d5}\
	\x03\x02\x02\x02\u{da}\u{d6}\x03\x02\x02\x02\u{db}\x21\x03\x02\x02\x02\u{dc}\
	\u{e1}\x05\x24\x13\x02\u{dd}\u{e1}\x05\x2a\x16\x02\u{de}\u{e1}\x05\x1c\x0f\
	\x02\u{df}\u{e1}\x07\x35\x02\x02\u{e0}\u{dc}\x03\x02\x02\x02\u{e0}\u{dd}\
	\x03\x02\x02\x02\u{e0}\u{de}\x03\x02\x02\x02\u{e0}\u{df}\x03\x02\x02\x02\
	\u{e1}\u{e4}\x03\x02\x02\x02\u{e2}\u{e3}\x07\x21\x02\x02\u{e3}\u{e5}\x07\
	\x35\x02\x02\u{e4}\u{e2}\x03\x02\x02\x02\u{e4}\u{e5}\x03\x02\x02\x02\u{e5}\
	\x23\x03\x02\x02\x02\u{e6}\u{eb}\x05\x26\x14\x02\u{e7}\u{e8}\x09\x02\x02\
	\x02\u{e8}\u{ea}\x05\x26\x14\x02\u{e9}\u{e7}\x03\x02\x02\x02\u{ea}\u{ed}\
	\x03\x02\x02\x02\u{eb}\u{e9}\x03\x02\x02\x02\u{eb}\u{ec}\x03\x02\x02\x02\
	\u{ec}\x25\x03\x02\x02\x02\u{ed}\u{eb}\x03\x02\x02\x02\u{ee}\u{f3}\x05\x28\
	\x15\x02\u{ef}\u{f0}\x09\x03\x02\x02\u{f0}\u{f2}\x05\x28\x15\x02\u{f1}\u{ef}\
	\x03\x02\x02\x02\u{f2}\u{f5}\x03\x02\x02\x02\u{f3}\u{f1}\x03\x02\x02\x02\
	\u{f3}\u{f4}\x03\x02\x02\x02\u{f4}\x27\x03\x02\x02\x02\u{f5}\u{f3}\x03\x02\
	\x02\x02\u{f6}\u{103}\x05\x1c\x0f\x02\u{f7}\u{103}\x07\x35\x02\x02\u{f8}\
	\u{103}\x07\x36\x02\x02\u{f9}\u{103}\x07\x37\x02\x02\u{fa}\u{103}\x07\x38\
	\x02\x02\u{fb}\u{103}\x05\x2a\x16\x02\u{fc}\u{103}\x05\x3c\x1f\x02\u{fd}\
	\u{103}\x07\x34\x02\x02\u{fe}\u{ff}\x07\x04\x02\x02\u{ff}\u{100}\x05\x24\
	\x13\x02\u{100}\u{101}\x07\x05\x02\x02\u{101}\u{103}\x03\x02\x02\x02\u{102}\
	\u{f6}\x03\x02\x02\x02\u{102}\u{f7}\x03\x02\x02\x02\u{102}\u{f8}\x03\x02\
	\x02\x02\u{102}\u{f9}\x03\x02\x02\x02\u{102}\u{fa}\x03\x02\x02\x02\u{102}\
	\u{fb}\x03\x02\x02\x02\u{102}\u{fc}\x03\x02\x02\x02\u{102}\u{fd}\x03\x02\
	\x02\x02\u{102}\u{fe}\x03\x02\x02\x02\u{103}\x29\x03\x02\x02\x02\u{104}\
	\u{105}\x07\x35\x02\x02\u{105}\u{10a}\x07\x04\x02\x02\u{106}\u{108}\x07\
	\x1b\x02\x02\u{107}\u{106}\x03\x02\x02\x02\u{107}\u{108}\x03\x02\x02\x02\
	\u{108}\u{109}\x03\x02\x02\x02\u{109}\u{10b}\x05\x1e\x10\x02\u{10a}\u{107}\
	\x03\x02\x02\x02\u{10a}\u{10b}\x03\x02\x02\x02\u{10b}\u{10c}\x03\x02\x02\
	\x02\u{10c}\u{112}\x07\x05\x02\x02\u{10d}\u{10e}\x07\x28\x02\x02\u{10e}\
	\u{10f}\x07\x04\x02\x02\u{10f}\u{110}\x05\x10\x09\x02\u{110}\u{111}\x07\
	\x05\x02\x02\u{111}\u{113}\x03\x02\x02\x02\u{112}\u{10d}\x03\x02\x02\x02\
	\u{112}\u{113}\x03\x02\x02\x02\u{113}\x2b\x03\x02\x02\x02\u{114}\u{115}\
	\x09\x04\x02\x02\u{115}\u{118}\x07\x07\x02\x02\u{116}\u{119}\x05\x34\x1b\
	\x02\u{117}\u{119}\x05\x2e\x18\x02\u{118}\u{116}\x03\x02\x02\x02\u{118}\
	\u{117}\x03\x02\x02\x02\u{119}\x2d\x03\x02\x02\x02\u{11a}\u{11f}\x05\x30\
	\x19\x02\u{11b}\u{11c}\x07\x1a\x02\x02\u{11c}\u{11e}\x05\x30\x19\x02\u{11d}\
	\u{11b}\x03\x02\x02\x02\u{11e}\u{121}\x03\x02\x02\x02\u{11f}\u{11d}\x03\
	\x02\x02\x02\u{11f}\u{120}\x03\x02\x02\x02\u{120}\x2f\x03\x02\x02\x02\u{121}\
	\u{11f}\x03\x02\x02\x02\u{122}\u{127}\x05\x32\x1a\x02\u{123}\u{124}\x07\
	\x19\x02\x02\u{124}\u{126}\x05\x32\x1a\x02\u{125}\u{123}\x03\x02\x02\x02\
	\u{126}\u{129}\x03\x02\x02\x02\u{127}\u{125}\x03\x02\x02\x02\u{127}\u{128}\
	\x03\x02\x02\x02\u{128}\x31\x03\x02\x02\x02\u{129}\u{127}\x03\x02\x02\x02\
	\u{12a}\u{130}\x05\x34\x1b\x02\u{12b}\u{12c}\x07\x04\x02\x02\u{12c}\u{12d}\
	\x05\x2e\x18\x02\u{12d}\u{12e}\x07\x05\x02\x02\u{12e}\u{130}\x03\x02\x02\
	\x02\u{12f}\u{12a}\x03\x02\x02\x02\u{12f}\u{12b}\x03\x02\x02\x02\u{130}\
	\x33\x03\x02\x02\x02\u{131}\u{132}\x05\x24\x13\x02\u{132}\u{133}\x05\x44\
	\x23\x02\u{133}\u{134}\x05\x24\x13\x02\u{134}\u{167}\x03\x02\x02\x02\u{135}\
	\u{136}\x05\x1c\x0f\x02\u{136}\u{13d}\x05\x44\x23\x02\u{137}\u{13e}\x05\
	\x1c\x0f\x02\u{138}\u{13e}\x07\x38\x02\x02\u{139}\u{13e}\x07\x35\x02\x02\
	\u{13a}\u{13e}\x07\x36\x02\x02\u{13b}\u{13e}\x07\x37\x02\x02\u{13c}\u{13e}\
	\x07\x34\x02\x02\u{13d}\u{137}\x03\x02\x02\x02\u{13d}\u{138}\x03\x02\x02\
	\x02\u{13d}\u{139}\x03\x02\x02\x02\u{13d}\u{13a}\x03\x02\x02\x02\u{13d}\
	\u{13b}\x03\x02\x02\x02\u{13d}\u{13c}\x03\x02\x02\x02\u{13e}\u{167}\x03\
	\x02\x02\x02\u{13f}\u{140}\x07\x35\x02\x02\u{140}\u{147}\x05\x44\x23\x02\
	\u{141}\u{148}\x05\x1c\x0f\x02\u{142}\u{148}\x07\x38\x02\x02\u{143}\u{148}\
	\x07\x35\x02\x02\u{144}\u{148}\x07\x36\x02\x02\u{145}\u{148}\x07\x37\x02\
	\x02\u{146}\u{148}\x07\x34\x02\x02\u{147}\u{141}\x03\x02\x02\x02\u{147}\
	\u{142}\x03\x02\x02\x02\u{147}\u{143}\x03\x02\x02\x02\u{147}\u{144}\x03\
	\x02\x02\x02\u{147}\u{145}\x03\x02\x02\x02\u{147}\u{146}\x03\x02\x02\x02\
	\u{148}\u{167}\x03\x02\x02\x02\u{149}\u{14a}\x07\x34\x02\x02\u{14a}\u{151}\
	\x05\x44\x23\x02\u{14b}\u{152}\x05\x1c\x0f\x02\u{14c}\u{152}\x07\x38\x02\
	\x02\u{14d}\u{152}\x07\x35\x02\x02\u{14e}\u{152}\x07\x36\x02\x02\u{14f}\
	\u{152}\x07\x37\x02\x02\u{150}\u{152}\x07\x34\x02\x02\u{151}\u{14b}\x03\
	\x02\x02\x02\u{151}\u{14c}\x03\x02\x02\x02\u{151}\u{14d}\x03\x02\x02\x02\
	\u{151}\u{14e}\x03\x02\x02\x02\u{151}\u{14f}\x03\x02\x02\x02\u{151}\u{150}\
	\x03\x02\x02\x02\u{152}\u{167}\x03\x02\x02\x02\u{153}\u{155}\x05\x1c\x0f\
	\x02\u{154}\u{156}\x05\x42\x22\x02\u{155}\u{154}\x03\x02\x02\x02\u{155}\
	\u{156}\x03\x02\x02\x02\u{156}\u{167}\x03\x02\x02\x02\u{157}\u{159}\x07\
	\x35\x02\x02\u{158}\u{15a}\x05\x42\x22\x02\u{159}\u{158}\x03\x02\x02\x02\
	\u{159}\u{15a}\x03\x02\x02\x02\u{15a}\u{167}\x03\x02\x02\x02\u{15b}\u{15d}\
	\x07\x34\x02\x02\u{15c}\u{15e}\x05\x42\x22\x02\u{15d}\u{15c}\x03\x02\x02\
	\x02\u{15d}\u{15e}\x03\x02\x02\x02\u{15e}\u{167}\x03\x02\x02\x02\u{15f}\
	\u{167}\x07\x38\x02\x02\u{160}\u{167}\x07\x36\x02\x02\u{161}\u{167}\x07\
	\x37\x02\x02\u{162}\u{167}\x05\x0e\x08\x02\u{163}\u{167}\x05\x36\x1c\x02\
	\u{164}\u{167}\x05\x38\x1d\x02\u{165}\u{167}\x05\x3a\x1e\x02\u{166}\u{131}\
	\x03\x02\x02\x02\u{166}\u{135}\x03\x02\x02\x02\u{166}\u{13f}\x03\x02\x02\
	\x02\u{166}\u{149}\x03\x02\x02\x02\u{166}\u{153}\x03\x02\x02\x02\u{166}\
	\u{157}\x03\x02\x02\x02\u{166}\u{15b}\x03\x02\x02\x02\u{166}\u{15f}\x03\
	\x02\x02\x02\u{166}\u{160}\x03\x02\x02\x02\u{166}\u{161}\x03\x02\x02\x02\
	\u{166}\u{162}\x03\x02\x02\x02\u{166}\u{163}\x03\x02\x02\x02\u{166}\u{164}\
	\x03\x02\x02\x02\u{166}\u{165}\x03\x02\x02\x02\u{167}\x35\x03\x02\x02\x02\
	\u{168}\u{169}\x07\x1c\x02\x02\u{169}\u{16a}\x07\x04\x02\x02\u{16a}\u{16b}\
	\x05\x0c\x07\x02\u{16b}\u{16c}\x07\x05\x02\x02\u{16c}\x37\x03\x02\x02\x02\
	\u{16d}\u{171}\x05\x1c\x0f\x02\u{16e}\u{171}\x07\x35\x02\x02\u{16f}\u{171}\
	\x07\x34\x02\x02\u{170}\u{16d}\x03\x02\x02\x02\u{170}\u{16e}\x03\x02\x02\
	\x02\u{170}\u{16f}\x03\x02\x02\x02\u{171}\u{172}\x03\x02\x02\x02\u{172}\
	\u{174}\x07\x1e\x02\x02\u{173}\u{175}\x07\x1f\x02\x02\u{174}\u{173}\x03\
	\x02\x02\x02\u{174}\u{175}\x03\x02\x02\x02\u{175}\u{176}\x03\x02\x02\x02\
	\u{176}\u{177}\x07\x1d\x02\x02\u{177}\x39\x03\x02\x02\x02\u{178}\u{17c}\
	\x05\x1c\x0f\x02\u{179}\u{17c}\x07\x35\x02\x02\u{17a}\u{17c}\x07\x34\x02\
	\x02\u{17b}\u{178}\x03\x02\x02\x02\u{17b}\u{179}\x03\x02\x02\x02\u{17b}\
	\u{17a}\x03\x02\x02\x02\u{17c}\u{17d}\x03\x02\x02\x02\u{17d}\u{17e}\x07\
	\x20\x02\x02\u{17e}\u{181}\x07\x04\x02\x02\u{17f}\u{182}\x05\x0c\x07\x02\
	\u{180}\u{182}\x05\x1e\x10\x02\u{181}\u{17f}\x03\x02\x02\x02\u{181}\u{180}\
	\x03\x02\x02\x02\u{182}\u{183}\x03\x02\x02\x02\u{183}\u{184}\x07\x05\x02\
	\x02\u{184}\x3b\x03\x02\x02\x02\u{185}\u{187}\x07\x22\x02\x02\u{186}\u{188}\
	\x05\x3e\x20\x02\u{187}\u{186}\x03\x02\x02\x02\u{188}\u{189}\x03\x02\x02\
	\x02\u{189}\u{187}\x03\x02\x02\x02\u{189}\u{18a}\x03\x02\x02\x02\u{18a}\
	\u{18d}\x03\x02\x02\x02\u{18b}\u{18c}\x07\x25\x02\x02\u{18c}\u{18e}\x05\
	\x40\x21\x02\u{18d}\u{18b}\x03\x02\x02\x02\u{18d}\u{18e}\x03\x02\x02\x02\
	\u{18e}\u{18f}\x03\x02\x02\x02\u{18f}\u{190}\x07\x26\x02\x02\u{190}\x3d\
	\x03\x02\x02\x02\u{191}\u{192}\x07\x23\x02\x02\u{192}\u{193}\x05\x34\x1b\
	\x02\u{193}\u{194}\x07\x24\x02\x02\u{194}\u{195}\x05\x40\x21\x02\u{195}\
	\x3f\x03\x02\x02\x02\u{196}\u{19f}\x05\x24\x13\x02\u{197}\u{19f}\x05\x34\
	\x1b\x02\u{198}\u{19f}\x05\x1c\x0f\x02\u{199}\u{19f}\x07\x35\x02\x02\u{19a}\
	\u{19f}\x07\x36\x02\x02\u{19b}\u{19f}\x07\x37\x02\x02\u{19c}\u{19f}\x07\
	\x38\x02\x02\u{19d}\u{19f}\x07\x34\x02\x02\u{19e}\u{196}\x03\x02\x02\x02\
	\u{19e}\u{197}\x03\x02\x02\x02\u{19e}\u{198}\x03\x02\x02\x02\u{19e}\u{199}\
	\x03\x02\x02\x02\u{19e}\u{19a}\x03\x02\x02\x02\u{19e}\u{19b}\x03\x02\x02\
	\x02\u{19e}\u{19c}\x03\x02\x02\x02\u{19e}\u{19d}\x03\x02\x02\x02\u{19f}\
	\x41\x03\x02\x02\x02\u{1a0}\u{1a1}\x09\x05\x02\x02\u{1a1}\x43\x03\x02\x02\
	\x02\u{1a2}\u{1a3}\x09\x06\x02\x02\u{1a3}\x45\x03\x02\x02\x02\x2c\x49\x51\
	\x59\x6e\x74\x7f\u{8f}\u{92}\u{95}\u{a4}\u{ab}\u{ae}\u{b7}\u{c3}\u{ca}\u{da}\
	\u{e0}\u{e4}\u{eb}\u{f3}\u{102}\u{107}\u{10a}\u{112}\u{118}\u{11f}\u{127}\
	\u{12f}\u{13d}\u{147}\u{151}\u{155}\u{159}\u{15d}\u{166}\u{170}\u{174}\u{17b}\
	\u{181}\u{189}\u{18d}\u{19e}";
