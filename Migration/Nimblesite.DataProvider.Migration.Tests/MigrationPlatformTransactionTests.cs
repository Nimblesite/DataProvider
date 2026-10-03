using System.Data;
using System.Data.Common;
using System.Globalization;
using Nimblesite.DataProvider.Core;

namespace Nimblesite.DataProvider.Migration.Tests;

public sealed partial record MigrationPlatformRegressionTests
{
    // Implements [MIG-TEST-CROSS-PLATFORM]: one transaction contract for every provider.
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    public async Task Transact_ReturnsValuePropagatesFailureAndReleasesTransaction(string provider)
    {
        await WithAsyncTargetAsync(provider, AssertTransactionContractAsync).ConfigureAwait(true);
    }

    private static async Task AssertTransactionContractAsync(MigrationTarget target)
    {
        Assert.Equal(ConnectionState.Open, target.Connection.State);
        var callbacks = 0;
        var committed = await target
            .Connection.Transact(async transaction =>
            {
                callbacks++;
                await AssertTransactionQueryAsync(target, transaction).ConfigureAwait(false);
                return 42;
            })
            .ConfigureAwait(false);
        Assert.Equal(42, committed);
        Assert.Equal(1, callbacks);
        Assert.Equal(ConnectionState.Open, target.Connection.State);
        AssertCanStartAnotherTransaction(target);

        var failure = await Assert
            .ThrowsAsync<InvalidOperationException>(() =>
                target.Connection.Transact<int>(async transaction =>
                {
                    callbacks++;
                    await AssertTransactionQueryAsync(target, transaction).ConfigureAwait(false);
                    throw new InvalidOperationException("transaction callback failed");
                })
            )
            .ConfigureAwait(true);
        Assert.Equal("transaction callback failed", failure.Message);
        Assert.Equal(2, callbacks);
        Assert.Equal(ConnectionState.Open, target.Connection.State);
        AssertCanStartAnotherTransaction(target);

        var recovered = await target
            .Connection.Transact(async transaction =>
            {
                callbacks++;
                await AssertTransactionQueryAsync(target, transaction).ConfigureAwait(false);
                return 7;
            })
            .ConfigureAwait(false);
        Assert.Equal(7, recovered);
        Assert.Equal(3, callbacks);
        AssertCanStartAnotherTransaction(target);
    }

    private static async Task AssertTransactionQueryAsync(
        MigrationTarget target,
        IDbTransaction transaction
    )
    {
        if (transaction is not DbTransaction dbTransaction)
        {
            Assert.Fail($"{target.Provider} returned a transaction without DbTransaction support");
            return;
        }
        Assert.Same(target.Connection, dbTransaction.Connection);
        using var command = target.Connection.CreateCommand();
        command.Transaction = dbTransaction;
        command.CommandText = "SELECT 1";
        var value = await command.ExecuteScalarAsync().ConfigureAwait(false);
        Assert.Equal(1, Convert.ToInt32(value, CultureInfo.InvariantCulture));
    }

    private static void AssertCanStartAnotherTransaction(MigrationTarget target)
    {
        using var transaction = target.Connection.BeginTransaction();
        Assert.Same(target.Connection, transaction.Connection);
        transaction.Rollback();
        Assert.Equal(ConnectionState.Open, target.Connection.State);
    }
}
