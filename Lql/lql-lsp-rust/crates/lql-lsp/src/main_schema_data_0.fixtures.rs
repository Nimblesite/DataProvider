// Implements [LQL-RUST-INDEPENDENT-EXPECTATIONS].
fn independent_schema_data_1() -> lql_analyzer::SchemaCache { lql_analyzer::SchemaCache::from_tables(vec![lql_analyzer::TableInfo {
            name: "users".to_string(),
            schema: "public".to_string(),
            columns: vec![
                lql_analyzer::ColumnInfo {
                    name: "id".to_string(),
                    sql_type: "uuid".to_string(),
                    is_nullable: false,
                    is_primary_key: true,
                },
                lql_analyzer::ColumnInfo {
                    name: "name".to_string(),
                    sql_type: "text".to_string(),
                    is_nullable: true,
                    is_primary_key: false,
                },
            ],
        }]) }

// Implements [LQL-RUST-INDEPENDENT-EXPECTATIONS].
fn independent_schema_data_2() -> lql_analyzer::SchemaCache { lql_analyzer::SchemaCache::from_tables(vec![lql_analyzer::TableInfo {
            name: "users".to_string(),
            schema: "public".to_string(),
            columns: vec![
                lql_analyzer::ColumnInfo {
                    name: "id".to_string(),
                    sql_type: "uuid".to_string(),
                    is_nullable: false,
                    is_primary_key: true,
                },
                lql_analyzer::ColumnInfo {
                    name: "name".to_string(),
                    sql_type: "text".to_string(),
                    is_nullable: true,
                    is_primary_key: false,
                },
            ],
        }]) }

// Implements [LQL-RUST-INDEPENDENT-EXPECTATIONS].
fn independent_schema_data_3() -> lql_analyzer::SchemaCache { lql_analyzer::SchemaCache::from_tables(vec![
            lql_analyzer::TableInfo {
                name: "users".to_string(),
                schema: "public".to_string(),
                columns: vec![lql_analyzer::ColumnInfo {
                    name: "id".to_string(),
                    sql_type: "uuid".to_string(),
                    is_nullable: false,
                    is_primary_key: true,
                }],
            },
            lql_analyzer::TableInfo {
                name: "orders".to_string(),
                schema: "public".to_string(),
                columns: vec![lql_analyzer::ColumnInfo {
                    name: "id".to_string(),
                    sql_type: "uuid".to_string(),
                    is_nullable: false,
                    is_primary_key: true,
                }],
            },
        ]) }

// Implements [LQL-RUST-INDEPENDENT-EXPECTATIONS].
fn independent_schema_data_4() -> lql_analyzer::SchemaCache { lql_analyzer::SchemaCache::from_tables(vec![lql_analyzer::TableInfo {
            name: "users".to_string(),
            schema: "public".to_string(),
            columns: vec![
                lql_analyzer::ColumnInfo {
                    name: "id".to_string(),
                    sql_type: "uuid".to_string(),
                    is_nullable: false,
                    is_primary_key: true,
                },
                lql_analyzer::ColumnInfo {
                    name: "name".to_string(),
                    sql_type: "text".to_string(),
                    is_nullable: true,
                    is_primary_key: false,
                },
            ],
        }]) }

// Implements [LQL-RUST-INDEPENDENT-EXPECTATIONS].


// Implements [LQL-RUST-INDEPENDENT-EXPECTATIONS].
fn independent_schema_data_6() -> lql_analyzer::SchemaCache { lql_analyzer::SchemaCache::from_tables(vec![lql_analyzer::TableInfo {
            name: "users".to_string(),
            schema: "public".to_string(),
            columns: vec![
                lql_analyzer::ColumnInfo {
                    name: "id".to_string(),
                    sql_type: "uuid".to_string(),
                    is_nullable: false,
                    is_primary_key: true,
                },
                lql_analyzer::ColumnInfo {
                    name: "name".to_string(),
                    sql_type: "text".to_string(),
                    is_nullable: true,
                    is_primary_key: false,
                },
            ],
        }]) }
