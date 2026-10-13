#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task

using System.Text.Json;
using System.Threading.RateLimiting;

namespace Nimblesite.Sync.Http;

/// <summary>
/// Extension methods for configuring sync API endpoints and services.
/// These are TOOLS for spinning up a sync server - not an actual server.
/// </summary>
public static class SyncEndpointExtensions
{
    /// <summary>
    /// Adds sync API services to the service collection.
    /// Includes rate limiting, subscription manager, and logging.
    /// </summary>
    public static IServiceCollection AddSyncApiServices(
        this IServiceCollection services,
        bool isDevelopment = false
    )
    {
        services.AddEndpointsApiExplorer();
        services.AddSingleton<ApiSubscriptionManager>();

        services.AddLogging(config =>
        {
            config.AddConsole();
            config.SetMinimumLevel(isDevelopment ? LogLevel.Debug : LogLevel.Information);
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(
                "sync",
                context =>
                    RateLimitPartition.GetSlidingWindowLimiter(
                        partitionKey: context.Request.Headers["X-Client-Id"].FirstOrDefault()
                            ?? context.Connection.RemoteIpAddress?.ToString()
                            ?? "anonymous",
                        factory: _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = 1000,
                            Window = TimeSpan.FromMinutes(1),
                            SegmentsPerWindow = 6,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 10,
                        }
                    )
            );

            options.AddPolicy(
                "sse",
                context =>
                    RateLimitPartition.GetConcurrencyLimiter(
                        partitionKey: context.Request.Headers["X-Client-Id"].FirstOrDefault()
                            ?? context.Connection.RemoteIpAddress?.ToString()
                            ?? "anonymous",
                        factory: _ => new ConcurrencyLimiterOptions
                        {
                            PermitLimit = 100,
                            QueueLimit = 10,
                        }
                    )
            );
        });

        return services;
    }

    /// <summary>
    /// Adds request timeout middleware for production use.
    /// </summary>
    public static IApplicationBuilder UseSyncRequestTimeout(
        this IApplicationBuilder app,
        TimeSpan? timeout = null
    ) =>
        app.Use(
            async (context, next) =>
            {
                using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(30));
                context.RequestAborted = cts.Token;
                await next();
            }
        );

    /// <summary>
    /// Maps all sync API endpoints to the endpoint route builder.
    /// </summary>
    public static IEndpointRouteBuilder MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/health",
                () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow })
            )
            .AllowAnonymous();

        app.MapGet(
                "/sync/changes",
                (
                    long fromVersion,
                    int batchSize,
                    string? connectionString,
                    string dbType,
                    ILogger<ApiSubscriptionManager> logger,
                    IConfiguration config
                ) =>
                {
                    if (
                        ResolveConnectionString(connectionString, dbType, config) is not { } connStr
                    )
                    {
                        logger.LogWarning("API: No connection string for {DbType}", dbType);
                        return Results.BadRequest($"No connection string configured for {dbType}");
                    }

                    batchSize = Math.Min(batchSize, 5000);

                    logger.LogInformation(
                        "API: Pull changes from version {Version}, batchSize {Size}, dbType {Type}",
                        fromVersion,
                        batchSize,
                        dbType
                    );

                    try
                    {
                        var entries = SyncHelpers.FetchChanges(
                            connStr,
                            dbType,
                            fromVersion,
                            batchSize,
                            logger
                        );

                        return Results.Ok(
                            new
                            {
                                Changes = entries,
                                FromVersion = fromVersion,
                                ToVersion = entries.Count > 0
                                    ? entries.Max(e => e.Version)
                                    : fromVersion,
                                HasMore = entries.Count == batchSize,
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "API: Failed to fetch changes");
                        return Results.Problem(ex.Message);
                    }
                }
            )
            .RequireRateLimiting("sync");

        app.MapPost(
                "/sync/changes",
                async (
                    HttpRequest request,
                    string? connectionString,
                    string dbType,
                    ILogger<ApiSubscriptionManager> logger,
                    IConfiguration config,
                    ApiSubscriptionManager subscriptions
                ) =>
                {
                    if (
                        ResolveConnectionString(connectionString, dbType, config) is not { } connStr
                    )
                    {
                        return Results.BadRequest($"No connection string configured for {dbType}");
                    }

                    logger.LogInformation("API: Push changes, dbType {Type}", dbType);

                    try
                    {
                        var body = await JsonSerializer.DeserializeAsync<PushChangesRequest>(
                            request.Body
                        );
                        if (body?.Changes is null)
                        {
                            return Results.BadRequest("Changes array required");
                        }

                        if (body.Changes.Count > 10000)
                        {
                            return Results.BadRequest("Max 10000 changes per request");
                        }

                        var applied = SyncHelpers.ApplyChanges(
                            connStr,
                            dbType,
                            body.Changes,
                            body.OriginId,
                            logger
                        );

                        foreach (var entry in body.Changes)
                        {
                            subscriptions.NotifyChange(entry);
                        }

                        return Results.Ok(
                            new { Applied = applied, Timestamp = DateTime.UtcNow.ToString("O") }
                        );
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "API: Failed to push changes");
                        return Results.Problem(ex.Message);
                    }
                }
            )
            .RequireRateLimiting("sync");

        app.MapPost(
                "/sync/clients",
                async (
                    HttpRequest request,
                    string? connectionString,
                    string dbType,
                    ILogger<ApiSubscriptionManager> logger,
                    IConfiguration config
                ) =>
                {
                    if (
                        ResolveConnectionString(connectionString, dbType, config) is not { } connStr
                    )
                    {
                        return Results.BadRequest($"No connection string configured for {dbType}");
                    }

                    logger.LogInformation("API: Register client, dbType {Type}", dbType);

                    try
                    {
                        var body = await JsonSerializer.DeserializeAsync<RegisterClientRequest>(
                            request.Body
                        );
                        if (body?.OriginId is null)
                        {
                            return Results.BadRequest("OriginId required");
                        }

                        var client = new SyncClient(
                            body.OriginId,
                            body.LastSyncVersion,
                            DateTime.UtcNow.ToString("O"),
                            DateTime.UtcNow.ToString("O")
                        );

                        _ = SyncHelpers.UpsertClient(connStr, dbType, client, logger);

                        return Results.Ok(new { Registered = true, Client = client });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "API: Failed to register client");
                        return Results.Problem(ex.Message);
                    }
                }
            )
            .RequireRateLimiting("sync");

        app.MapGet(
                "/sync/state",
                (
                    string? connectionString,
                    string dbType,
                    ILogger<ApiSubscriptionManager> logger,
                    IConfiguration config
                ) =>
                {
                    if (
                        ResolveConnectionString(connectionString, dbType, config) is not { } connStr
                    )
                    {
                        return Results.BadRequest($"No connection string configured for {dbType}");
                    }

                    logger.LogInformation("API: Get sync state, dbType {Type}", dbType);

                    try
                    {
                        var maxVersion = SyncHelpers.GetMaxVersion(connStr, dbType, logger);

                        return Results.Ok(
                            new
                            {
                                MaxVersion = maxVersion,
                                Timestamp = DateTime.UtcNow.ToString("O"),
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "API: Failed to get sync state");
                        return Results.Problem(ex.Message);
                    }
                }
            )
            .RequireRateLimiting("sync");

        app.MapGet(
                "/sync/subscribe",
                async (
                    HttpContext context,
                    string tableName,
                    string? pkValue,
                    ApiSubscriptionManager subscriptions,
                    ILogger<ApiSubscriptionManager> logger,
                    CancellationToken ct
                ) =>
                {
                    logger.LogInformation(
                        "API: SSE subscribe to table {Table}, pk {Pk}",
                        tableName,
                        pkValue
                    );

                    context.Response.Headers.Append("Content-Type", "text/event-stream");
                    context.Response.Headers.Append("Cache-Control", "no-cache");
                    context.Response.Headers.Append("Connection", "keep-alive");

                    var subscriptionId = Guid.NewGuid().ToString();
                    var channel = subscriptions.Subscribe(subscriptionId, tableName, pkValue);

                    try
                    {
                        await context.Response.WriteAsync(
                            $"event: connected\ndata: {{\"subscriptionId\":\"{subscriptionId}\"}}\n\n",
                            ct
                        );
                        await context.Response.Body.FlushAsync(ct);

                        await foreach (var change in channel.Reader.ReadAllAsync(ct))
                        {
                            var json = JsonSerializer.Serialize(change);
                            await context.Response.WriteAsync(
                                $"event: change\ndata: {json}\n\n",
                                ct
                            );
                            await context.Response.Body.FlushAsync(ct);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        logger.LogInformation(
                            "API: SSE subscription {Id} disconnected",
                            subscriptionId
                        );
                    }
                    finally
                    {
                        subscriptions.Unsubscribe(subscriptionId);
                    }
                }
            )
            .RequireRateLimiting("sse");

        app.MapDelete(
                "/sync/subscribe/{subscriptionId}",
                (
                    string subscriptionId,
                    ApiSubscriptionManager subscriptions,
                    ILogger<ApiSubscriptionManager> logger
                ) =>
                {
                    logger.LogInformation("API: Unsubscribe {Id}", subscriptionId);
                    subscriptions.Unsubscribe(subscriptionId);
                    return Results.Ok(new { Unsubscribed = true });
                }
            )
            .RequireRateLimiting("sync");

        return app;
    }

    private static string? ResolveConnectionString(
        string? connectionString,
        string dbType,
        IConfiguration config
    )
    {
        var connStr =
            connectionString ?? config.GetConnectionString(dbType.ToUpperInvariant()) ?? "";
        return string.IsNullOrEmpty(connStr) ? null : connStr;
    }
}
