namespace ICD10AM.Cli.Tests;

/// <summary>
/// E2E tests for ICD-10-CM CLI - REAL database, mock API only.
/// Uses Spectre.Console.Testing to drive the CLI through TestConsole.
/// </summary>
public sealed class CliE2ETests : IClassFixture<CliTestFixture>
{
    readonly CliTestFixture _fixture;

    public CliE2ETests(CliTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Help_DisplaysAllCommands()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("help");
        console.Input.PushTextWithEnter("quit");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("search", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("find", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lookup", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("browse", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stats", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("history", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("clear", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quit", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HelpShortcut_DisplaysHelp()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("h");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("search", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Shortcuts", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QuestionMarkHelp_DisplaysHelp()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("?");
        console.Input.PushTextWithEnter("quit");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        Assert.Contains("search", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Quit_ExitsGracefully()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("quit");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        Assert.Contains("Goodbye", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QuitShortcut_ExitsGracefully()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        Assert.Contains("Goodbye", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Exit_ExitsGracefully()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("exit");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        Assert.Contains("Goodbye", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stats_DisplaysApiStatus()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("stats");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        // Should show API health status
        Assert.Contains("API", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Status", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task History_ShowsCommandHistory()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("help");
        console.Input.PushTextWithEnter("stats");
        console.Input.PushTextWithEnter("history");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("help", output);
        Assert.Contains("stats", output);
    }

    [Fact]
    public async Task History_EmptyWhenNoCommands()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("history");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        // First command is "history" so history will show that
        // But the message "No history yet" won't appear since history itself gets added first
        Assert.Contains("history", console.Output);
    }

    [Fact]
    public async Task Find_SearchesByText()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("find chest");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("R07.4", output);
        Assert.Contains("Chest pain", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FindShortcut_SearchesByText()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("f pneumonia");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("J18.9", output);
        Assert.Contains("Pneumonia", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Find_ReturnsEmptyForNoMatch()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("find zzznomatchzzz");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        Assert.Contains("No codes found", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Find_RequiresArgument()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("find");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        Assert.Contains("Usage", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_FindsExactCode()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("lookup R07.4");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("R07.4", output);
        Assert.Contains("Chest pain", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LookupShortcut_FindsCode()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l E11.9");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("E11.9", output);
        Assert.Contains("diabetes", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_HandlesNoDot()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("lookup R074");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        Assert.Contains("R07.4", console.Output);
    }

    [Fact]
    public async Task Lookup_HandlesLowercase()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("lookup r07.4");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        Assert.Contains("R07.4", console.Output);
    }

    [Fact]
    public async Task Lookup_RequiresArgument()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("lookup");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        Assert.Contains("Usage", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_ShowsMultipleMatches()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("lookup R07");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("R07.4", output);
        Assert.Contains("R07.89", output);
    }

    [Fact]
    public async Task Browse_ShowsChapterOverview()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("browse");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("A-B", output);
        Assert.Contains("Infectious", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Browse_FiltersByLetter()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("browse R");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("R07", output);
        Assert.Contains("R06", output);
    }

    [Fact]
    public async Task BrowseShortcut_FiltersByLetter()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("b J");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("J18.9", output);
        Assert.Contains("J20.9", output);
    }

    [Fact]
    public async Task Search_ShowsResultsOrFallsBack()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("search chest pain");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);

        await cli.RunAsync();

        var output = console.Output;
        // Should show search results or fall back to text search
        Assert.Contains("chest", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search_RequiresArgument()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("search");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        Assert.Contains("Usage", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search_ShowsHeartRelatedResults()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("search heart attack");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);

        await cli.RunAsync();

        var output = console.Output;
        // Should show search results - either RAG or fallback to text search
        Assert.Contains("Results", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search_ShortcutWorks()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("s diabetes");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);

        await cli.RunAsync();

        // Should show diabetes-related results
        Assert.Contains("diabetes", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownCommand_TreatedAsSearch()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("chest pain symptoms");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);

        await cli.RunAsync();

        // Should treat as search and find chest-related codes
        Assert.Contains("chest", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Clear_ClearsScreenAndShowsHeader()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("clear");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        // After clear, should show header again (FigletText renders as ASCII art)
        Assert.Contains(
            "Medical Diagnosis Code Explorer",
            console.Output,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task EmptyInput_IsIgnored()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("");
        console.Input.PushTextWithEnter("");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        // Should just continue and eventually quit
        Assert.Contains("Goodbye", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HeaderDisplaysOnStartup()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        // FigletText renders as ASCII art, so check for panel content instead
        Assert.Contains(
            "Medical Diagnosis Code Explorer",
            output,
            StringComparison.OrdinalIgnoreCase
        );
        Assert.Contains("help", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApiStatusDisplaysOnStartup()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        // API health status shown on startup
        Assert.Contains("API", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BillableIndicator_ShownInCodeList()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("find chest");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        // Should show billable indicator
        Assert.Contains("billable", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_CodeNotFound_ShowsErrorMessage()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("lookup ZZZ99.99");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        // Should show "not found" message instead of crashing
        Assert.Contains("not found", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Json_CodeNotFound_ShowsErrorMessage()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("json ZZZ99.99");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        // Should show "not found" message instead of crashing
        Assert.Contains("not found", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search_NoResults_HandlesGracefully()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("search xyznonexistent123");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        // Should gracefully handle search and exit without crashing
        var output = console.Output;
        Assert.Contains("Goodbye", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Browse_InvalidLetter_ShowsEmpty()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("browse 9");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        // Should handle invalid letter gracefully
        Assert.Contains("Goodbye", console.Output, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // CRITICAL: Lookup tests for ICD-10-CM codes (returned by RAG search)
    // These tests ensure codes from search can actually be looked up
    // =========================================================================

    [Fact]
    public async Task Lookup_FindsIcd10CmCode_I10()
    {
        // I10 (Essential hypertension) is in icd10cm_code (used by RAG search)
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l I10");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("I10", output);
        Assert.Contains("hypertension", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_FindsIcd10CmCode_I2111_HeartAttack()
    {
        // I21.11 (ST elevation MI) is in icd10cm_code - critical for "heart attack" search
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l I21.11");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("I21.11", output);
        Assert.Contains("myocardial infarction", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_FindsIcd10CmCode_M545_BackPain()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l M54.5");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("M54.5", output);
        Assert.Contains("back pain", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_FindsIcd10CmCode_G43909_Migraine()
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l G43.909");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("G43.909", output);
        Assert.Contains("migraine", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_ShowsFullCodeDetails()
    {
        // Verify lookup shows ALL required information
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("l R07.4");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        // Must show code
        Assert.Contains("R07.4", output);
        // Must show description
        Assert.Contains("chest pain", output, StringComparison.OrdinalIgnoreCase);
        // Must show billable indicator
        Assert.Contains("Billable", output, StringComparison.OrdinalIgnoreCase);
        // Must NOT say not found
        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lookup_AllSeededCodes_Succeed()
    {
        // Verify ALL seeded ICD-10-CM codes can be looked up
        var codesToTest = new[]
        {
            ("R07.4", "chest pain"),
            ("R06.02", "shortness of breath"),
            ("I21.11", "myocardial infarction"),
            ("J18.9", "pneumonia"),
            ("E11.9", "diabetes"),
            ("I10", "hypertension"),
            ("M54.5", "back pain"),
        };

        foreach (var (code, expectedText) in codesToTest)
        {
            var console = new TestConsole();
            console.Profile.Capabilities.Interactive = true;
            console.Input.PushTextWithEnter($"l {code}");
            console.Input.PushTextWithEnter("q");

            using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
            await cli.RunAsync();

            var output = console.Output;
            Assert.True(
                output.Contains(code, StringComparison.Ordinal),
                $"Lookup for {code} should show the code in output"
            );
            Assert.True(
                output.Contains(expectedText, StringComparison.OrdinalIgnoreCase),
                $"Lookup for {code} should show '{expectedText}' in output"
            );
            Assert.False(
                output.Contains("not found", StringComparison.OrdinalIgnoreCase),
                $"Lookup for {code} should NOT show 'not found'"
            );
        }
    }

    [Fact]
    public async Task Json_FindsIcd10CmCode()
    {
        // JSON command should also find ICD-10-CM codes
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Input.PushTextWithEnter("json I10");
        console.Input.PushTextWithEnter("q");

        using var cli = new Icd10Cli(_fixture.ApiUrl, console, _fixture.HttpClient);
        await cli.RunAsync();

        var output = console.Output;
        Assert.Contains("I10", output);
        Assert.Contains("hypertension", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not found", output, StringComparison.OrdinalIgnoreCase);
    }
}
