// Implements [LQL-RUST-INDEPENDENT-EXPECTATIONS].
fn independent_after_pipe_returns_pipeline_ops_data_3() -> [&'static str; 11] { [
                "select",
                "filter",
                "join",
                "group_by",
                "order_by",
                "limit",
                "offset",
                "having",
                "union",
                "insert",
                "left_join",
            ] }

// Implements [LQL-RUST-INDEPENDENT-EXPECTATIONS].
fn independent_aggregate_completions_returned_data_4() -> [&'static str; 8] { [
                "count",
                "sum",
                "avg",
                "max",
                "min",
                "row_number",
                "rank",
                "dense_rank",
            ] }

// Implements [LQL-RUST-INDEPENDENT-EXPECTATIONS].
fn independent_string_functions_returned_data_5() -> [&'static str; 9] { [
                "concat",
                "substring",
                "length",
                "trim",
                "upper",
                "lower",
                "round",
                "abs",
                "coalesce",
            ] }
