//! Integration tests for `db::fetch_postgres_schema` using a real
//! Postgres container started via testcontainers-modules.
//!
//! Closes the coverage gap on `crates/lql-lsp/src/db.rs` that comes from
//! never exercising the actual Postgres query paths in unit tests. This
//! test starts an ephemeral Postgres, seeds it with a known schema, then
//! calls fetch_postgres_schema and asserts the cache is populated.

use testcontainers_modules::{
    postgres::Postgres,
    testcontainers::{runners::AsyncRunner, ContainerAsync},
};
use tokio_postgres::NoTls;

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Starts an ephemeral Postgres container and returns it with a libpq URI.
async fn start_postgres() -> (ContainerAsync<Postgres>, String) {
    let container = Postgres::default()
        .start()
        .await
        .expect("failed to start postgres container");
    let host = container.get_host().await.expect("container has no host");
    let port = container
        .get_host_port_ipv4(5432)
        .await
        .expect("container exposes no port");
    let conn_str =
        format!("host={host} port={port} user=postgres password=postgres dbname=postgres");
    (container, conn_str)
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Runs `ddl` against `conn_str` through a fresh tokio-postgres connection.
async fn seed_postgres(conn_str: &str, ddl: &str) {
    let (client, connection) = tokio_postgres::connect(conn_str, NoTls)
        .await
        .expect("seeder connect");
    let handle = tokio::spawn(async move {
        let _ = connection.await;
    });
    client.batch_execute(ddl).await.expect("seed schema");
    drop(client);
    let _ = handle.await;
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Asserts `table` has a column `name` with the given primary-key and
/// nullability flags.
fn assert_column_flags(
    table: &lql_analyzer::TableInfo,
    name: &str,
    is_primary_key: bool,
    is_nullable: bool,
) {
    let column = table
        .columns
        .iter()
        .find(|c| c.name == name)
        .expect("column missing");
    assert_eq!(column.is_primary_key, is_primary_key, "{name} PK mismatch");
    assert_eq!(
        column.is_nullable, is_nullable,
        "{name} nullability mismatch"
    );
}

// Implements [LQL-RUST-SHARED-TEST-WORKFLOWS].
/// Seeds `conn_str` with `ddl`, then fetches the schema — through the
/// dispatch path when `dispatch` is set, otherwise the Postgres path.
async fn seeded_schema(conn_str: &str, ddl: &str, dispatch: bool) -> lql_analyzer::SchemaCache {
    seed_postgres(conn_str, ddl).await;
    if dispatch {
        lql_lsp::db::fetch_schema(conn_str)
            .await
            .expect("dispatch must succeed")
    } else {
        lql_lsp::db::fetch_postgres_schema(conn_str)
            .await
            .expect("fetch_postgres_schema must succeed against live DB")
    }
}

#[tokio::test]
async fn fetch_postgres_schema_returns_seeded_tables() {
    let (container, conn_str) = start_postgres().await;

    // Seed a known schema using tokio-postgres directly.
    let schema = seeded_schema(
        &conn_str,
        "CREATE TABLE pg_test_users (
                id UUID PRIMARY KEY NOT NULL,
                name TEXT NOT NULL,
                email TEXT
            );
            CREATE TABLE pg_test_orders (
                id UUID PRIMARY KEY NOT NULL,
                user_id UUID NOT NULL,
                total NUMERIC(10,2) NOT NULL
            );",
        false,
    )
    .await;

    assert!(
        schema.get_table("pg_test_users").is_some(),
        "expected pg_test_users in schema"
    );
    assert!(
        schema.get_table("pg_test_orders").is_some(),
        "expected pg_test_orders in schema"
    );

    let users = schema.get_table("pg_test_users").unwrap();
    assert_column_flags(users, "id", true, false);
    assert_column_flags(users, "email", false, true);

    // Container drops here, stopping the postgres instance.
    drop(container);
}

#[tokio::test]
async fn fetch_schema_dispatches_to_postgres_for_libpq_uri() {
    // Drives the dispatch arm of `fetch_schema` (db.rs line 132) — when
    // the connection string is NOT detected as SQLite, the call routes
    // to fetch_postgres_schema.
    let (container, conn_str) = start_postgres().await;

    // Seed something so the schema is non-empty.
    let schema = seeded_schema(
        &conn_str,
        "CREATE TABLE dispatch_pg_test (id UUID PRIMARY KEY NOT NULL);",
        true,
    )
    .await;
    assert!(schema.get_table("dispatch_pg_test").is_some());

    drop(container);
}
