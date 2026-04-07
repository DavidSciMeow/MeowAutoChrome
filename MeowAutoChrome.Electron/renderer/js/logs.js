// logs.js: simple logs page logic — fetch recent logs, render, connect to logHub
(function () {
    'use strict';

    function getApiEndpoint(name) {
        try {
            if (window.__apiEndpoints && window.__apiEndpoints[name]) return window.__apiEndpoints[name];
        } catch { }
        // fallback
        if (name === 'logsContent') return '/api/logs/content';
        if (name === 'logsClear') return '/api/logs/clear';
        return '/' + name;
    }

    function buildHubUrl() {
        try {
            if (window.__apiBase) return window.__apiBase.replace(/\/$/, '') + '/logHub';
            if (window.__apiEndpoints && window.__apiEndpoints.logsContent) {
                return window.__apiEndpoints.logsContent.replace(/\/api\/logs\/content\/?$/, '') + '/logHub';
            }
        } catch { }
        return '/logHub';
    }

    // Deterministic hash -> color utilities for category pills
    function hashCode(str) {
        if (!str) return 0;
        let h = 0;
        for (let i = 0; i < str.length; i++) {
            h = ((h << 5) - h) + str.charCodeAt(i);
            h |= 0;
        }
        return h;
    }

    function categoryColorStyle(cat) {
        if (!cat) return '';
        const hue = Math.abs(hashCode(cat)) % 360;
        const bg = `linear-gradient(180deg, hsla(${hue},60%,92%,1) 0%, hsla(${hue},60%,98%,1) 100%)`;
        const border = `1px solid hsla(${hue},60%,80%,1)`;
        const color = '#0f172a';
        return `background: ${bg}; border: ${border}; color: ${color};`;
    }

    // NOTE: auto-scroll removed — new entries will be prepended (newest at top)

    function toRowHtml(e) {
        const escaped = (s) => (s || '').toString().replace(/</g, '&lt;').replace(/>/g, '&gt;');
        const raw = escaped(JSON.stringify(e?._raw ? e._raw : e || {}, null, 0));
        const allEmpty = !(e?.TimestampText || e?.LevelText || e?.Category || e?.Message);
        // Main row with columns — single-line layout, category as pill
        // Render timestamp as a single-line compact block (date + time)
        const tsRaw = (e?.TimestampText || '').toString();

        const catStyle = categoryColorStyle(e?.Category);
        const main = `<div class="log-row d-flex align-items-center border-bottom">
            <div class="log-ts" style="width:180px;flex:0 0 180px;padding-right:8px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">${escaped(tsRaw)}</div>
                <div style="width:100px;flex:0 0 100px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;"><span class="badge bg-secondary" style="white-space:nowrap;display:inline-block;overflow:hidden;text-overflow:ellipsis;max-width:100%;">${escaped(e?.LevelText)}</span></div>
                <div style="width:260px;flex:0 0 260px;display:flex;align-items:center;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;"><span class="log-category" style="white-space:nowrap;display:inline-block;overflow:hidden;text-overflow:ellipsis;max-width:100%;${catStyle}">${escaped(e?.Category)}</span></div>
                <div class="log-message" style="flex:1 1 auto;min-width:0;">${escaped(e?.Message)}</div>
            </div>`;

        // If all fields empty, show only raw JSON prominently
        if (allEmpty) {
            return `<div class="log-row text-muted py-1 border-bottom"><div style="flex:1 1 auto;white-space:pre-wrap">${raw}</div></div>`;
        }

        // Always include a collapsible raw JSON block (hidden by default via CSS) for diagnostics
        return `${main}<div class="log-row-raw text-muted small" style="display:none; padding:.25rem .5rem 1rem 10px; white-space:pre-wrap;">${raw}</div>`;
    }

    // Normalize an incoming entry to a consistent shape so rendering code
    // doesn't need to care about camelCase vs PascalCase or other variants.
    function normalizeEntry(src) {
        if (!src) return { TimestampText: '', LevelText: '', FilterLevel: '', Category: '', Message: '', _raw: src };
        const e = src;
        const ts = e.timestampText ?? e.TimestampText ?? e.timestamp ?? e.Timestamp ?? '';
        const levelText = e.levelText ?? e.LevelText ?? (e.level != null ? String(e.level) : '') ?? '';
        let filterLevel = e.filterLevel ?? e.FilterLevel ?? '';
        const category = e.category ?? e.Category ?? e.CategoryName ?? '';
        const message = e.message ?? e.Message ?? e.msg ?? e.text ?? '';

        // If server didn't provide FilterLevel, derive it from numeric e.level or textual levelText
        if (!filterLevel) {
            const rawLevel = (e.level !== undefined && e.level !== null) ? e.level : levelText;
            if (rawLevel !== undefined && rawLevel !== null && rawLevel !== '') {
                // numeric LogLevel mapping: Trace=0, Debug=1, Information=2, Warning=3, Error=4, Critical=5
                if (typeof rawLevel === 'number' || /^\d+$/.test(String(rawLevel))) {
                    const lv = parseInt(rawLevel, 10);
                    if (lv === 3) filterLevel = 'warn';
                    else if (lv === 1) filterLevel = 'debug';
                    else if (lv === 4 || lv === 5) filterLevel = 'error';
                    else if (lv === 2) filterLevel = 'info';
                    else filterLevel = 'trace';
                } else {
                    const s = String(rawLevel).toLowerCase();
                    if (s === 'warning') filterLevel = 'warn';
                    else if (s === 'debug') filterLevel = 'debug';
                    else if (s === 'error' || s === 'critical') filterLevel = 'error';
                    else if (s === 'information' || s === 'info') filterLevel = 'info';
                    else if (s === 'trace') filterLevel = 'trace';
                    else filterLevel = 'trace';
                }
            } else {
                filterLevel = 'trace';
            }
        }

        return { TimestampText: ts, LevelText: levelText, FilterLevel: filterLevel, Category: category, Message: message, _raw: src };
    }

    async function init() {
        console.debug('logs.init starting');
        const container = document.getElementById('logsContainer');
        if (!container) return;

        const exportBtn = document.getElementById('exportLogsBtn');
        const clearBtn = document.getElementById('clearLogsBtn');
        const refreshBtn = document.getElementById('refreshLogsBtn');
        const filterLevel = document.getElementById('filterLevel');
        const filterCategory = document.getElementById('filterCategory');
        const filterText = document.getElementById('filterText');

        // auto-scroll UI/logic removed per user request

        let entries = [];
        // Keys for entries we've already rendered to avoid duplicates (JSON of raw or composite key)
        const seenKeys = new Set();
        let pollIntervalId = null;

        function entryKey(ne) {
            try {
                if (ne && ne._raw) return JSON.stringify(ne._raw);
            } catch { }
            return `${ne?.TimestampText}|${ne?.LevelText}|${ne?.Category}|${ne?.Message}`;
        }

        function applyFilters(list) {
            const lv = filterLevel?.value || '';
            const cat = filterCategory?.value || '';
            const txt = (filterText?.value || '').toLowerCase();
            return list.filter(e => {
                if (lv && e.FilterLevel !== lv) return false;
                if (cat && e.Category !== cat) return false;
                if (txt) {
                    const hay = (e.Message + ' ' + e.Category + ' ' + e.LevelText + ' ' + e.TimestampText).toLowerCase();
                    if (!hay.includes(txt)) return false;
                }
                return true;
            });
        }

        function renderAll() {
            // Render entries with newest first (entries array keeps newest at index 0)
            const filtered = applyFilters(entries);
            container.innerHTML = filtered.map(toRowHtml).join('');
            console.debug('renderAll: rendered', filtered.length, 'entries');
        }

        function addEntry(e) {
            const ne = normalizeEntry(e);
            const k = entryKey(ne);
            if (seenKeys.has(k)) { console.debug('addEntry: duplicate key', k); return; } // already rendered
            seenKeys.add(k);
            console.debug('addEntry: adding key', k);
            // keep entries array with newest first
            entries.unshift(ne);
            // update category options
            const exists = Array.from(filterCategory.options).some(o => o.value === ne.Category);
            if (!exists && ne.Category) {
                const opt = document.createElement('option'); opt.value = ne.Category; opt.textContent = ne.Category; filterCategory.appendChild(opt);
            }
            const shouldAppend = applyFilters([ne]).length > 0;
            if (!shouldAppend) return;
            const div = document.createElement('div'); div.innerHTML = toRowHtml(ne);
            // insert the new nodes at the top while preserving their internal order
            while (div.lastChild) container.insertBefore(div.lastChild, container.firstChild);
        }

        async function loadInitial() {
            try {
                const res = await fetch(getApiEndpoint('logsContent'), { cache: 'no-store' });
                if (!res.ok) return;
                const data = await res.json();
                // ensure newest entries appear first
                entries = (data.entries || []).map(normalizeEntry).reverse();
                // populate seen keys so polling/SignalR don't duplicate
                seenKeys.clear();
                for (const e of entries) {
                    try { seenKeys.add(entryKey(e)); } catch { }
                }
                console.debug('loadInitial: loaded', entries.length, 'entries');
                // populate categories
                const cats = new Set(entries.map(e => e.Category).filter(Boolean));
                cats.forEach(c => { const o = document.createElement('option'); o.value = c; o.textContent = c; filterCategory.appendChild(o); });
                renderAll();
            } catch (e) { console.warn('加载日志失败', e); }
        }

        // wire controls
        exportBtn?.addEventListener('click', () => {
            const filtered = applyFilters(entries);
            const rows = [['Timestamp', 'Level', 'Category', 'Message']].concat(filtered.map(e => [e.TimestampText, e.LevelText, e.Category, e.Message]));
            const csv = rows.map(r => r.map(v => '"' + ('' + (v ?? '')).replace(/"/g, '""') + '"').join(',')).join('\r\n');
            const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a'); a.href = url; a.download = 'meowautochrome-logs.csv'; document.body.appendChild(a); a.click(); a.remove(); URL.revokeObjectURL(url);
        });

        clearBtn?.addEventListener('click', async () => {
            if (!confirm('确定要清空日志文件吗？此操作不可恢复。')) return;
            try {
                const res = await fetch(getApiEndpoint('logsClear'), { method: 'POST' });
                if (res.ok) {
                    entries = [];
                    container.innerHTML = '';
                    // clear category list
                    filterCategory.innerHTML = '<option value="">所有插件/类别</option>';
                    alert('已清空日志');
                } else {
                    alert('清空日志失败');
                }
            } catch (e) { console.warn(e); alert('清空日志时发生错误'); }
        });

        refreshBtn?.addEventListener('click', loadInitial);
        filterLevel?.addEventListener('change', renderAll);
        filterCategory?.addEventListener('change', renderAll);
        filterText?.addEventListener('input', () => { setTimeout(renderAll, 100); });

        // auto-scroll UI removed; nothing to reflect

        await loadInitial();

        // Click handler (event delegation) to open modal with details
        container.addEventListener('click', (ev) => {
            try {
                const el = ev.target;
                const row = el.closest('.log-row');
                if (!row || !container.contains(row)) return;

                // raw JSON may be in the next sibling .log-row-raw or inside the row
                let rawEl = null;
                if (row.nextElementSibling && row.nextElementSibling.classList && row.nextElementSibling.classList.contains('log-row-raw')) rawEl = row.nextElementSibling;
                if (!rawEl) rawEl = row.querySelector('.log-row-raw');
                const rawText = rawEl ? rawEl.textContent?.trim() : row.textContent?.trim() || '';

                // extract columns if present
                const ts = (row.children && row.children[0]) ? row.children[0].textContent.trim() : '';
                let lvl = '';
                if (row.children && row.children[1]) {
                    const b = row.children[1].querySelector('.badge');
                    lvl = b ? b.textContent.trim() : row.children[1].textContent.trim();
                }
                const cat = (row.children && row.children[2]) ? row.children[2].textContent.trim() : '';
                const msg = (row.children && row.children[3]) ? row.children[3].textContent.trim() : '';

                // Only open modal when content is clipped/truncated or raw JSON exists
                const hasRaw = !!rawEl;
                const badgeEl = (row.children && row.children[1]) ? row.children[1].querySelector('.badge') : null;
                const catInner = (row.children && row.children[2]) ? row.children[2].querySelector('.log-category') : null;
                const msgInner = row.querySelector('.log-message') || (row.children && row.children[3]) || null;
                const isClipped = (el) => {
                    try { if (!el) return false; return el.scrollWidth > el.clientWidth + 1; } catch { return false; }
                };
                const shouldOpen = hasRaw || isClipped(row.children && row.children[0]) || isClipped(badgeEl) || isClipped(catInner) || isClipped(msgInner);
                if (!shouldOpen) return;

                const modalEl = document.getElementById('logDetailModal');
                if (!modalEl) return;
                const setText = (id, v) => { const el = document.getElementById(id); if (el) el.textContent = v || ''; };
                setText('logDetailTimestamp', ts);
                setText('logDetailLevel', lvl);
                setText('logDetailCategory', cat);
                setText('logDetailMessage', msg);
                setText('logDetailRaw', rawText);

                const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
                modal.show();
            } catch (e) { console.warn('open log detail failed', e); }
        });

        // copy JSON button
        const copyBtn = document.getElementById('logDetailCopyBtn');
        if (copyBtn) copyBtn.addEventListener('click', () => {
            const raw = document.getElementById('logDetailRaw')?.textContent || '';
            if (!raw) return alert('无 JSON 内容可复制');
            navigator.clipboard?.writeText(raw).then(() => { alert('已复制 JSON'); }).catch(() => { alert('复制失败'); });
        });

        // connect SignalR
        try {
            const hubUrl = buildHubUrl();
            console.debug('Attempting LogHub at', hubUrl, '(__apiBase=', window.__apiBase, ')');

            // Helper to wire handlers
            const wireHandlers = (connection) => {
                connection.on('ReceiveLog', (payload) => {
                    try { addEntry(payload); } catch (e) { console.warn(e); }
                });
                connection.onreconnected(() => { /* noop */ });
                connection.onclose(() => { /* noop */ });
            };

            // Try normal negotiate flow first
            let conn = new signalR.HubConnectionBuilder().withUrl(hubUrl).withAutomaticReconnect().build();
            wireHandlers(conn);
            try {
                await conn.start();
            } catch (err) {
                console.warn('LogHub 初次连接失败', err);
                const msg = (err && err.message) ? err.message : String(err || '');
                // If negotiate returned 404 / Not Found, attempt skipNegotiation + WebSockets transport as a fallback
                if (msg.includes('Not Found') || msg.includes('404') || msg.includes('Failed to complete negotiation')) {
                    console.debug('LogHub negotiate 404 — 尝试跳过协商并使用 WebSocket');
                    try {
                        conn = new signalR.HubConnectionBuilder().withUrl(hubUrl, { skipNegotiation: true, transport: signalR.HttpTransportType.WebSockets }).withAutomaticReconnect().build();
                        wireHandlers(conn);
                        await conn.start();
                        console.debug('LogHub 使用 WebSocket 连接成功');
                    } catch (err2) {
                        console.warn('使用 WebSocket 直接连接 LogHub 失败', err2);
                    }
                } else {
                    console.warn('LogHub 连接失败（非 negotiate 404）', err);
                }
            }
        } catch (e) {
            console.warn('LogHub 连接失败', e);
        }

        // Start polling fallback to keep UI refreshed even if SignalR not available
        function startPolling() {
            if (pollIntervalId) return;
            console.debug('startPolling: starting poll interval');
            pollIntervalId = setInterval(async () => {
                try {
                    console.debug('poll: fetching logs');
                    const res = await fetch(getApiEndpoint('logsContent'), { cache: 'no-store' });
                    if (!res.ok) { console.debug('poll: logsContent returned', res.status); return; }
                    const data = await res.json();
                    // data.entries is oldest->newest
                    const apiEntries = (data.entries || []).map(normalizeEntry);
                    let added = 0;
                    for (const ne of apiEntries) {
                        const k = entryKey(ne);
                        if (!seenKeys.has(k)) {
                            try { addEntry(ne); added++; } catch (e) { console.warn('poll addEntry failed', e); }
                        }
                    }
                    if (added > 0) console.debug('poll: added', added, 'new entries');
                } catch (err) {
                    console.warn('poll error', err);
                }
            }, 1500);
        }

        // always keep polling active so view updates even without SignalR
        startPolling();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();

})();
