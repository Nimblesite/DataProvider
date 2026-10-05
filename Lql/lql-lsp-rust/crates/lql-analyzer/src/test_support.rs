use crate::schema::ColumnInfo;

// Implements [LQL-TEST-COLUMN-FIXTURE].
/// Builds a column fixture for schema-aware analyzer tests.
pub(crate) fn make_col(name: &str, sql_type: &str, nullable: bool, pk: bool) -> ColumnInfo {
    ColumnInfo {
        name: name.to_string(),
        sql_type: sql_type.to_string(),
        is_nullable: nullable,
        is_primary_key: pk,
    }
}
