// Implements [LQL-RUST-INDEPENDENT-EXPECTATIONS].
fn independent_schema_data_3() -> crate::schema::SchemaCache { crate::schema::SchemaCache::from_tables(vec![
            crate::schema::TableInfo {
                name: "Users".to_string(),
                schema: "public".to_string(),
                columns: vec![
                    crate::schema::ColumnInfo {
                        name: "id".to_string(),
                        sql_type: "uuid".to_string(),
                        is_nullable: false,
                        is_primary_key: true,
                    },
                    crate::schema::ColumnInfo {
                        name: "Name".to_string(),
                        sql_type: "text".to_string(),
                        is_nullable: true,
                        is_primary_key: false,
                    },
                ],
            },
            crate::schema::TableInfo {
                name: "Orders".to_string(),
                schema: "sales".to_string(),
                columns: vec![crate::schema::ColumnInfo {
                    name: "total".to_string(),
                    sql_type: "numeric".to_string(),
                    is_nullable: false,
                    is_primary_key: false,
                }],
            },
        ]) }
