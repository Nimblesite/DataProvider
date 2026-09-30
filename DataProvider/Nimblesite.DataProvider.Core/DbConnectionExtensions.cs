using System.Data;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Core;

/// <summary>
/// Static extension methods for IDbConnection following FP patterns.
/// All methods return Result types for explicit error handling.
/// </summary>
/// <example>
/// <code>
/// using var connection = new SqliteConnection("Data Source=mydb.db");
/// connection.Open();
///
/// // Execute a query with mapping
/// var result = connection.Query&lt;Customer&gt;(
///     sql: "SELECT Id, Name FROM Customers WHERE Active = 1",
///     mapper: reader => new Customer(
///         Id: reader.GetInt32(0),
///         Name: reader.GetString(1)
///     )
/// );
///
/// // Pattern match on the result
/// var customers = result switch
/// {
///     Result&lt;IReadOnlyList&lt;Customer&gt;, SqlError&gt;.Ok&lt;IReadOnlyList&lt;Customer&gt;, SqlError&gt; ok => ok.Value,
///     Result&lt;IReadOnlyList&lt;Customer&gt;, SqlError&gt;.Error&lt;IReadOnlyList&lt;Customer&gt;, SqlError&gt; err => throw new Exception(err.Value.Message),
///     _ => throw new InvalidOperationException()
/// };
/// </code>
/// </example>
public static class DbConnectionExtensions
{
    /// <summary>
    /// Execute a query and return results.
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="connection">The database connection</param>
    /// <param name="sql">The SQL query</param>
    /// <param name="parameters">Optional parameters</param>
    /// <param name="mapper">Function to map from IDataReader to T</param>
    /// <returns>Result with list of T or error</returns>
    /// <example>
    /// <code>
    /// var result = connection.Query&lt;Product&gt;(
    ///     sql: "SELECT * FROM Products WHERE Price > @minPrice",
    ///     parameters: [new SqliteParameter("@minPrice", 10.00)],
    ///     mapper: r => new Product(r.GetInt32(0), r.GetString(1), r.GetDecimal(2))
    /// );
    /// </code>
    /// </example>
    public static Result<IReadOnlyList<T>, SqlError> Query<T>(
        this IDbConnection connection,
        string sql,
        IEnumerable<IDataParameter>? parameters = null,
        Func<IDataReader, T>? mapper = null
    )
    {
        if (connection == null)
            return new Result<IReadOnlyList<T>, SqlError>.Error<IReadOnlyList<T>, SqlError>(
                SqlError.Create("Connection is null")
            );

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<IReadOnlyList<T>, SqlError>.Error<IReadOnlyList<T>, SqlError>(
                SqlError.Create("SQL is null or empty")
            );

        try
        {
            using var command = connection.CreateCommandWith(sql, parameters);

            var results = new List<T>();
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                if (mapper != null)
                {
                    results.Add(mapper(reader));
                }
            }

            return new Result<IReadOnlyList<T>, SqlError>.Ok<IReadOnlyList<T>, SqlError>(
                results.AsReadOnly()
            );
        }
        catch (Exception ex)
        {
            return new Result<IReadOnlyList<T>, SqlError>.Error<IReadOnlyList<T>, SqlError>(
                SqlError.FromException(ex)
            );
        }
    }

    /// <summary>
    /// Execute a non-query command (INSERT, UPDATE, DELETE).
    /// </summary>
    /// <param name="connection">The database connection</param>
    /// <param name="sql">The SQL command</param>
    /// <param name="parameters">Optional parameters</param>
    /// <returns>Result with rows affected or error</returns>
    /// <example>
    /// <code>
    /// var result = connection.Execute(
    ///     sql: "UPDATE Products SET Price = @price WHERE Id = @id",
    ///     parameters: [
    ///         new SqliteParameter("@price", 19.99),
    ///         new SqliteParameter("@id", 42)
    ///     ]
    /// );
    ///
    /// if (result is Result&lt;int, SqlError&gt;.Ok&lt;int, SqlError&gt; ok)
    ///     Console.WriteLine($"Updated {ok.Value} rows");
    /// </code>
    /// </example>
    public static Result<int, SqlError> Execute(
        this IDbConnection connection,
        string sql,
        IEnumerable<IDataParameter>? parameters = null
    )
    {
        if (connection == null)
            return new Result<int, SqlError>.Error<int, SqlError>(
                SqlError.Create("Connection is null")
            );

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<int, SqlError>.Error<int, SqlError>(
                SqlError.Create("SQL is null or empty")
            );

        try
        {
            using var command = connection.CreateCommandWith(sql, parameters);

            var rowsAffected = command.ExecuteNonQuery();
            return new Result<int, SqlError>.Ok<int, SqlError>(rowsAffected);
        }
        catch (Exception ex)
        {
            return new Result<int, SqlError>.Error<int, SqlError>(SqlError.FromException(ex));
        }
    }

    /// <summary>
    /// Execute a scalar command
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="connection">The database connection</param>
    /// <param name="sql">The SQL command</param>
    /// <param name="parameters">Optional parameters</param>
    /// <returns>Result with scalar value or error</returns>
    public static Result<T?, SqlError> Scalar<T>(
        this IDbConnection connection,
        string sql,
        IEnumerable<IDataParameter>? parameters = null
    )
    {
        if (connection == null)
            return new Result<T?, SqlError>.Error<T?, SqlError>(
                SqlError.Create("Connection is null")
            );

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<T?, SqlError>.Error<T?, SqlError>(
                SqlError.Create("SQL is null or empty")
            );

        try
        {
            using var command = connection.CreateCommandWith(sql, parameters);

            var result = command.ExecuteScalar();
            return new Result<T?, SqlError>.Ok<T?, SqlError>(result is T value ? value : default);
        }
        catch (Exception ex)
        {
            return new Result<T?, SqlError>.Error<T?, SqlError>(SqlError.FromException(ex));
        }
    }

    /// <summary>
    /// Execute a SelectStatement by generating platform-specific SQL and mapping results.
    /// </summary>
    /// <typeparam name="T">Result row type</typeparam>
    /// <param name="connection">The database connection</param>
    /// <param name="statement">The abstract SQL statement</param>
    /// <param name="sqlGenerator">Function that converts the statement to platform-specific SQL (returns Result)</param>
    /// <param name="parameters">Optional parameters to pass to the command</param>
    /// <param name="mapper">Mapper from IDataReader to T (required)</param>
    /// <returns>Result with list of T or SqlError on failure</returns>
    public static Result<IReadOnlyList<T>, SqlError> GetRecords<T>(
        this IDbConnection connection,
        SelectStatement statement,
        Func<SelectStatement, Result<string, SqlError>> sqlGenerator,
        Func<IDataReader, T> mapper,
        IEnumerable<IDataParameter>? parameters = null
    )
    {
        if (connection == null)
            return new Result<IReadOnlyList<T>, SqlError>.Error<IReadOnlyList<T>, SqlError>(
                SqlError.Create("Connection is null")
            );
        if (statement == null)
            return new Result<IReadOnlyList<T>, SqlError>.Error<IReadOnlyList<T>, SqlError>(
                SqlError.Create("SelectStatement is null")
            );
        if (sqlGenerator == null)
            return new Result<IReadOnlyList<T>, SqlError>.Error<IReadOnlyList<T>, SqlError>(
                SqlError.Create("sqlGenerator is null")
            );
        if (mapper == null)
            return new Result<IReadOnlyList<T>, SqlError>.Error<IReadOnlyList<T>, SqlError>(
                SqlError.Create("Mapper is required for GetRecords<T>")
            );

        var sqlResult = sqlGenerator(statement);
        if (sqlResult is Result<string, SqlError>.Error<string, SqlError> sqlFail)
        {
            return new Result<IReadOnlyList<T>, SqlError>.Error<IReadOnlyList<T>, SqlError>(
                sqlFail.Value
            );
        }

        var sql = ((Result<string, SqlError>.Ok<string, SqlError>)sqlResult).Value;
        return connection.Query(sql, parameters, mapper);
    }

    private static IDbCommand CreateCommandWith(
        this IDbConnection connection,
        string sql,
        IEnumerable<IDataParameter>? parameters
    )
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        if (parameters != null)
        {
            foreach (var parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }
        }
        return command;
    }
}
