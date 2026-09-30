using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using Nimblesite.DataProvider.Migration.Core;
using Nimblesite.DataProvider.Migration.SQLite;

namespace Nimblesite.Reporting.Integration.Tests;

/// <summary>
/// Shared E2E fixture: real SQLite DB (YAML migrated) + real Reporting.Api + React static host + Playwright.
/// No mocks. No in-memory databases. The whole stack, running for real.
/// </summary>
public sealed class ReportingE2EFixture : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private IHost? _apiHost;
    private IHost? _frontendHost;

    /// <summary>
    /// Playwright instance shared by all tests.
    /// </summary>
    public IPlaywright? Playwright { get; private set; }

    /// <summary>
    /// Browser instance shared by all tests.
    /// </summary>
    public IBrowser? Browser { get; private set; }

    /// <summary>
    /// URL where Reporting.Api is running.
    /// </summary>
    public string ApiUrl { get; private set; } = "";

    /// <summary>
    /// URL where the React renderer is served.
    /// </summary>
    public string FrontendUrl { get; private set; } = "";

    /// <summary>
    /// SQLite connection string for direct DB verification.
    /// </summary>
    public string ConnectionString => _connectionString;

    public ReportingE2EFixture()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"reporting_e2e_{Guid.NewGuid()}.db");
        _connectionString = $"Data Source={_dbPath}";
    }

    public async Task InitializeAsync()
    {
        Console.WriteLine("[E2E] Setting up reporting E2E test infrastructure...");

        // 1. Create real SQLite database using YAML migration
        CreateDatabaseFromYaml();
        SeedTestData();
        Console.WriteLine($"[E2E] SQLite database created at {_dbPath}");

        // 2. Start Reporting.Api as a real host
        _apiHost = CreateApiHost();
        await _apiHost.StartAsync();
        var apiServer = _apiHost.Services.GetRequiredService<IServer>();
        var apiAddresses = apiServer.Features.Get<IServerAddressesFeature>();
        ApiUrl = apiAddresses!.Addresses.First();
        Console.WriteLine($"[E2E] Reporting.Api started on {ApiUrl}");

        // 3. Verify API is reachable
        await WaitForApiAsync(ApiUrl, "/api/reports");

        // 4. Start frontend static file host for React renderer
        _frontendHost = CreateFrontendHost();
        await _frontendHost.StartAsync();
        var frontendServer = _frontendHost.Services.GetRequiredService<IServer>();
        var frontendAddresses = frontendServer.Features.Get<IServerAddressesFeature>();
        FrontendUrl = frontendAddresses!.Addresses.First();
        Console.WriteLine($"[E2E] React renderer hosted on {FrontendUrl}");

        // 5. Launch Playwright + Chromium
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true }
        );
        Console.WriteLine("[E2E] Playwright browser launched");
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (Browser is not null)
                await Browser.CloseAsync();
        }
        catch
        { /* best-effort cleanup */
        }
        Playwright?.Dispose();

        try
        {
            if (_frontendHost is not null)
                await _frontendHost.StopAsync(TimeSpan.FromSeconds(5));
        }
        catch
        { /* best-effort cleanup */
        }
        _frontendHost?.Dispose();

        try
        {
            if (_apiHost is not null)
                await _apiHost.StopAsync(TimeSpan.FromSeconds(5));
        }
        catch
        { /* best-effort cleanup */
        }
        _apiHost?.Dispose();

        // Cleanup SQLite file
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch (IOException)
            { /* file may be locked */
            }
        }
    }

    /// <summary>
    /// Creates a new Playwright page with console logging attached.
    /// </summary>
    public async Task<IPage> CreatePageAsync()
    {
        var page = await Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER {msg.Type}] {msg.Text}");
        page.PageError += (_, err) => Console.WriteLine($"[PAGE ERROR] {err}");
        return page;
    }

    /// <summary>
    /// Opens a new page and navigates to the frontend with the API URL configured.
    /// </summary>
    public async Task<IPage> CreateReportPageAsync(string reportId = "")
    {
        var page = await CreatePageAsync();

        // Inject the API base URL config before any script runs
        await page.AddInitScriptAsync(
            $@"window.reportConfig = {{
                apiBaseUrl: '{ApiUrl}',
                reportId: '{reportId}'
            }};"
        );

        await page.GotoAsync(FrontendUrl);
        return page;
    }

    private void CreateDatabaseFromYaml()
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "e2e-test-schema.yaml");
        var schema = SchemaYamlSerializer.FromYamlFile(schemaPath);

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        foreach (var table in schema.Tables)
        {
            var ddl = SqliteDdlGenerator.Generate(new CreateTableOperation(table));
            using var cmd = new SqliteCommand(ddl, connection);
            cmd.ExecuteNonQuery();
        }
    }

    private void SeedTestData()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var products = new[]
        {
            ("prod-1", "Alpha Widget", "Widgets", 29.99, 100),
            ("prod-2", "Beta Gadget", "Gadgets", 49.99, 50),
            ("prod-3", "Gamma Widget", "Widgets", 19.99, 200),
            ("prod-4", "Delta Gadget", "Gadgets", 79.99, 25),
            ("prod-5", "Epsilon Doohickey", "Doohickeys", 9.99, 500),
            ("prod-6", "Zeta Widget", "Widgets", 39.99, 75),
        };

        foreach (var (id, name, category, price, stock) in products)
        {
            using var cmd = new SqliteCommand(
                "INSERT INTO products (Id, Name, Category, Price, Stock) VALUES (@id, @name, @category, @price, @stock)",
                connection
            );
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@category", category);
            cmd.Parameters.AddWithValue("@price", price);
            cmd.Parameters.AddWithValue("@stock", stock);
            cmd.ExecuteNonQuery();
        }
    }

    private IHost CreateApiHost()
    {
        // Clear ASPNETCORE_URLS that Microsoft.AspNetCore.Mvc.Testing may have set
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", null);

        var reportsDir = Path.Combine(AppContext.BaseDirectory, "Reports");

        return Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls("http://127.0.0.1:0");
                webBuilder.UseSetting("ConnectionStrings:reporting-db", _connectionString);
                webBuilder.UseSetting("ReportsDirectory", reportsDir);
                webBuilder.UseStartup<ReportingApiStartup>();
            })
            .Build();
    }

    private static IHost CreateFrontendHost()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", null);

        var wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var fileProvider = new PhysicalFileProvider(wwwrootPath);

        return Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls("http://127.0.0.1:0");
                webBuilder.Configure(app =>
                {
                    app.UseCors(policy =>
                        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
                    );
                    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
                    app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
                });
                webBuilder.ConfigureServices(services =>
                {
                    services.AddCors();
                });
            })
            .Build();
    }

    private static async Task WaitForApiAsync(string baseUrl, string endpoint)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (var i = 0; i < 60; i++)
        {
            try
            {
                var response = await client.GetAsync($"{baseUrl}{endpoint}");
                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
                    return;
            }
            catch
            { /* API not ready yet */
            }
            await Task.Delay(500);
        }
        throw new TimeoutException($"Reporting API at {baseUrl} did not start within 30 seconds");
    }
}

/// <summary>
/// Single collection definition for ALL Reporting E2E tests.
/// All tests share ONE fixture instance. Tests run sequentially.
/// </summary>
[CollectionDefinition("Reporting E2E")]
public sealed class ReportingE2ECollection : ICollectionFixture<ReportingE2EFixture>;
