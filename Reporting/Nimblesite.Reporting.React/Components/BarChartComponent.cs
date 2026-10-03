using System;
using H5;
using Nimblesite.Reporting.React.Core;
using static Nimblesite.Reporting.React.Core.Elements;

namespace Nimblesite.Reporting.React.Components
{
    /// <summary>
    /// Renders a bar chart using SVG.
    /// </summary>
    public static class BarChartComponent
    {
        private const int ChartWidth = 500;
        private const int ChartHeight = 300;
        private const int Padding = 50;
        private const int BottomPadding = 80;

        private static readonly string[] BarColors = new[]
        {
            "#00BCD4",
            "#2E4450",
            "#FF6B6B",
            "#4CAF50",
            "#FF9800",
            "#9C27B0",
            "#3F51B5",
            "#009688",
        };

        /// <summary>
        /// Renders a bar chart from data source results.
        /// </summary>
        public static ReactElement Render(
            string title,
            object xAxisDef,
            object yAxisDef,
            object dataSourceResult,
            string cssClass = null,
            object cssStyle = null
        )
        {
            var xField = Script.Get<string>(xAxisDef, "field");
            var yField = Script.Get<string>(yAxisDef, "field");
            var yLabel = Script.Get<string>(yAxisDef, "label") ?? yField;
            var columnNames = Script.Get<string[]>(dataSourceResult, "columnNames");
            var rows = Script.Get<object[]>(dataSourceResult, "rows") ?? new object[0];

            var xIndex = ColumnLookup.FindColumnIndex(columnNames, xField);
            var yIndex = ColumnLookup.FindColumnIndex(columnNames, yField);

            var chartClassName = cssClass != null ? "report-chart " + cssClass : "report-chart";

            if (xIndex < 0 || yIndex < 0 || rows.Length == 0)
            {
                return Div(
                    className: chartClassName,
                    style: cssStyle,
                    children: new[]
                    {
                        H(3, className: "report-component-title", children: new[] { Text(title) }),
                        P(children: new[] { Text("No data available") }),
                    }
                );
            }

            // Extract values
            var labels = new string[rows.Length];
            var values = new double[rows.Length];
            var maxValue = 0.0;

            for (var i = 0; i < rows.Length; i++)
            {
                var row = Script.Write<object[]>("rows[i]");
                labels[i] = Script.Write<object>("row[xIndex]")?.ToString() ?? "";
                var rawVal = Script.Write<object>("row[yIndex]");
                values[i] = Script.Call<double>("Number", rawVal);
                if (values[i] > maxValue)
                    maxValue = values[i];
            }

            if (maxValue == 0)
                maxValue = 1;

            // Build SVG bars
            var drawWidth = ChartWidth - Padding * 2;
            var drawHeight = ChartHeight - Padding - BottomPadding;
            var barWidth = drawWidth / (rows.Length * 2.0);
            var barElements = new ReactElement[rows.Length * 3 + 4]; // bars + labels + axes
            var elementIndex = 0;

            // Y-axis line
            barElements[elementIndex++] = Line(
                x1: Padding,
                y1: Padding,
                x2: Padding,
                y2: ChartHeight - BottomPadding,
                stroke: "#ccc",
                strokeWidth: 1
            );

            // X-axis line
            barElements[elementIndex++] = Line(
                x1: Padding,
                y1: ChartHeight - BottomPadding,
                x2: ChartWidth - Padding,
                y2: ChartHeight - BottomPadding,
                stroke: "#ccc",
                strokeWidth: 1
            );

            // Y-axis label
            barElements[elementIndex++] = SvgText(
                x: 15,
                y: ChartHeight / 2.0,
                content: yLabel,
                fill: "#666",
                fontSize: "11px",
                textAnchor: "middle",
                transform: "rotate(-90, 15, " + (ChartHeight / 2.0) + ")"
            );

            // Max value label
            barElements[elementIndex++] = SvgText(
                x: Padding - 5,
                y: Padding + 4,
                content: Math.Round(maxValue).ToString(),
                fill: "#666",
                fontSize: "10px",
                textAnchor: "end"
            );

            for (var i = 0; i < rows.Length; i++)
            {
                var barHeight = (values[i] / maxValue) * drawHeight;
                var x = Padding + i * (drawWidth / rows.Length) + barWidth * 0.5;
                var y = ChartHeight - BottomPadding - barHeight;
                var color = BarColors[i % BarColors.Length];

                // Bar
                barElements[elementIndex++] = Rect(
                    x: x,
                    y: y,
                    width: barWidth,
                    height: barHeight,
                    fill: color
                );

                // Value label above bar
                barElements[elementIndex++] = SvgText(
                    x: x + barWidth / 2.0,
                    y: y - 5,
                    content: Math.Round(values[i]).ToString(),
                    fill: "#333",
                    fontSize: "10px",
                    textAnchor: "middle"
                );

                // X-axis label below bar
                barElements[elementIndex++] = SvgText(
                    x: x + barWidth / 2.0,
                    y: ChartHeight - BottomPadding + 15,
                    content: TruncateLabel(labels[i], 12),
                    fill: "#666",
                    fontSize: "10px",
                    textAnchor: "middle"
                );
            }

            // Trim array to actual size
            var finalElements = new ReactElement[elementIndex];
            Array.Copy(barElements, finalElements, elementIndex);

            return Div(
                className: chartClassName,
                style: cssStyle,
                children: new[]
                {
                    H(3, className: "report-component-title", children: new[] { Text(title) }),
                    Svg(
                        className: "report-bar-chart",
                        width: ChartWidth,
                        height: ChartHeight,
                        viewBox: "0 0 " + ChartWidth + " " + ChartHeight,
                        children: finalElements
                    ),
                }
            );
        }

        private static string TruncateLabel(string label, int maxLen)
        {
            if (label == null)
                return "";
            return label.Length <= maxLen ? label : label.Substring(0, maxLen - 1) + "…";
        }
    }
}
