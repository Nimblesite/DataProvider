use std::{collections::HashMap, sync::Arc, time::Instant};

/// Cached database schema — tables, columns, and types.
/// Thread-safe and designed for concurrent read access.
#[derive(Debug, Clone)]
pub struct SchemaCache {
    tables: Arc<HashMap<String, TableInfo>>,
    last_refreshed: Option<Instant>,
}

/// Schema information for a single database table.
#[derive(Debug, Clone)]
pub struct TableInfo {
    /// Table name as it appears in the database.
    pub name: String,
    /// Database schema (e.g., "public" for Postgres).
    pub schema: String,
    /// Columns in this table.
    pub columns: Vec<ColumnInfo>,
}

/// Schema information for a single column.
#[derive(Debug, Clone)]
pub struct ColumnInfo {
    /// Column name.
    pub name: String,
    /// SQL type as reported by the database (e.g., "uuid", "text", "integer").
    pub sql_type: String,
    /// Whether this column accepts NULL values.
    pub is_nullable: bool,
    /// Whether this column is (part of) the primary key.
    pub is_primary_key: bool,
}

impl Default for SchemaCache {
    fn default() -> Self {
        Self {
            tables: Arc::new(HashMap::new()),
            last_refreshed: None,
        }
    }
}

impl SchemaCache {
    /// Build a new schema cache from a list of tables.
    pub fn from_tables(tables: Vec<TableInfo>) -> Self {
        let map: HashMap<String, TableInfo> = tables
            .into_iter()
            .map(|t| (t.name.to_lowercase(), t))
            .collect();
        Self {
            tables: Arc::new(map),
            last_refreshed: Some(Instant::now()),
        }
    }

    /// Check whether the cache has any schema data.
    pub fn is_empty(&self) -> bool {
        self.tables.is_empty()
    }

    /// Number of cached tables.
    pub fn table_count(&self) -> usize {
        self.tables.len()
    }

    /// Get all table names (original case).
    pub fn table_names(&self) -> Vec<&str> {
        self.tables.values().map(|t| t.name.as_str()).collect()
    }

    /// Look up a table by name (case-insensitive).
    pub fn get_table(&self, name: &str) -> Option<&TableInfo> {
        self.tables.get(&name.to_lowercase())
    }

    /// Get columns for a table (case-insensitive lookup).
    pub fn get_columns(&self, table_name: &str) -> Vec<&ColumnInfo> {
        match self.tables.get(&table_name.to_lowercase()) {
            Some(table) => table.columns.iter().collect(),
            None => Vec::new(),
        }
    }

    /// Get the age of the cache, or None if never refreshed.
    pub fn age(&self) -> Option<std::time::Duration> {
        self.last_refreshed.map(|t| t.elapsed())
    }

    /// Whether the cache is stale (older than the given duration).
    pub fn is_stale(&self, max_age: std::time::Duration) -> bool {
        match self.last_refreshed {
            Some(t) => t.elapsed() > max_age,
            None => true,
        }
    }
}

impl TableInfo {
    /// Get a column by name (case-insensitive).
    pub fn get_column(&self, name: &str) -> Option<&ColumnInfo> {
        let lower = name.to_lowercase();
        self.columns.iter().find(|c| c.name.to_lowercase() == lower)
    }

    /// Get the primary key column(s).
    pub fn primary_key_columns(&self) -> Vec<&ColumnInfo> {
        self.columns.iter().filter(|c| c.is_primary_key).collect()
    }
}

impl ColumnInfo {
    /// Format a human-readable type description for hover.
    pub fn type_description(&self) -> String {
        let mut desc = self.sql_type.clone();
        if self.is_primary_key {
            desc.push_str(" (PK)");
        }
        if !self.is_nullable {
            desc.push_str(" NOT NULL");
        }
        desc
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::test_support::make_col;

    fn make_table(name: &str, schema: &str, cols: Vec<ColumnInfo>) -> TableInfo {
        TableInfo {
            name: name.to_string(),
            schema: schema.to_string(),
            columns: cols,
        }
    }

    fn sample_schema() -> SchemaCache {
        independent_expanded_schema_data_1()
    }

    #[test]
    fn test_default_is_empty() {
        crate::test_support::verify_cache_default_is_empty();
    }

    #[test]
    fn test_from_tables() {
        crate::test_support::verify_cache_from_tables(&sample_schema());
    }

    #[test]
    fn test_table_names() {
        let cache = sample_schema();
        let names = cache.table_names();
        assert_eq!(names.len(), 2);
        assert!(names.contains(&"users"));
        assert!(names.contains(&"orders"));
    }

    #[test]
    fn test_get_table_case_insensitive() {
        let cache = sample_schema();
        crate::test_support::verify_case_insensitive(&["users", "Users", "USERS"], |v| {
            cache.get_table(v).is_some()
        });
    }

    #[test]
    fn test_get_columns() {
        let cache = sample_schema();
        let cols = cache.get_columns("users");
        assert_eq!(cols.len(), 3);
        let names: Vec<&str> = cols.iter().map(|c| c.name.as_str()).collect();
        assert!(names.contains(&"id"));
        assert!(names.contains(&"name"));
        assert!(names.contains(&"email"));
    }

    #[test]
    fn test_get_columns_nonexistent_table() {
        crate::test_support::verify_cache_columns_empty(&sample_schema());
    }

    #[test]
    fn test_age_is_some_after_creation() {
        crate::test_support::verify_cache_age_some(&sample_schema());
    }

    #[test]
    fn test_is_stale_default_always_stale() {
        crate::test_support::verify_staleness(&SchemaCache::default(), 3600, true);
    }

    #[test]
    fn test_is_stale_freshly_created() {
        crate::test_support::verify_staleness(&sample_schema(), 3600, false);
    }

    #[test]
    fn test_table_get_column_case_insensitive() {
        let table = make_table("users", "public", vec![make_col("Id", "uuid", false, true)]);
        crate::test_support::verify_case_insensitive(&["id", "ID", "Id"], |v| {
            table.get_column(v).is_some()
        });
    }

    #[test]
    fn test_primary_key_columns() {
        let table = make_table(
            "users",
            "public",
            vec![
                make_col("id", "uuid", false, true),
                make_col("name", "text", false, false),
            ],
        );
        let pks = table.primary_key_columns();
        assert_eq!(pks.len(), 1);
        assert_eq!(pks[0].name, "id");
    }

    #[test]
    fn test_primary_key_columns_none() {
        let table = make_table(
            "log",
            "public",
            vec![make_col("message", "text", true, false)],
        );
        assert!(table.primary_key_columns().is_empty());
    }

    #[test]
    fn test_type_description_pk_not_null() {
        verify_expanded_test_type_description_pk_not_null(
            "id",
            "uuid",
            false,
            true,
            "uuid (PK) NOT NULL",
        );
    }

    #[test]
    fn test_type_description_nullable() {
        verify_expanded_test_type_description_pk_not_null("email", "text", true, false, "text");
    }

    #[test]
    fn test_type_description_not_null_no_pk() {
        verify_expanded_test_type_description_pk_not_null(
            "name",
            "text",
            false,
            false,
            "text NOT NULL",
        );
    }

    #[test]
    fn test_type_description_pk_nullable() {
        verify_expanded_test_type_description_pk_not_null("id", "uuid", true, true, "uuid (PK)");
    }

    #[test]
    fn test_clone_schema_cache() {
        let cache = sample_schema();
        let cloned = cache.clone();
        assert_eq!(cloned.table_count(), cache.table_count());
    }

    // Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
    fn verify_expanded_test_type_description_pk_not_null(
        input0: &str,
        input1: &str,
        input2: bool,
        input3: bool,
        input4: &str,
    ) {
        crate::test_support::verify_type_description(input0, input1, input2, input3, input4);
    }

    include!("schema_expanded_schema_data_0.fixtures.rs");
}
