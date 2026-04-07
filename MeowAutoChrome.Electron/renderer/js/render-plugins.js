// Minimal renderPlugins implementation for Electron renderer
(function () {
    const pluginHost = document.getElementById('pluginHost');
    if (!pluginHost) {
        console.warn('render-plugins: pluginHost not found');
        window.renderPlugins = function () { /* noop */ };
        return;
    }

    function stateClass(state) {
        return state === 'Running' ? 'badge bg-success' : state === 'Paused' ? 'badge bg-warning text-dark' : state === 'Stopped' ? 'badge bg-secondary' : 'badge bg-info';
    }

    async function postJsonUrl(url, body) {
        const headers = { 'Content-Type': 'application/json' };
        if (window.BrowserIndex && window.BrowserIndex.browserHubConnectionId)
            headers['X-BrowserHub-ConnectionId'] = window.BrowserIndex.browserHubConnectionId;
        const res = await fetch(url, { method: 'POST', headers, body: JSON.stringify(body || {}) });
        if (!res.ok) {
            const t = await res.text().catch(() => null);
            throw new Error(t || ('Request failed: ' + res.status));
        }
        return await res.json().catch(() => null);
    }

    function api(key, fallback) {
        if (window.__apiEndpoints && window.__apiEndpoints[key]) return window.__apiEndpoints[key];
        if (fallback) return fallback;
        // best-effort derive from base plugins url
        if (window.__apiEndpoints && window.__apiEndpoints.plugins) return window.__apiEndpoints.plugins.replace(/\/$/, '') + '/' + key.replace(/^\//, '');
        return '/api/' + key.replace(/^\//, '');
    }

    function showToast(title, message, isError) {
        try {
            const container = document.getElementById('toastContainer') || (() => {
                const c = document.createElement('div'); c.id = 'toastContainer'; c.className = 'toast-container position-fixed top-0 end-0 p-3'; document.body.appendChild(c); return c;
            })();
            const el = document.createElement('div'); el.className = 'toast align-items-center text-bg-' + (isError ? 'danger' : 'success') + ' border-0'; el.setAttribute('role', 'status'); el.setAttribute('aria-live', 'polite'); el.setAttribute('aria-atomic', 'true');
            el.innerHTML = `<div class="d-flex"><div class="toast-body"><strong>${title}</strong><div>${message}</div></div><button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button></div>`;
            container.appendChild(el);
            const t = new bootstrap.Toast(el, { delay: 4000 }); t.show();
        } catch (e) { console.log(title, message); }
    }

    function normalizeExecutionData(data) {
        if (data === null || data === undefined)
            return {};

        if (Array.isArray(data))
            return { result: JSON.stringify(data) };

        if (typeof data === 'object') {
            const entries = Object.entries(data);
            return Object.fromEntries(entries.map(([key, value]) => [key, value === null || value === undefined ? '' : typeof value === 'string' ? value : JSON.stringify(value)]));
        }

        return { result: String(data) };
    }

    function pushExecutionResponseToPluginOutput(pluginId, targetId, response) {
        if (!pluginId || !targetId || !window.BrowserPlugins?.applyPluginOutputUpdate)
            return;

        const normalizedData = normalizeExecutionData(response?.data);
        const fallbackMessage = Object.keys(normalizedData).length === 0 && response?.state ? '状态: ' + response.state : null;
        window.BrowserPlugins.applyPluginOutputUpdate({
            pluginId,
            targetId: response?.targetId || targetId,
            message: response?.message ?? fallbackMessage,
            data: normalizedData,
            state: response?.state,
            openModal: false,
            timestampUtc: new Date().toISOString()
        });
    }

    window.renderPlugins = function (plugins) {
        pluginHost.replaceChildren();
        window.BrowserUI = window.BrowserUI || {};
        window.BrowserUI.pluginCatalog = window.BrowserUI.pluginCatalog || new Map();
        window.BrowserUI.pluginOutputUi = window.BrowserUI.pluginOutputUi || new Map();

        if (!plugins || plugins.length === 0) {
            const empty = document.createElement('div'); empty.className = 'text-muted'; empty.textContent = '未发现可用插件。'; pluginHost.appendChild(empty); return;
        }

        for (const plugin of plugins) {
            window.BrowserUI.pluginCatalog.set(plugin.id, plugin);

            const card = document.createElement('div'); card.className = 'border rounded p-2 mb-2';

            const header = document.createElement('div'); header.className = 'd-flex justify-content-between align-items-start gap-2 mb-2';
            const title = document.createElement('div'); title.className = 'fw-semibold'; title.textContent = plugin.name || plugin.id; header.appendChild(title);

            const actions = document.createElement('div'); actions.className = 'd-flex gap-2 align-items-center';
            const outputBtn = document.createElement('button'); outputBtn.type = 'button'; outputBtn.className = 'btn btn-sm btn-outline-secondary position-relative'; outputBtn.textContent = '消息';
            const outputBadge = document.createElement('span'); outputBadge.className = 'plugin-output-badge position-absolute top-0 start-0 translate-middle text-bg-danger d-none';
            outputBtn.appendChild(outputBadge);
            outputBtn.addEventListener('click', () => window.BrowserPlugins?.openPluginOutputModal?.(plugin.id));
            actions.appendChild(outputBtn);

            const outputModeBtn = document.createElement('button'); outputModeBtn.type = 'button'; outputModeBtn.className = 'btn btn-sm btn-outline-secondary';
            const syncOutputModeText = () => {
                const mode = window.BrowserPlugins?.getPluginOutputMode?.(plugin.id) || 'inline';
                outputModeBtn.textContent = mode === 'toast' ? 'Toast' : '面板';
                outputModeBtn.title = mode === 'toast' ? '当前新消息使用 Toast 展示，点击切回面板展示' : '当前新消息显示在卡片内，点击切换到 Toast';
            };
            syncOutputModeText();
            outputModeBtn.addEventListener('click', () => {
                const currentMode = window.BrowserPlugins?.getPluginOutputMode?.(plugin.id) || 'inline';
                const nextMode = currentMode === 'toast' ? 'inline' : 'toast';
                window.BrowserPlugins?.setPluginOutputMode?.(plugin.id, nextMode);
                syncOutputModeText();
                window.BrowserPlugins?.syncPluginOutputUi?.(plugin.id);
            });
            actions.appendChild(outputModeBtn);

            const state = document.createElement('span'); state.className = stateClass(plugin.state || ''); state.textContent = plugin.state || ''; actions.appendChild(state);

            header.appendChild(actions);
            card.appendChild(header);

            if (plugin.description) {
                const desc = document.createElement('div'); desc.className = 'text-muted mb-2'; desc.textContent = plugin.description; card.appendChild(desc);
            }

            const outputPreview = document.createElement('button');
            outputPreview.type = 'button';
            outputPreview.className = 'plugin-output-preview plugin-output-preview-button mb-2';
            outputPreview.addEventListener('click', () => window.BrowserPlugins?.openPluginOutputModal?.(plugin.id));
            const outputPreviewHeader = document.createElement('div');
            outputPreviewHeader.className = 'plugin-output-preview-header';
            const outputPreviewTitle = document.createElement('div');
            outputPreviewTitle.className = 'plugin-output-preview-title';
            outputPreviewTitle.textContent = '消息';
            const outputPreviewMode = document.createElement('div');
            outputPreviewMode.className = 'plugin-output-preview-mode';
            outputPreviewHeader.appendChild(outputPreviewTitle);
            outputPreviewHeader.appendChild(outputPreviewMode);
            const outputPreviewSummary = document.createElement('div');
            outputPreviewSummary.className = 'plugin-output-preview-summary';
            const outputPreviewMeta = document.createElement('div');
            outputPreviewMeta.className = 'plugin-output-preview-meta';
            outputPreview.appendChild(outputPreviewHeader);
            outputPreview.appendChild(outputPreviewSummary);
            outputPreview.appendChild(outputPreviewMeta);
            card.appendChild(outputPreview);

            window.BrowserUI.pluginOutputUi.set(plugin.id, {
                button: outputBtn,
                badge: outputBadge,
                preview: outputPreview,
                previewMode: outputPreviewMode,
                previewSummary: outputPreviewSummary,
                previewMeta: outputPreviewMeta
            });
            window.BrowserPlugins?.syncPluginOutputUi?.(plugin.id);

            // controls
            if ((plugin.controls || []).length) {
                const controlsWrap = document.createElement('div'); controlsWrap.className = 'd-flex flex-wrap gap-2 mb-2';
                const stateLower = (plugin.state || '').toLowerCase();
                for (const c of plugin.controls) {
                    const cmd = (c.command || '').toLowerCase();
                    const btn = document.createElement('button'); btn.type = 'button'; btn.className = 'btn btn-sm btn-outline-secondary'; btn.textContent = c.name || c.command;

                    // Enable/disable standard lifecycle controls based on plugin state
                    if (cmd === 'start') {
                        btn.disabled = (stateLower === 'running');
                    } else if (cmd === 'stop' || cmd === 'pause') {
                        btn.disabled = (stateLower !== 'running');
                    } else if (cmd === 'resume') {
                        btn.disabled = (stateLower !== 'paused');
                    }

                    btn.addEventListener('click', async () => {
                        try {
                            btn.disabled = true;
                            const url = api('pluginsControl', '/api/plugins/control');
                            const res = await postJsonUrl(url, { pluginId: plugin.id, command: c.command, arguments: {} });
                            pushExecutionResponseToPluginOutput(plugin.id, c.command, res);
                            showToast('控制', (res?.message || ('状态: ' + (res?.state || 'ok'))), false);
                            await (window.BrowserUI.loadPlugins?.() || Promise.resolve());
                        } catch (e) { showToast('控制失败', e.message || String(e), true); } finally { btn.disabled = false; }
                    });
                    controlsWrap.appendChild(btn);
                }
                card.appendChild(controlsWrap);
            }

            // functions
            const functionsWrap = document.createElement('div'); functionsWrap.className = 'd-flex flex-wrap gap-2';
            if ((plugin.functions || []).length) {
                for (const fn of plugin.functions) {
                    const btn = document.createElement('button'); btn.type = 'button'; btn.className = 'btn btn-sm btn-outline-primary'; btn.textContent = fn.name || fn.id;
                    btn.addEventListener('click', async () => {
                        try {
                            btn.disabled = true;
                            const url = api('pluginsRun', '/api/plugins/run');
                            const res = await postJsonUrl(url, { pluginId: plugin.id, functionId: fn.id, arguments: {} });
                            pushExecutionResponseToPluginOutput(plugin.id, fn.id, res);
                            showToast('执行', '返回: ' + ((res?.message && !res?.data) ? res.message : JSON.stringify(res?.data ?? res)), false);
                        } catch (e) { showToast('执行失败', e.message || String(e), true); } finally { btn.disabled = false; }
                    });
                    functionsWrap.appendChild(btn);
                }
            } else {
                const empty = document.createElement('div'); empty.className = 'text-muted'; empty.textContent = '无可用函数'; functionsWrap.appendChild(empty);
            }
            card.appendChild(functionsWrap);

            pluginHost.appendChild(card);
        }
    };

})();
