namespace Nimblesite.DataProvider.Migration.Postgres;

// Implements [MIG-PG-SHARED-CATALOG-READER].
internal static class PostgresCatalogQuery
{
    internal static void Read(
        NpgsqlConnection connection,
        Action<NpgsqlCommand> configure,
        Action<NpgsqlDataReader> read
    )
    {
        using var command = connection.CreateCommand();
        configure(obj: command);
        using var reader = command.ExecuteReader();
        while (reader.Read())
            read(obj: reader);
    }
}
