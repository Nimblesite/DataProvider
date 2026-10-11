use crate::span::Span;

/// A parse error with location information.
#[derive(Debug, Clone, PartialEq)]
pub struct ParseError {
    pub message: String,
    pub span: Span,
    pub severity: Severity,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Severity {
    Error,
    Warning,
    Info,
}

impl ParseError {
    /// Builds a parse error with an explicit severity.
    fn with_severity(message: impl Into<String>, span: Span, severity: Severity) -> Self {
        Self {
            message: message.into(),
            span,
            severity,
        }
    }

    pub fn error(message: impl Into<String>, span: Span) -> Self {
        Self::with_severity(message, span, Severity::Error)
    }

    pub fn warning(message: impl Into<String>, span: Span) -> Self {
        Self::with_severity(message, span, Severity::Warning)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn error_constructor() {
        let err = ParseError::error("test error", Span::new(0, 5));
        assert_eq!(err.message, "test error");
        assert_eq!(err.span, Span::new(0, 5));
        assert_eq!(err.severity, Severity::Error);
    }

    #[test]
    fn warning_constructor() {
        let err = ParseError::warning("test warning", Span::new(10, 20));
        assert_eq!(err.message, "test warning");
        assert_eq!(err.span, Span::new(10, 20));
        assert_eq!(err.severity, Severity::Warning);
    }

    #[test]
    fn error_accepts_string() {
        let err = ParseError::error(String::from("owned string"), Span::empty(0));
        assert_eq!(err.message, "owned string");
    }

    #[test]
    fn error_equality() {
        let a = ParseError::error("msg", Span::new(0, 1));
        let b = ParseError::error("msg", Span::new(0, 1));
        assert_eq!(a, b);
    }

    #[test]
    fn error_inequality_message() {
        let a = ParseError::error("msg1", Span::new(0, 1));
        let b = ParseError::error("msg2", Span::new(0, 1));
        assert_ne!(a, b);
    }

    #[test]
    fn error_inequality_severity() {
        let a = ParseError::error("msg", Span::new(0, 1));
        let b = ParseError::warning("msg", Span::new(0, 1));
        assert_ne!(a, b);
    }

    #[test]
    fn error_clone() {
        let err = ParseError::error("test", Span::new(5, 10));
        let cloned = err.clone();
        assert_eq!(err, cloned);
    }

    #[test]
    fn severity_variants() {
        assert_ne!(Severity::Error, Severity::Warning);
        assert_ne!(Severity::Warning, Severity::Info);
        assert_ne!(Severity::Error, Severity::Info);
    }

    #[test]
    fn error_debug() {
        let err = ParseError::error("test", Span::new(0, 1));
        let debug = format!("{:?}", err);
        assert!(debug.contains("test"));
    }
}
