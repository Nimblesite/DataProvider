// Implements [LQL-RUST-INDEPENDENT-EXPECTATIONS].
fn independent_schema_data_1() -> crate::schema::SchemaCache { crate::schema::SchemaCache::from_tables(vec![
            crate::schema::TableInfo {
                name: "users".to_string(),
                schema: "public".to_string(),
                columns: vec![
                    crate::schema::ColumnInfo {
                        name: "id".to_string(),
                        sql_type: "uuid".to_string(),
                        is_nullable: false,
                        is_primary_key: true,
                    },
                    crate::schema::ColumnInfo {
                        name: "name".to_string(),
                        sql_type: "text".to_string(),
                        is_nullable: false,
                        is_primary_key: false,
                    },
                    crate::schema::ColumnInfo {
                        name: "email".to_string(),
                        sql_type: "text".to_string(),
                        is_nullable: true,
                        is_primary_key: false,
                    },
                ],
            },
            crate::schema::TableInfo {
                name: "orders".to_string(),
                schema: "public".to_string(),
                columns: (0..8)
                    .map(|i| crate::schema::ColumnInfo {
                        name: format!("col{i}"),
                        sql_type: "integer".to_string(),
                        is_nullable: false,
                        is_primary_key: i == 0,
                    })
                    .collect(),
            },
        ]) }
