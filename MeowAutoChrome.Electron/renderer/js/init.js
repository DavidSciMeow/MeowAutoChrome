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
    const gateArchivePanel = document.getElementById('playwrightGateArchivePanel');
    const gateArchivePath = document.getElementById('playwrightGateArchivePath');
    const gateArchiveChooseBtn = document.getElementById('playwrightGateArchiveChooseBtn');
    const gateArchiveValidation = document.getElementById('playwrightGateArchiveValidation');
    const gateDownloadLinks = document.getElementById('playwrightGateDownloadLinks');
    const gateInstallBtn = document.getElementById('playwrightGateInstallBtn');
    const gateRetryBtn = document.getElementById('playwrightGateRetryBtn');
    const gateOutput = document.getElementById('playwrightGateOutput');
    let initialized = false;
    let validatingGateArchive = false;

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

    function renderDownloadLinks(container, links) {
        if (!container) return;
        container.replaceChildren();
        for (const link of links || []) {
            const row = document.createElement('div');
            const anchor = document.createElement('a');
            anchor.href = link.url;
            anchor.target = '_blank';
            anchor.rel = 'noreferrer noopener';
            anchor.textContent = link.label || link.url;
            row.appendChild(anchor);
            if (link.description) {
                const text = document.createElement('span');
                text.className = 'text-muted';
                text.textContent = ' - ' + link.description;
                row.appendChild(text);
            }
            container.appendChild(row);
        }
    }

    function renderArchiveValidation(container, result, idleText) {
        if (!container) return;
        if (!result) {
            container.replaceChildren();
            container.className = 'playwright-archive-validation d-none';
            if (idleText) {
                const title = document.createElement('div');
                title.className = 'playwright-archive-validation-title';
                title.textContent = idleText;
                container.appendChild(title);
                container.className = 'playwright-archive-validation';
            }
            return;
        }

        container.replaceChildren();
        container.className = 'playwright-archive-validation ' + (result.isValid ? 'is-valid' : 'is-invalid');
        const title = document.createElement('div');
        title.className = 'playwright-archive-validation-title';
        title.textContent = result.summary || (result.isValid ? '校验通过' : '校验失败');
        container.appendChild(title);
        const detail = document.createElement('div');
        detail.className = 'playwright-archive-validation-detail';
        detail.textContent = result.detail || '';
        container.appendChild(detail);
        const checks = document.createElement('div');
        checks.className = 'playwright-archive-validation-checks';
        const items = [
            ['文件存在', !!result.exists],
            ['文件名是 chrome-win64.zip', !!result.fileNameMatches],
            ['压缩包可读取', !!result.archiveReadable],
            ['包含 chrome-win64/chrome.exe', !!result.containsExpectedLayout]
        ];
        for (const [label, ok] of items) {
            const item = document.createElement('div');
            item.className = 'playwright-archive-validation-check ' + (ok ? 'is-pass' : 'is-fail');
            item.textContent = (ok ? '通过' : '失败') + ' · ' + label;
            checks.appendChild(item);
        }
        container.appendChild(checks);
    }

    async function validateGateArchive() {
        const archivePath = gateArchivePath?.value?.trim() || '';
        if (!archivePath) {
            renderArchiveValidation(gateArchiveValidation, null, '请选择一个 chrome-win64.zip 后，系统会在这里显示校验结果。');
            return null;
        }

        if (validatingGateArchive) return null;
        validatingGateArchive = true;
        renderArchiveValidation(gateArchiveValidation, {
            isValid: false,
            exists: false,
            fileNameMatches: false,
            archiveReadable: false,
            containsExpectedLayout: false,
            summary: '正在校验压缩包...',
            detail: '正在检查文件是否存在、文件名是否正确，以及压缩包内部结构是否符合预期。'
        });
        try {
            const response = await fetch(window.__apiEndpoints.playwrightValidateArchive, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ archivePath })
            });
            const payload = await response.json().catch(() => ({}));
            if (!response.ok) {
                throw new Error(payload.error || payload.message || '压缩包校验失败');
            }
            renderArchiveValidation(gateArchiveValidation, payload);
            return payload;
        } catch (error) {
            renderArchiveValidation(gateArchiveValidation, {
                isValid: false,
                exists: false,
                fileNameMatches: false,
                archiveReadable: false,
                containsExpectedLayout: false,
                summary: '校验请求失败',
                detail: error.message || '无法完成压缩包校验。'
            });
            return null;
        } finally {
            validatingGateArchive = false;
        }
    }

    function syncGateInstallControls(status) {
        if (gateArchivePanel)
            gateArchivePanel.classList.remove('d-none');

        if (gateInstallBtn)
            gateInstallBtn.textContent = '导入并识别压缩包';

        if (gateDownloadLinks)
            renderDownloadLinks(gateDownloadLinks, status?.downloadLinks || []);

        if (gateArchivePath?.value?.trim()) {
            validateGateArchive().catch(() => { });
        } else {
            renderArchiveValidation(gateArchiveValidation, null, '请选择一个 chrome-win64.zip 后，系统会在这里显示校验结果。');
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
            syncGateInstallControls(null);
            return;
        }

        if (gateInstallDir) gateInstallDir.textContent = status.browserInstallDirectory || '未知';
        if (gateScriptPath) gateScriptPath.textContent = status.scriptPath || '未找到';
        if (gateMessage) gateMessage.textContent = status.message || (status.isInstalled ? 'Playwright Chromium 已就绪。' : 'Playwright Chromium 尚未安装。');
        if (gateStatus) gateStatus.textContent = status.isInstalled ? '已安装，可继续使用。' : '未安装，浏览器能力已被阻断。';
        syncGateInstallControls(status);
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
        const archivePath = gateArchivePath?.value?.trim() || null;
        if (!archivePath) {
            if (gateStatus) gateStatus.textContent = '请先选择本地 chrome-win64.zip';
            window.showNotification?.('在线安装已移除，请先下载并选择 chrome-win64.zip。', 'warning');
            return;
        }

        if (gateInstallBtn) gateInstallBtn.disabled = true;
        if (gateRetryBtn) gateRetryBtn.disabled = true;
        if (gateStatus) gateStatus.textContent = '正在识别并导入本地压缩包，请稍候...';

        try {
            const validation = await validateGateArchive();
            if (!validation?.isValid) {
                if (gateStatus) gateStatus.textContent = validation?.summary || '压缩包校验失败';
                return;
            }

            const payload = await getJson(window.__apiEndpoints.playwrightInstall, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ archivePath })
            });
            applyPlaywrightStatus(payload);
            if (payload.operationState === 'skipped') {
                if (gateStatus) gateStatus.textContent = payload.message || '已检测到浏览器已安装，未重复安装。';
                window.showNotification?.(payload.message || '已检测到浏览器已安装，未重复安装。', 'warning');
                return;
            }

            if (payload.isInstalled) {
                await bootstrapBrowser();
                try { window.BrowserUI?.refreshStatus?.(); } catch { }
                window.showNotification?.('已根据压缩包识别结果完成导入。', 'success');
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
        pluginsInstalled: mk('/api/plugins/installed'),
        pluginsAssemblyState: mk('/api/plugins/assembly-state'),
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
        playwrightValidateArchive: mk('/api/playwright/validate-archive'),
        playwrightUninstall: mk('/api/playwright/uninstall'),
        playwrightUninstallAll: mk('/api/playwright/uninstall?all=true')
    });

    gateInstallBtn?.addEventListener('click', () => { installPlaywrightFromGate().catch(() => { }); });
    gateArchiveChooseBtn?.addEventListener('click', async () => {
        try {
            const result = await window.meow?.chooseFile?.([{ name: 'ZIP Archives', extensions: ['zip'] }]);
            if (!result || result.canceled || !result.path) return;
            if (gateArchivePath) gateArchivePath.value = result.path;
            await validateGateArchive();
            syncGateInstallControls(window.__playwrightRuntimeStatus || null);
        } catch (error) {
            console.warn('选择离线压缩包失败', error);
        }
    });
    gateArchivePath?.addEventListener('input', () => syncGateInstallControls(window.__playwrightRuntimeStatus || null));
    gateArchivePath?.addEventListener('change', () => { validateGateArchive().catch(() => { }); syncGateInstallControls(window.__playwrightRuntimeStatus || null); });
    gateArchivePath?.addEventListener('blur', () => { validateGateArchive().catch(() => { }); syncGateInstallControls(window.__playwrightRuntimeStatus || null); });
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
