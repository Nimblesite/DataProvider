using Nimblesite.Reporting.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddReportingApi();

var app = builder.Build();

app.UseCors(ReportingApi.CorsPolicy);
app.UseReportingBearerAuth(app.Configuration, app.Logger);

// Serve static files for the React renderer
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapReportingApi(app.Configuration, app.Logger);

app.Run();

/// <summary>
/// Partial class to allow test access via WebApplicationFactory.
/// </summary>
public partial class Program { }
