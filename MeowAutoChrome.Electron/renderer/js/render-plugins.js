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
            let payload = null;
            try {
                payload = await res.json();
            } catch {
                const text = await res.text().catch(() => null);
                payload = text ? { detail: text } : null;
            }

            const message = payload?.detail || payload?.error || payload?.message || ('Request failed: ' + res.status);
            const error = new Error(message);
            error.status = res.status;
            error.payload = payload;
            throw error;
        }
        return await res.json().catch(() => null);
    }

    function formatRequestError(error) {
        const payload = error?.payload || null;
        const parts = [
            payload?.error,
            payload?.detail,
            error?.message
        ].filter((value, index, array) => value && array.indexOf(value) === index);

        return parts.length ? parts.join(' | ') : String(error || '未知错误');
    }

    function api(key, fallback) {
        if (window.__apiEndpoints && window.__apiEndpoints[key]) return window.__apiEndpoints[key];
        if (fallback) return fallback;
        // best-effort derive from base plugins url
        if (window.__apiEndpoints && window.__apiEndpoints.plugins) return window.__apiEndpoints.plugins.replace(/\/$/, '') + '/' + key.replace(/^\//, '');
        return '/api/' + key.replace(/^\//, '');
    }

    function showToast(title, message, isError, actionLabel, onAction) {
        try {
            const container = document.getElementById('toastContainer') || (() => {
                const c = document.createElement('div'); c.id = 'toastContainer'; c.className = 'toast-container position-fixed top-0 end-0 p-3'; document.body.appendChild(c); return c;
            })();
            const el = document.createElement('div'); el.className = 'toast align-items-center text-bg-' + (isError ? 'danger' : 'success') + ' border-0'; el.setAttribute('role', 'status'); el.setAttribute('aria-live', 'polite'); el.setAttribute('aria-atomic', 'true');
            const actionMarkup = actionLabel ? `<button type="button" class="btn btn-sm btn-light me-2 plugin-toast-action">${actionLabel}</button>` : '';
            el.innerHTML = `<div class="d-flex"><div class="toast-body"><strong>${title}</strong><div>${message}</div></div>${actionMarkup}<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button></div>`;
            container.appendChild(el);
            if (actionLabel && typeof onAction === 'function') {
                el.querySelector('.plugin-toast-action')?.addEventListener('click', () => {
                    try { onAction(); } finally { bootstrap.Toast.getOrCreateInstance(el).hide(); }
                });
            }
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
            toastRequested: false,
            timestampUtc: new Date().toISOString()
        });
    }

    async function executePluginTarget(btn, pluginId, targetId, endpointKey, fallbackUrl, requestBodyFactory, onSuccess) {
        btn.disabled = true;
        try {
            const url = api(endpointKey, fallbackUrl);
            const response = await postJsonUrl(url, requestBodyFactory());
            pushExecutionResponseToPluginOutput(pluginId, targetId, response);
            if (typeof onSuccess === 'function')
                await onSuccess(response);
        } finally {
            btn.disabled = false;
        }
    }

    async function openPluginInvocation(plugin, target, btn, options) {
        const parameters = Array.isArray(target?.parameters) ? target.parameters : [];
        const invoke = async (argumentsPayload) => {
            await executePluginTarget(
                btn,
                plugin.id,
                options.targetId,
                options.endpointKey,
                options.fallbackUrl,
                () => options.requestBodyFactory(argumentsPayload || {}),
                options.onSuccess);
        };

        if (!parameters.length || !window.BrowserPlugins?.openPluginArgumentModal) {
            await invoke({});
            return;
        }

        window.BrowserPlugins.openPluginArgumentModal(
            target.name || target.command || '插件参数',
            target.description || '',
            parameters,
            options.submitText,
            invoke);
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
            const outputPreviewBadge = document.createElement('span');
            outputPreviewBadge.className = 'plugin-output-preview-badge d-none';
            outputPreviewHeader.appendChild(outputPreviewTitle);
            outputPreviewHeader.appendChild(outputPreviewBadge);
            const outputPreviewSummary = document.createElement('div');
            outputPreviewSummary.className = 'plugin-output-preview-summary';
            const outputPreviewMeta = document.createElement('div');
            outputPreviewMeta.className = 'plugin-output-preview-meta';
            outputPreview.appendChild(outputPreviewHeader);
            outputPreview.appendChild(outputPreviewSummary);
            outputPreview.appendChild(outputPreviewMeta);
            card.appendChild(outputPreview);

            window.BrowserUI.pluginOutputUi.set(plugin.id, {
                preview: outputPreview,
                previewBadge: outputPreviewBadge,
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
                            await openPluginInvocation(plugin, c, btn, {
                                targetId: c.command,
                                endpointKey: 'pluginsControl',
                                fallbackUrl: '/api/plugins/control',
                                submitText: c.name || '执行',
                                requestBodyFactory: (argumentsPayload) => ({ pluginId: plugin.id, command: c.command, arguments: argumentsPayload }),
                                onSuccess: async () => {
                                    await (window.BrowserUI.loadPlugins?.() || Promise.resolve());
                                }
                            });
                        } catch (e) { showToast('控制失败', formatRequestError(e), true); } finally { btn.disabled = false; }
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
                            await openPluginInvocation(plugin, fn, btn, {
                                targetId: fn.id,
                                endpointKey: 'pluginsRun',
                                fallbackUrl: '/api/plugins/run',
                                submitText: fn.name || '执行',
                                requestBodyFactory: (argumentsPayload) => ({ pluginId: plugin.id, functionId: fn.id, arguments: argumentsPayload })
                            });
                        } catch (e) { showToast('执行失败', formatRequestError(e), true); } finally { btn.disabled = false; }
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
