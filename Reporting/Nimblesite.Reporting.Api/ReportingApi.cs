using System.Collections.Immutable;
using Microsoft.AspNetCore.Http.Json;
using Nimblesite.Reporting.Engine;
using EngineError = Outcome.Result<
    Nimblesite.Reporting.Engine.ReportExecutionResult,
    Nimblesite.Sql.Model.SqlError
>.Error<Nimblesite.Reporting.Engine.ReportExecutionResult, Nimblesite.Sql.Model.SqlError>;
using EngineOk = Outcome.Result<
    Nimblesite.Reporting.Engine.ReportExecutionResult,
    Nimblesite.Sql.Model.SqlError
>.Ok<Nimblesite.Reporting.Engine.ReportExecutionResult, Nimblesite.Sql.Model.SqlError>;
using EngineResult = Outcome.Result<
    Nimblesite.Reporting.Engine.ReportExecutionResult,
    Nimblesite.Sql.Model.SqlError
>;
using LoadDirError = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<Nimblesite.Reporting.Engine.ReportDefinition>,
    Nimblesite.Sql.Model.SqlError
>.Error<
    System.Collections.Immutable.ImmutableArray<Nimblesite.Reporting.Engine.ReportDefinition>,
    Nimblesite.Sql.Model.SqlError
>;
using LoadDirOk = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<Nimblesite.Reporting.Engine.ReportDefinition>,
    Nimblesite.Sql.Model.SqlError
>.Ok<
    System.Collections.Immutable.ImmutableArray<Nimblesite.Reporting.Engine.ReportDefinition>,
    Nimblesite.Sql.Model.SqlError
>;

namespace Nimblesite.Reporting.Api;

/// <summary>
/// The Reporting HTTP API: report listing, metadata, execution and export, guarded by
/// Gatekeeper bearer authentication. Shared by the host and its E2E tests.
/// Implements [REPORT-SECURITY] and [REPORT-AUTH-BEARER].
/// </summary>
public static class ReportingApi
{
    /// <summary>
    /// CORS policy name used by the report viewer.
    /// </summary>
    public const string CorsPolicy = "ReportViewer";

    /// <summary>
    /// Registers camelCase JSON and the report viewer CORS policy.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddReportingApi(this IServiceCollection services) =>
        services
            .Configure<JsonOptions>(options =>
                options.SerializerOptions.PropertyNamingPolicy = System
                    .Text
                    .Json
                    .JsonNamingPolicy
                    .CamelCase
            )
            .AddCors(options =>
                options.AddPolicy(
                    CorsPolicy,
                    policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
                )
            );

    /// <summary>
    /// Rejects every <c>/api</c> request without a valid Gatekeeper bearer token with 401.
    /// Must run after CORS (so preflights succeed) and before the endpoints.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configuration">Configuration holding <c>Jwt:SigningKey</c>.</param>
    /// <param name="logger">Logger for rejected requests.</param>
    /// <returns>The same application builder.</returns>
    public static IApplicationBuilder UseReportingBearerAuth(
        this IApplicationBuilder app,
        IConfiguration configuration,
        ILogger logger
    )
    {
        var signingKey = GatekeeperBearerAuth.ParseSigningKey(
            configuration[GatekeeperBearerAuth.SigningKeySetting]
        );
        logger.LogInformation(
            "Reporting bearer auth signing key: {KeyState}",
            signingKey.IsDefaultOrEmpty ? "missing (all API requests rejected)" : "present"
        );
        return app.Use(next =>
            context =>
                !context.Request.Path.StartsWithSegments("/api", StringComparison.Ordinal)
                || GatekeeperBearerAuth.IsAuthorized(
                    authorizationHeader: context.Request.Headers.Authorization,
                    signingKey: signingKey,
                    now: DateTimeOffset.UtcNow
                )
                    ? next(context)
                    : Reject(context, logger)
        );
    }

    /// <summary>
    /// Maps the <c>/api/reports</c> endpoints over the configured reports and connections.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configuration">Configuration with <c>ReportsDirectory</c> and connections.</param>
    /// <param name="logger">Logger for report loading and execution.</param>
    /// <returns>The report route group.</returns>
    public static RouteGroupBuilder MapReportingApi(
        this IEndpointRouteBuilder endpoints,
        IConfiguration configuration,
        ILogger logger
    )
    {
        var reports = LoadReports(configuration, logger);
        var connections = ReportingConnections.FromConfiguration(configuration);
        EngineResult Execute(ReportDefinition report, ImmutableDictionary<string, string> values) =>
            ReportEngine.Execute(
                report: report,
                parameters: values,
                connectionFactory: connectionRef =>
                    ReportingConnections.Open(connections, connectionRef),
                lqlTranspiler: (connectionRef, lql) =>
                    ReportingConnections.Transpile(connections, connectionRef, lql),
                logger: logger
            );
        var group = endpoints.MapGroup("/api/reports").WithTags("Reports");
        MapEndpoints(group, reports, Execute);
        return group;
    }

    private static void MapEndpoints(
        RouteGroupBuilder group,
        ImmutableDictionary<string, ReportDefinition> reports,
        Func<ReportDefinition, ImmutableDictionary<string, string>, EngineResult> execute
    )
    {
        group.MapGet(
            "/",
            () =>
                Results.Ok(
                    reports.Values.Select(ReportMetadataMapper.ToMetadata).ToImmutableArray()
                )
        );
        group.MapGet(
            "/{id}",
            (string id) =>
                reports.TryGetValue(id, out var report)
                    ? Results.Ok(ReportMetadataMapper.ToMetadata(report))
                    : NotFound(id)
        );
        group.MapPost(
            "/{id}/execute",
            (string id, ReportExecuteRequest request) =>
                reports.TryGetValue(id, out var report)
                    ? ToResult(execute(report, request.Parameters))
                    : NotFound(id)
        );
        group.MapGet(
            "/{id}/export",
            (string id, string? datasource, string? format) =>
                reports.TryGetValue(id, out var report)
                    ? Export(
                        execute(report, ImmutableDictionary<string, string>.Empty),
                        report,
                        datasource,
                        format
                    )
                    : NotFound(id)
        );
    }

    private static Task Reject(HttpContext context, ILogger logger)
    {
        logger.LogWarning("Rejected unauthenticated reporting API request");
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    private static ImmutableDictionary<string, ReportDefinition> LoadReports(
        IConfiguration configuration,
        ILogger logger
    )
    {
        var reportsDir =
            configuration["ReportsDirectory"] ?? Path.Combine(AppContext.BaseDirectory, "Reports");
        var reports = ReportConfigLoader.LoadFromDirectory(
            directoryPath: reportsDir,
            logger: logger
        ) switch
        {
            LoadDirOk ok => ok.Value.ToImmutableDictionary(r => r.Id),
            LoadDirError => ImmutableDictionary<string, ReportDefinition>.Empty,
        };
        logger.LogInformation("Loaded {Count} report definitions", reports.Count);
        return reports;
    }

    private static IResult ToResult(EngineResult result) =>
        result switch
        {
            EngineOk ok => Results.Ok(ok.Value),
            EngineError error => Results.Problem(error.Value.Message),
        };

    private static IResult NotFound(string id) =>
        Results.NotFound(new { Error = $"Report '{id}' not found" });

    private static IResult Export(
        EngineResult result,
        ReportDefinition report,
        string? datasource,
        string? format
    )
    {
        if (result is not EngineOk ok)
        {
            return ToResult(result);
        }
        var targetDs = datasource ?? report.DataSources.FirstOrDefault()?.Id;
        return targetDs is null || !ok.Value.DataSources.TryGetValue(targetDs, out var dsResult)
                ? Results.NotFound(new { Error = $"Data source '{targetDs}' not found" })
            : format == "csv" ? Results.Text(FormatAdapter.ToCsv(dsResult), contentType: "text/csv")
            : Results.Ok(dsResult);
    }
}
