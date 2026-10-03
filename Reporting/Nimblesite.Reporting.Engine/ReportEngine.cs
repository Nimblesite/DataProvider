using System.Collections.Immutable;
using System.Data;
using Microsoft.Extensions.Logging;
using Nimblesite.Sql.Model;
using Outcome;

namespace Nimblesite.Reporting.Engine;

using ConnError = Result<IDbConnection, SqlError>.Error<IDbConnection, SqlError>;
using ConnOk = Result<IDbConnection, SqlError>.Ok<IDbConnection, SqlError>;
using DsError = Result<DataSourceResult, SqlError>.Error<DataSourceResult, SqlError>;
using DsOk = Result<DataSourceResult, SqlError>.Ok<DataSourceResult, SqlError>;
using DsResult = Result<DataSourceResult, SqlError>;
using EngineError = Result<ReportExecutionResult, SqlError>.Error<ReportExecutionResult, SqlError>;
using EngineOk = Result<ReportExecutionResult, SqlError>.Ok<ReportExecutionResult, SqlError>;
using EngineResult = Result<ReportExecutionResult, SqlError>;
using TranspileError = Result<string, SqlError>.Error<string, SqlError>;
using TranspileOk = Result<string, SqlError>.Ok<string, SqlError>;

/// <summary>
/// Executes report data sources and assembles results.
/// </summary>
public static class ReportEngine
{
    /// <summary>
    /// Executes all data sources in a report definition and returns assembled results.
    /// </summary>
    /// <param name="report">The report definition to execute.</param>
    /// <param name="parameters">Parameter values provided by the user.</param>
    /// <param name="connectionFactory">Factory that creates open IDbConnection from a connection ref name.</param>
    /// <param name="lqlTranspiler">Function that transpiles LQL to SQL for the target database.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>Result containing all data source results or an error.</returns>
    public static EngineResult Execute(
        ReportDefinition report,
        ImmutableDictionary<string, string> parameters,
        Func<string, Result<IDbConnection, SqlError>> connectionFactory,
        Func<string, Result<string, SqlError>> lqlTranspiler,
        ILogger logger
    ) =>
        Execute(
            report: report,
            parameters: parameters,
            connectionFactory: connectionFactory,
            lqlTranspiler: (_, lql) => lqlTranspiler(lql),
            logger: logger
        );

    /// <summary>
    /// Executes all data sources in a report definition, transpiling LQL for the
    /// provider behind each data source's connection ref.
    /// </summary>
    /// <param name="report">The report definition to execute.</param>
    /// <param name="parameters">Parameter values provided by the user.</param>
    /// <param name="connectionFactory">Factory that creates open IDbConnection from a connection ref name.</param>
    /// <param name="lqlTranspiler">Transpiles LQL (second argument) to SQL for the connection ref (first argument).</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>Result containing all data source results or an error.</returns>
    public static EngineResult Execute(
        ReportDefinition report,
        ImmutableDictionary<string, string> parameters,
        Func<string, Result<IDbConnection, SqlError>> connectionFactory,
        Func<string, string, Result<string, SqlError>> lqlTranspiler,
        ILogger logger
    )
    {
        logger.LogInformation(
            "Executing report {ReportId} with {ParamCount} parameters",
            report.Id,
            parameters.Count
        );

        var results = ImmutableDictionary.CreateBuilder<string, DataSourceResult>();

        foreach (var ds in report.DataSources)
        {
            logger.LogInformation(
                "Executing data source {DataSourceId} (type: {Type})",
                ds.Id,
                ds.Type
            );

            var dsResult = ExecuteDataSource(
                dataSource: ds,
                parameters: parameters,
                connectionFactory: connectionFactory,
                lqlTranspiler: lqlTranspiler,
                logger: logger
            );

            switch (dsResult)
            {
                case DsError dsErr:
                    logger.LogError(
                        "Data source {DataSourceId} failed: {Error}",
                        ds.Id,
                        dsErr.Value.Message
                    );
                    return new EngineError(dsErr.Value);
                case DsOk dsOk:
                    results.Add(ds.Id, dsOk.Value);
                    logger.LogInformation(
                        "Data source {DataSourceId} returned {RowCount} rows",
                        ds.Id,
                        dsOk.Value.TotalRows
                    );
                    break;
            }
        }

        return new EngineOk(
            new ReportExecutionResult(
                ReportId: report.Id,
                ExecutedAt: DateTimeOffset.UtcNow,
                DataSources: results.ToImmutable()
            )
        );
    }

    /// <summary>
    /// Executes a single data source and returns its result.
    /// </summary>
    internal static DsResult ExecuteDataSource(
        DataSourceDefinition dataSource,
        ImmutableDictionary<string, string> parameters,
        Func<string, Result<IDbConnection, SqlError>> connectionFactory,
        Func<string, string, Result<string, SqlError>> lqlTranspiler,
        ILogger logger
    )
    {
        return dataSource.Type switch
        {
            DataSourceType.Sql => ExecuteSql(
                dataSource: dataSource,
                parameters: parameters,
                connectionFactory: connectionFactory,
                logger: logger
            ),
            DataSourceType.Lql => ExecuteLql(
                dataSource: dataSource,
                parameters: parameters,
                connectionFactory: connectionFactory,
                lqlTranspiler: lqlTranspiler,
                logger: logger
            ),
            DataSourceType.Api => new DsError(
                SqlError.Create("API data sources are not yet supported")
            ),
            _ => new DsError(SqlError.Create($"Unknown data source type: {dataSource.Type}")),
        };
    }

    private static DsResult ExecuteSql(
        DataSourceDefinition dataSource,
        ImmutableDictionary<string, string> parameters,
        Func<string, Result<IDbConnection, SqlError>> connectionFactory,
        ILogger logger
    )
    {
        if (string.IsNullOrWhiteSpace(dataSource.Query))
        {
            return new DsError(SqlError.Create("SQL data source has no query"));
        }

        if (string.IsNullOrWhiteSpace(dataSource.ConnectionRef))
        {
            return new DsError(SqlError.Create("SQL data source has no connection reference"));
        }

        return connectionFactory(dataSource.ConnectionRef) switch
        {
            ConnError connErr => new DsError(connErr.Value),
            ConnOk connOk => ExecuteQueryOnConnection(
                connection: connOk.Value,
                sql: dataSource.Query,
                parameterNames: dataSource.Parameters,
                parameterValues: parameters,
                logger: logger
            ),
        };
    }

    private static DsResult ExecuteLql(
        DataSourceDefinition dataSource,
        ImmutableDictionary<string, string> parameters,
        Func<string, Result<IDbConnection, SqlError>> connectionFactory,
        Func<string, string, Result<string, SqlError>> lqlTranspiler,
        ILogger logger
    )
    {
        if (string.IsNullOrWhiteSpace(dataSource.Query))
        {
            return new DsError(SqlError.Create("LQL data source has no query"));
        }

        if (string.IsNullOrWhiteSpace(dataSource.ConnectionRef))
        {
            return new DsError(SqlError.Create("LQL data source has no connection reference"));
        }

        return lqlTranspiler(dataSource.ConnectionRef, dataSource.Query) switch
        {
            TranspileError transpileErr => new DsError(transpileErr.Value),
            TranspileOk transpileOk => ExecuteTranspiledSql(
                sql: transpileOk.Value,
                connectionRef: dataSource.ConnectionRef,
                parameterNames: dataSource.Parameters,
                parameterValues: parameters,
                connectionFactory: connectionFactory,
                logger: logger
            ),
        };
    }

    private static DsResult ExecuteTranspiledSql(
        string sql,
        string connectionRef,
        ImmutableArray<string> parameterNames,
        ImmutableDictionary<string, string> parameterValues,
        Func<string, Result<IDbConnection, SqlError>> connectionFactory,
        ILogger logger
    )
    {
        logger.LogInformation("LQL transpiled to SQL: {Sql}", sql);

        return connectionFactory(connectionRef) switch
        {
            ConnError connErr => new DsError(connErr.Value),
            ConnOk connOk => ExecuteQueryOnConnection(
                connection: connOk.Value,
                sql: sql,
                parameterNames: parameterNames,
                parameterValues: parameterValues,
                logger: logger
            ),
        };
    }

    private static DsResult ExecuteQueryOnConnection(
        IDbConnection connection,
        string sql,
        ImmutableArray<string> parameterNames,
        ImmutableDictionary<string, string> parameterValues,
        ILogger logger
    )
    {
        // The factory hands over a freshly opened connection; the engine owns it.
        using var owned = connection;
        try
        {
            using var command = owned.CreateCommand();
            command.CommandText = sql;

            foreach (var paramName in parameterNames)
            {
                if (parameterValues.TryGetValue(paramName, out var value))
                {
                    var param = command.CreateParameter();
                    param.ParameterName = $"@{paramName}";
                    param.Value = value;
                    command.Parameters.Add(param);
                }
            }

            using var reader = command.ExecuteReader();

            var columnNames = ImmutableArray.CreateBuilder<string>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                columnNames.Add(reader.GetName(i));
            }

            var rows = ImmutableArray.CreateBuilder<ImmutableArray<object?>>();
            while (reader.Read())
            {
                var row = ImmutableArray.CreateBuilder<object?>();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row.Add(reader.IsDBNull(i) ? null : reader.GetValue(i));
                }
                rows.Add(row.ToImmutable());
            }

            return new DsOk(
                new DataSourceResult(
                    ColumnNames: columnNames.ToImmutable(),
                    Rows: rows.ToImmutable(),
                    TotalRows: rows.Count
                )
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute query");
            return new DsError(SqlError.FromException(ex));
        }
    }
}
