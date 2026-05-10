use lql_analyzer::{ColumnInfo, SchemaCache, TableInfo};
use std::collections::HashMap;
use std::time::Duration;

const CONNECT_TIMEOUT: Duration = Duration::from_secs(10);
const QUERY_TIMEOUT: Duration = Duration::from_secs(30);

/// Discover a connection string from environment variables.
/// Priority: LQL_CONNECTION_STRING > DATABASE_URL
pub fn discover_connection_string() -> Option<String> {
    std::env::var("LQL_CONNECTION_STRING")
        .ok()
        .filter(|s| !s.is_empty())
        .or_else(|| std::env::var("DATABASE_URL").ok().filter(|s| !s.is_empty()))
}

/// Convert Npgsql-style connection string to libpq key=value format.
/// "Host=localhost;Database=mydb;Username=user;Password=pass"
/// becomes "host=localhost dbname=mydb user=user password=pass"
pub fn normalize_connection_string(input: &str) -> String {
    if input.starts_with("postgres://") || input.starts_with("postgresql://") {
        return input.to_string();
    }
    if !input.contains(';') {
        return input.to_string();
    }

    let params: HashMap<String, String> = input
        .split(';')
        .filter_map(|part| {
            let part = part.trim();
            if part.is_empty() {
                return None;
            }
            part.split_once('=')
                .map(|(k, v)| (k.trim().to_lowercase(), v.trim().to_string()))
        })
        .collect();

    let mut parts = Vec::new();
    if let Some(h) = params.get("host").or(params.get("server")) {
        parts.push(format!("host={h}"));
    }
    if let Some(p) = params.get("port") {
        parts.push(format!("port={p}"));
    }
    if let Some(d) = params.get("database").or(params.get("initial catalog")) {
        parts.push(format!("dbname={d}"));
    }
    if let Some(u) = params
        .get("username")
        .or(params.get("user id"))
        .or(params.get("uid"))
    {
        parts.push(format!("user={u}"));
    }
    if let Some(p) = params.get("password").or(params.get("pwd")) {
        parts.push(format!("password={p}"));
    }

    parts.join(" ")
}

/// Detect whether a connection string points to a SQLite database.
fn is_sqlite(connection_string: &str) -> bool {
    connection_string.ends_with(".db")
        || connection_string.ends_with(".sqlite")
        || connection_string.ends_with(".sqlite3")
        || connection_string.starts_with("sqlite:")
        || connection_string.starts_with("file:")
}

/// Fetch schema from a SQLite database file.
pub fn fetch_sqlite_schema(db_path: &str) -> std::result::Result<SchemaCache, String> {
    let path = db_path
        .trim_start_matches("sqlite:")
        .trim_start_matches("file:");

    let conn = rusqlite::Connection::open(path).map_err(|e| format!("SQLite open failed: {e}"))?;

    let mut table_stmt = conn
        .prepare("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")
        .map_err(|e| format!("SQLite query failed: {e}"))?;

    let table_names: Vec<String> = table_stmt
        .query_map([], |row| row.get(0))
        .map_err(|e| format!("SQLite table list failed: {e}"))?
        .filter_map(|r| r.ok())
        .collect();

    let mut tables = Vec::new();
    for table_name in &table_names {
        let mut col_stmt = conn
            .prepare(&format!("PRAGMA table_info('{}')", table_name))
            .map_err(|e| format!("PRAGMA table_info failed for {table_name}: {e}"))?;

        let columns: Vec<ColumnInfo> = col_stmt
            .query_map([], |row| {
                let name: String = row.get(1)?;
                let sql_type: String = row.get(2)?;
                let notnull: i32 = row.get(3)?;
                let pk: i32 = row.get(5)?;
                Ok(ColumnInfo {
                    name,
                    sql_type,
                    is_nullable: notnull == 0,
                    is_primary_key: pk != 0,
                })
            })
            .map_err(|e| format!("PRAGMA failed for {table_name}: {e}"))?
            .filter_map(|r| r.ok())
            .collect();

        tables.push(TableInfo {
            name: table_name.clone(),
            schema: "main".to_string(),
            columns,
        });
    }

    Ok(SchemaCache::from_tables(tables))
}

/// Fetch database schema. Dispatches to SQLite or PostgreSQL based on the connection string.
pub async fn fetch_schema(connection_string: &str) -> std::result::Result<SchemaCache, String> {
    if is_sqlite(connection_string) {
        let path = connection_string.to_string();
        return tokio::task::spawn_blocking(move || fetch_sqlite_schema(&path))
            .await
            .map_err(|e| format!("SQLite task failed: {e}"))?;
    }
    fetch_postgres_schema(connection_string).await
}

/// Fetch full database schema from PostgreSQL via information_schema.
pub async fn fetch_postgres_schema(
    connection_string: &str,
) -> std::result::Result<SchemaCache, String> {
    let conn_str = normalize_connection_string(connection_string);

    let (client, connection) = tokio::time::timeout(
        CONNECT_TIMEOUT,
        tokio_postgres::connect(&conn_str, tokio_postgres::NoTls),
    )
    .await
    .map_err(|_| format!("DB connect timed out after {}s", CONNECT_TIMEOUT.as_secs()))?
    .map_err(|e| format!("DB connect failed: {e}"))?;

    tokio::spawn(async move {
        let _ = connection.await;
    });

    let col_rows = tokio::time::timeout(
        QUERY_TIMEOUT,
        client.query(
            "SELECT c.table_schema, c.table_name, c.column_name, c.data_type, \
                    c.is_nullable, \
                    CASE WHEN pk.column_name IS NOT NULL THEN 'YES' ELSE 'NO' END as is_pk \
             FROM information_schema.columns c \
             LEFT JOIN ( \
                 SELECT kcu.table_schema, kcu.table_name, kcu.column_name \
                 FROM information_schema.key_column_usage kcu \
                 JOIN information_schema.table_constraints tc \
                   ON kcu.constraint_name = tc.constraint_name \
                   AND kcu.table_schema = tc.table_schema \
                 WHERE tc.constraint_type = 'PRIMARY KEY' \
             ) pk ON c.table_schema = pk.table_schema \
                  AND c.table_name = pk.table_name \
                  AND c.column_name = pk.column_name \
             WHERE c.table_schema NOT IN ('pg_catalog', 'information_schema') \
             ORDER BY c.table_schema, c.table_name, c.ordinal_position",
            &[],
        ),
    )
    .await
    .map_err(|_| format!("Schema query timed out after {}s", QUERY_TIMEOUT.as_secs()))?
    .map_err(|e| format!("Failed to query columns: {e}"))?;

    let mut tables_map: HashMap<(String, String), Vec<ColumnInfo>> = HashMap::new();
    for row in &col_rows {
        let schema: String = row.get(0);
        let table: String = row.get(1);
        tables_map
            .entry((schema, table))
            .or_default()
            .push(ColumnInfo {
                name: row.get(2),
                sql_type: row.get(3),
                is_nullable: row.get::<_, String>(4) == "YES",
                is_primary_key: row.get::<_, String>(5) == "YES",
            });
    }

    let tables: Vec<TableInfo> = tables_map
        .into_iter()
        .map(|((schema, name), columns)| TableInfo {
            name,
            schema,
            columns,
        })
        .collect();

    Ok(SchemaCache::from_tables(tables))
}

#[cfg(test)]
mod tests {
    use super::*;

    // ── normalize_connection_string ────────────────────────────────────
    #[test]
    fn passthrough_postgres_uri() {
        let uri = "postgres://user:pass@localhost/mydb";
        assert_eq!(normalize_connection_string(uri), uri);
    }

    #[test]
    fn passthrough_postgresql_uri() {
        let uri = "postgresql://user:pass@localhost/mydb";
        assert_eq!(normalize_connection_string(uri), uri);
    }

    #[test]
    fn passthrough_libpq_format() {
        let s = "host=localhost dbname=mydb user=postgres";
        assert_eq!(normalize_connection_string(s), s);
    }

    #[test]
    fn convert_npgsql_basic() {
        let result =
            normalize_connection_string("Host=localhost;Database=mydb;Username=user;Password=pass");
        assert!(result.contains("host=localhost"));
        assert!(result.contains("dbname=mydb"));
        assert!(result.contains("user=user"));
        assert!(result.contains("password=pass"));
    }

    #[test]
    fn convert_npgsql_with_port() {
        let result =
            normalize_connection_string("Host=db;Port=5433;Database=test;Username=u;Password=p");
        assert!(result.contains("host=db"));
        assert!(result.contains("port=5433"));
        assert!(result.contains("dbname=test"));
    }

    #[test]
    fn convert_npgsql_alternative_keys() {
        let result =
            normalize_connection_string("Server=db;Initial Catalog=mydb;User Id=admin;Pwd=secret");
        assert!(result.contains("host=db"));
        assert!(result.contains("dbname=mydb"));
        assert!(result.contains("user=admin"));
        assert!(result.contains("password=secret"));
    }

    #[test]
    fn convert_npgsql_uid_key() {
        let result =
            normalize_connection_string("Host=localhost;Database=test;Uid=user;Password=pass");
        assert!(result.contains("user=user"));
    }

    #[test]
    fn trailing_semicolon() {
        let result =
            normalize_connection_string("Host=localhost;Database=test;Username=u;Password=p;");
        assert!(result.contains("host=localhost"));
        assert!(result.contains("dbname=test"));
    }

    #[test]
    fn whitespace_in_params() {
        let result = normalize_connection_string(
            "Host = localhost ; Database = mydb ; Username = u ; Password = p",
        );
        assert!(result.contains("host=localhost"));
        assert!(result.contains("dbname=mydb"));
    }

    #[test]
    fn empty_string() {
        assert_eq!(normalize_connection_string(""), "");
    }

    // ── discover_connection_string ─────────────────────────────────────
    // All env-var tests combined into one to avoid parallel test races
    // (env vars are process-global shared state).
    #[test]
    fn discover_connection_string_all_cases() {
        // 1) None when unset
        std::env::remove_var("LQL_CONNECTION_STRING");
        std::env::remove_var("DATABASE_URL");
        assert!(discover_connection_string().is_none());

        // 2) LQL_CONNECTION_STRING takes effect
        std::env::set_var("LQL_CONNECTION_STRING", "host=localhost dbname=test");
        std::env::remove_var("DATABASE_URL");
        assert_eq!(
            discover_connection_string(),
            Some("host=localhost dbname=test".to_string())
        );

        // 3) Falls back to DATABASE_URL
        std::env::remove_var("LQL_CONNECTION_STRING");
        std::env::set_var("DATABASE_URL", "postgres://u:p@h/d");
        assert_eq!(
            discover_connection_string(),
            Some("postgres://u:p@h/d".to_string())
        );

        // 4) LQL takes priority over DATABASE_URL
        std::env::set_var("LQL_CONNECTION_STRING", "host=primary");
        std::env::set_var("DATABASE_URL", "postgres://secondary");
        assert_eq!(
            discover_connection_string(),
            Some("host=primary".to_string())
        );

        // 5) Empty LQL_CONNECTION_STRING is skipped
        std::env::set_var("LQL_CONNECTION_STRING", "");
        std::env::set_var("DATABASE_URL", "postgres://fallback");
        assert_eq!(
            discover_connection_string(),
            Some("postgres://fallback".to_string())
        );

        // Cleanup
        std::env::remove_var("LQL_CONNECTION_STRING");
        std::env::remove_var("DATABASE_URL");
    }

    // ── is_sqlite ─────────────────────────────────────────────────────
    #[test]
    fn is_sqlite_detects_db_extension() {
        assert!(is_sqlite("/path/to/file.db"));
        assert!(is_sqlite("/path/to/file.sqlite"));
        assert!(is_sqlite("/path/to/file.sqlite3"));
        assert!(is_sqlite("sqlite:/path/to/file"));
        assert!(is_sqlite("file:/path/to/file.db"));
    }

    #[test]
    fn is_sqlite_rejects_postgres() {
        assert!(!is_sqlite("postgres://user:pass@localhost/mydb"));
        assert!(!is_sqlite("host=localhost dbname=mydb"));
    }

    // ── fetch_sqlite_schema ───────────────────────────────────────────
    #[test]
    fn fetch_sqlite_schema_reads_tables_and_columns() {
        let dir = std::env::temp_dir().join("lql_test_schema.db");
        let path = dir.to_str().unwrap();

        // Create a test database
        let conn = rusqlite::Connection::open(path).unwrap();
        conn.execute_batch(
            "CREATE TABLE IF NOT EXISTS users (
                id TEXT PRIMARY KEY NOT NULL,
                name TEXT NOT NULL,
                email TEXT
            );
            CREATE TABLE IF NOT EXISTS orders (
                id TEXT PRIMARY KEY NOT NULL,
                user_id TEXT NOT NULL,
                total REAL NOT NULL
            );",
        )
        .unwrap();
        drop(conn);

        let schema = fetch_sqlite_schema(path).unwrap();
        assert_eq!(schema.table_count(), 2);

        let users = schema.get_table("users").expect("users table missing");
        assert_eq!(users.columns.len(), 3);
        assert!(users
            .columns
            .iter()
            .any(|c| c.name == "id" && c.is_primary_key));
        assert!(users
            .columns
            .iter()
            .any(|c| c.name == "email" && c.is_nullable));
        assert!(users
            .columns
            .iter()
            .any(|c| c.name == "name" && !c.is_nullable));

        // Cleanup
        std::fs::remove_file(path).ok();
    }

    #[test]
    fn fetch_sqlite_schema_nonexistent_file() {
        let result = fetch_sqlite_schema("/tmp/lql_nonexistent_db_12345.db");
        // rusqlite creates the file if it doesn't exist, so this actually succeeds
        // with 0 tables — that's fine behavior
        assert!(result.is_ok());
        assert_eq!(result.unwrap().table_count(), 0);
        std::fs::remove_file("/tmp/lql_nonexistent_db_12345.db").ok();
    }

    // ── fetch_schema dispatch ─────────────────────────────────────────

    #[tokio::test]
    async fn fetch_schema_dispatches_to_sqlite_for_db_extension() {
        let path = std::env::temp_dir().join("lql_dispatch_test.db");
        let path_str = path.to_str().unwrap();

        let conn = rusqlite::Connection::open(path_str).unwrap();
        conn.execute_batch(
            "CREATE TABLE IF NOT EXISTS dispatch_users (id TEXT PRIMARY KEY NOT NULL);",
        )
        .unwrap();
        drop(conn);

        let schema = fetch_schema(path_str).await.unwrap();
        assert!(schema.get_table("dispatch_users").is_some());

        std::fs::remove_file(path_str).ok();
    }

    #[tokio::test]
    async fn fetch_schema_dispatches_to_sqlite_for_sqlite_prefix() {
        let path = std::env::temp_dir().join("lql_dispatch_prefix.db");
        let path_str = path.to_str().unwrap();

        let conn = rusqlite::Connection::open(path_str).unwrap();
        conn.execute_batch(
            "CREATE TABLE IF NOT EXISTS dispatch_orders (id TEXT PRIMARY KEY NOT NULL);",
        )
        .unwrap();
        drop(conn);

        let with_prefix = format!("sqlite:{path_str}");
        let schema = fetch_schema(&with_prefix).await.unwrap();
        assert!(schema.get_table("dispatch_orders").is_some());

        std::fs::remove_file(path_str).ok();
    }

    // ── fetch_postgres_schema connect failure ─────────────────────────

    #[tokio::test]
    async fn fetch_postgres_schema_connect_failure_returns_error() {
        // Port 1 is reserved (tcpmux) — the connect will be refused, exercising
        // the connect error branch (line 147) without waiting for the timeout.
        let result = fetch_postgres_schema(
            "host=127.0.0.1 port=1 dbname=test user=test password=test connect_timeout=1",
        )
        .await;
        assert!(result.is_err(), "connect to localhost:1 must fail");
        let err = result.unwrap_err();
        assert!(
            err.contains("DB connect"),
            "error must mention DB connect: {err}"
        );
    }

    #[tokio::test]
    async fn fetch_postgres_schema_via_npgsql_format_routes_normalize() {
        // Drives normalize_connection_string from inside fetch_postgres_schema
        // and exercises the connect-error branch with a different format.
        let result =
            fetch_postgres_schema("Host=127.0.0.1;Port=1;Database=x;Username=u;Password=p").await;
        assert!(result.is_err());
    }
}
