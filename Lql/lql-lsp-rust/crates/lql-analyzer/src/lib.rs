pub mod completion;
pub mod diagnostics;
pub mod hover;
pub mod schema;
pub mod scope;
pub mod symbols;

pub use completion::{get_completions, CompletionContext, CompletionItem, CompletionKind};
pub use diagnostics::{analyze, Diagnostic, DiagnosticSeverity};
pub use hover::{get_hover, get_hover_with_schema, HoverInfo};
pub use schema::{ColumnInfo, SchemaCache, TableInfo};
pub use scope::{build_scope, ScopeMap};
pub use symbols::{extract_symbols, DocumentSymbol, SymbolKind};

#[cfg(test)]
mod test_support;

#[cfg(test)]
mod tests;
