using Migration;
using Migration.SQLite;

namespace ICD10AM.Api;

/// <summary>
/// Database initialization for ICD10AM.Api using Migration tool.
/// </summary>
internal static class DatabaseSetup
{
    /// <summary>
    /// Creates the database schema using Migration.
    /// </summary>
    public static void Initialize(SqliteConnection connection, ILogger logger)
    {
        try
        {
            // Check if tables already exist (e.g., in test scenarios)
            using (var checkCmd = connection.CreateCommand())
            {
                checkCmd.CommandText =
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='icd10am_chapter'";
                var count = Convert.ToInt64(
                    checkCmd.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture
                );
                if (count > 0)
                {
                    logger.Log(
                        LogLevel.Information,
                        "ICD-10-AM database schema already exists, skipping initialization"
                    );
                    return;
                }
            }

            var yamlPath = Path.Combine(AppContext.BaseDirectory, "icd10am-schema.yaml");
            var schema = SchemaYamlSerializer.FromYamlFile(yamlPath);

            foreach (var table in schema.Tables)
            {
                var ddl = SqliteDdlGenerator.Generate(new CreateTableOperation(table));
                using var cmd = connection.CreateCommand();
                cmd.CommandText = ddl;
                cmd.ExecuteNonQuery();
                logger.Log(LogLevel.Debug, "Created table {TableName}", table.Name);
            }

            logger.Log(LogLevel.Information, "Created ICD-10-AM database schema from YAML");
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, ex, "Failed to create ICD-10-AM database schema");
        }
    }
}
