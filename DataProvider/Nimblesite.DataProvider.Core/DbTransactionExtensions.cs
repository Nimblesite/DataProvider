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
    ) =>
        RunCommand<IReadOnlyList<T>>(
            transaction: transaction,
            sql: sql,
            parameters: parameters,
            operation: command =>
            {
                var results = new List<T>();
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    if (mapper != null)
                    {
                        results.Add(mapper(reader));
                    }
                }

                return results.AsReadOnly();
            }
        );

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
    ) =>
        RunCommand(
            transaction: transaction,
            sql: sql,
            parameters: parameters,
            operation: command => command.ExecuteNonQuery()
        );

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
    ) =>
        RunCommand<T?>(
            transaction: transaction,
            sql: sql,
            parameters: parameters,
            operation: command => command.ExecuteScalar() is T value ? value : default
        );

    /// <summary>
    /// Validate the transaction, its bound connection, and the SQL text
    /// </summary>
    private static SqlError? Validate(IDbTransaction transaction, string sql)
    {
        if (transaction?.Connection == null)
            return SqlError.Create("Transaction or connection is null");

        if (string.IsNullOrWhiteSpace(sql))
            return SqlError.Create("SQL is null or empty");

        return null;
    }

    /// <summary>
    /// Create a transaction-bound command, run the operation, and convert failures to SqlError
    /// </summary>
    private static Result<TResult, SqlError> RunCommand<TResult>(
        IDbTransaction transaction,
        string sql,
        IEnumerable<IDataParameter>? parameters,
        Func<IDbCommand, TResult> operation
    )
    {
        if (Validate(transaction: transaction, sql: sql) is { } guardError)
            return new Result<TResult, SqlError>.Error<TResult, SqlError>(guardError);

        try
        {
            if (transaction.Connection is not { } connection)
                return new Result<TResult, SqlError>.Error<TResult, SqlError>(
                    SqlError.Create("Transaction or connection is null")
                );

            using var command = CreateCommand(
                connection: connection,
                transaction: transaction,
                sql: sql,
                parameters: parameters
            );

            return new Result<TResult, SqlError>.Ok<TResult, SqlError>(operation(command));
        }
        catch (Exception ex)
        {
            return new Result<TResult, SqlError>.Error<TResult, SqlError>(
                SqlError.FromException(ex)
            );
        }
    }

    /// <summary>
    /// Create a command bound to the transaction, with SQL text and optional parameters
    /// </summary>
    private static IDbCommand CreateCommand(
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
