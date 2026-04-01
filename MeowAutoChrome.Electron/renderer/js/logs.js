(function () {
    const logViewer = document.getElementById('logViewer');
    const refreshButton = document.getElementById('refreshLogsButton');
    const clearButton = document.getElementById('clearLogsButton');
    const autoRefreshCheckbox = document.getElementById('logsAutoRefresh');
    const lastUpdated = document.getElementById('logsLastUpdated');
    const logsSummary = document.getElementById('logsSummary');
    const filterCheckboxes = Array.from(document.querySelectorAll('[data-log-filter]'));
    const stateKey = 'meowautochrome.logs.view-state';
    let logEntries = [];
    let timer = null;

    if (!logViewer || !refreshButton || !clearButton || !autoRefreshCheckbox || !lastUpdated || !logsSummary) {
        return;
    }

    function resolveApi(key, fallback) {
        return (window.__apiEndpoints && window.__apiEndpoints[key]) || fallback;
    }

    function loadViewState() {
        try {
            return JSON.parse(localStorage.getItem(stateKey) || 'null');
        } catch {
            return null;
        }
    }

    function saveViewState() {
        try {
            localStorage.setItem(stateKey, JSON.stringify({
                autoRefresh: autoRefreshCheckbox.checked,
                filters: Object.fromEntries(filterCheckboxes.map((checkbox) => [checkbox.value, checkbox.checked]))
            }));
        } catch {
        }
    }

    function restoreViewState() {
        const state = loadViewState();
        if (!state) {
            return;
        }

        if (typeof state.autoRefresh === 'boolean') {
            autoRefreshCheckbox.checked = state.autoRefresh;
        }

        for (const checkbox of filterCheckboxes) {
            if (typeof state.filters?.[checkbox.value] === 'boolean') {
                checkbox.checked = state.filters[checkbox.value];
            }
        }
    }

    function getSelectedLevels() {
        return new Set(filterCheckboxes.filter((checkbox) => checkbox.checked).map((checkbox) => checkbox.value));
    }

    function isNearBottom() {
        return logViewer.scrollHeight - logViewer.scrollTop - logViewer.clientHeight < 24;
    }

    function renderLogs(options) {
        const keepBottom = options?.keepBottom ?? false;
        const wasNearBottom = isNearBottom();
        const levels = getSelectedLevels();
        const visibleEntries = logEntries.filter((entry) => levels.has(entry.filterLevel));
        logViewer.replaceChildren();

        if (!visibleEntries.length) {
            const empty = document.createElement('div');
            empty.className = 'log-entry-empty p-3 text-muted';
            empty.textContent = '当前筛选条件下没有日志。';
            logViewer.appendChild(empty);
        } else {
            for (const entry of visibleEntries) {
                const item = document.createElement('div');
                item.className = 'border-bottom p-3';

                const meta = document.createElement('div');
                meta.className = 'd-flex flex-wrap gap-2 align-items-center small text-muted mb-2';

                const timestamp = document.createElement('span');
                timestamp.textContent = entry.timestampText;
                meta.appendChild(timestamp);

                const badge = document.createElement('span');
                badge.className = 'badge ' + (entry.filterLevel === 'error' ? 'bg-danger' : entry.filterLevel === 'warn' ? 'bg-warning text-dark' : 'bg-secondary');
                badge.textContent = entry.levelText;
                meta.appendChild(badge);

                const category = document.createElement('span');
                category.textContent = entry.category;
                meta.appendChild(category);

                const message = document.createElement('div');
                message.className = 'small';
                message.textContent = entry.message;

                item.appendChild(meta);
                item.appendChild(message);
                logViewer.appendChild(item);
            }
        }

        logsSummary.textContent = '显示 ' + visibleEntries.length + ' / ' + logEntries.length + ' 行';
        if (keepBottom || wasNearBottom) {
            logViewer.scrollTop = logViewer.scrollHeight;
        }
    }

    async function refreshLogs(options) {
        const silent = options?.silent ?? false;
        if (!silent) {
            refreshButton.disabled = true;
        }

        try {
            const response = await fetch(resolveApi('logsContent', '/api/logs/content'), {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });

            if (!response.ok) {
                throw new Error('日志获取失败: ' + response.status);
            }

            const payload = await response.json();
            logEntries = payload.entries || [];
            lastUpdated.textContent = payload.lastUpdatedLocal || '暂无日志';
            renderLogs({ keepBottom: true });
        } catch (error) {
            window.showNotification?.(error.message || '日志获取失败。', 'danger');
        } finally {
            refreshButton.disabled = false;
        }
    }

    function updateAutoRefresh() {
        if (timer) {
            clearInterval(timer);
            timer = null;
        }

        if (autoRefreshCheckbox.checked) {
            timer = setInterval(() => {
                refreshLogs({ silent: true }).catch(() => { });
            }, 3000);
        }
    }

    refreshButton.addEventListener('click', () => {
        refreshLogs().catch(() => { });
    });

    clearButton.addEventListener('click', async () => {
        if (!confirm('确认清空日志？')) {
            return;
        }

        try {
            clearButton.disabled = true;
            const response = await fetch(resolveApi('logsClear', '/api/logs/clear'), { method: 'POST' });
            if (!response.ok) {
                throw new Error('清空日志失败: ' + response.status);
            }

            await refreshLogs();
            window.showNotification?.('日志已清空。', 'success');
        } catch (error) {
            window.showNotification?.(error.message || '清空日志失败。', 'danger');
        } finally {
            clearButton.disabled = false;
        }
    });

    autoRefreshCheckbox.addEventListener('change', () => {
        saveViewState();
        updateAutoRefresh();
    });

    for (const checkbox of filterCheckboxes) {
        checkbox.addEventListener('change', () => {
            saveViewState();
            renderLogs();
        });
    }

    restoreViewState();
    updateAutoRefresh();
    refreshLogs().catch(() => { });
})();