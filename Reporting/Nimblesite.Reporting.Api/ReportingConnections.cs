using System.Collections.Immutable;
using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Nimblesite.Lql.Core;
using Nimblesite.Lql.Postgres;
using Nimblesite.Lql.SQLite;
using Nimblesite.Lql.SqlServer;
using Nimblesite.Reporting.Engine;
using Nimblesite.Sql.Model;
using Npgsql;
using ConnError = Outcome.Result<System.Data.IDbConnection, Nimblesite.Sql.Model.SqlError>.Error<
    System.Data.IDbConnection,
    Nimblesite.Sql.Model.SqlError
>;
using ConnOk = Outcome.Result<System.Data.IDbConnection, Nimblesite.Sql.Model.SqlError>.Ok<
    System.Data.IDbConnection,
    Nimblesite.Sql.Model.SqlError
>;
using ConnResult = Outcome.Result<System.Data.IDbConnection, Nimblesite.Sql.Model.SqlError>;
using LqlParseError = Outcome.Result<
    Nimblesite.Lql.Core.LqlStatement,
    Nimblesite.Sql.Model.SqlError
>.Error<Nimblesite.Lql.Core.LqlStatement, Nimblesite.Sql.Model.SqlError>;
using LqlParseOk = Outcome.Result<
    Nimblesite.Lql.Core.LqlStatement,
    Nimblesite.Sql.Model.SqlError
>.Ok<Nimblesite.Lql.Core.LqlStatement, Nimblesite.Sql.Model.SqlError>;
using SqlConnection = Microsoft.Data.SqlClient.SqlConnection;
using TranspileError = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>.Error<
    string,
    Nimblesite.Sql.Model.SqlError
>;
using TranspileResult = Outcome.Result<string, Nimblesite.Sql.Model.SqlError>;

namespace Nimblesite.Reporting.Api;

/// <summary>
/// Server-side connection registry for report data sources. Each connection string is
/// opened with the provider it targets (SQLite, PostgreSQL or SQL Server), and LQL is
/// transpiled for that provider. Implements [REPORT-CONNECTIONS].
/// </summary>
public static class ReportingConnections
{
    private static readonly ImmutableArray<string> SqlServerKeywords =
    [
        "Server",
        "Initial Catalog",
        "User Id",
        "Integrated Security",
        "TrustServerCertificate",
    ];

    /// <summary>
    /// Builds the registry from <c>ConnectionStrings:*</c>. <c>ConnectionProviders:{name}</c>
    /// (Sqlite, Postgres or SqlServer) overrides provider detection for a connection.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>Connection configs keyed by connection ref.</returns>
    public static ImmutableDictionary<string, ConnectionConfig> FromConfiguration(
        IConfiguration configuration
    ) =>
        configuration
            .GetSection("ConnectionStrings")
            .GetChildren()
            .ToImmutableDictionary(
                section => section.Key,
                section => new ConnectionConfig(
                    Provider: Enum.TryParse<DatabaseProvider>(
                        configuration[$"ConnectionProviders:{section.Key}"],
                        ignoreCase: true,
                        out var configured
                    )
                        ? configured
                        : DetectProvider(section.Value ?? ""),
                    ConnectionString: section.Value ?? ""
                )
            );

    /// <summary>
    /// Detects the provider from connection string keywords, parsed with the ADO.NET
    /// connection string parser: <c>Host</c> is PostgreSQL, SQL Server keywords are SQL
    /// Server, anything else is a SQLite data source.
    /// </summary>
    /// <param name="connectionString">The connection string to inspect.</param>
    /// <returns>The detected provider.</returns>
    public static DatabaseProvider DetectProvider(string connectionString)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        return builder.ContainsKey("Host") ? DatabaseProvider.Postgres
            : SqlServerKeywords.Any(builder.ContainsKey) ? DatabaseProvider.SqlServer
            : DatabaseProvider.Sqlite;
    }

    /// <summary>
    /// Opens the connection registered under <paramref name="connectionRef"/>.
    /// </summary>
    /// <param name="connections">The connection registry.</param>
    /// <param name="connectionRef">The report data source's connection ref.</param>
    /// <returns>An open connection the caller owns, or an error.</returns>
    public static ConnResult Open(
        ImmutableDictionary<string, ConnectionConfig> connections,
        string connectionRef
    )
    {
        if (!connections.TryGetValue(connectionRef, out var config))
        {
            return new ConnError(SqlError.Create($"Connection '{connectionRef}' not found"));
        }
        return config.Provider switch
        {
            DatabaseProvider.Postgres => OpenWith(cs => new NpgsqlConnection(cs), config),
            DatabaseProvider.SqlServer => OpenWith(cs => new SqlConnection(cs), config),
            _ => OpenWith(cs => new SqliteConnection(cs), config),
        };
    }

    private static ConnResult OpenWith(Func<string, IDbConnection> create, ConnectionConfig config)
    {
        try
        {
            var connection = create(config.ConnectionString);
            connection.Open();
            return new ConnOk(connection);
        }
        catch (Exception ex)
        {
            return new ConnError(SqlError.FromException(ex));
        }
    }

    /// <summary>
    /// Transpiles LQL to SQL for the provider behind <paramref name="connectionRef"/>.
    /// </summary>
    /// <param name="connections">The connection registry.</param>
    /// <param name="connectionRef">The report data source's connection ref.</param>
    /// <param name="lqlCode">The LQL query.</param>
    /// <returns>Provider SQL, or an error.</returns>
    public static TranspileResult Transpile(
        ImmutableDictionary<string, ConnectionConfig> connections,
        string connectionRef,
        string lqlCode
    ) =>
        LqlStatementConverter.ToStatement(lqlCode) switch
        {
            LqlParseError error => new TranspileError(error.Value),
            LqlParseOk ok => connections.TryGetValue(connectionRef, out var config)
                ? ToProviderSql(ok.Value, config.Provider)
                : new TranspileError(SqlError.Create($"Connection '{connectionRef}' not found")),
        };

    private static TranspileResult ToProviderSql(
        LqlStatement statement,
        DatabaseProvider provider
    ) =>
        provider switch
        {
            DatabaseProvider.Postgres => statement.ToPostgreSql(),
            DatabaseProvider.SqlServer => statement.ToSqlServer(),
            DatabaseProvider.Sqlite => statement.ToSQLite(),
            _ => new TranspileError(SqlError.Create($"Unsupported provider: {provider}")),
        };
}
