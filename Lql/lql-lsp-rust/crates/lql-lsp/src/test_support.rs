//! Shared test helpers for the `lql-lsp` crate.
//!
//! Algorithms shared by unit tests in [`crate::ai`] and [`crate::db`].

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts every string in `needles` occurs inside `haystack`.
pub(crate) fn assert_contains_all(haystack: &str, needles: &[&str]) {
    for needle in needles {
        assert!(
            haystack.contains(needle),
            "missing '{needle}' in '{haystack}'"
        );
    }
}
