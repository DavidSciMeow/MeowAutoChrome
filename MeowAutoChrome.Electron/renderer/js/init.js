// init.js: initialize API endpoints, check Playwright runtime, then fetch status and start SignalR when running under Electron (file://)
(async function () {
    const candidates = [
        (window.__apiBase || 'http://127.0.0.1:5000'),
        'http://localhost:5000',
        'http://127.0.0.1:5001',
        'http://localhost:5001'
    ];
    const gateElement = document.getElementById('playwrightGate');
    const gateMessage = document.getElementById('playwrightGateMessage');
    const gateStatus = document.getElementById('playwrightGateStatus');
    const gateInstallDir = document.getElementById('playwrightGateInstallDir');
    const gateScriptPath = document.getElementById('playwrightGateScriptPath');
    const gateInstallMode = document.getElementById('playwrightGateInstallMode');
    const gateInstallBtn = document.getElementById('playwrightGateInstallBtn');
    const gateRetryBtn = document.getElementById('playwrightGateRetryBtn');
    const gateOutput = document.getElementById('playwrightGateOutput');
    let initialized = false;

    async function probe(url) {
        try {
            const res = await fetch(url + '/health', { method: 'GET', cache: 'no-store' });
            return res.ok;
        } catch {
            return false;
        }
    }

    let base = candidates[0];
    for (const c of candidates) {
        if (window.__apiBase && c !== window.__apiBase) continue;
        try {
            const ok = await probe(c);
            if (ok) {
                base = c;
                break;
            }
        } catch { }
    }

    function mk(p) {
        return base.replace(/\/$/, '') + '/' + p.replace(/^\//, '');
    }

    function setGateVisible(visible) {
        gateElement?.classList.toggle('d-none', !visible);
    }

    function applyInstallModeOptions(select, status) {
        if (!select) return;
        const offlineOption = select.querySelector('option[value="offline"]');
        if (offlineOption) offlineOption.disabled = !status?.offlinePackageAvailable;
        if (status?.offlinePackageAvailable) {
            select.value = 'offline';
        } else if (select.value === 'offline' && !status?.offlinePackageAvailable) {
            select.value = 'online';
        }
    }

    function applyPlaywrightStatus(status) {
        window.__playwrightRuntimeStatus = status || null;
        window.dispatchEvent(new CustomEvent('meow:playwright-status', { detail: status || null }));

        if (!status) {
            setGateVisible(true);
            if (gateMessage) gateMessage.textContent = '无法读取 Playwright 运行时状态。';
            if (gateStatus) gateStatus.textContent = '状态读取失败';
            if (gateOutput) {
                gateOutput.textContent = '';
                gateOutput.classList.add('d-none');
            }
            applyInstallModeOptions(gateInstallMode, null);
            return;
        }

        if (gateInstallDir) gateInstallDir.textContent = status.browserInstallDirectory || '未知';
        if (gateScriptPath) gateScriptPath.textContent = status.scriptPath || '未找到';
        if (gateMessage) gateMessage.textContent = status.message || (status.isInstalled ? 'Playwright Chromium 已就绪。' : 'Playwright Chromium 尚未安装。');
        if (gateStatus) gateStatus.textContent = status.isInstalled ? '已安装，可继续使用。' : '未安装，浏览器能力已被阻断。';
        applyInstallModeOptions(gateInstallMode, status);
        if (gateOutput) {
            const output = status.output || '';
            gateOutput.textContent = output;
            gateOutput.classList.toggle('d-none', !output);
        }

        setGateVisible(!status.isInstalled);
    }

    async function getJson(url, options) {
        const response = await fetch(url, options);
        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
            throw new Error(payload.error || payload.message || ('请求失败: ' + response.status));
        }
        return payload;
    }

    async function loadPlaywrightStatus() {
        const status = await getJson(window.__apiEndpoints.playwrightStatus, { cache: 'no-store' });
        applyPlaywrightStatus(status);
        return status;
    }

    async function bootstrapBrowser() {
        if (initialized) return;
        initialized = true;

        try {
            const statusResponse = await fetch(window.__apiEndpoints.status, { cache: 'no-store' });
            if (statusResponse.ok) {
                const data = await statusResponse.json();
                window.__browserConfig = window.__browserConfig || {};
                window.__browserConfig.defaultPluginPanelWidth = data.pluginPanelWidth ?? window.__browserConfig.defaultPluginPanelWidth ?? 320;
                window.__browserConfig.minPluginPanelWidth = window.__browserConfig.minPluginPanelWidth ?? 200;
                window.__browserConfig.maxPluginPanelWidth = window.__browserConfig.maxPluginPanelWidth ?? 900;
                try { window.BrowserUI?.applyStatus?.(data); } catch { }
            }
        } catch (e) {
            console.warn('无法读取状态', e);
        }

        try {
            const hubUrl = base.replace(/\/$/, '') + '/browserHub';
            const conn = new signalR.HubConnectionBuilder().withUrl(hubUrl).withAutomaticReconnect().build();

            conn.on('ReceiveFrame', (data) => {
                try { window.BrowserScreencast?.drawFrame?.({ data }); } catch (e) { console.warn(e); }
            });
            conn.on('ScreencastDisabled', () => {
                const noSig = document.getElementById('noSignal');
                if (noSig) {
                    noSig.textContent = '实时画面已关闭';
                    noSig.style.display = 'flex';
                }
            });
            conn.on('ReceivePluginOutput', update => {
                try { window.BrowserPlugins?.applyPluginOutputUpdate?.(update); } catch (e) { console.warn(e); }
            });
            conn.on('StatusUpdated', (status) => {
                try { window.BrowserUI?.applyStatus?.(status); } catch (e) { console.warn(e); }
            });

            conn.onreconnecting(() => {
                const status = document.getElementById('connStatus');
                if (status) {
                    status.textContent = '重连中...';
                    status.className = 'badge bg-warning text-dark';
                }
            });

            conn.onreconnected(connectionId => {
                const status = document.getElementById('connStatus');
                if (status) {
                    status.textContent = '已连接';
                    status.className = 'badge bg-success';
                }
                window.BrowserIndex = window.BrowserIndex || {};
                window.BrowserIndex.browserHubConnectionId = connectionId || conn.connectionId || null;
                try { window.BrowserUI?.refreshStatus?.(); window.BrowserUI?.loadPlugins?.(); } catch { }
            });

            conn.onclose(() => {
                const status = document.getElementById('connStatus');
                if (status) {
                    status.textContent = '已断开';
                    status.className = 'badge bg-danger';
                }
                const noSig = document.getElementById('noSignal');
                if (noSig) noSig.style.display = 'flex';
            });

            await conn.start();
            window.BrowserIndex = window.BrowserIndex || {};
            window.BrowserIndex.browserHubConnectionId = conn.connectionId || null;
            try { window.BrowserInput?.init?.(conn, document.getElementById('browserCanvas')); } catch (e) { console.warn(e); }
            try { window.BrowserUI?.refreshStatus?.(); window.BrowserUI?.loadPlugins?.(); } catch (e) { console.warn(e); }
        } catch (e) {
            console.warn('SignalR 连接失败', e);
            const status = document.getElementById('connStatus');
            if (status) {
                status.textContent = '连接失败';
                status.className = 'badge bg-danger';
            }
        }
    }

    async function installPlaywrightFromGate() {
        const mode = gateInstallMode?.value || 'online';
        if (gateInstallBtn) gateInstallBtn.disabled = true;
        if (gateRetryBtn) gateRetryBtn.disabled = true;
        if (gateStatus) gateStatus.textContent = mode === 'offline' ? '正在离线安装 Chromium，请稍候...' : '正在在线安装 Chromium，请稍候...';

        try {
            const payload = await getJson(window.__apiEndpoints.playwrightInstall + '?mode=' + encodeURIComponent(mode), { method: 'POST' });
            applyPlaywrightStatus(payload);
            if (payload.isInstalled) {
                await bootstrapBrowser();
                try { window.BrowserUI?.refreshStatus?.(); } catch { }
                window.showNotification?.(mode === 'offline' ? 'Playwright Chromium 离线安装完成。' : 'Playwright Chromium 在线安装完成。', 'success');
            }
        } catch (error) {
            applyPlaywrightStatus(window.__playwrightRuntimeStatus || null);
            if (gateStatus) gateStatus.textContent = error.message || '安装失败';
            if (gateOutput) {
                gateOutput.textContent = error.message || 'Playwright Chromium 安装失败。';
                gateOutput.classList.remove('d-none');
            }
            window.showNotification?.(error.message || 'Playwright Chromium 安装失败。', 'danger');
        } finally {
            if (gateInstallBtn) gateInstallBtn.disabled = false;
            if (gateRetryBtn) gateRetryBtn.disabled = false;
        }
    }

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
        input: mk('/api/input'),
        playwrightStatus: mk('/api/playwright/status'),
        playwrightInstall: mk('/api/playwright/install'),
        playwrightUninstall: mk('/api/playwright/uninstall'),
        playwrightUninstallAll: mk('/api/playwright/uninstall?all=true')
    });

    gateInstallBtn?.addEventListener('click', () => { installPlaywrightFromGate().catch(() => { }); });
    gateRetryBtn?.addEventListener('click', () => {
        loadPlaywrightStatus().catch((error) => {
            console.warn('重新检查 Playwright 状态失败', error);
            applyPlaywrightStatus(null);
        });
    });

    try {
        const playwrightStatus = await loadPlaywrightStatus();
        if (!playwrightStatus.isInstalled) return;
        await bootstrapBrowser();
    } catch (error) {
        console.warn('初始化 Playwright 状态失败', error);
        applyPlaywrightStatus(null);
        if (gateStatus) gateStatus.textContent = error.message || '状态读取失败';
    }
})();
