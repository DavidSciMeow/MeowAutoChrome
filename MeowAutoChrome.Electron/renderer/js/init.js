// init.js: initialize API endpoints, fetch status, and start SignalR hub when running under Electron (file://)
(async function () {
    const candidates = [
        (window.__apiBase || 'http://127.0.0.1:5000'),
        'http://localhost:5000',
        'http://127.0.0.1:5001',
        'http://localhost:5001'
    ];

    async function probe(url) {
        try {
            const res = await fetch(url + '/health', { method: 'GET', cache: 'no-store' });
            return res.ok;
        } catch { return false; }
    }

    let base = candidates[0];
    for (const c of candidates) {
        // fast probe; break on first healthy
        // prefer explicit __apiBase when present
        if (window.__apiBase && c !== window.__apiBase) continue;
        // use probe but don't block too long
        try {
            const ok = await probe(c);
            if (ok) { base = c; break; }
        } catch { }
    }

    function mk(p) { return base.replace(/\/$/, '') + '/' + p.replace(/^\//, ''); }

    window.__apiEndpoints = Object.assign(window.__apiEndpoints || {}, {
        status: mk('/api/status'),
        settings: mk('/api/settings'),
        settingsAutosave: mk('/api/settings/autosave'),
        settingsReset: mk('/api/settings/reset'),
        plugins: mk('/api/plugins'),
        pluginsControl: mk('/api/plugins/control'),
        pluginsRun: mk('/api/plugins/run'),
        pluginsUpload: mk('/api/plugins/upload'),
        pluginsRoot: mk('/api/plugins/root'),
        pluginsLoad: mk('/api/plugins/load'),
        pluginsRefresh: mk('/api/plugins/refresh'),
        pluginsUnload: mk('/api/plugins/unload'),
        pluginsDelete: mk('/api/plugins/delete'),
        tabsClose: mk('/api/tabs/close'),
        tabsSelect: mk('/api/tabs/select'),
        tabsNew: mk('/api/tabs/new'),
        instances: mk('/api/instances'),
        instancesPreview: mk('/api/instances/preview'),
        instancesValidateFolder: mk('/api/instances/validate-folder'),
        instancesClose: mk('/api/instances/close'),
        instancesHeadless: mk('/api/instances/headless'),
        instancesViewport: mk('/api/instances/viewport'),
        instancesSettings: mk('/api/instances/settings'),
        screencastSettings: mk('/api/screencast/settings'),
        screenshot: mk('/api/screenshot'),
        navigation: mk('/api/navigation/navigate'),
        navigationBack: mk('/api/navigation/back'),
        navigationForward: mk('/api/navigation/forward'),
        navigationReload: mk('/api/navigation/reload'),
        layout: mk('/api/layout'),
        logsContent: mk('/api/logs/content'),
        logsClear: mk('/api/logs/clear'),
        input: mk('/api/input')
    });

    // fetch status to populate window.__browserConfig
    try {
        const s = await fetch(window.__apiEndpoints.status, { cache: 'no-store' });
        if (s.ok) {
            const data = await s.json();
            window.__browserConfig = window.__browserConfig || {};
            window.__browserConfig.defaultPluginPanelWidth = data.pluginPanelWidth ?? window.__browserConfig.defaultPluginPanelWidth ?? 320;
            window.__browserConfig.minPluginPanelWidth = window.__browserConfig.minPluginPanelWidth ?? 200;
            window.__browserConfig.maxPluginPanelWidth = window.__browserConfig.maxPluginPanelWidth ?? 900;
            // allow other scripts to react
            if (window.BrowserUI && typeof window.BrowserUI.applyStatus === 'function') try { window.BrowserUI.applyStatus(data); } catch { }
        }
    } catch (e) { console.warn('无法读取状态', e); }

    // start SignalR hub using absolute URL (since origin may be file://)
    try {
        const hubUrl = base.replace(/\/$/, '') + '/browserHub';
        const conn = new signalR.HubConnectionBuilder().withUrl(hubUrl).withAutomaticReconnect().build();

        conn.on('ReceiveFrame', (data, w, h) => {
            try { window.BrowserScreencast?.drawFrame?.({ data }); } catch (e) { console.warn(e); }
        });
        conn.on('ScreencastDisabled', () => { const noSig = document.getElementById('noSignal'); if (noSig) { noSig.textContent = '实时画面已关闭'; noSig.style.display = 'flex'; } });
        conn.on('ReceivePluginOutput', update => { try { window.BrowserPlugins?.applyPluginOutputUpdate?.(update); } catch (e) { console.warn(e); } });

        // TabsUpdated removed — clients should rely on StatusUpdated for full state

        // Receive full status payload and apply immediately (preferred)
        conn.on('StatusUpdated', (status) => {
            try { window.BrowserUI?.applyStatus?.(status); } catch (e) { console.warn(e); }
        });

        conn.onreconnecting(() => { const status = document.getElementById('connStatus'); if (status) { status.textContent = '重连中...'; status.className = 'badge bg-warning text-dark'; } });
        conn.onreconnected(connectionId => {
            const status = document.getElementById('connStatus'); if (status) { status.textContent = '已连接'; status.className = 'badge bg-success'; }
            window.BrowserIndex = window.BrowserIndex || {};
            window.BrowserIndex.browserHubConnectionId = connectionId || conn.connectionId || null;
            try { window.BrowserUI?.refreshStatus?.(); window.BrowserUI?.loadPlugins?.(); } catch { }
        });

        conn.onclose(() => {
            const status = document.getElementById('connStatus'); if (status) { status.textContent = '已断开'; status.className = 'badge bg-danger'; }
            const noSig = document.getElementById('noSignal'); if (noSig) noSig.style.display = 'flex';
        });

        await conn.start();
        window.BrowserIndex = window.BrowserIndex || {};
        window.BrowserIndex.browserHubConnectionId = conn.connectionId || null;
        try { window.BrowserInput?.init?.(conn, document.getElementById('browserCanvas')); } catch (e) { console.warn(e); }
        try { window.BrowserUI?.refreshStatus?.(); window.BrowserUI?.loadPlugins?.(); } catch (e) { console.warn(e); }
    } catch (e) {
        console.warn('SignalR 连接失败', e);
        const status = document.getElementById('connStatus'); if (status) { status.textContent = '连接失败'; status.className = 'badge bg-danger'; }
    }

})();
