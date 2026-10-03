using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nimblesite.Reporting.Api;

namespace Nimblesite.Reporting.Integration.Tests;

/// <summary>
/// Hosts the real Reporting.Api (<see cref="ReportingApi"/>) for the E2E fixture:
/// the same services, bearer auth and endpoints as Reporting.Api/Program.cs.
/// </summary>
public sealed class ReportingApiStartup
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportingApiStartup"/> class.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    public ReportingApiStartup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Configures services for the reporting API.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    public static void ConfigureServices(IServiceCollection services) => services.AddReportingApi();

    /// <summary>
    /// Configures the application middleware and endpoints.
    /// </summary>
    /// <param name="app">Application builder.</param>
    public void Configure(IApplicationBuilder app)
    {
        var logger = app.ApplicationServices.GetRequiredService<ILogger<ReportingApiStartup>>();
        app.UseCors(ReportingApi.CorsPolicy);
        app.UseReportingBearerAuth(_configuration, logger);
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapReportingApi(_configuration, logger));
    }
}
