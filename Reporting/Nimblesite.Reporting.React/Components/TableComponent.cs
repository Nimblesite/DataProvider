using H5;
using Nimblesite.Reporting.React.Core;
using static Nimblesite.Reporting.React.Core.Elements;

namespace Nimblesite.Reporting.React.Components
{
    /// <summary>
    /// Renders a data table from report data.
    /// </summary>
    public static class TableComponent
    {
        /// <summary>
        /// Renders a table with headers and rows from a data source result.
        /// </summary>
        public static ReactElement Render(
            string title,
            object columnDefs,
            object dataSourceResult,
            int pageSize,
            string cssClass = null,
            object cssStyle = null
        )
        {
            var columns = Script.Write<object[]>("columnDefs") ?? new object[0];
            var allColumnNames = Script.Get<string[]>(dataSourceResult, "columnNames");
            var rows = Script.Get<object[]>(dataSourceResult, "rows") ?? new object[0];

            // Build header cells
            var headerCells = new ReactElement[columns.Length];
            for (var i = 0; i < columns.Length; i++)
            {
                var header = Script.Get<string>(columns[i], "header");
                headerCells[i] = Th(
                    className: "report-table-th",
                    children: new[] { Text(header ?? "") }
                );
            }

            var headerRow = Tr(className: "report-table-header-row", children: headerCells);

            // Build data rows (limited by pageSize)
            var displayCount = pageSize > 0 && rows.Length > pageSize ? pageSize : rows.Length;
            var dataRows = new ReactElement[displayCount];

            for (var r = 0; r < displayCount; r++)
            {
                var row = Script.Write<object[]>("rows[r]");
                var cells = new ReactElement[columns.Length];

                for (var c = 0; c < columns.Length; c++)
                {
                    var field = Script.Get<string>(columns[c], "field");
                    var colIndex = ColumnLookup.FindColumnIndex(allColumnNames, field);
                    var cellValue = colIndex >= 0 ? Script.Write<object>("row[colIndex]") : null;
                    cells[c] = Td(
                        className: "report-table-td",
                        children: new[] { Text(cellValue != null ? cellValue.ToString() : "") }
                    );
                }

                dataRows[r] = Tr(className: "report-table-row", children: cells);
            }

            var containerClassName =
                cssClass != null ? "report-table-container " + cssClass : "report-table-container";

            return Div(
                className: containerClassName,
                style: cssStyle,
                children: new[]
                {
                    H(3, className: "report-component-title", children: new[] { Text(title) }),
                    Table(
                        className: "report-table",
                        children: new[]
                        {
                            THead(children: new[] { headerRow }),
                            TBody(children: dataRows),
                        }
                    ),
                    rows.Length > displayCount
                        ? P(
                            className: "report-table-overflow",
                            children: new[]
                            {
                                Text("Showing " + displayCount + " of " + rows.Length + " rows"),
                            }
                        )
                        : Fragment(),
                }
            );
        }
    }
}
