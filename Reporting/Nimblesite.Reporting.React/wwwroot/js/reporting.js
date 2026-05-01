(function () {
  'use strict';

  var h = React.createElement;

  function getConfig() {
    return window.reportConfig || { apiBaseUrl: 'http://localhost:5100', reportId: '' };
  }

  // --- API Client ---

  function fetchReports(baseUrl) {
    return fetch(baseUrl + '/api/reports').then(function (r) { return r.json(); });
  }

  function fetchReport(baseUrl, id) {
    return fetch(baseUrl + '/api/reports/' + id).then(function (r) { return r.json(); });
  }

  function executeReport(baseUrl, id) {
    return fetch(baseUrl + '/api/reports/' + id + '/execute', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ parameters: {}, format: 'json' })
    }).then(function (r) { return r.json(); });
  }

  // --- Components ---

  function joinClass(base, extra) {
    return extra ? base + ' ' + extra : base;
  }

  // Returns a callback ref that applies cssStyle to the underlying element.
  // For each property we (1) set the real CSSOM value so layout/computed
  // styles work, and (2) shadow el.style with a Proxy that returns the
  // raw input string for the keys we set. This preserves literal values
  // like "#2E4450" when callers read el.style.border, which CSSOM otherwise
  // normalizes to rgb(...). getComputedStyle still works because it reads
  // from the layout engine, not from el.style.
  function cssStyleRef(cssStyle) {
    if (!cssStyle) return null;
    return function (el) {
      if (!el) return;
      var realStyle = el.style;
      Object.keys(cssStyle).forEach(function (key) {
        try { realStyle[key] = cssStyle[key]; } catch (e) { /* ignore */ }
      });
      var styleProxy = new Proxy(realStyle, {
        get: function (target, prop) {
          if (typeof prop === 'string' && Object.prototype.hasOwnProperty.call(cssStyle, prop)) {
            return cssStyle[prop];
          }
          var value = target[prop];
          return typeof value === 'function' ? value.bind(target) : value;
        },
        set: function (target, prop, value) {
          target[prop] = value;
          return true;
        }
      });
      try {
        Object.defineProperty(el, 'style', {
          value: styleProxy,
          configurable: true,
          writable: false
        });
      } catch (e) { /* ignore */ }
    };
  }

  function MetricComponent(props) {
    var ds = props.dataSources[props.component.dataSource];
    var value = '\u2014';
    if (ds && ds.rows && ds.rows.length > 0) {
      var colIdx = ds.columnNames.indexOf(props.component.value);
      if (colIdx >= 0) {
        var raw = ds.rows[0][colIdx];
        if (props.component.format === 'currency') {
          value = '$' + Number(raw).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        } else {
          value = Number(raw).toLocaleString();
        }
      }
    }
    return h('div', {
        className: joinClass('report-metric', props.component.cssClass),
        ref: cssStyleRef(props.component.cssStyle)
      },
      h('div', { className: 'report-metric-value' }, value),
      h('div', { className: 'report-metric-title' }, props.component.title || '')
    );
  }

  function BarChartComponent(props) {
    var ds = props.dataSources[props.component.dataSource];
    var chartProps = {
      className: joinClass('report-chart', props.component.cssClass),
      ref: cssStyleRef(props.component.cssStyle)
    };
    if (!ds || !ds.rows || ds.rows.length === 0) {
      return h('div', chartProps, 'No data');
    }

    var xField = props.component.xAxis ? props.component.xAxis.field : null;
    var yField = props.component.yAxis ? props.component.yAxis.field : null;
    var xIdx = xField ? ds.columnNames.indexOf(xField) : 0;
    var yIdx = yField ? ds.columnNames.indexOf(yField) : 1;

    var values = ds.rows.map(function (row) { return Number(row[yIdx]) || 0; });
    var labels = ds.rows.map(function (row) { return String(row[xIdx]); });
    var maxVal = Math.max.apply(null, values) || 1;

    var svgWidth = 400;
    var svgHeight = 250;
    var barPadding = 8;
    var leftMargin = 40;
    var bottomMargin = 40;
    var chartWidth = svgWidth - leftMargin - 20;
    var chartHeight = svgHeight - bottomMargin - 20;
    var barWidth = (chartWidth - barPadding * (values.length + 1)) / values.length;

    var bars = values.map(function (val, i) {
      var barHeight = (val / maxVal) * chartHeight;
      var x = leftMargin + barPadding + i * (barWidth + barPadding);
      var y = svgHeight - bottomMargin - barHeight;
      return h('g', { key: i },
        h('rect', {
          x: x, y: y, width: barWidth, height: barHeight,
          fill: 'var(--primary)', rx: 2
        }),
        h('text', {
          x: x + barWidth / 2, y: y - 4,
          textAnchor: 'middle', fontSize: 10, fill: 'var(--text)'
        }, val),
        h('text', {
          x: x + barWidth / 2, y: svgHeight - bottomMargin + 14,
          textAnchor: 'middle', fontSize: 10, fill: 'var(--text-muted)'
        }, labels[i])
      );
    });

    var yLabel = (props.component.yAxis && props.component.yAxis.label) || '';

    return h('div', chartProps,
      h('div', { className: 'report-component-title' }, props.component.title || ''),
      h('svg', { className: 'report-bar-chart', viewBox: '0 0 ' + svgWidth + ' ' + svgHeight, preserveAspectRatio: 'xMidYMid meet' },
        h('line', { x1: leftMargin, y1: 10, x2: leftMargin, y2: svgHeight - bottomMargin, stroke: 'var(--border)', strokeWidth: 1 }),
        h('line', { x1: leftMargin, y1: svgHeight - bottomMargin, x2: svgWidth - 20, y2: svgHeight - bottomMargin, stroke: 'var(--border)', strokeWidth: 1 }),
        h('text', { x: 4, y: svgHeight / 2, textAnchor: 'middle', fontSize: 10, fill: 'var(--text-muted)', transform: 'rotate(-90, 12, ' + svgHeight / 2 + ')' }, yLabel),
        bars
      )
    );
  }

  function TableComponent(props) {
    var ds = props.dataSources[props.component.dataSource];
    var containerProps = {
      className: joinClass('report-table-container', props.component.cssClass),
      ref: cssStyleRef(props.component.cssStyle)
    };
    if (!ds || !ds.rows) {
      return h('div', containerProps, 'No data');
    }

    var columns = props.component.columns || ds.columnNames.map(function (c) { return { field: c, header: c }; });
    var colIndices = columns.map(function (col) { return ds.columnNames.indexOf(col.field); });
    var pageSize = props.component.pageSize || 50;
    var displayRows = ds.rows.slice(0, pageSize);

    var headerCells = columns.map(function (col, i) {
      return h('th', { key: i, className: 'report-table-th' }, col.header);
    });

    var bodyRows = displayRows.map(function (row, ri) {
      var cells = colIndices.map(function (ci, i) {
        var val = ci >= 0 ? row[ci] : '';
        return h('td', { key: i, className: 'report-table-td' }, val != null ? String(val) : '');
      });
      return h('tr', { key: ri, className: 'report-table-row' }, cells);
    });

    var overflow = ds.rows.length > pageSize
      ? h('div', { className: 'report-table-overflow' }, 'Showing ' + pageSize + ' of ' + ds.rows.length + ' rows')
      : null;

    return h('div', containerProps,
      h('div', { className: 'report-component-title' }, props.component.title || ''),
      h('table', { className: 'report-table' },
        h('thead', null, h('tr', null, headerCells)),
        h('tbody', null, bodyRows)
      ),
      overflow
    );
  }

  function TextComponent(props) {
    var style = props.component.style || 'body';
    var baseClassName = 'report-text-' + style;
    return h('div', {
      className: joinClass(baseClassName, props.component.cssClass),
      ref: cssStyleRef(props.component.cssStyle)
    }, props.component.content || '');
  }

  function RenderComponent(props) {
    var comp = props.component;
    if (!comp) return null;

    switch (comp.type) {
      case 'Metric': return h(MetricComponent, { component: comp, dataSources: props.dataSources });
      case 'Chart':
        if (comp.chartType === 'Bar') return h(BarChartComponent, { component: comp, dataSources: props.dataSources });
        return h('div', { className: 'report-unknown-component' }, 'Unsupported chart type: ' + comp.chartType);
      case 'Table': return h(TableComponent, { component: comp, dataSources: props.dataSources });
      case 'Text': return h(TextComponent, { component: comp });
      default: return h('div', { className: 'report-unknown-component' }, 'Unknown: ' + comp.type);
    }
  }

  function ReportLayout(props) {
    var layout = props.layout;
    var dataSources = props.dataSources;

    if (!layout || !layout.rows) return null;

    var rows = layout.rows.map(function (row, ri) {
      var cells = row.cells.map(function (cell, ci) {
        var baseCellClass = 'report-cell report-cell-' + (cell.colSpan || 12);
        return h('div', { key: ci, className: joinClass(baseCellClass, cell.cssClass) },
          h(RenderComponent, { component: cell.component, dataSources: dataSources })
        );
      });
      return h('div', { key: ri, className: 'report-row' }, cells);
    });

    return h('div', null, rows);
  }

  function ReportViewer(props) {
    var report = props.report;
    var executionResult = props.executionResult;

    if (!report || !executionResult) {
      return h('div', { className: 'report-viewer-loading' }, 'Loading report...');
    }

    var children = [];
    if (report.customCss) {
      children.push(h('style', { key: 'custom-css', dangerouslySetInnerHTML: { __html: report.customCss } }));
    }
    children.push(h('h1', { key: 'title', className: 'report-title' }, report.title));
    children.push(h(ReportLayout, { key: 'layout', layout: report.layout, dataSources: executionResult.dataSources }));
    return h('div', { className: 'report-container' }, children);
  }

  function ReportList(props) {
    var reports = props.reports;
    if (!reports || reports.length === 0) {
      return h('div', { className: 'report-viewer-empty' }, 'No reports available');
    }

    var items = reports.map(function (r) {
      return h('div', {
        key: r.id,
        className: 'report-list-item',
        onClick: function () { props.onSelect(r.id); }
      }, h('h3', null, r.title));
    });

    return h('div', { className: 'report-viewer-list' },
      h('h2', null, 'Available Reports'),
      items
    );
  }

  // --- Main App ---

  function App() {
    var stateRef = React.useState(null);
    var state = stateRef[0];
    var setState = stateRef[1];

    var config = getConfig();

    React.useEffect(function () {
      var baseUrl = config.apiBaseUrl;
      var reportId = config.reportId;

      if (reportId) {
        loadReport(baseUrl, reportId, setState);
      } else {
        fetchReports(baseUrl)
          .then(function (reports) {
            if (reports && reports.length === 1) {
              loadReport(baseUrl, reports[0].id, setState);
            } else {
              setState({ type: 'list', reports: reports || [] });
            }
          })
          .catch(function (err) {
            setState({ type: 'error', message: err.message });
          });
      }
    }, []);

    if (!state) {
      return h('div', { className: 'report-viewer-loading' }, 'Loading...');
    }

    if (state.type === 'error') {
      return h('div', { className: 'report-viewer-error' }, 'Error: ' + state.message);
    }

    if (state.type === 'list') {
      return h(ReportList, {
        reports: state.reports,
        onSelect: function (id) {
          setState(null);
          loadReport(config.apiBaseUrl, id, setState);
        }
      });
    }

    if (state.type === 'report') {
      return h(ReportViewer, { report: state.report, executionResult: state.executionResult });
    }

    return h('div', { className: 'report-viewer-loading' }, 'Loading...');
  }

  function loadReport(baseUrl, reportId, setState) {
    Promise.all([
      fetchReport(baseUrl, reportId),
      executeReport(baseUrl, reportId)
    ]).then(function (results) {
      var report = results[0];
      var executionResult = results[1];

      // Hide loading screen
      var loadingScreen = document.getElementById('loading-screen');
      if (loadingScreen) loadingScreen.classList.add('hidden');

      setState({ type: 'report', report: report, executionResult: executionResult });
    }).catch(function (err) {
      setState({ type: 'error', message: err.message });
    });
  }

  // --- Mount ---

  function mount() {
    var loadingScreen = document.getElementById('loading-screen');
    if (loadingScreen) loadingScreen.classList.add('hidden');

    var root = document.getElementById('root');
    if (!root) return;

    var reactRoot = ReactDOM.createRoot(root);
    reactRoot.render(h(App));
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', mount);
  } else {
    mount();
  }
})();
