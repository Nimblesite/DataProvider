// Implements [LQL-RUST-INDEPENDENT-EXPECTATIONS].
fn independent_expanded_schema_data_1() -> crate::schema::SchemaCache { crate::schema::SchemaCache::from_tables(vec![
            make_table(
                "users",
                "public",
                vec![
                    make_col("id", "uuid", false, true),
                    make_col("name", "text", false, false),
                    make_col("email", "text", true, false),
                ],
            ),
            make_table(
                "orders",
                "public",
                vec![
                    make_col("id", "uuid", false, true),
                    make_col("user_id", "uuid", false, false),
                    make_col("total", "numeric", true, false),
                ],
            ),
        ]) }
