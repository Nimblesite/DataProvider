//! Public surface of the LQL Language Server.
//!
//! The binary entry point lives in `main.rs`; this library facade exists
//! so integration tests can call into `db` and `ai` modules directly
//! (binary targets cannot be imported from `tests/` files).

pub mod ai;
pub mod db;
