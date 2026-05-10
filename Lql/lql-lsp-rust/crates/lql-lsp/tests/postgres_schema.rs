//! Integration tests for `db::fetch_postgres_schema` using a real
//! Postgres container started via testcontainers-modules.
//!
//! Closes the coverage gap on `crates/lql-lsp/src/db.rs` that comes from
//! never exercising the actual Postgres query paths in unit tests. This
//! test starts an ephemeral Postgres, seeds it with a known schema, then
//! calls fetch_postgres_schema and asserts the cache is populated.

use testcontainers_modules::postgres::Postgres;
use testcontainers_modules::testcontainers::runners::AsyncRunner;
use tokio_postgres::NoTls;

#[tokio::test]
async fn fetch_postgres_schema_returns_seeded_tables() {
    // Start a fresh Postgres container.
    let container = Postgres::default()
        .start()
        .await
        .expect("failed to start postgres container");

    let host = container.get_host().await.expect("container has no host");
    let port = container
        .get_host_port_ipv4(5432)
        .await
        .expect("container exposes no port");

    // Seed a known schema using tokio-postgres directly.
    let conn_str =
        format!("host={host} port={port} user=postgres password=postgres dbname=postgres");
    let (client, connection) = tokio_postgres::connect(&conn_str, NoTls)
        .await
        .expect("seeder connect");
    let connection_handle = tokio::spawn(async move {
        let _ = connection.await;
    });

    client
        .batch_execute(
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
        )
        .await
        .expect("seed schema");

    drop(client);
    let _ = connection_handle.await;

    // Now exercise fetch_postgres_schema against the seeded DB.
    let schema = lql_lsp::db::fetch_postgres_schema(&conn_str)
        .await
        .expect("fetch_postgres_schema must succeed against live DB");

    assert!(
        schema.get_table("pg_test_users").is_some(),
        "expected pg_test_users in schema"
    );
    assert!(
        schema.get_table("pg_test_orders").is_some(),
        "expected pg_test_orders in schema"
    );

    let users = schema.get_table("pg_test_users").unwrap();
    let id_col = users
        .columns
        .iter()
        .find(|c| c.name == "id")
        .expect("id column missing");
    assert!(id_col.is_primary_key, "id must be marked PK");
    assert!(!id_col.is_nullable, "id must be NOT NULL");

    let email_col = users
        .columns
        .iter()
        .find(|c| c.name == "email")
        .expect("email column missing");
    assert!(email_col.is_nullable, "email must be nullable");
    assert!(!email_col.is_primary_key);

    // Container drops here, stopping the postgres instance.
    drop(container);
}

#[tokio::test]
async fn fetch_schema_dispatches_to_postgres_for_libpq_uri() {
    // Drives the dispatch arm of `fetch_schema` (db.rs line 132) — when
    // the connection string is NOT detected as SQLite, the call routes
    // to fetch_postgres_schema.
    let container = Postgres::default()
        .start()
        .await
        .expect("failed to start postgres container");

    let host = container.get_host().await.unwrap();
    let port = container.get_host_port_ipv4(5432).await.unwrap();
    let conn_str =
        format!("host={host} port={port} user=postgres password=postgres dbname=postgres");

    // Seed something so the schema is non-empty.
    let (client, connection) = tokio_postgres::connect(&conn_str, NoTls)
        .await
        .expect("seeder connect");
    let h = tokio::spawn(async move {
        let _ = connection.await;
    });
    client
        .batch_execute("CREATE TABLE dispatch_pg_test (id UUID PRIMARY KEY NOT NULL);")
        .await
        .expect("seed");
    drop(client);
    let _ = h.await;

    let schema = lql_lsp::db::fetch_schema(&conn_str)
        .await
        .expect("dispatch must succeed");
    assert!(schema.get_table("dispatch_pg_test").is_some());

    drop(container);
}
