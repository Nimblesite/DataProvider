using System.Data;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.DataProvider.Core;

/// <summary>
/// Static extension methods for IDbTransaction following FP patterns
/// </summary>
public static class DbTransactionExtensions
{
    /// <summary>
    /// Execute a query within a transaction and return results
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="transaction">The database transaction</param>
    /// <param name="sql">The SQL query</param>
    /// <param name="parameters">Optional parameters</param>
    /// <param name="mapper">Function to map from IDataReader to T</param>
    /// <returns>Result with list of T or error</returns>
    public static Result<IReadOnlyList<T>, SqlError> Query<T>(
        this IDbTransaction transaction,
        string sql,
        IEnumerable<IDataParameter>? parameters = null,
        Func<IDataReader, T>? mapper = null
    )
    {
        if (transaction?.Connection == null)
            return new Result<IReadOnlyList<T>, SqlError>.Error<IReadOnlyList<T>, SqlError>(
                SqlError.Create("Transaction or connection is null")
            );

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<IReadOnlyList<T>, SqlError>.Error<IReadOnlyList<T>, SqlError>(
                SqlError.Create("SQL is null or empty")
            );

        try
        {
            if (transaction.Connection is not { } connection)
                return new Result<IReadOnlyList<T>, SqlError>.Error<IReadOnlyList<T>, SqlError>(
                    SqlError.Create("Transaction or connection is null")
                );

            using var command = CreateConfiguredCommand(connection, transaction, sql, parameters);

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
    /// Execute a non-query command within a transaction
    /// </summary>
    /// <param name="transaction">The database transaction</param>
    /// <param name="sql">The SQL command</param>
    /// <param name="parameters">Optional parameters</param>
    /// <returns>Result with rows affected or error</returns>
    public static Result<int, SqlError> Execute(
        this IDbTransaction transaction,
        string sql,
        IEnumerable<IDataParameter>? parameters = null
    )
    {
        if (transaction?.Connection == null)
            return new Result<int, SqlError>.Error<int, SqlError>(
                SqlError.Create("Transaction or connection is null")
            );

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<int, SqlError>.Error<int, SqlError>(
                SqlError.Create("SQL is null or empty")
            );

        try
        {
            if (transaction.Connection is not { } connection)
                return new Result<int, SqlError>.Error<int, SqlError>(
                    SqlError.Create("Transaction or connection is null")
                );

            using var command = CreateConfiguredCommand(connection, transaction, sql, parameters);

            var rowsAffected = command.ExecuteNonQuery();
            return new Result<int, SqlError>.Ok<int, SqlError>(rowsAffected);
        }
        catch (Exception ex)
        {
            return new Result<int, SqlError>.Error<int, SqlError>(SqlError.FromException(ex));
        }
    }

    /// <summary>
    /// Execute a scalar command within a transaction
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    /// <param name="transaction">The database transaction</param>
    /// <param name="sql">The SQL command</param>
    /// <param name="parameters">Optional parameters</param>
    /// <returns>Result with scalar value or error</returns>
    public static Result<T?, SqlError> Scalar<T>(
        this IDbTransaction transaction,
        string sql,
        IEnumerable<IDataParameter>? parameters = null
    )
    {
        if (transaction?.Connection == null)
            return new Result<T?, SqlError>.Error<T?, SqlError>(
                SqlError.Create("Transaction or connection is null")
            );

        if (string.IsNullOrWhiteSpace(sql))
            return new Result<T?, SqlError>.Error<T?, SqlError>(
                SqlError.Create("SQL is null or empty")
            );

        try
        {
            if (transaction.Connection is not { } connection)
                return new Result<T?, SqlError>.Error<T?, SqlError>(
                    SqlError.Create("Transaction or connection is null")
                );

            using var command = CreateConfiguredCommand(connection, transaction, sql, parameters);

            var result = command.ExecuteScalar();
            return new Result<T?, SqlError>.Ok<T?, SqlError>(result is T value ? value : default);
        }
        catch (Exception ex)
        {
            return new Result<T?, SqlError>.Error<T?, SqlError>(SqlError.FromException(ex));
        }
    }

    private static IDbCommand CreateConfiguredCommand(
        IDbConnection connection,
        IDbTransaction transaction,
        string sql,
        IEnumerable<IDataParameter>? parameters
    )
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
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
