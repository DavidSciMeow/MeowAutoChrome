(function () {
    const config = window.__browserConfig || {};
    const canvas = document.getElementById('browserCanvas');
    const browserStage = canvas ? canvas.closest('.browser-stage') : null;
    const browserWorkspace = document.getElementById('browserWorkspace');
    const browserPaneDivider = document.getElementById('browserPaneDivider');
    const pluginDrawerPane = document.getElementById('pluginDrawerPane');
    const pluginDrawerOpenBtn = document.getElementById('pluginDrawerOpenBtn');
    const pluginDrawerCloseBtn = document.getElementById('pluginDrawerCloseBtn');
    const pluginDrawerBackdrop = document.getElementById('pluginDrawerBackdrop');
    const statusBadge = document.getElementById('connStatus');
    const noSignal = document.getElementById('noSignal');
    const browserEmptyTitle = document.getElementById('browserEmptyTitle');
    const browserEmptyCopy = document.getElementById('browserEmptyCopy');
    const cpuDisplay = document.getElementById('cpuDisplay');
    const memoryDisplay = document.getElementById('memoryDisplay');
    const browserError = document.getElementById('browserError');
    const urlInput = document.getElementById('urlInput');
    const liveToggleBtn = document.getElementById('liveToggleBtn');
    const headlessBadge = document.getElementById('headlessBadge');
    const headlessToggleIcon = document.getElementById('headlessToggleIcon');
    const headlessToggleBtn = document.getElementById('headlessToggleBtn');
    const backBtn = document.getElementById('backBtn');
    const forwardBtn = document.getElementById('forwardBtn');
    const reloadBtn = document.getElementById('reloadBtn');
    const goBtn = document.getElementById('goBtn');
    const shotBtn = document.getElementById('shotBtn');
    const newTabBtn = document.getElementById('newTabBtn');
    const newInstanceBtn = document.getElementById('newInstanceBtn');
    const tabsShell = document.getElementById('tabsShell');
    const tabsStatusPanel = document.getElementById('tabsStatusPanel');
    const pluginHost = document.getElementById('pluginHost');
    const screenshotPreviewModalElement = document.getElementById('screenshotPreviewModal');
    const screenshotPreviewImage = document.getElementById('screenshotPreviewImage');
    const createInstanceModalElement = document.getElementById('createInstanceModal');
    const emptyStateCreateInstanceBtn = document.getElementById('emptyStateCreateInstanceBtn');
    const createInstanceDisplayNameInput = document.getElementById('createInstanceDisplayName');
    const createInstanceUserDataDirectoryInput = document.getElementById('createInstanceUserDataDirectory');
    const createInstancePathTooltip = document.getElementById('createInstancePathTooltip');
    const createInstancePathText = createInstancePathTooltip?.querySelector('.create-path-text');
    const createInstanceOpenBtn = createInstancePathTooltip?.querySelector('.create-path-open-btn');
    const createInstanceCopyBtn = createInstancePathTooltip?.querySelector('.create-path-copy-btn');
    const instanceUserDataDirectoryOpenBtn = document.getElementById('instanceUserDataDirectoryOpenBtn');
    const createInstanceOwnerPluginIdInput = document.getElementById('createInstanceOwnerPluginId');
    const createInstanceSubmitBtn = document.getElementById('createInstanceSubmitBtn');
    const instanceSettingsModalElement = document.getElementById('instanceSettingsModal');
    const instanceSettingsModalTitle = document.getElementById('instanceSettingsModalTitle');
    const instanceSettingsModalSummary = document.getElementById('instanceSettingsModalSummary');
    const instanceSettingsInstanceId = document.getElementById('instanceSettingsInstanceId');
    const instanceSettingsCurrentUserDataDirectory = document.getElementById('instanceSettingsCurrentUserDataDirectory');
    const instanceSettingsIsSelected = document.getElementById('instanceSettingsIsSelected');
    const instanceUserDataDirectoryInput = document.getElementById('instanceUserDataDirectoryInput');
    const instanceUseProgramUserAgentInput = document.getElementById('instanceUseProgramUserAgentInput');
    const instanceUserAgentInput = document.getElementById('instanceUserAgentInput');
    const instanceUserAgentProgramHint = document.getElementById('instanceUserAgentProgramHint');
    const instanceUserAgentEffectiveHint = document.getElementById('instanceUserAgentEffectiveHint');
    const instanceUserAgentLockHint = document.getElementById('instanceUserAgentLockHint');
    const instanceViewportWidthInput = document.getElementById('instanceViewportWidthInput');
    const instanceViewportHeightInput = document.getElementById('instanceViewportHeightInput');
    const instancePreserveAspectRatioInput = document.getElementById('instancePreserveAspectRatioInput');
    const instanceAutoResizeViewportInput = document.getElementById('instanceAutoResizeViewportInput');
    const instanceSettingsSubmitBtn = document.getElementById('instanceSettingsSubmitBtn');

    let statusBusy = false;
    let isEditingUrl = false;
    let liveDisplayEnabled = true;
    let screencastAvailable = false;
    let resizeSyncTimer = null;
    let applyingScreencastSettings = false;
    let pointerResizeActive = false;
    let pluginPanelWidth = Number(config.defaultPluginPanelWidth) || 320;
    let pluginDrawerOpen = true;
    let selectedInstanceId = null;
    let currentViewport = null;
    // `instanceHeadless` tracks the current instance's headless state (from /api/status).
    let instanceHeadless = false;
    // `globalHeadless` tracks the globally saved Headless setting (from /api/settings).
    let globalHeadless = false;
    let screenshotPreviewUrl = null;

    function buildAbsoluteUrl(url) {
        if (!url) return url;
        if (/^https?:\/\//i.test(url)) return url;
        const apiBase = (window.__apiBase || window.meow?.apiBase || '').replace(/\/$/, '');
        if (apiBase) {
            return apiBase + (url.startsWith('/') ? url : '/' + url.replace(/^\/+/, ''));
        }
        return url;
    }

    function resolveApi(key, fallback) {
        return (window.__apiEndpoints && window.__apiEndpoints[key]) || fallback || key;
    }

    function getValue(obj, camel, pascal, fallback) {
        if (!obj) return fallback;
        if (obj[camel] !== undefined) return obj[camel];
        if (pascal && obj[pascal] !== undefined) return obj[pascal];
        return fallback;
    }

    async function postJson(urlOrKey, body) {
        const fn = window.BrowserIndex?.postJson;
        if (typeof fn === 'function') {
            return fn(urlOrKey, body);
        }
        const url = resolveApi(urlOrKey, urlOrKey);
        const response = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body || {})
        });
        if (!response.ok) throw new Error('Request failed: ' + response.status);
        return await response.json();
    }

    function showError(message, level) {
        const text = message || '';
        if (browserError) {
            browserError.textContent = text;
            browserError.classList.remove('d-none', 'alert-danger', 'alert-warning');
            browserError.classList.add(level === 'warning' ? 'alert-warning' : 'alert-danger');
        }
        if (text) {
            window.showNotification?.(text, level === 'warning' ? 'warning' : 'danger');
        }
    }

    function clearError() {
        if (!browserError) return;
        browserError.textContent = '';
        browserError.classList.add('d-none');
    }

    function getCanvasViewportSize() {
        if (window.BrowserScreencast?.getCanvasViewportSize) {
            return window.BrowserScreencast.getCanvasViewportSize();
        }
        const width = Math.max(320, Math.round(browserStage?.clientWidth || canvas?.clientWidth || 1280));
        const height = Math.max(240, Math.round(browserStage?.clientHeight || canvas?.clientHeight || 800));
        return { width, height };
    }

    function resizeCanvasBuffer(width, height) {
        if (window.BrowserScreencast?.resizeCanvasBuffer) {
            window.BrowserScreencast.resizeCanvasBuffer(width, height);
            return;
        }
        if (!canvas) return;
        if (canvas.width !== width || canvas.height !== height) {
            canvas.width = width;
            canvas.height = height;
        }
    }

    function renderResourceMetrics(data) {
        if (cpuDisplay) {
            const cpu = Number(getValue(data, 'cpuUsagePercent', 'CpuUsagePercent', 0));
            cpuDisplay.textContent = 'CPU ' + cpu.toFixed(1) + '%';
        }
        if (memoryDisplay) {
            const memory = Number(getValue(data, 'memoryUsageMb', 'MemoryUsageMb', 0));
            memoryDisplay.textContent = 'RAM ' + memory.toFixed(1) + ' MB';
        }
    }

    function updateStatusBadge(hasInstance) {
        if (!statusBadge) return;
        // statusBadge (`connStatus`) represents backend connection state.
        // When applyStatus is called (after a successful status fetch) mark the connection as established.
        statusBadge.textContent = '已连接';
        statusBadge.className = 'badge ' + (screencastAvailable && liveDisplayEnabled ? 'bg-success' : 'bg-secondary');
    }

    function setLiveDisplayEnabled(enabled) {
        liveDisplayEnabled = !!enabled;
        if (liveToggleBtn) {
            liveToggleBtn.textContent = liveDisplayEnabled ? '实时画面' : 'TAB 状态';
            liveToggleBtn.className = 'btn btn-sm browser-live-toggle ' + (liveDisplayEnabled ? 'btn-primary' : 'btn-danger');
        }
        canvas?.classList.toggle('d-none', !liveDisplayEnabled);
        tabsStatusPanel?.classList.toggle('d-none', liveDisplayEnabled);
        tabsShell?.classList.toggle('d-none', !screencastAvailable || !liveDisplayEnabled);
        if (!liveDisplayEnabled && noSignal) {
            noSignal.style.display = 'none';
        }
    }

    function renderBrowserStageState(options) {
        const hasInstance = !!options?.hasInstance;
        const supportsScreencast = !!options?.supportsScreencast;
        const headless = !!options?.headless;

        if (browserEmptyTitle) {
            browserEmptyTitle.textContent = !hasInstance
                ? '还没有浏览器实例'
                : headless && !supportsScreencast
                    ? '当前模式没有实时画面'
                    : '浏览器正在本地窗口中运行';
        }

        if (browserEmptyCopy) {
            browserEmptyCopy.textContent = !hasInstance
                ? '先创建一个实例，再开始打开页面、截图和执行插件动作。'
                : headless && !supportsScreencast
                    ? '可以切换运行模式，或继续通过 TAB 状态视图管理页面。'
                    : 'Headful 模式下，浏览器使用真实窗口运行，这里保留控制栏、TAB 状态和插件区。';
        }
    }

    function setScreencastAvailability(available, hasInstance) {
        screencastAvailable = !!available;
        if (liveToggleBtn) liveToggleBtn.disabled = !screencastAvailable;
        if (!screencastAvailable) {
            setLiveDisplayEnabled(false);
            if (noSignal) {
                noSignal.style.display = 'flex';
            }
        } else if (noSignal && hasInstance) {
            noSignal.style.display = 'none';
        }
        updateStatusBadge(hasInstance);
    }

    // Update internal instance-level headless state (does NOT change the toolbar button UI).
    function setInstanceHeadlessState(headless) {
        instanceHeadless = !!headless;
    }

    // Update the toolbar/button UI to reflect the global Headless setting.
    function setGlobalHeadlessUi(headless) {
        globalHeadless = !!headless;
        if (headlessBadge) {
            headlessBadge.textContent = globalHeadless ? 'Headless' : 'Headful';
            headlessBadge.className = 'badge ' + (globalHeadless ? 'bg-secondary' : 'bg-success');
        }
        if (headlessToggleBtn) {
            headlessToggleBtn.classList.remove('btn-primary', 'btn-outline-secondary');
            if (globalHeadless) headlessToggleBtn.classList.add('btn-outline-secondary');
            else headlessToggleBtn.classList.add('btn-primary');
        }
    }

    // Load global settings (used to initialize and confirm the toolbar state).
    async function loadGlobalSettings() {
        try {
            const r = await fetch(resolveApi('settings', '/api/settings'), { cache: 'no-store' });
            if (!r.ok) throw new Error('读取设置失败: ' + r.status);
            const payload = await r.json();
            const head = getValue(payload, 'headless', 'Headless', getValue(payload, 'isHeadless', 'IsHeadless', false));
            setGlobalHeadlessUi(!!head);
        } catch (error) {
            // Silent failure is acceptable for UI init; keep console trace for debugging.
            console.debug('loadGlobalSettings error', error && (error.message || error));
        }
    }

    function clampPluginPanelWidth(width) {
        const workspaceWidth = Math.max(0, browserWorkspace?.clientWidth || 0);
        if (workspaceWidth <= 0) return pluginPanelWidth;
        // Enforce plugin panel to be between 30% and 50% of the workspace width.
        const minPct = 0.30;
        const maxPct = 0.50;
        const minWidth = Math.round(workspaceWidth * minPct);
        const maxWidth = Math.round(workspaceWidth * maxPct);
        return Math.max(minWidth, Math.min(maxWidth, Math.round(Number(width) || pluginPanelWidth)));
    }

    function setPluginDrawerOpen(open) {
        pluginDrawerOpen = !!open;
        browserWorkspace?.classList.toggle('plugin-drawer-open', pluginDrawerOpen);
        pluginDrawerBackdrop?.classList.toggle('d-none', !pluginDrawerOpen);
        pluginDrawerOpenBtn?.setAttribute('aria-expanded', pluginDrawerOpen ? 'true' : 'false');
        pluginDrawerPane?.setAttribute('aria-hidden', pluginDrawerOpen ? 'false' : 'true');
    }

    function syncPluginDrawerMode() {
        browserWorkspace?.classList.add('plugin-drawer-mode');
        pluginDrawerOpenBtn?.classList.remove('d-none');
        pluginDrawerCloseBtn?.classList.remove('d-none');
        setPluginDrawerOpen(pluginDrawerOpen);
    }

    function applyPluginPanelWidth(width) {
        pluginPanelWidth = clampPluginPanelWidth(width);
        document.documentElement.style.setProperty('--plugin-panel-width', pluginPanelWidth + 'px');
        const range = document.getElementById('PluginPanelWidth');
        const value = document.getElementById('pluginPanelWidthValue');
        if (range) range.value = String(pluginPanelWidth);
        if (value) value.textContent = pluginPanelWidth + ' px';
    }

    // Persistence of plugin panel width removed — layout no longer saved from client.

    async function syncScreencastSettings(force) {
        const viewport = getCanvasViewportSize();
        resizeCanvasBuffer(viewport.width, viewport.height);
        if (applyingScreencastSettings) return;
        applyingScreencastSettings = true;
        try {
            await postJson(resolveApi('screencastSettings', '/api/screencast/settings'), {
                enabled: liveDisplayEnabled,
                maxWidth: viewport.width,
                maxHeight: viewport.height,
                frameIntervalMs: 100
            });
        } catch (error) {
            if (force) showError(error.message || '画面设置同步失败。', 'warning');
        } finally {
            applyingScreencastSettings = false;
        }
    }

    function scheduleScreencastSync(force) {
        if (resizeSyncTimer) clearTimeout(resizeSyncTimer);
        resizeSyncTimer = setTimeout(() => { syncScreencastSettings(force).catch(() => { }); }, force ? 0 : 150);
    }

    function readViewportFromStatus(data) {
        const viewport = getValue(data, 'currentViewport', 'CurrentViewport', null);
        if (!viewport) {
            return { width: 1280, height: 800, viewportType: 'Auto' };
        }
        return {
            width: Number(getValue(viewport, 'width', 'Width', 1280)),
            height: Number(getValue(viewport, 'height', 'Height', 800)),
            viewportType: getValue(viewport, 'viewportType', 'ViewportType', 'Auto')
        };
    }

    function populateInstanceSettingsForm(payload, fallbackName) {
        const instanceName = getValue(payload, 'instanceName', 'InstanceName', fallbackName || '实例');
        const viewport = getValue(payload, 'viewport', 'Viewport', null) || {};
        const userAgent = getValue(payload, 'userAgent', 'UserAgent', null) || {};

        if (instanceSettingsModalTitle) instanceSettingsModalTitle.textContent = instanceName + ' 设置';
        if (instanceSettingsModalSummary) instanceSettingsModalSummary.textContent = '当前迁移版本可保存 UserData 目录、Headless 状态和基础视口尺寸。实例级 UA 与高级视口策略暂未接入 Core。';
        if (instanceSettingsInstanceId) instanceSettingsInstanceId.value = getValue(payload, 'instanceId', 'InstanceId', '');
        if (instanceSettingsCurrentUserDataDirectory) instanceSettingsCurrentUserDataDirectory.value = getValue(payload, 'userDataDirectory', 'UserDataDirectory', '') || '';
        if (instanceSettingsIsSelected) instanceSettingsIsSelected.value = String(!!getValue(payload, 'isSelected', 'IsSelected', false));
        if (instanceUserDataDirectoryInput) instanceUserDataDirectoryInput.value = getValue(payload, 'userDataDirectory', 'UserDataDirectory', '') || '';
        if (instanceViewportWidthInput) instanceViewportWidthInput.value = String(getValue(viewport, 'width', 'Width', currentViewport?.width || 1280));
        if (instanceViewportHeightInput) instanceViewportHeightInput.value = String(getValue(viewport, 'height', 'Height', currentViewport?.height || 800));
        if (instanceUseProgramUserAgentInput) {
            instanceUseProgramUserAgentInput.checked = !!getValue(userAgent, 'useProgramUserAgent', 'UseProgramUserAgent', true);
            instanceUseProgramUserAgentInput.disabled = true;
        }
        if (instanceUserAgentInput) {
            instanceUserAgentInput.value = getValue(userAgent, 'userAgent', 'UserAgent', '') || '';
            instanceUserAgentInput.disabled = true;
            instanceUserAgentInput.placeholder = '当前版本暂不支持保存实例级 UA';
        }
        if (instanceUserAgentProgramHint) instanceUserAgentProgramHint.textContent = '程序设置 UA：' + (getValue(userAgent, 'programUserAgent', 'ProgramUserAgent', null) || '未设置');
        if (instanceUserAgentEffectiveHint) instanceUserAgentEffectiveHint.textContent = '当前生效 UA：' + (getValue(userAgent, 'effectiveUserAgent', 'EffectiveUserAgent', null) || '浏览器默认');
        if (instanceUserAgentLockHint) {
            instanceUserAgentLockHint.textContent = '当前迁移版本暂未接入实例级 UA 保存。';
            instanceUserAgentLockHint.classList.remove('d-none');
        }
        if (instancePreserveAspectRatioInput) {
            instancePreserveAspectRatioInput.checked = !!getValue(viewport, 'preserveAspectRatio', 'PreserveAspectRatio', false);
            instancePreserveAspectRatioInput.disabled = true;
        }
        if (instanceAutoResizeViewportInput) {
            instanceAutoResizeViewportInput.checked = !!getValue(viewport, 'autoResizeViewport', 'AutoResizeViewport', false);
        }
    }

    async function openInstanceSettings(instanceId, fallbackName) {
        if (!instanceId || !instanceSettingsModalElement) return;
        clearError();
        try {
            const response = await fetch(resolveApi('instancesSettings', '/api/instances/settings') + '?instanceId=' + encodeURIComponent(instanceId), { cache: 'no-store' });
            if (!response.ok) throw new Error('实例设置读取失败: ' + response.status);
            const payload = await response.json();
            populateInstanceSettingsForm(payload, fallbackName);
            new bootstrap.Modal(instanceSettingsModalElement).show();
        } catch (error) {
            showError(error.message || '实例设置读取失败。');
        }
    }

    async function saveInstanceSettings() {
        if (!instanceSettingsInstanceId?.value) return;
        const viewportWidth = Math.max(1, Number(instanceViewportWidthInput?.value || currentViewport?.width || 1280));
        const viewportHeight = Math.max(1, Number(instanceViewportHeightInput?.value || currentViewport?.height || 800));

        instanceSettingsSubmitBtn.disabled = true;
        clearError();
        try {
            const instanceId = instanceSettingsInstanceId.value;
            await postJson(resolveApi('instancesSettings', '/api/instances/settings'), {
                instanceId,
                isHeadless: instanceHeadless,
                userDataDirectory: instanceUserDataDirectoryInput?.value?.trim() || instanceSettingsCurrentUserDataDirectory?.value || '',
                viewportWidth,
                viewportHeight
            });

            if (instanceAutoResizeViewportInput?.checked && instanceId === selectedInstanceId) {
                const liveViewport = getCanvasViewportSize();
                await postJson(resolveApi('instancesViewport', '/api/instances/viewport'), {
                    width: liveViewport.width,
                    height: liveViewport.height
                });
            }

            bootstrap.Modal.getInstance(instanceSettingsModalElement)?.hide();
            await refreshStatus();
            window.showNotification?.('实例设置已保存。', 'success');
        } catch (error) {
            showError(error.message || '实例设置保存失败。');
        } finally {
            instanceSettingsSubmitBtn.disabled = false;
        }
    }

    async function loadCreateInstancePreview() {
        if (!createInstancePathTooltip || !createInstancePathText) return;
        const ownerPluginId = createInstanceOwnerPluginIdInput?.value || 'ui';
        const root = createInstanceUserDataDirectoryInput?.value?.trim() || '';
        try {
            const url = new URL(buildAbsoluteUrl(resolveApi('instancesPreview', '/api/instances/preview')));
            url.searchParams.set('ownerPluginId', ownerPluginId);
            if (root) url.searchParams.set('userDataDirectoryRoot', root);
            const response = await fetch(url.toString(), { cache: 'no-store' });
            if (!response.ok) throw new Error('实例路径预览失败: ' + response.status);
            const payload = await response.json();
            createInstanceUserDataDirectoryInput.dataset.previewId = payload.instanceId || '';
            const previewText = payload.userDataDirectory || '自动生成';
            createInstancePathText.textContent = previewText;
            createInstancePathTooltip.style.display = '';
        } catch (error) {
            const message = error.message || '无法预览路径';
            createInstancePathText.textContent = message;
            createInstancePathTooltip.style.display = '';
        }
    }

    async function createInstance() {
        const ownerPluginId = createInstanceOwnerPluginIdInput?.value || 'ui';
        const displayName = createInstanceDisplayNameInput?.value?.trim() || null;
        const userDataDirectory = createInstanceUserDataDirectoryInput?.value?.trim() || null;
        const previewInstanceId = createInstanceUserDataDirectoryInput?.dataset.previewId || null;

        createInstanceSubmitBtn.disabled = true;
        clearError();
        try {
            await postJson(resolveApi('instances', '/api/instances'), {
                ownerPluginId,
                displayName,
                userDataDirectory,
                previewInstanceId
            });
            bootstrap.Modal.getInstance(createInstanceModalElement)?.hide();
            if (createInstanceDisplayNameInput) createInstanceDisplayNameInput.value = '';
            if (createInstanceUserDataDirectoryInput) createInstanceUserDataDirectoryInput.value = '';
            if (createInstancePathTooltip) createInstancePathTooltip.style.display = 'none';
            if (createInstancePathText) createInstancePathText.textContent = '';
            await refreshStatus();
            await loadPlugins();
            window.showNotification?.('实例已创建。', 'success');
        } catch (error) {
            showError(error.message || '创建实例失败。');
        } finally {
            createInstanceSubmitBtn.disabled = false;
        }
    }

    async function refreshStatus() {
        if (statusBusy) return;
        statusBusy = true;
        try {
            const response = await fetch(resolveApi('status', '/api/status'), { cache: 'no-store' });
            if (!response.ok) throw new Error('状态获取失败: ' + response.status);
            applyStatus(await response.json());
        } catch (error) {
            showError(error.message || '状态获取失败。', 'warning');
        } finally {
            statusBusy = false;
        }
    }

    async function loadPlugins() {
        try {
            const response = await fetch(resolveApi('plugins', '/api/plugins'), { cache: 'no-store' });
            if (!response.ok) throw new Error('插件列表获取失败: ' + response.status);
            const payload = await response.json();
            const plugins = payload?.plugins || [];
            window.renderPlugins?.(plugins);
            const errors = Array.isArray(payload?.errors) ? payload.errors.filter(Boolean) : [];
            if (errors.length) showError(errors.join('； '), 'warning');
        } catch (error) {
            if (pluginHost) {
                pluginHost.replaceChildren();
                const node = document.createElement('div');
                node.className = 'text-danger small';
                node.textContent = error.message || '插件列表获取失败。';
                pluginHost.appendChild(node);
            }
        }
    }

    async function selectTab(tabId) {
        await postJson('tabsSelect', { tabId });
        await refreshStatus();
    }

    async function closeInstance(instanceId) {
        await postJson('instancesClose', { instanceId });
        await refreshStatus();
    }

    async function createTab() {
        await postJson('tabsNew', { instanceId: selectedInstanceId, url: urlInput?.value?.trim() || 'about:blank' });
        await refreshStatus();
    }

    async function navigate() {
        await postJson('navigation', { url: urlInput?.value?.trim() || '' });
        isEditingUrl = false;
        await refreshStatus();
    }

    async function goBack() {
        await postJson(resolveApi('navigationBack', '/api/navigation/back'), {});
        await refreshStatus();
    }

    async function goForward() {
        await postJson(resolveApi('navigationForward', '/api/navigation/forward'), {});
        await refreshStatus();
    }

    async function reloadPage() {
        await postJson(resolveApi('navigationReload', '/api/navigation/reload'), {});
        await refreshStatus();
    }

    async function toggleHeadless() {
        try {
            // Toggle the global Headless setting (toolbar button reflects global state).
            const newHeadless = !globalHeadless;
            // Optimistically update the toolbar UI to reflect new global headless state
            setGlobalHeadlessUi(newHeadless);
            // Read current global settings snapshot and POST autosave with updated Headless
            const settingsUrl = resolveApi('settings', '/api/settings');
            const r = await fetch(settingsUrl, { cache: 'no-store' });
            if (!r.ok) throw new Error('读取设置失败: ' + r.status);
            const payload = await r.json();

            const form = new FormData();
            form.append('SearchUrlTemplate', payload.searchUrlTemplate || '');
            form.append('ScreencastFps', String(payload.screencastFps ?? 30));
            form.append('PluginPanelWidth', String(payload.pluginPanelWidth ?? (pluginPanelWidth || 320)));
            form.append('UserDataDirectory', payload.userDataDirectory || '');
            if (payload.userAgent) form.append('UserAgent', payload.userAgent);
            form.append('AllowInstanceUserAgentOverride', payload.allowInstanceUserAgentOverride ? 'true' : 'false');
            form.append('Headless', newHeadless ? 'true' : 'false');

            const autosaveUrl = resolveApi('settingsAutosave', '/api/settings/autosave');
            const saveResp = await fetch(autosaveUrl, { method: 'POST', body: form, headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!saveResp.ok) {
                const err = await saveResp.json().catch(() => null);
                throw new Error(err?.message || '设置保存失败: ' + saveResp.status);
            }

            // Refresh status (instance data) then re-load global settings to ensure toolbar UI
            // reflects the persisted global value (server may update instances asynchronously).
            await refreshStatus();
            await loadGlobalSettings();
            scheduleScreencastSync(true);
            window.showNotification?.('Headless 设置已保存。', 'success');
        } catch (error) {
            showError(error.message || '运行模式切换失败。');
        }
    }

    async function previewScreenshot() {
        if (!shotBtn) return;
        shotBtn.disabled = true;
        try {
            const response = await fetch(resolveApi('screenshot', '/api/screenshot') + '?ts=' + Date.now(), { cache: 'no-store' });
            if (!response.ok) throw new Error('截图失败: ' + response.status);
            const blob = await response.blob();
            if (screenshotPreviewUrl) URL.revokeObjectURL(screenshotPreviewUrl);
            screenshotPreviewUrl = URL.createObjectURL(blob);
            if (screenshotPreviewImage) screenshotPreviewImage.src = screenshotPreviewUrl;
            if (screenshotPreviewModalElement) new bootstrap.Modal(screenshotPreviewModalElement).show();
        } catch (error) {
            showError(error.message || '截图失败。');
        } finally {
            shotBtn.disabled = false;
        }
    }

    function applyStatus(data) {
        const currentUrl = getValue(data, 'currentUrl', 'CurrentUrl', '') || '';
        const title = getValue(data, 'title', 'Title', null) || '浏览器控制台';
        const errorMessage = getValue(data, 'errorMessage', 'ErrorMessage', null);
        const supportsScreencast = !!getValue(data, 'supportsScreencast', 'SupportsScreencast', false);
        const screencastEnabled = !!getValue(data, 'screencastEnabled', 'ScreencastEnabled', false);
        const tabs = getValue(data, 'tabs', 'Tabs', []);
        const hasInstance = !!getValue(data, 'currentInstanceId', 'CurrentInstanceId', null);
        const pluginWidth = Number(getValue(data, 'pluginPanelWidth', 'PluginPanelWidth', pluginPanelWidth));
        selectedInstanceId = getValue(data, 'currentInstanceId', 'CurrentInstanceId', null);
        currentViewport = readViewportFromStatus(data);
        // Preserve instance-level headless state separately from the global UI state.
        setInstanceHeadlessState(!!getValue(data, 'isHeadless', 'IsHeadless', false));

        if (!isEditingUrl && urlInput) urlInput.value = currentUrl || '';
        document.title = title + ' - MeowAutoChrome';
        if (errorMessage) showError(errorMessage); else clearError();

        // NOTE: toolbar/button UI (headlessBadge/headlessToggleBtn) reflects the global
        // Headless setting and is therefore updated via `loadGlobalSettings` /
        // `setGlobalHeadlessUi`. Do not overwrite the toolbar UI from status polling.

        // Update the eye icon inside the headless toggle to indicate instance presence:
        // open eye when instance exists, slashed eye when none.
        if (headlessToggleIcon) {
            headlessToggleIcon.classList.remove('fa-eye', 'fa-eye-slash', 'text-success', 'text-muted');
            if (hasInstance) {
                headlessToggleIcon.classList.add('fa-eye', 'text-success');
            } else {
                headlessToggleIcon.classList.add('fa-eye-slash', 'text-muted');
            }
        }

        renderResourceMetrics(data);
        applyPluginPanelWidth(pluginWidth);
        setScreencastAvailability(supportsScreencast, hasInstance);
        if (supportsScreencast) setLiveDisplayEnabled(screencastEnabled);
        renderBrowserStageState({ hasInstance, supportsScreencast, headless: instanceHeadless });
        updateStatusBadge(hasInstance);

        if (!hasInstance && noSignal) {
            noSignal.style.display = 'flex';
        }

        if (backBtn) backBtn.disabled = !hasInstance;
        if (forwardBtn) forwardBtn.disabled = !hasInstance;
        if (reloadBtn) reloadBtn.disabled = !hasInstance;
        if (goBtn) goBtn.disabled = !hasInstance;
        if (shotBtn) shotBtn.disabled = !hasInstance;
        if (newTabBtn) newTabBtn.disabled = !hasInstance;

        window.renderTabs?.(tabs || []);
        window.renderTabsStatus?.(tabs || []);

        if (instanceViewportWidthInput && !instanceSettingsModalElement?.classList.contains('show')) instanceViewportWidthInput.value = String(currentViewport.width || 1280);
        if (instanceViewportHeightInput && !instanceSettingsModalElement?.classList.contains('show')) instanceViewportHeightInput.value = String(currentViewport.height || 800);
    }

    function bindPaneResize() {
        if (!browserPaneDivider || !browserWorkspace) return;
        browserPaneDivider.addEventListener('pointerdown', event => {
            pointerResizeActive = true;
            browserPaneDivider.setPointerCapture?.(event.pointerId);
            const startX = event.clientX;
            const startWidth = pluginPanelWidth;

            const onMove = moveEvent => {
                const delta = startX - moveEvent.clientX;
                applyPluginPanelWidth(startWidth + delta);
            };

            const onUp = upEvent => {
                pointerResizeActive = false;
                browserPaneDivider.releasePointerCapture?.(upEvent.pointerId);
                window.removeEventListener('pointermove', onMove);
                window.removeEventListener('pointerup', onUp);
                // No persistence call — user requested settings not be saved client-side.
            };

            window.addEventListener('pointermove', onMove);
            window.addEventListener('pointerup', onUp);
        });
    }

    urlInput?.addEventListener('focus', () => { isEditingUrl = true; });
    urlInput?.addEventListener('blur', () => { isEditingUrl = false; });
    urlInput?.addEventListener('keydown', event => {
        if (event.key === 'Enter') {
            event.preventDefault();
            navigate().catch(error => showError(error.message || '导航失败。'));
        }
    });

    goBtn?.addEventListener('click', () => navigate().catch(error => showError(error.message || '导航失败。')));
    backBtn?.addEventListener('click', () => goBack().catch(error => showError(error.message || '返回失败。')));
    forwardBtn?.addEventListener('click', () => goForward().catch(error => showError(error.message || '前进失败。')));
    reloadBtn?.addEventListener('click', () => reloadPage().catch(error => showError(error.message || '刷新失败。')));
    shotBtn?.addEventListener('click', () => previewScreenshot().catch(() => { }));
    newTabBtn?.addEventListener('click', () => createTab().catch(error => showError(error.message || '新建 TAB 失败。')));
    newInstanceBtn?.addEventListener('click', async () => {
        if (createInstanceModalElement) {
            await loadCreateInstancePreview();
            new bootstrap.Modal(createInstanceModalElement).show();
        }
    });
    emptyStateCreateInstanceBtn?.addEventListener('click', async () => {
        if (createInstanceModalElement) {
            await loadCreateInstancePreview();
            new bootstrap.Modal(createInstanceModalElement).show();
        }
    });
    createInstanceSubmitBtn?.addEventListener('click', () => createInstance().catch(() => { }));
    createInstanceUserDataDirectoryInput?.addEventListener('input', () => loadCreateInstancePreview().catch(() => { }));
    createInstanceDisplayNameInput?.addEventListener('input', () => loadCreateInstancePreview().catch(() => { }));
    instanceSettingsSubmitBtn?.addEventListener('click', () => saveInstanceSettings().catch(() => { }));
    document.getElementById('headlessToggleBtn')?.addEventListener('click', () => toggleHeadless().catch(() => { }));
    liveToggleBtn?.addEventListener('click', () => {
        if (!screencastAvailable) return;
        setLiveDisplayEnabled(!liveDisplayEnabled);
        scheduleScreencastSync(true);
    });

    createInstanceCopyBtn?.addEventListener('click', async () => {
        const text = createInstancePathText?.textContent?.trim() || '';
        if (!text) return;
        try {
            await navigator.clipboard.writeText(text);
            window.showNotification?.('实例目录预览已复制。', 'success');
        } catch {
            showError('复制实例目录预览失败。', 'warning');
        }
    });

    createInstanceOpenBtn?.addEventListener('click', async () => {
        const targetPath = createInstancePathText?.textContent?.trim() || '';
        if (!targetPath) return;

        try {
            const result = await window.meow?.openPath?.(targetPath);
            if (!result?.ok) {
                showError(result?.message || '打开创建位置失败。', 'warning');
                return;
            }

            window.showNotification?.('创建位置已打开。', 'success');
        } catch {
            showError('打开创建位置失败。', 'warning');
        }
    });

    instanceUserDataDirectoryOpenBtn?.addEventListener('click', async () => {
        const target = instanceUserDataDirectoryInput?.value?.trim() || instanceSettingsCurrentUserDataDirectory?.value || '';
        if (!target) {
            showError('路径为空或未设置。', 'warning');
            return;
        }

        try {
            const result = await window.meow?.openPath?.(target);
            if (!result?.ok) {
                showError(result?.message || '打开目录失败。', 'warning');
                return;
            }

            window.showNotification?.('目录已打开。', 'success');
        } catch (err) {
            showError('打开目录失败。', 'warning');
        }
    });

    window.addEventListener('resize', () => {
        if (pointerResizeActive) return;
        applyPluginPanelWidth(pluginPanelWidth);
        if (selectedInstanceId && instanceAutoResizeViewportInput?.checked) {
            scheduleScreencastSync(false);
        }
    });

    pluginDrawerOpenBtn?.addEventListener('click', () => setPluginDrawerOpen(!pluginDrawerOpen));
    pluginDrawerCloseBtn?.addEventListener('click', () => setPluginDrawerOpen(false));
    pluginDrawerBackdrop?.addEventListener('click', () => setPluginDrawerOpen(false));

    window.addEventListener('meow:pagechange', event => {
        if (event.detail?.page === 'browser') {
            refreshStatus().catch(() => { });
            loadPlugins().catch(() => { });
        }
    });

    bindPaneResize();
    syncPluginDrawerMode();
    applyPluginPanelWidth(pluginPanelWidth);

    window.BrowserIndex = Object.assign(window.BrowserIndex || {}, { showError, clearError });
    window.BrowserUI = Object.assign(window.BrowserUI || {}, {
        refreshStatus,
        loadPlugins,
        selectTab,
        closeInstance,
        navigate,
        previewScreenshot,
        openInstanceSettings,
        applyStatus,
        setLiveDisplayEnabled,
        pluginCatalog: window.BrowserUI?.pluginCatalog || new Map(),
        pluginOutputStore: window.BrowserUI?.pluginOutputStore || new Map(),
        pluginOutputUi: window.BrowserUI?.pluginOutputUi || new Map()
    });

    setTimeout(() => {
        refreshStatus().catch(() => { });
        loadPlugins().catch(() => { });
        // Ensure toolbar reflects persisted global Headless setting on startup
        loadGlobalSettings().catch(() => { });
    }, 0);

    setInterval(() => {
        if (document.hidden) return;
        if (document.querySelector('[data-page="browser"]')?.classList.contains('d-none')) return;
        refreshStatus().catch(() => { });
    }, 2500);
})();
