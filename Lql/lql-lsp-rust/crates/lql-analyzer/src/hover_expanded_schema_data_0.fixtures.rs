// Implements [LQL-RUST-INDEPENDENT-EXPECTATIONS].
fn independent_expanded_schema_data_1() -> crate::schema::SchemaCache { crate::schema::SchemaCache::from_tables(vec![crate::schema::TableInfo {
            name: "users".to_string(),
            schema: "public".to_string(),
            columns: vec![
                make_col("id", "uuid", false, true),
                make_col("name", "text", false, false),
                make_col("email", "text", true, false),
            ],
        }]) }
