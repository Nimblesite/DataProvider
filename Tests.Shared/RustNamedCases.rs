// Implements [LQL-RUST-NAMED-CASE-MATRICES].
// Shared by Rust test modules; scenario execution stays in their callers.
pub(crate) fn named_cases<'a, T>(cases: &'a [(&str, T)]) -> impl Iterator<Item = &'a T> {
    cases.iter().map(|(name, input)| {
        assert!(!name.is_empty(), "Scenario name must be present");
        input
    })
}
