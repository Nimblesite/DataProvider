using System.Data;
using System.Data.Common;

namespace Nimblesite.DataProvider.Core;

/// <summary>
/// Provides transactional helpers as extension methods for <see cref="DbConnection"/>.
/// Opens a transaction, executes the provided delegate, and commits or rolls back accordingly.
/// </summary>
/// <example>
/// <code>
/// // Execute multiple operations in a transaction
/// await connection.Transact(async tx =>
/// {
///     await tx.InsertUserAsync(new User { Id = Guid.NewGuid(), Name = "John" });
///     await tx.InsertOrderAsync(new Order { UserId = userId, Total = 99.99m });
/// });
///
/// // Execute with a return value
/// var userId = await connection.Transact(async tx =>
/// {
///     var user = new User { Id = Guid.NewGuid(), Name = "Jane" };
///     await tx.InsertUserAsync(user);
///     return user.Id;
/// });
/// </code>
/// </example>
public static class DbTransact
{
    /// <summary>
    /// Executes the specified asynchronous <paramref name="body"/> within a database transaction.
    /// The transaction is committed on success, or rolled back if an exception is thrown.
    /// </summary>
    /// <param name="cn">The database connection. It will be opened if not already open.</param>
    /// <param name="body">The asynchronous delegate to execute within the transaction.</param>
    /// <returns>A task that completes when the transactional operation finishes.</returns>
    public static Task Transact(this DbConnection cn, Func<IDbTransaction, Task> body)
    {
        ArgumentNullException.ThrowIfNull(cn);
        ArgumentNullException.ThrowIfNull(body);

        return cn.Transact(async tx =>
        {
            await body(tx).ConfigureAwait(false);
            return true;
        });
    }

    /// <summary>
    /// Executes the specified asynchronous <paramref name="body"/> within a database transaction and returns a result.
    /// The transaction is committed on success, or rolled back if an exception is thrown.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="cn">The database connection. It will be opened if not already open.</param>
    /// <param name="body">The asynchronous delegate to execute within the transaction.</param>
    /// <returns>A task that resolves to the result returned by <paramref name="body"/>.</returns>
    public static async Task<T> Transact<T>(
        this DbConnection cn,
        Func<IDbTransaction, Task<T>> body
    )
    {
        ArgumentNullException.ThrowIfNull(cn);
        ArgumentNullException.ThrowIfNull(body);

        if (cn.State != ConnectionState.Open)
            await cn.OpenAsync().ConfigureAwait(false);

#pragma warning disable CA2007
        await using var tx = await cn.BeginTransactionAsync().ConfigureAwait(false);
#pragma warning restore CA2007
        try
        {
            var result = await body(tx).ConfigureAwait(false);
            await tx.CommitAsync().ConfigureAwait(false);
            return result;
        }
        catch
        {
            await tx.RollbackAsync().ConfigureAwait(false);
            throw;
        }
    }
}
