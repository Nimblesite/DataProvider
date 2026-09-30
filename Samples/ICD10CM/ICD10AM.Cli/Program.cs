using System.Collections.Immutable;
using System.Text.Json;
using RestClient.Net;
using Spectre.Console;
using Spectre.Console.Rendering;
using Urls;

var apiUrl = args.Length > 0 ? args[0] : FindApiUrl();

using var app = new Icd10Cli(apiUrl, AnsiConsole.Console);
await app.RunAsync();
return 0;

static string FindApiUrl()
{
    var envUrl = Environment.GetEnvironmentVariable("ICD10_API_URL");
    return envUrl ?? "http://localhost:5558";
}

/// <summary>
/// ICD-10-AM code from API.
/// </summary>
internal sealed record Icd10Code(
    string Id,
    string Code,
    string ShortDescription,
    string LongDescription,
    long Billable,
    string? CategoryCode,
    string? BlockCode,
    string? ChapterNumber,
    string? ChapterTitle,
    string? InclusionTerms,
    string? ExclusionTerms,
    string? CodeAlso,
    string? CodeFirst
);

/// <summary>
/// RAG search result from API.
/// </summary>
internal sealed record SearchResult(
    string Code,
    string Description,
    string LongDescription,
    double Confidence
);

/// <summary>
/// API search response.
/// </summary>
internal sealed record SearchResponse(
    ImmutableArray<SearchResult> Results,
    string Query,
    string Model
);

/// <summary>
/// Chapter from API.
/// </summary>
internal sealed record Chapter(
    string Id,
    string ChapterNumber,
    string Title,
    string CodeRangeStart,
    string CodeRangeEnd
);

/// <summary>
/// Health response from API.
/// </summary>
internal sealed record HealthResponse(string Status, string Service);

/// <summary>
/// Search request to API.
/// </summary>
internal sealed record SearchRequest(string Query, int? Limit, bool IncludeAchi, string? Format);

/// <summary>
/// Error response from API.
/// </summary>
internal sealed record ErrorResponse(string? Detail);

/// <summary>
/// Beautiful TUI for ICD-10-AM code lookup via API.
/// </summary>
sealed class Icd10Cli : IDisposable
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    readonly HttpClient _httpClient;
    readonly HttpClient? _ownedHttpClient;
    readonly List<string> _history = [];
    readonly string _apiUrl;
    readonly IAnsiConsole _console;

    /// <summary>
    /// Creates CLI with API URL.
    /// </summary>
    public Icd10Cli(string apiUrl, IAnsiConsole console)
        : this(apiUrl, console, null) { }

    /// <summary>
    /// Creates CLI with API URL and optional HTTP client for testing.
    /// </summary>
    public Icd10Cli(string apiUrl, IAnsiConsole console, HttpClient? httpClient)
    {
        _apiUrl = apiUrl.TrimEnd('/');
        _console = console;
        _ownedHttpClient = httpClient is null ? new HttpClient() : null;
        _httpClient = httpClient ?? _ownedHttpClient!;
    }

    /// <summary>
    /// Runs the interactive CLI loop.
    /// </summary>
    public async Task RunAsync()
    {
        RenderHeader();
        await RenderStatsAsync().ConfigureAwait(false);

        while (true)
        {
            _console.WriteLine();
            _console.MarkupLine("[dim]Enter symptoms, condition, or code (h=help, q=quit):[/]");
            var input = _console.Prompt(new TextPrompt<string>("[cyan]>[/]").AllowEmpty());

            if (string.IsNullOrWhiteSpace(input))
                continue;

            _history.Add(input);
            var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var cmd = parts[0].ToLowerInvariant();
            var arg = parts.Length > 1 ? parts[1] : "";

            switch (cmd)
            {
                case "q":
                case "quit":
                case "exit":
                    RenderGoodbye();
                    return;

                case "?":
                case "h":
                case "help":
                    RenderHelp();
                    break;

                case "s":
                case "search":
                    if (string.IsNullOrWhiteSpace(arg))
                        _console.MarkupLine("[yellow]Usage:[/] search <query>");
                    else
                        await SearchAsync(arg).ConfigureAwait(false);
                    break;

                case "l":
                case "lookup":
                    if (string.IsNullOrWhiteSpace(arg))
                        _console.MarkupLine("[yellow]Usage:[/] lookup <code>");
                    else
                        await LookupAsync(arg).ConfigureAwait(false);
                    break;

                case "f":
                case "find":
                    if (string.IsNullOrWhiteSpace(arg))
                        _console.MarkupLine("[yellow]Usage:[/] find <text>");
                    else
                        await FindAsync(arg).ConfigureAwait(false);
                    break;

                case "b":
                case "browse":
                    await BrowseAsync(arg).ConfigureAwait(false);
                    break;

                case "j":
                case "json":
                    if (string.IsNullOrWhiteSpace(arg))
                        _console.MarkupLine("[yellow]Usage:[/] json <code>");
                    else
                        await ShowJsonAsync(arg).ConfigureAwait(false);
                    break;

                case "stats":
                    _console.Clear();
                    _console.MarkupLine("[bold cyan]ICD-10-AM Statistics[/]");
                    _console.Write(new Rule().RuleStyle("grey"));
                    _console.WriteLine();
                    await RenderStatsAsync().ConfigureAwait(false);
                    break;

                case "history":
                    RenderHistory();
                    break;

                case "clear":
                    _console.Clear();
                    RenderHeader();
                    break;

                default:
                    await SearchAsync(input).ConfigureAwait(false);
                    break;
            }
        }
    }

    void RenderHeader()
    {
        var header = new FigletText("ICD-10-AM").Centered().Color(Color.Cyan1);

        _console.Write(header);

        var panel = new Panel(
            "[grey]Medical Diagnosis Code Explorer[/]\n"
                + $"[dim]API: {_apiUrl.EscapeMarkup()}[/]\n"
                + "[dim]Type [cyan]help[/] for commands, or just start typing to search[/]"
        )
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey)
            .Padding(1, 0);

        _console.Write(panel);
    }

    void RenderHelp()
    {
        _console.Clear();
        _console.MarkupLine("[bold cyan]ICD-10-AM Help[/]");
        _console.Write(new Rule().RuleStyle("grey"));
        _console.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[cyan]Command[/]").Width(20))
            .AddColumn(new TableColumn("[cyan]Description[/]"));

        table.AddRow("[green]search[/] [dim]<query>[/]", "Semantic RAG search (AI-powered)");
        table.AddRow("[green]find[/] [dim]<text>[/]", "Text search in descriptions");
        table.AddRow("[green]lookup[/] [dim]<code>[/]", "Direct code lookup (e.g., R07.9)");
        table.AddRow("[green]json[/] [dim]<code>[/]", "Show raw JSON for a code");
        table.AddRow("[green]browse[/]", "Browse chapters");
        table.AddRow("[green]stats[/]", "Show API status");
        table.AddRow("[green]history[/]", "Show command history");
        table.AddRow("[green]clear[/]", "Clear screen");
        table.AddRow("[green]help[/]", "Show this help");
        table.AddRow("[green]quit[/]", "Exit the application");
        table.AddEmptyRow();
        table.AddRow("[dim]<anything else>[/]", "[dim]Treated as search query[/]");

        _console.Write(table);

        _console.MarkupLine(
            "\n[dim]Shortcuts: s=search, f=find, l=lookup, j=json, b=browse, h=help, q=quit[/]"
        );
    }

    async Task RenderStatsAsync()
    {
        var result = await _httpClient
            .GetAsync(
                url: $"{_apiUrl}/health".ToAbsoluteUrl(),
                deserializeSuccess: DeserializeHealth,
                deserializeError: DeserializeError
            )
            .ConfigureAwait(false);

        switch (result)
        {
            case OkHealth(var health):
                var grid = new Grid().AddColumn().AddColumn();
                grid.AddRow(
                    new Panel($"[bold green]✓[/]\n[dim]API Status[/]")
                        .Border(BoxBorder.Rounded)
                        .BorderColor(Color.Green),
                    new Panel($"[bold cyan]{_apiUrl.EscapeMarkup()}[/]\n[dim]Endpoint[/]")
                        .Border(BoxBorder.Rounded)
                        .BorderColor(Color.Cyan1)
                );
                _console.Write(grid);
                break;

            case HealthErrorResponse(ApiErrorResponseError _):
            case HealthErrorResponse(ApiExceptionError _):
                _console.MarkupLine($"[red]API unavailable[/]");
                _console.MarkupLine(
                    $"[yellow]Make sure the API is running at {_apiUrl.EscapeMarkup()}[/]"
                );
                break;
        }
    }

    void RenderHistory()
    {
        _console.Clear();
        _console.MarkupLine("[bold cyan]ICD-10-AM Command History[/]");
        _console.Write(new Rule().RuleStyle("grey"));
        _console.WriteLine();

        if (_history.Count == 0)
        {
            _console.MarkupLine("[dim]No history yet.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[dim]#[/]").RightAligned())
            .AddColumn("[cyan]Command[/]");

        for (var i = 0; i < _history.Count; i++)
        {
            table.AddRow($"[dim]{i + 1}[/]", _history[i].EscapeMarkup());
        }

        _console.Write(table);
    }

    async Task SearchAsync(string query)
    {
        var results = await _console
            .Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync(
                "Searching via AI...",
                async _ =>
                {
                    var requestBody = new SearchRequest(query, 15, false, null);
                    var json = JsonSerializer.Serialize(requestBody, JsonOptions);
                    var content = new StringContent(
                        json,
                        System.Text.Encoding.UTF8,
                        "application/json"
                    );

                    var result = await _httpClient
                        .PostAsync(
                            $"{_apiUrl}/api/search".ToAbsoluteUrl(),
                            content,
                            DeserializeSearchResponse,
                            DeserializeError
                        )
                        .ConfigureAwait(false);

                    return result switch
                    {
                        OkSearch(var response) => response.Results,
                        SearchErrorResponse(ApiErrorResponseError _) => [],
                        SearchErrorResponse(ApiExceptionError _) => [],
                    };
                }
            )
            .ConfigureAwait(false);

        if (results.Length == 0)
        {
            _console.MarkupLine("[yellow]No results. Falling back to text search...[/]");
            await FindAsync(query).ConfigureAwait(false);
            return;
        }

        RenderSearchResults(query, results);
    }

    void RenderSearchResults(string query, ImmutableArray<SearchResult> results)
    {
        _console.Clear();
        _console.MarkupLine("[bold cyan]ICD-10-AM AI Search Results[/]");
        _console.Write(new Rule().RuleStyle("grey"));
        _console.MarkupLine($"[dim]Query:[/] [cyan]{query.EscapeMarkup()}[/]\n");

        if (results.Length == 0)
        {
            _console.MarkupLine("[yellow]No results found.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .AddColumn(new TableColumn("[cyan]Score[/]").Width(7).RightAligned())
            .AddColumn(new TableColumn("[cyan]Code[/]").Width(10))
            .AddColumn(new TableColumn("[cyan]Description[/]"));

        foreach (var r in results)
        {
            var scoreColor = r.Confidence switch
            {
                > 0.8 => "green",
                > 0.6 => "yellow",
                _ => "dim",
            };

            table.AddRow(
                $"[{scoreColor}]{r.Confidence:P0}[/]",
                $"[bold]{r.Code.EscapeMarkup()}[/]",
                Truncate(r.Description, 60).EscapeMarkup()
            );
        }

        _console.Write(table);
        _console.MarkupLine(
            $"[dim]{results.Length} results - type [cyan]l <code>[/] for details[/]"
        );
    }

    async Task FindAsync(string text)
    {
        var codes = await _console
            .Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync(
                "Searching...",
                async _ =>
                {
                    var result = await _httpClient
                        .GetAsync(
                            url: $"{_apiUrl}/api/icd10am/codes?q={Uri.EscapeDataString(text)}&limit=20".ToAbsoluteUrl(),
                            deserializeSuccess: DeserializeCodes,
                            deserializeError: DeserializeError
                        )
                        .ConfigureAwait(false);

                    return result switch
                    {
                        OkCodes(var c) => c,
                        CodesErrorResponse(ApiErrorResponseError _) => [],
                        CodesErrorResponse(ApiExceptionError _) => [],
                    };
                }
            )
            .ConfigureAwait(false);

        RenderCodeList($"Text search: {text}", codes);
    }

    async Task LookupAsync(string code)
    {
        var normalized = code.ToUpperInvariant().Replace(".", "");
        if (normalized.Length > 3)
        {
            normalized = normalized[..3] + "." + normalized[3..];
        }

        var result = await _console
            .Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync(
                "Looking up...",
                async _ =>
                {
                    // Try ICD-10-CM first (matches RAG search results)
                    var r = await _httpClient
                        .GetAsync(
                            url: $"{_apiUrl}/api/icd10cm/codes/{Uri.EscapeDataString(normalized)}".ToAbsoluteUrl(),
                            deserializeSuccess: DeserializeCode,
                            deserializeError: DeserializeError
                        )
                        .ConfigureAwait(false);

                    if (r is OkCode(var cmCode))
                    {
                        return cmCode;
                    }

                    // Fall back to ICD-10-AM
                    var amResult = await _httpClient
                        .GetAsync(
                            url: $"{_apiUrl}/api/icd10am/codes/{Uri.EscapeDataString(normalized)}".ToAbsoluteUrl(),
                            deserializeSuccess: DeserializeCode,
                            deserializeError: DeserializeError
                        )
                        .ConfigureAwait(false);

                    return amResult switch
                    {
                        OkCode(var c) => c,
                        CodeErrorResponse(ApiErrorResponseError _) => null,
                        CodeErrorResponse(ApiExceptionError _) => null,
                    };
                }
            )
            .ConfigureAwait(false);

        if (result is not null)
        {
            RenderCodeDetail(result);
            return;
        }

        // Exact match not found - try searching for codes starting with input
        var codes = await _console
            .Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync(
                "Searching for matching codes...",
                async _ =>
                {
                    // Try ICD-10-CM first
                    var r = await _httpClient
                        .GetAsync(
                            url: $"{_apiUrl}/api/icd10cm/codes?q={Uri.EscapeDataString(normalized)}&limit=20".ToAbsoluteUrl(),
                            deserializeSuccess: DeserializeCodes,
                            deserializeError: DeserializeError
                        )
                        .ConfigureAwait(false);

                    if (r is OkCodes(var cmCodes) && cmCodes.Length > 0)
                    {
                        return cmCodes
                            .Where(x =>
                                x.Code.StartsWith(normalized, StringComparison.OrdinalIgnoreCase)
                            )
                            .ToImmutableArray();
                    }

                    // Fall back to ICD-10-AM
                    var amResult = await _httpClient
                        .GetAsync(
                            url: $"{_apiUrl}/api/icd10am/codes?q={Uri.EscapeDataString(normalized)}&limit=20".ToAbsoluteUrl(),
                            deserializeSuccess: DeserializeCodes,
                            deserializeError: DeserializeError
                        )
                        .ConfigureAwait(false);

                    return amResult switch
                    {
                        OkCodes(var c) =>
                        [
                            .. c.Where(x =>
                                x.Code.StartsWith(normalized, StringComparison.OrdinalIgnoreCase)
                            ),
                        ],
                        CodesErrorResponse(ApiErrorResponseError _) => [],
                        CodesErrorResponse(ApiExceptionError _) => [],
                    };
                }
            )
            .ConfigureAwait(false);

        if (codes.Length > 0)
        {
            RenderCodeList($"Codes matching {code}", codes);
        }
        else
        {
            _console.MarkupLine($"[yellow]Code not found:[/] {code.EscapeMarkup()}");
        }
    }

    async Task BrowseAsync(string letterFilter)
    {
        if (!string.IsNullOrWhiteSpace(letterFilter))
        {
            // Filter codes by starting letter
            var letter = letterFilter.Trim().ToUpperInvariant();
            var codes = await _console
                .Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync(
                    $"Loading codes starting with {letter}...",
                    async _ =>
                    {
                        var result = await _httpClient
                            .GetAsync(
                                url: $"{_apiUrl}/api/icd10am/codes?q={Uri.EscapeDataString(letter)}&limit=50".ToAbsoluteUrl(),
                                deserializeSuccess: DeserializeCodes,
                                deserializeError: DeserializeError
                            )
                            .ConfigureAwait(false);

                        return result switch
                        {
                            OkCodes(var c) => c.Where(code =>
                                    code.Code.StartsWith(letter, StringComparison.OrdinalIgnoreCase)
                                )
                                .ToImmutableArray(),
                            CodesErrorResponse(ApiErrorResponseError _) => [],
                            CodesErrorResponse(ApiExceptionError _) => [],
                        };
                    }
                )
                .ConfigureAwait(false);

            RenderCodeList($"Codes starting with {letter}", codes);
            return;
        }

        var chapters = await _console
            .Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync(
                "Loading chapters...",
                async _ =>
                {
                    var result = await _httpClient
                        .GetAsync(
                            url: $"{_apiUrl}/api/icd10am/chapters".ToAbsoluteUrl(),
                            deserializeSuccess: DeserializeChapters,
                            deserializeError: DeserializeError
                        )
                        .ConfigureAwait(false);

                    return result switch
                    {
                        OkChapters(var c) => c,
                        ChaptersErrorResponse(ApiErrorResponseError _) => [],
                        ChaptersErrorResponse(ApiExceptionError _) => [],
                    };
                }
            )
            .ConfigureAwait(false);

        RenderChapters(chapters);
    }

    void RenderChapters(ImmutableArray<Chapter> chapters)
    {
        _console.Clear();
        _console.MarkupLine("[bold cyan]ICD-10-AM Chapters[/]");
        _console.Write(new Rule().RuleStyle("grey"));
        _console.WriteLine();

        if (chapters.Length == 0)
        {
            _console.MarkupLine("[yellow]No chapters found.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[cyan]#[/]").Width(5))
            .AddColumn(new TableColumn("[cyan]Range[/]").Width(8))
            .AddColumn(new TableColumn("[cyan]Title[/]"));

        foreach (var chapter in chapters)
        {
            var rangeStart =
                chapter.CodeRangeStart.Length > 0 ? chapter.CodeRangeStart[0].ToString() : "";
            var rangeEnd =
                chapter.CodeRangeEnd.Length > 0 ? chapter.CodeRangeEnd[0].ToString() : "";
            var range = rangeStart == rangeEnd ? rangeStart : $"{rangeStart}-{rangeEnd}";

            table.AddRow(
                $"[bold]{chapter.ChapterNumber.EscapeMarkup()}[/]",
                $"[green]{range}[/]",
                chapter.Title.EscapeMarkup()
            );
        }

        _console.Write(table);
        _console.MarkupLine(
            $"\n[dim]{chapters.Length} chapters - type [cyan]b <letter>[/] to browse codes[/]"
        );
    }

    void RenderCodeList(string title, ImmutableArray<Icd10Code> codes)
    {
        _console.Clear();
        _console.MarkupLine("[bold cyan]ICD-10-AM Results[/]");
        _console.Write(new Rule().RuleStyle("grey"));
        _console.MarkupLine($"[dim]{title.EscapeMarkup()}[/]\n");

        if (codes.Length == 0)
        {
            _console.MarkupLine("[yellow]No codes found.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[cyan]Code[/]").Width(10))
            .AddColumn(new TableColumn("[cyan]Description[/]"))
            .AddColumn(new TableColumn("[cyan]$[/]").Width(3).Centered());

        foreach (var code in codes)
        {
            table.AddRow(
                $"[bold]{code.Code.EscapeMarkup()}[/]",
                Truncate(code.ShortDescription, 55).EscapeMarkup(),
                code.Billable == 1 ? "[green]✓[/]" : "[dim]-[/]"
            );
        }

        _console.Write(table);
        _console.MarkupLine(
            $"[dim]{codes.Length} codes ([green]✓[/] = billable) - type [cyan]l <code>[/] for details[/]"
        );
    }

    void RenderCodeDetail(Icd10Code code)
    {
        _console.Clear();
        _console.MarkupLine("[bold cyan]ICD-10-AM Code Detail[/]");
        _console.Write(new Rule().RuleStyle("grey"));
        _console.WriteLine();

        var rows = new List<IRenderable>
        {
            new Markup($"[bold cyan]{code.Code.EscapeMarkup()}[/]"),
            new Rule().RuleStyle("grey"),
            new Markup($"[bold]{code.ShortDescription.EscapeMarkup()}[/]"),
            new Text(""),
            new Markup($"[dim]{code.LongDescription.EscapeMarkup()}[/]"),
        };

        if (!string.IsNullOrWhiteSpace(code.ChapterNumber))
        {
            rows.Add(new Text(""));
            rows.Add(new Markup("[magenta]Chapter:[/]"));
            rows.Add(
                new Markup(
                    $"[dim]{code.ChapterNumber.EscapeMarkup()} - {(code.ChapterTitle ?? "").EscapeMarkup()}[/]"
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(code.BlockCode))
        {
            rows.Add(new Text(""));
            rows.Add(new Markup("[green]Block:[/]"));
            rows.Add(new Markup($"[dim]{code.BlockCode.EscapeMarkup()}[/]"));
        }

        if (!string.IsNullOrWhiteSpace(code.CategoryCode))
        {
            rows.Add(new Text(""));
            rows.Add(new Markup("[yellow]Category:[/]"));
            rows.Add(new Markup($"[dim]{code.CategoryCode.EscapeMarkup()}[/]"));
        }

        if (!string.IsNullOrWhiteSpace(code.InclusionTerms))
        {
            rows.Add(new Text(""));
            rows.Add(new Markup("[blue]Includes:[/]"));
            rows.Add(new Markup($"[dim]{code.InclusionTerms.EscapeMarkup()}[/]"));
        }

        if (!string.IsNullOrWhiteSpace(code.ExclusionTerms))
        {
            rows.Add(new Text(""));
            rows.Add(new Markup("[red]Excludes:[/]"));
            rows.Add(new Markup($"[dim]{code.ExclusionTerms.EscapeMarkup()}[/]"));
        }

        if (!string.IsNullOrWhiteSpace(code.CodeAlso))
        {
            rows.Add(new Text(""));
            rows.Add(new Markup("[olive]Code Also:[/]"));
            rows.Add(new Markup($"[dim]{code.CodeAlso.EscapeMarkup()}[/]"));
        }

        if (!string.IsNullOrWhiteSpace(code.CodeFirst))
        {
            rows.Add(new Text(""));
            rows.Add(new Markup("[orange3]Code First:[/]"));
            rows.Add(new Markup($"[dim]{code.CodeFirst.EscapeMarkup()}[/]"));
        }

        rows.Add(new Text(""));
        rows.Add(
            new Markup(
                code.Billable == 1
                    ? "[green]✓ Billable[/]"
                    : "[yellow]Not directly billable (category code)[/]"
            )
        );

        var panel = new Panel(new Rows(rows))
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Header("[cyan] Code Detail [/]")
            .Padding(1, 1);

        _console.Write(panel);
    }

    async Task ShowJsonAsync(string code)
    {
        var normalized = code.ToUpperInvariant().Replace(".", "");
        if (normalized.Length > 3)
        {
            normalized = normalized[..3] + "." + normalized[3..];
        }

        // Try ICD-10-CM first (matches RAG search results)
        var result = await _httpClient
            .GetAsync(
                url: $"{_apiUrl}/api/icd10cm/codes/{Uri.EscapeDataString(normalized)}".ToAbsoluteUrl(),
                deserializeSuccess: DeserializeCode,
                deserializeError: DeserializeError
            )
            .ConfigureAwait(false);

        if (result is not OkCode)
        {
            // Fall back to ICD-10-AM
            result = await _httpClient
                .GetAsync(
                    url: $"{_apiUrl}/api/icd10am/codes/{Uri.EscapeDataString(normalized)}".ToAbsoluteUrl(),
                    deserializeSuccess: DeserializeCode,
                    deserializeError: DeserializeError
                )
                .ConfigureAwait(false);
        }

        switch (result)
        {
            case OkCode(var c):
                var json = JsonSerializer.Serialize(c, JsonOptions);
                _console.Clear();
                _console.MarkupLine("[bold cyan]ICD-10 JSON[/]");
                _console.Write(new Rule().RuleStyle("grey"));
                _console.MarkupLine($"[dim]Code:[/] [cyan]{c.Code.EscapeMarkup()}[/]\n");
                _console.Write(new Panel(json).Border(BoxBorder.Rounded).BorderColor(Color.Grey));
                break;

            case CodeErrorResponse(ApiErrorResponseError _):
            case CodeErrorResponse(ApiExceptionError _):
                _console.MarkupLine($"[yellow]Code not found:[/] {code.EscapeMarkup()}");
                break;
        }
    }

    void RenderGoodbye()
    {
        _console.WriteLine();
        _console.Write(new Rule("[cyan]Goodbye![/]").RuleStyle("grey"));
    }

    static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";

    // Deserialization helpers
    static async Task<HealthResponse?> DeserializeHealth(
        HttpResponseMessage r,
        CancellationToken ct
    ) =>
        await JsonSerializer
            .DeserializeAsync<HealthResponse>(
                await r.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                JsonOptions,
                ct
            )
            .ConfigureAwait(false);

    static async Task<SearchResponse?> DeserializeSearchResponse(
        HttpResponseMessage r,
        CancellationToken ct
    ) =>
        await JsonSerializer
            .DeserializeAsync<SearchResponse>(
                await r.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                JsonOptions,
                ct
            )
            .ConfigureAwait(false);

    static async Task<ImmutableArray<Icd10Code>> DeserializeCodes(
        HttpResponseMessage r,
        CancellationToken ct
    )
    {
        var codes = await JsonSerializer
            .DeserializeAsync<Icd10Code[]>(
                await r.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                JsonOptions,
                ct
            )
            .ConfigureAwait(false);
        return codes is null ? [] : [.. codes];
    }

    static async Task<Icd10Code?> DeserializeCode(HttpResponseMessage r, CancellationToken ct) =>
        await JsonSerializer
            .DeserializeAsync<Icd10Code>(
                await r.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                JsonOptions,
                ct
            )
            .ConfigureAwait(false);

    static async Task<ImmutableArray<Chapter>> DeserializeChapters(
        HttpResponseMessage r,
        CancellationToken ct
    )
    {
        var chapters = await JsonSerializer
            .DeserializeAsync<Chapter[]>(
                await r.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                JsonOptions,
                ct
            )
            .ConfigureAwait(false);
        return chapters is null ? [] : [.. chapters];
    }

    static async Task<ErrorResponse?> DeserializeError(HttpResponseMessage r, CancellationToken ct)
    {
        try
        {
            var stream = await r.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            return stream.Length == 0
                ? null
                : await JsonSerializer
                    .DeserializeAsync<ErrorResponse>(stream, JsonOptions, ct)
                    .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Dispose() => _ownedHttpClient?.Dispose();
}
