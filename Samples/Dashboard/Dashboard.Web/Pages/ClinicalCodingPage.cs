using System;
using Dashboard.Api;
using Dashboard.Components;
using Dashboard.Models;
using Dashboard.React;
using static Dashboard.React.Elements;
using static Dashboard.React.Hooks;

namespace Dashboard.Pages
{
    /// <summary>
    /// Clinical coding page state.
    /// </summary>
    public class ClinicalCodingState
    {
        /// <summary>Current search query.</summary>
        public string SearchQuery { get; set; }

        /// <summary>Active search mode (keyword, semantic, lookup).</summary>
        public string SearchMode { get; set; }

        /// <summary>ICD-10 search results.</summary>
        public Icd10Code[] Icd10Results { get; set; }

        /// <summary>ACHI search results.</summary>
        public AchiCode[] AchiResults { get; set; }

        /// <summary>Semantic search results.</summary>
        public SemanticSearchResult[] SemanticResults { get; set; }

        /// <summary>Selected code for detail view.</summary>
        public Icd10Code SelectedCode { get; set; }

        /// <summary>Whether loading.</summary>
        public bool Loading { get; set; }

        /// <summary>Error message if any.</summary>
        public string Error { get; set; }

        /// <summary>Whether to include ACHI in semantic search.</summary>
        public bool IncludeAchi { get; set; }

        /// <summary>Copied code for feedback.</summary>
        public string CopiedCode { get; set; }
    }

    /// <summary>
    /// Clinical coding page for ICD-10 code lookup and search.
    /// </summary>
    public static class ClinicalCodingPage
    {
        /// <summary>
        /// Renders the clinical coding page.
        /// </summary>
        public static ReactElement Render()
        {
            var stateResult = UseState(
                new ClinicalCodingState
                {
                    SearchQuery = "",
                    SearchMode = "keyword",
                    Icd10Results = new Icd10Code[0],
                    AchiResults = new AchiCode[0],
                    SemanticResults = new SemanticSearchResult[0],
                    SelectedCode = null,
                    Loading = false,
                    Error = null,
                    IncludeAchi = false,
                    CopiedCode = null,
                }
            );

            var state = stateResult.State;
            var setState = stateResult.SetState;

            return Div(
                className: "page clinical-coding-page",
                children: new[]
                {
                    RenderHeader(),
                    RenderSearchSection(state, setState),
                    RenderContent(state, setState),
                }
            );
        }

        private static ReactElement RenderHeader() =>
            Div(
                className: "page-header",
                children: new[]
                {
                    Div(
                        className: "flex items-center gap-3",
                        children: new[]
                        {
                            Div(
                                className: "page-header-icon",
                                style: new
                                {
                                    background = "linear-gradient(135deg, #3b82f6, #8b5cf6)",
                                    borderRadius = "12px",
                                    padding = "12px",
                                    display = "flex",
                                    alignItems = "center",
                                    justifyContent = "center",
                                },
                                children: new[] { Icons.Code() }
                            ),
                            Div(
                                children: new[]
                                {
                                    H(
                                        2,
                                        className: "page-title",
                                        children: new[] { Text("Clinical Coding") }
                                    ),
                                    P(
                                        className: "page-description",
                                        children: new[]
                                        {
                                            Text(
                                                "Search ICD-10-AM diagnosis codes and ACHI procedure codes"
                                            ),
                                        }
                                    ),
                                }
                            ),
                        }
                    ),
                }
            );

        private static ReactElement RenderSearchSection(
            ClinicalCodingState state,
            Action<ClinicalCodingState> setState
        ) =>
            Div(
                className: "card mb-6",
                style: new { padding = "24px" },
                children: new[]
                {
                    RenderSearchTabs(state, setState),
                    RenderSearchInput(state, setState),
                    RenderSearchOptions(state, setState),
                }
            );

        private static ReactElement RenderSearchTabs(
            ClinicalCodingState state,
            Action<ClinicalCodingState> setState
        ) =>
            Div(
                className: "flex gap-2 mb-4",
                children: new[]
                {
                    RenderTab(
                        label: "Keyword Search",
                        icon: Icons.Search,
                        isActive: state.SearchMode == "keyword",
                        onClick: () => SetSearchMode(state, setState, mode: "keyword")
                    ),
                    RenderTab(
                        label: "AI Search",
                        icon: Icons.Sparkles,
                        isActive: state.SearchMode == "semantic",
                        onClick: () => SetSearchMode(state, setState, mode: "semantic")
                    ),
                    RenderTab(
                        label: "Code Lookup",
                        icon: Icons.FileText,
                        isActive: state.SearchMode == "lookup",
                        onClick: () => SetSearchMode(state, setState, mode: "lookup")
                    ),
                }
            );

        private static ReactElement RenderTab(
            string label,
            Func<ReactElement> icon,
            bool isActive,
            Action onClick
        ) =>
            Button(
                className: "btn " + (isActive ? "btn-primary" : "btn-secondary"),
                onClick: onClick,
                children: new[] { icon(), Text(label) }
            );

        private static void SetSearchMode(
            ClinicalCodingState state,
            Action<ClinicalCodingState> setState,
            string mode
        )
        {
            setState(
                new ClinicalCodingState
                {
                    SearchQuery = state.SearchQuery,
                    SearchMode = mode,
                    Icd10Results = new Icd10Code[0],
                    AchiResults = new AchiCode[0],
                    SemanticResults = new SemanticSearchResult[0],
                    SelectedCode = null,
                    Loading = false,
                    Error = null,
                    IncludeAchi = state.IncludeAchi,
                    CopiedCode = null,
                }
            );
        }

        private static ReactElement RenderSearchInput(
            ClinicalCodingState state,
            Action<ClinicalCodingState> setState
        )
        {
            var placeholder = GetPlaceholder(state.SearchMode);

            return Div(
                className: "flex gap-4",
                children: new[]
                {
                    Div(
                        className: "flex-1 search-input search-input-lg",
                        children: new[]
                        {
                            Span(className: "search-icon", children: new[] { Icons.Search() }),
                            Input(
                                className: "input input-lg",
                                type: "text",
                                placeholder: placeholder,
                                value: state.SearchQuery,
                                onChange: query => UpdateQuery(state, setState, query: query),
                                onKeyDown: key =>
                                {
                                    if (key == "Enter")
                                        ExecuteSearch(state, setState);
                                }
                            ),
                        }
                    ),
                    Button(
                        className: "btn btn-primary btn-lg",
                        onClick: () => ExecuteSearch(state, setState),
                        children: new[]
                        {
                            state.Loading ? Icons.Refresh() : Icons.Search(),
                            Text(state.Loading ? "Searching..." : "Search"),
                        }
                    ),
                }
            );
        }

        private static string GetPlaceholder(string mode)
        {
            if (mode == "keyword")
                return "Search by code, description, or keywords (e.g., 'diabetes', 'fracture')";
            if (mode == "semantic")
                return "Describe symptoms or conditions in natural language...";
            return "Enter exact ICD-10 code (e.g., 'E11.9', 'J18.9')";
        }

        private static void UpdateQuery(
            ClinicalCodingState state,
            Action<ClinicalCodingState> setState,
            string query
        )
        {
            setState(
                new ClinicalCodingState
                {
                    SearchQuery = query,
                    SearchMode = state.SearchMode,
                    Icd10Results = state.Icd10Results,
                    AchiResults = state.AchiResults,
                    SemanticResults = state.SemanticResults,
                    SelectedCode = state.SelectedCode,
                    Loading = state.Loading,
                    Error = state.Error,
                    IncludeAchi = state.IncludeAchi,
                    CopiedCode = state.CopiedCode,
                }
            );
        }

        private static ReactElement RenderSearchOptions(
            ClinicalCodingState state,
            Action<ClinicalCodingState> setState
        )
        {
            if (state.SearchMode != "semantic")
                return Text("");

            return Div(
                className: "flex items-center gap-4 mt-4",
                children: new[]
                {
                    Label(
                        className: "flex items-center gap-2 cursor-pointer",
                        children: new[]
                        {
                            Input(
                                className: "checkbox",
                                type: "checkbox",
                                value: state.IncludeAchi ? "true" : "",
                                onChange: _ => ToggleAchi(state, setState)
                            ),
                            Span(children: new[] { Text("Include ACHI procedure codes") }),
                        }
                    ),
                    Span(
                        className: "text-sm text-gray-500",
                        children: new[]
                        {
                            Icons.Sparkles(),
                            Text(" Powered by medical AI embeddings"),
                        }
                    ),
                }
            );
        }

        private static void ToggleAchi(
            ClinicalCodingState state,
            Action<ClinicalCodingState> setState
        )
        {
            setState(
                new ClinicalCodingState
                {
                    SearchQuery = state.SearchQuery,
                    SearchMode = state.SearchMode,
                    Icd10Results = state.Icd10Results,
                    AchiResults = state.AchiResults,
                    SemanticResults = state.SemanticResults,
                    SelectedCode = state.SelectedCode,
                    Loading = state.Loading,
                    Error = state.Error,
                    IncludeAchi = !state.IncludeAchi,
                    CopiedCode = state.CopiedCode,
                }
            );
        }

        private static async void ExecuteSearch(
            ClinicalCodingState state,
            Action<ClinicalCodingState> setState
        )
        {
            if (string.IsNullOrWhiteSpace(state.SearchQuery))
                return;

            setState(
                new ClinicalCodingState
                {
                    SearchQuery = state.SearchQuery,
                    SearchMode = state.SearchMode,
                    Icd10Results = new Icd10Code[0],
                    AchiResults = new AchiCode[0],
                    SemanticResults = new SemanticSearchResult[0],
                    SelectedCode = null,
                    Loading = true,
                    Error = null,
                    IncludeAchi = state.IncludeAchi,
                    CopiedCode = null,
                }
            );

            try
            {
                if (state.SearchMode == "keyword")
                {
                    var results = await ApiClient.SearchIcd10CodesAsync(
                        query: state.SearchQuery,
                        limit: 50
                    );
                    setState(
                        new ClinicalCodingState
                        {
                            SearchQuery = state.SearchQuery,
                            SearchMode = state.SearchMode,
                            Icd10Results = results,
                            AchiResults = new AchiCode[0],
                            SemanticResults = new SemanticSearchResult[0],
                            SelectedCode = null,
                            Loading = false,
                            Error = null,
                            IncludeAchi = state.IncludeAchi,
                            CopiedCode = null,
                        }
                    );
                }
                else if (state.SearchMode == "semantic")
                {
                    var results = await ApiClient.SemanticSearchAsync(
                        query: state.SearchQuery,
                        limit: 20,
                        includeAchi: state.IncludeAchi
                    );
                    setState(
                        new ClinicalCodingState
                        {
                            SearchQuery = state.SearchQuery,
                            SearchMode = state.SearchMode,
                            Icd10Results = new Icd10Code[0],
                            AchiResults = new AchiCode[0],
                            SemanticResults = results,
                            SelectedCode = null,
                            Loading = false,
                            Error = null,
                            IncludeAchi = state.IncludeAchi,
                            CopiedCode = null,
                        }
                    );
                }
                else
                {
                    var code = await ApiClient.GetIcd10CodeAsync(code: state.SearchQuery);
                    setState(
                        new ClinicalCodingState
                        {
                            SearchQuery = state.SearchQuery,
                            SearchMode = state.SearchMode,
                            Icd10Results = new Icd10Code[0],
                            AchiResults = new AchiCode[0],
                            SemanticResults = new SemanticSearchResult[0],
                            SelectedCode = code,
                            Loading = false,
                            Error = null,
                            IncludeAchi = state.IncludeAchi,
                            CopiedCode = null,
                        }
                    );
                }
            }
            catch (Exception ex)
            {
                setState(
                    new ClinicalCodingState
                    {
                        SearchQuery = state.SearchQuery,
                        SearchMode = state.SearchMode,
                        Icd10Results = new Icd10Code[0],
                        AchiResults = new AchiCode[0],
                        SemanticResults = new SemanticSearchResult[0],
                        SelectedCode = null,
                        Loading = false,
                        Error = ex.Message,
                        IncludeAchi = state.IncludeAchi,
                        CopiedCode = null,
                    }
                );
            }
        }

        private static ReactElement RenderContent(
            ClinicalCodingState state,
            Action<ClinicalCodingState> setState
        )
        {
            if (state.Loading)
                return RenderLoading();

            if (state.Error != null)
                return RenderError(state.Error);

            if (state.SelectedCode != null)
                return RenderCodeDetail(state, setState);

            if (state.SemanticResults.Length > 0)
                return RenderSemanticResults(state, setState);

            if (state.Icd10Results.Length > 0)
                return RenderKeywordResults(state, setState);

            return RenderEmptyState(state);
        }

        private static ReactElement RenderLoading() =>
            Div(
                className: "card",
                children: new[]
                {
                    Div(
                        className: "flex items-center justify-center p-12",
                        children: new[]
                        {
                            Div(
                                className: "loading-spinner",
                                style: new
                                {
                                    width = "48px",
                                    height = "48px",
                                    border = "4px solid #e5e7eb",
                                    borderTop = "4px solid #3b82f6",
                                    borderRadius = "50%",
                                    animation = "spin 1s linear infinite",
                                }
                            ),
                        }
                    ),
                }
            );

        private static ReactElement RenderError(string error) =>
            Div(
                className: "card",
                style: new { borderLeft = "4px solid var(--error)" },
                children: new[]
                {
                    Div(
                        className: "flex items-center gap-3 p-4",
                        children: new[]
                        {
                            Icons.X(),
                            Div(
                                children: new[]
                                {
                                    H(
                                        4,
                                        className: "font-semibold",
                                        children: new[] { Text("Search Error") }
                                    ),
                                    P(
                                        className: "text-sm text-gray-600",
                                        children: new[] { Text(error) }
                                    ),
                                    P(
                                        className: "text-sm text-gray-500 mt-2",
                                        children: new[]
                                        {
                                            Text(
                                                "Make sure the ICD-10 API (port 5090) is running."
                                            ),
                                        }
                                    ),
                                }
                            ),
                        }
                    ),
                }
            );

        private static ReactElement RenderEmptyState(ClinicalCodingState state)
        {
            var title = GetEmptyTitle(state.SearchMode);
            var description = GetEmptyDescription(state.SearchMode);

            return Div(
                className: "card",
                children: new[]
                {
                    Div(
                        className: "empty-state",
                        children: new[]
                        {
                            Div(
                                style: new
                                {
                                    background = "linear-gradient(135deg, #3b82f6, #8b5cf6)",
                                    borderRadius = "16px",
                                    padding = "20px",
                                    marginBottom = "16px",
                                },
                                children: new[] { Icons.Code() }
                            ),
                            H(4, className: "empty-state-title", children: new[] { Text(title) }),
                            P(
                                className: "empty-state-description",
                                children: new[] { Text(description) }
                            ),
                            RenderQuickSearches(state),
                        }
                    ),
                }
            );
        }

        private static string GetEmptyTitle(string mode)
        {
            if (mode == "semantic")
                return "AI-Powered Code Search";
            if (mode == "lookup")
                return "Direct Code Lookup";
            return "ICD-10-AM Code Search";
        }

        private static string GetEmptyDescription(string mode)
        {
            if (mode == "semantic")
                return "Describe symptoms in natural language and let AI find the right codes.";
            if (mode == "lookup")
                return "Enter an exact ICD-10 or ACHI code to view its full details.";
            return "Search diagnosis codes by keyword, description, or code fragment.";
        }

        private static ReactElement RenderQuickSearches(ClinicalCodingState state)
        {
            if (state.SearchMode == "lookup")
                return Text("");

            string[] examples;
            if (state.SearchMode == "semantic")
            {
                examples = new[]
                {
                    "Patient with chest pain and shortness of breath",
                    "Type 2 diabetes with kidney complications",
                    "Broken arm from fall",
                    "Chronic lower back pain",
                };
            }
            else
            {
                examples = new[] { "diabetes", "pneumonia", "fracture", "hypertension" };
            }

            var buttons = new ReactElement[examples.Length];
            for (int i = 0; i < examples.Length; i++)
            {
                var example = examples[i];
                buttons[i] = Button(
                    className: "btn btn-ghost btn-sm",
                    onClick: () => { },
                    children: new[] { Text(example) }
                );
            }

            return Div(
                className: "flex flex-wrap gap-2 mt-4",
                children: new ReactElement[]
                {
                    Span(className: "text-sm text-gray-500", children: new[] { Text("Try: ") }),
                }.Concat(buttons)
            );
        }

        private static ReactElement[] Concat(this ReactElement[] arr1, ReactElement[] arr2)
        {
            var result = new ReactElement[arr1.Length + arr2.Length];
            for (int i = 0; i < arr1.Length; i++)
                result[i] = arr1[i];
            for (int i = 0; i < arr2.Length; i++)
                result[arr1.Length + i] = arr2[i];
            return result;
        }

        private static ReactElement RenderKeywordResults(
            ClinicalCodingState state,
            Action<ClinicalCodingState> setState
        )
        {
            var resultRows = new ReactElement[state.Icd10Results.Length];
            for (int i = 0; i < state.Icd10Results.Length; i++)
            {
                var code = state.Icd10Results[i];
                resultRows[i] = RenderCodeRow(code, state, setState);
            }

            return Div(
                children: new[]
                {
                    Div(
                        className: "flex items-center justify-between mb-4",
                        children: new[]
                        {
                            Span(
                                className: "text-sm text-gray-600",
                                children: new[]
                                {
                                    Text(state.Icd10Results.Length + " results found"),
                                }
                            ),
                        }
                    ),
                    Div(
                        className: "table-container",
                        children: new[]
                        {
                            Table(
                                className: "table",
                                children: new[]
                                {
                                    THead(
                                        Tr(
                                            children: new[]
                                            {
                                                Th(children: new[] { Text("Code") }),
                                                Th(children: new[] { Text("Description") }),
                                                Th(children: new[] { Text("Chapter") }),
                                                Th(children: new[] { Text("Category") }),
                                                Th(children: new[] { Text("Status") }),
                                                Th(children: new[] { Text("") }),
                                            }
                                        )
                                    ),
                                    TBody(resultRows),
                                }
                            ),
                        }
                    ),
                }
            );
        }

        private static ReactElement RenderCodeRow(
            Icd10Code code,
            ClinicalCodingState state,
            Action<ClinicalCodingState> setState
        ) =>
            Tr(
                className: "search-result-row",
                onClick: () => SelectCode(code, state, setState),
                children: new[]
                {
                    Td(
                        children: new[]
                        {
                            Span(
                                className: "badge badge-primary",
                                style: new
                                {
                                    background = "linear-gradient(135deg, #3b82f6, #8b5cf6)",
                                    color = "white",
                                    fontWeight = "600",
                                },
                                children: new[] { Text(code.Code) }
                            ),
                        }
                    ),
                    Td(
                        className: "result-description-cell",
                        children: new[]
                        {
                            Span(children: new[] { Text(code.ShortDescription ?? "") }),
                            Div(
                                className: "result-tooltip",
                                children: new[]
                                {
                                    H(
                                        4,
                                        className: "font-semibold mb-2",
                                        children: new[] { Text(code.ShortDescription ?? "") }
                                    ),
                                    P(
                                        className: "text-sm text-gray-600 mb-3",
                                        children: new[]
                                        {
                                            Text(
                                                code.LongDescription ?? code.ShortDescription ?? ""
                                            ),
                                        }
                                    ),
                                    !string.IsNullOrEmpty(code.InclusionTerms)
                                        ? Div(
                                            className: "text-xs text-gray-500 mb-2",
                                            children: new[]
                                            {
                                                Span(
                                                    className: "font-semibold",
                                                    children: new[] { Text("Includes: ") }
                                                ),
                                                Text(code.InclusionTerms),
                                            }
                                        )
                                        : Text(""),
                                    !string.IsNullOrEmpty(code.ExclusionTerms)
                                        ? Div(
                                            className: "text-xs text-gray-500",
                                            children: new[]
                                            {
                                                Span(
                                                    className: "font-semibold",
                                                    children: new[] { Text("Excludes: ") }
                                                ),
                                                Text(code.ExclusionTerms),
                                            }
                                        )
                                        : Text(""),
                                }
                            ),
                        }
                    ),
                    Td(
                        className: "text-sm text-gray-600",
                        children: new[] { Text("Ch. " + code.ChapterNumber) }
                    ),
                    Td(
                        className: "text-sm text-gray-600",
                        children: new[] { Text(code.CategoryCode ?? "") }
                    ),
                    Td(
                        children: new[]
                        {
                            code.Billable
                                ? Span(
                                    className: "badge badge-success",
                                    children: new[] { Text("Billable") }
                                )
                                : Span(
                                    className: "badge badge-gray",
                                    children: new[] { Text("Non-billable") }
                                ),
                        }
                    ),
                    Td(
                        children: new[]
                        {
                            Button(
                                className: "btn btn-ghost btn-sm",
                                onClick: () => CopyCode(code.Code, state, setState),
                                children: new[]
                                {
                                    state.CopiedCode == code.Code ? Icons.Check() : Icons.Copy(),
                                }
                            ),
                        }
                    ),
                }
            );

        private static void SelectCode(
            Icd10Code code,
            ClinicalCodingState state,
            Action<ClinicalCodingState> setState
        )
        {
            setState(
                new ClinicalCodingState
                {
                    SearchQuery = state.SearchQuery,
                    SearchMode = state.SearchMode,
                    Icd10Results = state.Icd10Results,
                    AchiResults = state.AchiResults,
                    SemanticResults = state.SemanticResults,
                    SelectedCode = code,
                    Loading = false,
                    Error = null,
                    IncludeAchi = state.IncludeAchi,
                    CopiedCode = state.CopiedCode,
                }
            );
        }

        private static void CopyCode(
            string code,
            ClinicalCodingState state,
            Action<ClinicalCodingState> setState
        )
        {
            H5.Script.Call<object>("navigator.clipboard.writeText", code);
            setState(
                new ClinicalCodingState
                {
                    SearchQuery = state.SearchQuery,
                    SearchMode = state.SearchMode,
                    Icd10Results = state.Icd10Results,
                    AchiResults = state.AchiResults,
                    SemanticResults = state.SemanticResults,
                    SelectedCode = state.SelectedCode,
                    Loading = state.Loading,
                    Error = state.Error,
                    IncludeAchi = state.IncludeAchi,
                    CopiedCode = code,
                }
            );
        }

        private static ReactElement RenderSemanticResults(
            ClinicalCodingState state,
            Action<ClinicalCodingState> setState
        )
        {
            var resultRows = new ReactElement[state.SemanticResults.Length];
            for (int i = 0; i < state.SemanticResults.Length; i++)
            {
                var result = state.SemanticResults[i];
                resultRows[i] = RenderSemanticRow(result, state, setState);
            }

            return Div(
                children: new[]
                {
                    Div(
                        className: "flex items-center justify-between mb-4",
                        children: new[]
                        {
                            Div(
                                className: "flex items-center gap-2",
                                children: new[]
                                {
                                    Icons.Sparkles(),
                                    Span(
                                        className: "text-sm text-gray-600",
                                        children: new[]
                                        {
                                            Text(
                                                state.SemanticResults.Length + " AI-matched results"
                                            ),
                                        }
                                    ),
                                }
                            ),
                        }
                    ),
                    Div(
                        className: "table-container",
                        children: new[]
                        {
                            Table(
                                className: "table",
                                children: new[]
                                {
                                    THead(
                                        Tr(
                                            children: new[]
                                            {
                                                Th(children: new[] { Text("Code") }),
                                                Th(children: new[] { Text("Type") }),
                                                Th(children: new[] { Text("Description") }),
                                                Th(children: new[] { Text("Confidence") }),
                                            }
                                        )
                                    ),
                                    TBody(resultRows),
                                }
                            ),
                        }
                    ),
                }
            );
        }

        private static ReactElement RenderSemanticRow(
            SemanticSearchResult result,
            ClinicalCodingState state,
            Action<ClinicalCodingState> setState
        )
        {
            var confidencePercent = (int)(result.Confidence * 100);
            var confidenceColor =
                confidencePercent >= 80 ? "#22c55e"
                : confidencePercent >= 60 ? "#f59e0b"
                : "#ef4444";
            var badgeClass =
                confidencePercent >= 80 ? "badge-success"
                : confidencePercent >= 60 ? "badge-warning"
                : "badge-error";

            return Tr(
                className: "search-result-row",
                onClick: () => LookupSemanticCode(result.Code, state, setState),
                children: new[]
                {
                    Td(
                        children: new[]
                        {
                            Span(
                                className: "badge badge-primary",
                                style: new
                                {
                                    background = result.CodeType == "ACHI"
                                        ? "linear-gradient(135deg, #14b8a6, #0d9488)"
                                        : "linear-gradient(135deg, #3b82f6, #8b5cf6)",
                                    color = "white",
                                    fontWeight = "600",
                                },
                                children: new[] { Text(result.Code) }
                            ),
                        }
                    ),
                    Td(
                        children: new[]
                        {
                            Span(
                                className: result.CodeType == "ACHI"
                                    ? "badge badge-teal"
                                    : "badge badge-violet",
                                children: new[] { Text(result.CodeType ?? "ICD10CM") }
                            ),
                        }
                    ),
                    Td(
                        className: "result-description-cell",
                        children: new[]
                        {
                            Span(children: new[] { Text(result.Description ?? "") }),
                            Div(
                                className: "result-tooltip",
                                children: RenderSemanticTooltipContent(
                                    result: result,
                                    confidenceColor: confidenceColor,
                                    confidencePercent: confidencePercent
                                )
                            ),
                        }
                    ),
                    Td(
                        children: new[]
                        {
                            Div(
                                className: "flex items-center gap-2",
                                children: new[]
                                {
                                    Div(
                                        style: new
                                        {
                                            width = "60px",
                                            height = "8px",
                                            background = "#e5e7eb",
                                            borderRadius = "4px",
                                            overflow = "hidden",
                                        },
                                        children: new[]
                                        {
                                            Div(
                                                style: new
                                                {
                                                    width = confidencePercent + "%",
                                                    height = "100%",
                                                    background = confidenceColor,
                                                }
                                            ),
                                        }
                                    ),
                                    Span(
                                        className: "badge " + badgeClass,
                                        children: new[] { Text(confidencePercent + "%") }
                                    ),
                                }
                            ),
                        }
                    ),
                }
            );
        }

        private static ReactElement[] RenderSemanticTooltipContent(
            SemanticSearchResult result,
            string confidenceColor,
            int confidencePercent
        )
        {
            var elements = new System.Collections.Generic.List<ReactElement>
            {
                H(
                    4,
                    className: "font-semibold mb-2",
                    children: new[] { Text(result.Code + " - " + (result.Description ?? "")) }
                ),
                P(
                    className: "text-sm text-gray-600 mb-3",
                    children: new[] { Text(result.LongDescription ?? result.Description ?? "") }
                ),
            };

            if (!string.IsNullOrEmpty(result.InclusionTerms))
            {
                elements.Add(
                    Div(
                        className: "text-xs text-green-700 mb-2",
                        children: new[]
                        {
                            Span(
                                className: "font-semibold",
                                children: new[] { Text("Includes: ") }
                            ),
                            Text(result.InclusionTerms),
                        }
                    )
                );
            }

            if (!string.IsNullOrEmpty(result.ExclusionTerms))
            {
                elements.Add(
                    Div(
                        className: "text-xs text-red-700 mb-2",
                        children: new[]
                        {
                            Span(
                                className: "font-semibold",
                                children: new[] { Text("Excludes: ") }
                            ),
                            Text(result.ExclusionTerms),
                        }
                    )
                );
            }

            if (!string.IsNullOrEmpty(result.CodeAlso))
            {
                elements.Add(
                    Div(
                        className: "text-xs text-blue-700 mb-2",
                        children: new[]
                        {
                            Span(
                                className: "font-semibold",
                                children: new[] { Text("Code also: ") }
                            ),
                            Text(result.CodeAlso),
                        }
                    )
                );
            }

            if (!string.IsNullOrEmpty(result.CodeFirst))
            {
                elements.Add(
                    Div(
                        className: "text-xs text-purple-700 mb-2",
                        children: new[]
                        {
                            Span(
                                className: "font-semibold",
                                children: new[] { Text("Code first: ") }
                            ),
                            Text(result.CodeFirst),
                        }
                    )
                );
            }

            elements.Add(
                Div(
                    className: "text-xs text-gray-500 mt-2 pt-2 border-t border-gray-200",
                    children: new[]
                    {
                        Span(className: "font-semibold", children: new[] { Text("Type: ") }),
                        Text(result.CodeType ?? "ICD10CM"),
                        Text(" | "),
                        Span(className: "font-semibold", children: new[] { Text("Confidence: ") }),
                        Span(
                            style: new { color = confidenceColor },
                            children: new[] { Text(confidencePercent + "%") }
                        ),
                    }
                )
            );

            return elements.ToArray();
        }

        private static async void LookupSemanticCode(
            string code,
            ClinicalCodingState state,
            Action<ClinicalCodingState> setState
        )
        {
            setState(
                new ClinicalCodingState
                {
                    SearchQuery = code,
                    SearchMode = "lookup",
                    Icd10Results = new Icd10Code[0],
                    AchiResults = new AchiCode[0],
                    SemanticResults = new SemanticSearchResult[0],
                    SelectedCode = null,
                    Loading = true,
                    Error = null,
                    IncludeAchi = state.IncludeAchi,
                    CopiedCode = null,
                }
            );

            try
            {
                var result = await ApiClient.GetIcd10CodeAsync(code: code);
                setState(
                    new ClinicalCodingState
                    {
                        SearchQuery = code,
                        SearchMode = "lookup",
                        Icd10Results = new Icd10Code[0],
                        AchiResults = new AchiCode[0],
                        SemanticResults = new SemanticSearchResult[0],
                        SelectedCode = result,
                        Loading = false,
                        Error = null,
                        IncludeAchi = state.IncludeAchi,
                        CopiedCode = null,
                    }
                );
            }
            catch (Exception ex)
            {
                setState(
                    new ClinicalCodingState
                    {
                        SearchQuery = code,
                        SearchMode = "lookup",
                        Icd10Results = new Icd10Code[0],
                        AchiResults = new AchiCode[0],
                        SemanticResults = new SemanticSearchResult[0],
                        SelectedCode = null,
                        Loading = false,
                        Error = ex.Message,
                        IncludeAchi = state.IncludeAchi,
                        CopiedCode = null,
                    }
                );
            }
        }

        private static ReactElement RenderCodeDetail(
            ClinicalCodingState state,
            Action<ClinicalCodingState> setState
        )
        {
            var code = state.SelectedCode;

            return Div(
                children: new[]
                {
                    Button(
                        className: "btn btn-ghost mb-4",
                        onClick: () => ClearSelection(state, setState),
                        children: new[] { Icons.ChevronLeft(), Text("Back to results") }
                    ),
                    Div(
                        className: "card",
                        style: new { padding = "32px" },
                        children: new[]
                        {
                            Div(
                                className: "flex items-start justify-between mb-6",
                                children: new[]
                                {
                                    Div(
                                        children: new[]
                                        {
                                            Div(
                                                className: "flex items-center gap-3 mb-2",
                                                children: new[]
                                                {
                                                    Span(
                                                        style: new
                                                        {
                                                            background = "linear-gradient(135deg, #3b82f6, #8b5cf6)",
                                                            color = "white",
                                                            padding = "8px 20px",
                                                            borderRadius = "8px",
                                                            fontWeight = "700",
                                                            fontSize = "20px",
                                                        },
                                                        children: new[] { Text(code.Code) }
                                                    ),
                                                    code.Billable
                                                        ? Span(
                                                            className: "badge badge-success",
                                                            children: new[]
                                                            {
                                                                Icons.Check(),
                                                                Text("Billable"),
                                                            }
                                                        )
                                                        : Span(
                                                            className: "badge badge-gray",
                                                            children: new[] { Text("Non-billable") }
                                                        ),
                                                }
                                            ),
                                            H(
                                                2,
                                                className: "text-xl font-semibold mt-4",
                                                children: new[]
                                                {
                                                    Text(code.ShortDescription ?? ""),
                                                }
                                            ),
                                        }
                                    ),
                                    Button(
                                        className: "btn btn-primary",
                                        onClick: () => CopyCode(code.Code, state, setState),
                                        children: new[]
                                        {
                                            state.CopiedCode == code.Code
                                                ? Icons.Check()
                                                : Icons.Copy(),
                                            Text(
                                                state.CopiedCode == code.Code
                                                    ? "Copied!"
                                                    : "Copy Code"
                                            ),
                                        }
                                    ),
                                }
                            ),
                            Div(
                                className: "grid grid-cols-3 gap-4 mb-6 p-4",
                                style: new { background = "#f9fafb", borderRadius = "8px" },
                                children: new[]
                                {
                                    RenderDetailItem(
                                        label: "Chapter",
                                        value: code.ChapterNumber
                                            + " - "
                                            + (code.ChapterTitle ?? "")
                                    ),
                                    RenderDetailItem(label: "Block", value: code.BlockCode ?? ""),
                                    RenderDetailItem(
                                        label: "Category",
                                        value: code.CategoryCode ?? ""
                                    ),
                                }
                            ),
                            RenderDetailSection(
                                title: "Full Description",
                                content: code.LongDescription
                            ),
                            RenderDetailSection(
                                title: "Inclusion Terms",
                                content: code.InclusionTerms
                            ),
                            RenderDetailSection(
                                title: "Exclusion Terms",
                                content: code.ExclusionTerms
                            ),
                            RenderDetailSection(title: "Code Also", content: code.CodeAlso),
                            RenderDetailSection(title: "Code First", content: code.CodeFirst),
                        }
                    ),
                }
            );
        }

        private static ReactElement RenderDetailItem(string label, string value) =>
            Div(
                children: new[]
                {
                    Span(
                        className: "text-xs text-gray-500 uppercase tracking-wide",
                        children: new[] { Text(label) }
                    ),
                    P(className: "font-medium", children: new[] { Text(value) }),
                }
            );

        private static ReactElement RenderDetailSection(string title, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return Text("");

            return Div(
                className: "mb-4",
                children: new[]
                {
                    H(
                        4,
                        className: "font-semibold text-gray-700 mb-2",
                        children: new[] { Text(title) }
                    ),
                    Div(
                        className: "p-4",
                        style: new
                        {
                            background = "#f9fafb",
                            borderRadius = "8px",
                            borderLeft = "4px solid #3b82f6",
                        },
                        children: new[]
                        {
                            P(
                                className: "text-gray-700 whitespace-pre-wrap",
                                children: new[] { Text(content) }
                            ),
                        }
                    ),
                }
            );
        }

        private static void ClearSelection(
            ClinicalCodingState state,
            Action<ClinicalCodingState> setState
        )
        {
            setState(
                new ClinicalCodingState
                {
                    SearchQuery = state.SearchQuery,
                    SearchMode = state.SearchMode,
                    Icd10Results = state.Icd10Results,
                    AchiResults = state.AchiResults,
                    SemanticResults = state.SemanticResults,
                    SelectedCode = null,
                    Loading = false,
                    Error = null,
                    IncludeAchi = state.IncludeAchi,
                    CopiedCode = null,
                }
            );
        }
    }
}
