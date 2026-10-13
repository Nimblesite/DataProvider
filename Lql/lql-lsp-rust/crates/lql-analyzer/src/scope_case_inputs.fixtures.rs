// Implements [LQL-RUST-NAMED-CASE-MATRICES].
// Independent scenario names, inputs and expected values; no runners or assertions.
type ScopeCase<'a> = (&'a str, (&'a str, &'a str, &'a str, &'a str, &'a str));
const NAMED_CASES_1: &[ScopeCase<'_>] = &[];
const NAMED_CASES_2: &[(&str, (&str, &str, &str))] = &[];
const NAMED_CASES_3: &[(&str, (&str, &str, &str))] = &[];

// Implements [LQL-RUST-NAMED-CASE-MATRICES].
// Original `build_scope` table-detection scenarios: case name, source, table.
const NAMED_CASES_SCOPE_TABLE: &[(&str, (&str, &str))] = &[
    (
        "test_build_scope_table_reference",
        ("users |> select(users.id)", "users"),
    ),
    (
        "test_build_scope_table_in_join",
        ("users |> join(orders, on = users.id = orders.user_id)", "users"),
    ),
    (
        "test_build_scope_join_does_not_panic",
        (
            "users |> join(orders, on = users.id = orders.user_id) |> select(users.id)",
            "users",
        ),
    ),
    (
        "test_build_scope_nested_pipe_in_union_arg",
        ("users |> union(orders |> select(orders.id))", "users"),
    ),
];
