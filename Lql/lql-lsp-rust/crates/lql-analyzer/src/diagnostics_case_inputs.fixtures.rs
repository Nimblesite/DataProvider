// Implements [LQL-RUST-NAMED-CASE-MATRICES].
// Independent scenario names, inputs and expected values; no runners or assertions.
const NAMED_CASES_1: &[(&str, (&str,))] = &[];

// Implements [LQL-RUST-NAMED-CASE-MATRICES].
// Original pipe-spacing scenarios: case name, source, forbidden diagnostic match.
const NAMED_CASES_PIPE_SPACING: &[(&str, (&str, &str))] = &[
    (
        "test_pipe_with_tab_is_ok",
        ("users\t|>\tselect(users.id)", "Pipeline operator"),
    ),
    (
        "test_pipe_at_start_of_line",
        ("|> select(users.id)", "preceded by"),
    ),
    ("test_pipe_at_end_of_line", ("users |>", "followed by")),
    (
        "test_multiple_pipes_on_one_line",
        ("a |> filter(x) |> select(y)", "Pipeline operator"),
    ),
];
