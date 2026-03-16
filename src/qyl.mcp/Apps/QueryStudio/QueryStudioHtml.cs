namespace qyl.mcp.Apps.QueryStudio;

/// <summary>
///     Holds the Query Studio HTML as a compile-time constant.
///     Loaded by <see cref="QueryStudioResource" /> to serve via MCP resource protocol.
///     The HTML file at <c>query-studio.html</c> is the source of truth — keep in sync.
/// </summary>
internal static class QueryStudioHtml
{
    public const string Content = """
                                  <!DOCTYPE html>
                                  <html lang="en" class="dark">
                                  <head>
                                  <meta charset="utf-8">
                                  <meta name="viewport" content="width=device-width,initial-scale=1">
                                  <title>qyl Query Studio</title>
                                  <script src="https://cdn.tailwindcss.com"></script>
                                  <script>tailwind.config={darkMode:'class',theme:{extend:{colors:{bg:'#1e1e2e',surface:'#282a36',border:'#44475a',accent:'#bd93f9',keyword:'#ff79c6',fn:'#50fa7b',str:'#f1fa8c',num:'#bd93f9',comment:'#6272a4',err:'#ff5555',text:'#f8f8f2',muted:'#6272a4'}}}}</script>
                                  <style>
                                  *{scrollbar-width:thin;scrollbar-color:#44475a #1e1e2e}
                                  body{font-family:system-ui,-apple-system,sans-serif;background:#1e1e2e;color:#f8f8f2;margin:0;height:100vh;overflow:hidden}
                                  .editor-wrap{position:relative;font-family:'Cascadia Code','Fira Code','JetBrains Mono',monospace;font-size:13px;line-height:1.6}
                                  .line-numbers{position:absolute;left:0;top:0;bottom:0;width:40px;text-align:right;padding:12px 6px 12px 0;color:#6272a4;user-select:none;overflow:hidden;pointer-events:none}
                                  .editor-area{width:100%;min-height:120px;max-height:240px;resize:vertical;background:transparent;color:#f8f8f2;border:none;outline:none;font:inherit;padding:12px 12px 12px 48px;tab-size:2;white-space:pre;overflow:auto}
                                  .editor-area::placeholder{color:#6272a4}
                                  table.results{border-collapse:collapse;width:100%;font-size:12px}
                                  table.results th{position:sticky;top:0;z-index:1;background:#282a36;padding:6px 10px;text-align:left;cursor:pointer;border-bottom:2px solid #44475a;white-space:nowrap;user-select:none}
                                  table.results th:hover{color:#bd93f9}
                                  table.results td{padding:5px 10px;border-bottom:1px solid #44475a;white-space:nowrap;max-width:300px;overflow:hidden;text-overflow:ellipsis}
                                  table.results tr:hover td{background:#282a3680}
                                  .type-badge{font-size:10px;padding:1px 5px;border-radius:3px;background:#44475a;color:#bd93f9;margin-left:4px;font-weight:normal}
                                  .tree-item{cursor:pointer;padding:2px 0;user-select:none}
                                  .tree-item:hover{color:#bd93f9}
                                  .chart-bar{transition:height .3s ease}
                                  </style>
                                  </head>
                                  <body class="flex flex-col h-screen">

                                  <!-- Header -->
                                  <header class="flex items-center justify-between px-4 py-2 bg-surface border-b border-border shrink-0">
                                    <div class="flex items-center gap-3">
                                      <span class="text-accent font-bold text-sm tracking-wide">qyl</span>
                                      <span class="text-muted text-xs">Query Studio</span>
                                    </div>
                                    <div class="flex items-center gap-2">
                                      <select id="presets" class="bg-bg border border-border rounded px-2 py-1 text-xs text-text focus:border-accent outline-none">
                                        <option value="">Preset queries...</option>
                                      </select>
                                      <select id="viewMode" class="bg-bg border border-border rounded px-2 py-1 text-xs text-text focus:border-accent outline-none">
                                        <option value="table">Table</option>
                                        <option value="bar">Bar Chart</option>
                                        <option value="line">Line Chart</option>
                                      </select>
                                    </div>
                                  </header>

                                  <!-- Main -->
                                  <div class="flex flex-1 overflow-hidden">

                                    <!-- Schema sidebar -->
                                    <aside id="sidebar" class="w-56 bg-surface border-r border-border flex flex-col shrink-0 overflow-hidden">
                                      <div class="p-2 border-b border-border">
                                        <input id="schemaFilter" type="text" placeholder="Filter tables..." class="w-full bg-bg border border-border rounded px-2 py-1 text-xs text-text outline-none focus:border-accent">
                                      </div>
                                      <div id="schemaTree" class="flex-1 overflow-auto p-2 text-xs"></div>
                                    </aside>

                                    <!-- Editor + Results -->
                                    <main class="flex-1 flex flex-col overflow-hidden">
                                      <!-- Editor -->
                                      <div class="border-b border-border shrink-0">
                                        <div class="editor-wrap bg-bg">
                                          <div id="lineNums" class="line-numbers">1</div>
                                          <textarea id="editor" class="editor-area" placeholder="SELECT * FROM spans LIMIT 10" spellcheck="false"></textarea>
                                        </div>
                                        <div class="flex items-center justify-between px-3 py-1.5 bg-surface text-xs">
                                          <div class="flex items-center gap-3">
                                            <button id="runBtn" class="bg-accent text-bg px-3 py-0.5 rounded font-medium hover:brightness-110 transition">Run</button>
                                            <span class="text-muted">Ctrl+Enter</span>
                                          </div>
                                          <div id="statusBar" class="text-muted"></div>
                                        </div>
                                      </div>

                                      <!-- Results -->
                                      <div class="flex-1 overflow-auto relative" id="resultsPane">
                                        <div id="welcome" class="flex items-center justify-center h-full text-muted text-sm">
                                          <div class="text-center">
                                            <p class="mb-2">Write a SQL query and press <kbd class="px-1.5 py-0.5 bg-surface border border-border rounded text-xs">Ctrl+Enter</kbd> to execute</p>
                                            <p class="text-xs">or select a preset query from the dropdown above</p>
                                          </div>
                                        </div>
                                        <div id="tableView" class="hidden"></div>
                                        <div id="chartView" class="hidden p-4"></div>
                                        <div id="errorView" class="hidden p-4 text-err text-sm font-mono whitespace-pre-wrap"></div>
                                      </div>

                                      <!-- Footer: pagination + history -->
                                      <footer class="flex items-center justify-between px-3 py-1.5 bg-surface border-t border-border text-xs shrink-0">
                                        <div id="pagination" class="flex items-center gap-2"></div>
                                        <div class="flex items-center gap-2">
                                          <span class="text-muted">History:</span>
                                          <div id="history" class="flex gap-1 overflow-x-auto max-w-md"></div>
                                        </div>
                                      </footer>
                                    </main>
                                  </div>

                                  <script>
                                  const $ = s => document.querySelector(s);
                                  const state = {
                                    schema: null, results: null, query: '', history: [],
                                    page: 0, pageSize: 50, sortCol: -1, sortAsc: true,
                                    presets: [
                                      { name: 'Recent traces', sql: 'SELECT * FROM spans ORDER BY start_time DESC LIMIT 100' },
                                      { name: 'Error rate by service', sql: "SELECT service_name, COUNT(*) FILTER (WHERE status = 'ERROR') * 100.0 / COUNT(*) as error_rate FROM spans GROUP BY service_name" },
                                      { name: 'Slow operations', sql: 'SELECT operation_name, AVG(duration_ms) as avg_ms, MAX(duration_ms) as max_ms FROM spans GROUP BY operation_name ORDER BY avg_ms DESC LIMIT 20' },
                                      { name: 'Log volume', sql: "SELECT date_trunc('hour', timestamp) as hour, COUNT(*) as count FROM logs GROUP BY hour ORDER BY hour" },
                                      { name: 'Error distribution', sql: 'SELECT error_type, COUNT(*) as count, MIN(first_seen) as first_seen, MAX(last_seen) as last_seen FROM errors GROUP BY error_type ORDER BY count DESC' },
                                      { name: 'Service health', sql: "SELECT service_name, COUNT(*) as total_spans, COUNT(*) FILTER (WHERE status = 'ERROR') as errors, AVG(duration_ms) as avg_latency_ms FROM spans GROUP BY service_name ORDER BY errors DESC" },
                                    ]
                                  };

                                  // --- Init ---
                                  function init() {
                                    const sel = $('#presets');
                                    state.presets.forEach((p, i) => {
                                      const o = document.createElement('option');
                                      o.value = i; o.textContent = p.name;
                                      sel.appendChild(o);
                                    });
                                    sel.onchange = () => { if (sel.value !== '') { $('#editor').value = state.presets[sel.value].sql; updateLineNums(); sel.value = ''; }};

                                    $('#runBtn').onclick = executeQuery;
                                    $('#editor').addEventListener('keydown', e => {
                                      if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) { e.preventDefault(); executeQuery(); }
                                      if (e.key === 'Tab') { e.preventDefault(); insertTab(e.target); }
                                    });
                                    $('#editor').addEventListener('input', updateLineNums);
                                    $('#viewMode').onchange = renderResults;

                                    window.addEventListener('message', onMessage);
                                    postToHost({ type: 'tool_call', tool: 'qyl.app.query_schema' });
                                    setTimeout(() => { if (!state.schema) applySchema(buildFallbackSchema()); }, 1000);
                                  }

                                  function insertTab(el) {
                                    const s = el.selectionStart, e = el.selectionEnd;
                                    el.value = el.value.substring(0, s) + '  ' + el.value.substring(e);
                                    el.selectionStart = el.selectionEnd = s + 2;
                                    updateLineNums();
                                  }

                                  function updateLineNums() {
                                    const lines = $('#editor').value.split('\n').length;
                                    $('#lineNums').textContent = Array.from({length: lines}, (_, i) => i + 1).join('\n');
                                  }

                                  // --- Bridge ---
                                  function postToHost(msg) { window.parent?.postMessage(msg, '*'); }

                                  function onMessage(e) {
                                    const d = e.data;
                                    if (!d || !d.type) return;
                                    if (d.type === 'schema') applySchema(d.data);
                                    else if (d.type === 'tool_result') applyResults(d.data);
                                    else if (d.type === 'error') showError(d.data?.error || d.data);
                                  }

                                  // --- Schema ---
                                  function applySchema(data) {
                                    if (!data?.tables) return;
                                    state.schema = data;
                                    renderSchemaTree();
                                  }

                                  function renderSchemaTree(filter = '') {
                                    const el = $('#schemaTree');
                                    if (!state.schema) { el.innerHTML = '<span class="text-muted">No schema</span>'; return; }
                                    const f = filter.toLowerCase();
                                    let html = '';
                                    for (const t of state.schema.tables) {
                                      if (f && !t.name.toLowerCase().includes(f) && !t.columns.some(c => c.name.toLowerCase().includes(f))) continue;
                                      html += `<details open><summary class="tree-item font-medium text-fn py-1">${esc(t.name)} <span class="type-badge">${t.columns.length}</span></summary><div class="pl-3">`;
                                      for (const c of t.columns) {
                                        if (f && !c.name.toLowerCase().includes(f) && !t.name.toLowerCase().includes(f)) continue;
                                        html += `<div class="tree-item flex justify-between" onclick="insertColumn('${esc(c.name)}')">`
                                          + `<span>${esc(c.name)}</span><span class="type-badge">${esc(c.type)}</span></div>`;
                                      }
                                      html += '</div></details>';
                                    }
                                    el.innerHTML = html || '<span class="text-muted">No matches</span>';
                                  }

                                  $('#schemaFilter')?.addEventListener('input', e => renderSchemaTree(e.target.value));

                                  function insertColumn(name) {
                                    const ed = $('#editor');
                                    const s = ed.selectionStart;
                                    ed.value = ed.value.substring(0, s) + name + ed.value.substring(ed.selectionEnd);
                                    ed.selectionStart = ed.selectionEnd = s + name.length;
                                    ed.focus();
                                  }

                                  // --- Query execution ---
                                  async function executeQuery() {
                                    const sql = $('#editor').value.trim();
                                    if (!sql) return;
                                    state.query = sql;
                                    state.page = 0;
                                    state.sortCol = -1;
                                    $('#statusBar').textContent = 'Executing...';
                                    $('#runBtn').disabled = true;

                                    if (!state.history.includes(sql)) {
                                      state.history.unshift(sql);
                                      if (state.history.length > 10) state.history.pop();
                                      renderHistory();
                                    }

                                    try {
                                      const resp = await fetch('/api/v1/query', {
                                        method: 'POST', headers: { 'Content-Type': 'application/json' },
                                        body: JSON.stringify({ sql, limit: 1000 })
                                      });
                                      const t = performance.now();
                                      const data = await resp.json();
                                      if (!resp.ok) { showError(data.error || 'Query failed'); return; }

                                      const columns = data.columns.map(name => {
                                        const type = data.rows?.length > 0 && data.rows[0][name] != null
                                          ? inferType(data.rows[0][name]) : 'varchar';
                                        return { name, type };
                                      });
                                      const rows = (data.rows || []).map(row => data.columns.map(c => row[c] ?? null));
                                      applyResults({ columns, rows, rowCount: data.rowCount || rows.length, executionTimeMs: performance.now() - t, query: sql });
                                    } catch {
                                      postToHost({ type: 'tool_call', tool: 'qyl.app.execute_query', params: { sql, limit: 1000 } });
                                    }
                                  }

                                  function inferType(v) {
                                    if (typeof v === 'number') return Number.isInteger(v) ? 'bigint' : 'double';
                                    if (typeof v === 'boolean') return 'boolean';
                                    if (typeof v === 'string' && /^\d{4}-\d{2}-\d{2}/.test(v)) return 'timestamp';
                                    return 'varchar';
                                  }

                                  // --- Results ---
                                  function applyResults(data) {
                                    if (!data) return;
                                    if (data.error) { showError(data.error); return; }
                                    state.results = data;
                                    $('#statusBar').textContent = `${data.rowCount} rows in ${data.executionTimeMs?.toFixed(0) || '?'}ms`;
                                    $('#runBtn').disabled = false;
                                    renderResults();
                                  }

                                  function renderResults() {
                                    const mode = $('#viewMode').value;
                                    const r = state.results;
                                    hideAll();
                                    if (!r?.rows?.length) { $('#welcome').classList.remove('hidden'); return; }
                                    if (mode === 'table') renderTable();
                                    else renderChart(mode);
                                  }

                                  function hideAll() {
                                    for (const id of ['welcome','tableView','chartView','errorView'])
                                      $('#' + id).classList.add('hidden');
                                  }

                                  function showError(msg) {
                                    hideAll();
                                    $('#errorView').textContent = typeof msg === 'object' ? JSON.stringify(msg, null, 2) : msg;
                                    $('#errorView').classList.remove('hidden');
                                    $('#statusBar').textContent = 'Error';
                                    $('#runBtn').disabled = false;
                                  }

                                  function renderTable() {
                                    const r = state.results;
                                    let rows = [...r.rows];

                                    if (state.sortCol >= 0) {
                                      const i = state.sortCol, asc = state.sortAsc;
                                      rows.sort((a, b) => {
                                        const va = a[i], vb = b[i];
                                        if (va == null && vb == null) return 0;
                                        if (va == null) return 1; if (vb == null) return -1;
                                        if (typeof va === 'number' && typeof vb === 'number') return asc ? va - vb : vb - va;
                                        return asc ? String(va).localeCompare(String(vb)) : String(vb).localeCompare(String(va));
                                      });
                                    }

                                    const total = rows.length, pages = Math.ceil(total / state.pageSize);
                                    const start = state.page * state.pageSize;
                                    const pageRows = rows.slice(start, start + state.pageSize);

                                    let html = '<table class="results"><thead><tr>';
                                    r.columns.forEach((c, i) => {
                                      const arrow = state.sortCol === i ? (state.sortAsc ? ' \u25b2' : ' \u25bc') : '';
                                      html += `<th onclick="sortBy(${i})">${esc(c.name)}<span class="type-badge">${esc(c.type)}</span>${arrow}</th>`;
                                    });
                                    html += '</tr></thead><tbody>';

                                    for (const row of pageRows) {
                                      html += '<tr>';
                                      for (const cell of row) {
                                        const v = cell == null ? '<span class="text-muted">null</span>' : esc(String(cell));
                                        html += `<td title="${esc(String(cell ?? ''))}">${v}</td>`;
                                      }
                                      html += '</tr>';
                                    }
                                    html += '</tbody></table>';

                                    $('#tableView').innerHTML = html;
                                    $('#tableView').classList.remove('hidden');

                                    let pg = '';
                                    if (pages > 1) {
                                      pg += `<button onclick="goPage(${state.page - 1})" ${state.page === 0 ? 'disabled' : ''} class="px-2 py-0.5 bg-bg border border-border rounded text-muted hover:text-text disabled:opacity-30">\u25c0</button>`;
                                      pg += `<span class="text-muted">${state.page + 1}/${pages}</span>`;
                                      pg += `<button onclick="goPage(${state.page + 1})" ${state.page >= pages - 1 ? 'disabled' : ''} class="px-2 py-0.5 bg-bg border border-border rounded text-muted hover:text-text disabled:opacity-30">\u25b6</button>`;
                                      pg += `<button onclick="copyAll()" class="px-2 py-0.5 bg-bg border border-border rounded text-muted hover:text-text ml-2" title="Copy all as JSON">Copy JSON</button>`;
                                    }
                                    $('#pagination').innerHTML = pg;
                                  }

                                  function renderChart(mode) {
                                    const r = state.results;
                                    if (!r?.columns?.length || r.columns.length < 2) {
                                      showError('Charts need at least 2 columns (label + value)');
                                      return;
                                    }

                                    let labelIdx = 0, valueIdx = -1;
                                    for (let i = 0; i < r.columns.length; i++) {
                                      const t = r.columns[i].type;
                                      if (valueIdx < 0 && (t === 'bigint' || t === 'double' || t === 'integer')) valueIdx = i;
                                    }
                                    if (valueIdx < 0) valueIdx = 1;
                                    if (valueIdx === 0) labelIdx = 1;

                                    const labels = r.rows.map(row => String(row[labelIdx] ?? ''));
                                    const values = r.rows.map(row => Number(row[valueIdx]) || 0);
                                    const maxVal = Math.max(...values, 1);

                                    const el = $('#chartView');
                                    el.classList.remove('hidden');

                                    if (mode === 'bar') {
                                      const barW = Math.max(20, Math.min(60, Math.floor((el.clientWidth - 80) / values.length) - 4));
                                      let html = `<div class="text-xs text-muted mb-2">${esc(r.columns[valueIdx].name)} by ${esc(r.columns[labelIdx].name)}</div>`;
                                      html += '<div class="flex items-end gap-1" style="height:300px">';
                                      values.forEach((v, i) => {
                                        const h = Math.max(2, (v / maxVal) * 280);
                                        html += `<div class="flex flex-col items-center" style="width:${barW}px">`;
                                        html += `<span class="text-xs text-muted mb-1">${formatNum(v)}</span>`;
                                        html += `<div class="chart-bar bg-accent rounded-t w-full" style="height:${h}px" title="${labels[i]}: ${v}"></div>`;
                                        html += `<span class="text-xs text-muted mt-1 truncate w-full text-center" title="${esc(labels[i])}">${esc(labels[i].slice(0, 12))}</span>`;
                                        html += '</div>';
                                      });
                                      html += '</div>';
                                      el.innerHTML = html;
                                    } else {
                                      const w = el.clientWidth - 60, h = 280, pad = 30;
                                      const step = values.length > 1 ? (w - pad * 2) / (values.length - 1) : 0;
                                      const points = values.map((v, i) => [pad + i * step, h - pad - (v / maxVal) * (h - pad * 2)]);
                                      const path = points.map((p, i) => `${i === 0 ? 'M' : 'L'}${p[0]},${p[1]}`).join(' ');

                                      let svg = `<div class="text-xs text-muted mb-2">${esc(r.columns[valueIdx].name)} over ${esc(r.columns[labelIdx].name)}</div>`;
                                      svg += `<svg width="${w}" height="${h}" class="overflow-visible">`;
                                      svg += `<line x1="${pad}" y1="${h - pad}" x2="${w - pad}" y2="${h - pad}" stroke="#44475a" stroke-width="1"/>`;
                                      svg += `<line x1="${pad}" y1="${pad}" x2="${pad}" y2="${h - pad}" stroke="#44475a" stroke-width="1"/>`;
                                      svg += `<path d="${path}" fill="none" stroke="#bd93f9" stroke-width="2"/>`;
                                      points.forEach((p, i) => {
                                        svg += `<circle cx="${p[0]}" cy="${p[1]}" r="3" fill="#bd93f9"><title>${labels[i]}: ${values[i]}</title></circle>`;
                                      });
                                      const skip = Math.max(1, Math.floor(labels.length / 8));
                                      points.forEach((p, i) => {
                                        if (i % skip === 0) svg += `<text x="${p[0]}" y="${h - 8}" text-anchor="middle" fill="#6272a4" font-size="10">${esc(labels[i].slice(0, 10))}</text>`;
                                      });
                                      svg += '</svg>';
                                      el.innerHTML = svg;
                                    }
                                  }

                                  // --- Helpers ---
                                  function esc(s) { const d = document.createElement('div'); d.textContent = s; return d.innerHTML; }
                                  function formatNum(n) { return n >= 1e6 ? (n/1e6).toFixed(1)+'M' : n >= 1e3 ? (n/1e3).toFixed(1)+'K' : String(Math.round(n * 100) / 100); }

                                  window.sortBy = i => { state.sortAsc = state.sortCol === i ? !state.sortAsc : true; state.sortCol = i; renderTable(); };
                                  window.goPage = p => { state.page = Math.max(0, p); renderTable(); };
                                  window.copyAll = () => {
                                    const r = state.results; if (!r) return;
                                    const json = r.rows.map(row => Object.fromEntries(r.columns.map((c, i) => [c.name, row[i]])));
                                    navigator.clipboard.writeText(JSON.stringify(json, null, 2));
                                    $('#statusBar').textContent = 'Copied to clipboard';
                                  };
                                  window.insertColumn = insertColumn;

                                  function renderHistory() {
                                    $('#history').innerHTML = state.history.map((sql, i) =>
                                      `<button onclick="loadHistory(${i})" class="px-2 py-0.5 bg-bg border border-border rounded text-muted hover:text-text truncate max-w-[120px]" title="${esc(sql)}">${esc(sql.slice(0, 20))}</button>`
                                    ).join('');
                                  }
                                  window.loadHistory = i => { $('#editor').value = state.history[i]; updateLineNums(); };

                                  function buildFallbackSchema() {
                                    return { tables: [
                                      { name: 'spans', columns: [{name:'trace_id',type:'varchar'},{name:'span_id',type:'varchar'},{name:'operation_name',type:'varchar'},{name:'service_name',type:'varchar'},{name:'duration_ms',type:'double'},{name:'start_time',type:'timestamp'},{name:'status',type:'varchar'}]},
                                      { name: 'logs', columns: [{name:'timestamp',type:'timestamp'},{name:'severity',type:'varchar'},{name:'body',type:'varchar'},{name:'service_name',type:'varchar'}]},
                                      { name: 'errors', columns: [{name:'error_id',type:'varchar'},{name:'error_type',type:'varchar'},{name:'error_message',type:'varchar'},{name:'first_seen',type:'timestamp'},{name:'event_count',type:'bigint'}]},
                                    ]};
                                  }

                                  init();
                                  </script>
                                  </body>
                                  </html>
                                  """;
}
