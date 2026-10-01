using Microsoft.Data.SqlClient;
using Outcome;

namespace Nimblesite.DataProvider.SqlServer.SchemaInspection;

/// <summary>
/// SQL Server implementation of schema inspection
/// </summary>
internal sealed class SqlServerSchemaInspector : ISchemaInspector
{
    private readonly string _connectionString;

    // .NET type constants
    private const string DotNetInt32 = "Int32";
    private const string DotNetInt64 = "Int64";
    private const string DotNetInt16 = "Int16";
    private const string DotNetByte = "Byte";
    private const string DotNetDouble = "Double";
    private const string DotNetSingle = "Single";
    private const string DotNetString = "String";
    private const string DotNetBoolean = "Boolean";
    private const string DotNetDateTime = "DateTime";
    private const string DotNetDecimal = "Decimal";
    private const string DotNetGuid = "Guid";
    private const string DotNetByteArray = "Byte[]";

    // SQL Server type constants
    private const string SqlInt = "int";
    private const string SqlBigInt = "bigint";
    private const string SqlSmallInt = "smallint";
    private const string SqlTinyInt = "tinyint";
    private const string SqlFloat = "float";
    private const string SqlReal = "real";
    private const string SqlNVarCharMax = "nvarchar(max)";
    private const string SqlBit = "bit";
    private const string SqlDateTime = "datetime";
    private const string SqlDecimal = "decimal";
    private const string SqlUniqueIdentifier = "uniqueidentifier";
    private const string SqlVarBinaryMax = "varbinary(max)";
    private const string SqlNumeric = "numeric";
    private const string SqlMoney = "money";
    private const string SqlSmallMoney = "smallmoney";
    private const string SqlChar = "char";
    private const string SqlVarChar = "varchar";
    private const string SqlText = "text";
    private const string SqlNChar = "nchar";
    private const string SqlNVarChar = "nvarchar";
    private const string SqlNText = "ntext";
    private const string SqlDateTime2 = "datetime2";
    private const string SqlSmallDateTime = "smalldatetime";
    private const string SqlDate = "date";
    private const string SqlTime = "time";
    private const string SqlDateTimeOffset = "datetimeoffset";
    private const string SqlVarBinary = "varbinary";
    private const string SqlBinary = "binary";
    private const string SqlImage = "image";
    private const string SqlXml = "xml";
    private const string SqlSqlVariant = "sql_variant";

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerSchemaInspector"/> class
    /// </summary>
    /// <param name="connectionString">SQL Server connection string</param>
    public SqlServerSchemaInspector(string connectionString)
    {
        _connectionString = connectionString ?? string.Empty;
    }

    /// <summary>
    /// Gets table information including columns and their metadata
    /// </summary>
    /// <param name="schema">Table schema</param>
    /// <param name="tableName">Table name</param>
    /// <returns>Table information or null if not found</returns>
    public async Task<DatabaseTable?> GetTableAsync(string schema, string tableName)
    {
        if (string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(tableName))
            return null;

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var columns = await GetColumnsAsync(connection, schema, tableName)
                .ConfigureAwait(false);

            if (columns.Count == 0)
            {
                return null;
            }

            return new DatabaseTable
            {
                Schema = schema,
                Name = tableName,
                Columns = columns,
            };
        }
        catch (SqlException)
        {
            return null;
        }
    }

    /// <summary>
    /// Gets all tables in the database
    /// </summary>
    /// <returns>List of all tables</returns>
    public async Task<IReadOnlyList<DatabaseTable>> GetAllTablesAsync()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var tables = new List<DatabaseTable>();
            var tableNames = await GetTableNamesAsync(connection).ConfigureAwait(false);

            foreach (var (schema, name) in tableNames)
            {
                var columns = await GetColumnsAsync(connection, schema, name).ConfigureAwait(false);

                tables.Add(
                    new DatabaseTable
                    {
                        Schema = schema,
                        Name = name,
                        Columns = columns,
                    }
                );
            }

            return tables.AsReadOnly();
        }
        catch (SqlException)
        {
            return new List<DatabaseTable>().AsReadOnly();
        }
    }

    /// <summary>
    /// Gets metadata about the columns returned by a SQL query
    /// </summary>
    /// <param name="sqlQuery">The SQL query to analyze</param>
    /// <returns>Result containing metadata about the query result columns</returns>
    public async Task<
        Result<SqlQueryMetadata, Nimblesite.Sql.Model.SqlError>
    > GetSqlQueryMetadataAsync(string sqlQuery)
    {
        if (string.IsNullOrWhiteSpace(sqlQuery))
            return new Result<SqlQueryMetadata, Nimblesite.Sql.Model.SqlError>.Error<
                SqlQueryMetadata,
                Nimblesite.Sql.Model.SqlError
            >(new Nimblesite.Sql.Model.SqlError("SQL query cannot be null or empty"));

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            // Execute the query with WHERE 1=0 to get schema without data
            // Note: sqlQuery comes from compile-time source generation, not user input
            var schemaQuery = $"SELECT * FROM ({sqlQuery}) AS QueryResult WHERE 1=0";

#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
            using var command = new SqlCommand(schemaQuery, connection);
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities

            using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

            var columns = new List<DatabaseColumn>();
            var schemaTable = await reader.GetSchemaTableAsync().ConfigureAwait(false);

            if (schemaTable != null)
            {
                foreach (System.Data.DataRow row in schemaTable.Rows)
                {
                    var columnName = row["ColumnName"]?.ToString() ?? string.Empty;
                    var dataType = row["DataType"] as Type;
                    var isNullable = row["AllowDBNull"] as bool? ?? true;

                    // Map .NET type to SQL Server type string
                    var sqlType = dataType?.Name switch
                    {
                        DotNetInt32 => SqlInt,
                        DotNetInt64 => SqlBigInt,
                        DotNetInt16 => SqlSmallInt,
                        DotNetByte => SqlTinyInt,
                        DotNetDouble => SqlFloat,
                        DotNetSingle => SqlReal,
                        DotNetString => SqlNVarCharMax,
                        DotNetBoolean => SqlBit,
                        DotNetDateTime => SqlDateTime,
                        DotNetDecimal => SqlDecimal,
                        DotNetGuid => SqlUniqueIdentifier,
                        DotNetByteArray => SqlVarBinaryMax,
                        _ => SqlNVarCharMax,
                    };

                    var csharpType = SqlServerTypeToCSharpType(sqlType, isNullable);

                    columns.Add(
                        new DatabaseColumn
                        {
                            Name = columnName,
                            SqlType = sqlType,
                            CSharpType = csharpType,
                            IsNullable = isNullable,
                            IsPrimaryKey = false, // Query results don't have primary keys
                            IsIdentity = false,
                            IsComputed = false,
                            MaxLength = null,
                            Precision = null,
                            Scale = null,
                        }
                    );
                }
            }

            var metadata = new SqlQueryMetadata
            {
                Columns = columns.AsReadOnly(),
                SqlText = sqlQuery,
            };

            return new Result<SqlQueryMetadata, Nimblesite.Sql.Model.SqlError>.Ok<
                SqlQueryMetadata,
                Nimblesite.Sql.Model.SqlError
            >(metadata);
        }
        catch (SqlException ex)
        {
            return new Result<SqlQueryMetadata, Nimblesite.Sql.Model.SqlError>.Error<
                SqlQueryMetadata,
                Nimblesite.Sql.Model.SqlError
            >(new Nimblesite.Sql.Model.SqlError("SQL Server error during schema inspection", ex));
        }
        catch (Exception ex)
        {
            return new Result<SqlQueryMetadata, Nimblesite.Sql.Model.SqlError>.Error<
                SqlQueryMetadata,
                Nimblesite.Sql.Model.SqlError
            >(new Nimblesite.Sql.Model.SqlError("Error analyzing SQL query", ex));
        }
    }

    private static async Task<IReadOnlyList<(string Schema, string Name)>> GetTableNamesAsync(
        SqlConnection connection
    )
    {
        const string sql = """
            SELECT 
                TABLE_SCHEMA,
                TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_SCHEMA, TABLE_NAME
            """;

        using var command = new SqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

        var tables = new List<(string Schema, string Name)>();

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            tables.Add((reader.GetString(0), reader.GetString(1)));
        }

        return tables.AsReadOnly();
    }

    private static async Task<IReadOnlyList<DatabaseColumn>> GetColumnsAsync(
        SqlConnection connection,
        string schema,
        string tableName
    )
    {
        const string sql = """
            SELECT 
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE,
                c.CHARACTER_MAXIMUM_LENGTH,
                c.NUMERIC_PRECISION,
                c.NUMERIC_SCALE,
                c.COLUMN_DEFAULT,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PRIMARY_KEY,
                CASE WHEN COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') = 1 THEN 1 ELSE 0 END AS IS_IDENTITY,
                CASE WHEN COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsComputed') = 1 THEN 1 ELSE 0 END AS IS_COMPUTED
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT 
                    tc.TABLE_SCHEMA,
                    tc.TABLE_NAME,
                    ccu.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                INNER JOIN INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE ccu ON tc.CONSTRAINT_NAME = ccu.CONSTRAINT_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ) pk ON c.TABLE_SCHEMA = pk.TABLE_SCHEMA AND c.TABLE_NAME = pk.TABLE_NAME AND c.COLUMN_NAME = pk.COLUMN_NAME
            WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @tableName
            ORDER BY c.ORDINAL_POSITION
            """;

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@tableName", tableName);

        using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

        var columns = new List<DatabaseColumn>();

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var columnName = reader.GetString(0);
            var dataType = reader.GetString(1);
            var isNullable = reader.GetString(2).Equals("YES", StringComparison.OrdinalIgnoreCase);
            var maxLength = await reader.IsDBNullAsync(3).ConfigureAwait(false)
                ? null
                : (int?)reader.GetInt32(3);
            var precision = await reader.IsDBNullAsync(4).ConfigureAwait(false)
                ? null
                : (int?)reader.GetByte(4);
            var scale = await reader.IsDBNullAsync(5).ConfigureAwait(false)
                ? null
                : (int?)reader.GetByte(5);
            var isPrimaryKey = reader.GetInt32(7) == 1;
            var isIdentity = reader.GetInt32(8) == 1;
            var isComputed = reader.GetInt32(9) == 1;

            var csharpType = SqlServerTypeToCSharpType(dataType, isNullable);

            columns.Add(
                new DatabaseColumn
                {
                    Name = columnName,
                    SqlType = FormatSqlType(dataType, maxLength, precision, scale),
                    CSharpType = csharpType,
                    IsNullable = isNullable,
                    IsPrimaryKey = isPrimaryKey,
                    IsIdentity = isIdentity,
                    IsComputed = isComputed,
                    MaxLength = maxLength,
                    Precision = precision,
                    Scale = scale,
                }
            );
        }

        return columns.AsReadOnly();
    }

    private static string SqlServerTypeToCSharpType(string sqlType, bool isNullable)
    {
        var csharpType = sqlType.ToLowerInvariant() switch
        {
            SqlBit => "bool",
            SqlTinyInt => "byte",
            SqlSmallInt => "short",
            SqlInt => "int",
            SqlBigInt => "long",
            SqlDecimal or SqlNumeric => "decimal",
            SqlMoney or SqlSmallMoney => "decimal",
            SqlFloat => "double",
            SqlReal => "float",
            SqlChar or SqlVarChar or SqlText or SqlNChar or SqlNVarChar or SqlNText => "string",
            SqlDateTime or SqlDateTime2 or SqlSmallDateTime => "DateTime",
            SqlDate => "DateOnly",
            SqlTime => "TimeOnly",
            SqlDateTimeOffset => "DateTimeOffset",
            SqlUniqueIdentifier => "Guid",
            SqlVarBinary or SqlBinary or SqlImage => "byte[]",
            SqlXml => "string",
            SqlSqlVariant => "object",
            _ => "object",
        };

        // Add nullability for value types
        if (
            isNullable
            && csharpType != "string"
            && csharpType != "byte[]"
            && csharpType != "object"
        )
        {
            csharpType += "?";
        }

        return csharpType;
    }

    private static string FormatSqlType(
        string dataType,
        int? maxLength,
        int? precision,
        int? scale
    ) =>
        dataType.ToLowerInvariant() switch
        {
            SqlVarChar or SqlNVarChar or SqlChar or SqlNChar when maxLength.HasValue =>
                maxLength.Value == -1 ? $"{dataType}(MAX)" : $"{dataType}({maxLength})",
            SqlDecimal or SqlNumeric when precision.HasValue && scale.HasValue =>
                $"{dataType}({precision},{scale})",
            SqlFloat when precision.HasValue => $"{dataType}({precision})",
            _ => dataType,
        };
}
