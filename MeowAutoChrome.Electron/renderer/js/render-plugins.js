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

    window.renderPlugins = function (plugins) {
        pluginHost.replaceChildren();
        window.BrowserUI = window.BrowserUI || {};
        window.BrowserUI.pluginCatalog = window.BrowserUI.pluginCatalog || new Map();

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

            const unloadBtn = document.createElement('button'); unloadBtn.type = 'button'; unloadBtn.className = 'btn btn-sm btn-outline-danger'; unloadBtn.textContent = '卸载';
            unloadBtn.addEventListener('click', async () => {
                if (!confirm('确认卸载插件：' + plugin.name + '？')) return;
                try {
                    unloadBtn.disabled = true;
                    const url = (window.__apiEndpoints && window.__apiEndpoints.plugins) ? window.__apiEndpoints.plugins.replace(/\/$/, '') + '/unload' : '/api/plugins/unload';
                    const res = await postJsonUrl(url, { pluginId: plugin.id });
                    showToast('卸载', res?.success ? '卸载成功' : '卸载返回: ' + JSON.stringify(res), !res?.success);
                    await (window.BrowserUI.loadPlugins?.() || Promise.resolve());
                } catch (e) { showToast('卸载失败', e.message || String(e), true); }
                finally { unloadBtn.disabled = false; }
            });
            actions.appendChild(unloadBtn);

            header.appendChild(actions);
            card.appendChild(header);

            if (plugin.description) {
                const desc = document.createElement('div'); desc.className = 'text-muted mb-2'; desc.textContent = plugin.description; card.appendChild(desc);
            }

            // controls
            if ((plugin.controls || []).length) {
                const controlsWrap = document.createElement('div'); controlsWrap.className = 'd-flex flex-wrap gap-2 mb-2';
                for (const c of plugin.controls) {
                    const btn = document.createElement('button'); btn.type = 'button'; btn.className = 'btn btn-sm btn-outline-secondary'; btn.textContent = c.name || c.command;
                    btn.addEventListener('click', async () => {
                        try { btn.disabled = true; const url = api('pluginsControl', '/api/plugins/control'); const res = await postJsonUrl(url, { pluginId: plugin.id, command: c.command, arguments: {} }); showToast('控制', '已发送: ' + (res?.status ?? 'ok'), false); await (window.BrowserUI.loadPlugins?.() || Promise.resolve()); } catch (e) { showToast('控制失败', e.message || String(e), true); } finally { btn.disabled = false; }
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
                        try { btn.disabled = true; const url = api('pluginsRun', '/api/plugins/run'); const res = await postJsonUrl(url, { pluginId: plugin.id, functionId: fn.id, arguments: {} }); showToast('执行', '返回: ' + (res?.result ?? JSON.stringify(res)), false); } catch (e) { showToast('执行失败', e.message || String(e), true); } finally { btn.disabled = false; }
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
