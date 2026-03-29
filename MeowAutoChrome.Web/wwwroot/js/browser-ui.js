// Browser UI script extracted from Index.cshtml. Relies on signalr and window.BrowserIndex.postJson.
(function () {
    const config = window.__browserConfig || {};

    const canvas = document.getElementById("browserCanvas");
    const ctx = canvas ? canvas.getContext("2d") : null;
    const browserStage = canvas ? canvas.closest(".browser-stage") : null;
    const browserWorkspace = document.getElementById("browserWorkspace");
    const browserPaneDivider = document.getElementById("browserPaneDivider");
    const status = document.getElementById("connStatus");
    const noSig = document.getElementById("noSignal");
    const cpuTxt = document.getElementById("cpuDisplay");
    const memoryTxt = document.getElementById("memoryDisplay");
    const fpsTxt = document.getElementById("fpsDisplay");
    const browserError = document.getElementById("browserError");
    const urlInput = document.getElementById("urlInput");
    const tabsBar = document.getElementById("tabsBar");
    const tabsShell = document.getElementById("tabsShell");
    const tabsScrollLeftBtn = document.getElementById("tabsScrollLeftBtn");
    const tabsScrollRightBtn = document.getElementById("tabsScrollRightBtn");
    const newInstanceBtn = document.getElementById("newInstanceBtn");
    const liveToggleBtn = document.getElementById("liveToggleBtn");
    const tabsStatusPanel = document.getElementById("tabsStatusPanel");
    const pluginHost = document.getElementById("pluginHost");
    const pluginArgumentModalElement = document.getElementById("pluginArgumentModal");
    const screenshotPreviewModalElement = document.getElementById("screenshotPreviewModal");
    const pluginOutputModalElement = document.getElementById("pluginOutputModal");
    const instanceSettingsModalElement = document.getElementById("instanceSettingsModal");
    const screenshotPreviewImage = document.getElementById("screenshotPreviewImage");
    const pluginArgumentModalTitle = document.getElementById("pluginArgumentModalTitle");
    const pluginArgumentModalDescription = document.getElementById("pluginArgumentModalDescription");
    const pluginArgumentForm = document.getElementById("pluginArgumentForm");
    const pluginArgumentSubmitBtn = document.getElementById("pluginArgumentSubmitBtn");
    const pluginOutputModalTitle = document.getElementById("pluginOutputModalTitle");
    const pluginOutputModalSummary = document.getElementById("pluginOutputModalSummary");
    const pluginOutputModalData = document.getElementById("pluginOutputModalData");
    const pluginOutputModalEntries = document.getElementById("pluginOutputModalEntries");
    const instanceSettingsModalTitle = document.getElementById("instanceSettingsModalTitle");
    const instanceSettingsModalSummary = document.getElementById("instanceSettingsModalSummary");
    const instanceSettingsInstanceIdInput = document.getElementById("instanceSettingsInstanceId");
    const instanceSettingsCurrentUserDataDirectoryInput = document.getElementById("instanceSettingsCurrentUserDataDirectory");
    const instanceSettingsIsSelectedInput = document.getElementById("instanceSettingsIsSelected");
    const instanceUserDataDirectoryInput = document.getElementById("instanceUserDataDirectoryInput");
    const instanceUseProgramUserAgentInput = document.getElementById("instanceUseProgramUserAgentInput");
    const instanceUserAgentInput = document.getElementById("instanceUserAgentInput");
    const instanceUserAgentProgramHint = document.getElementById("instanceUserAgentProgramHint");
    const instanceUserAgentEffectiveHint = document.getElementById("instanceUserAgentEffectiveHint");
    const instanceUserAgentLockHint = document.getElementById("instanceUserAgentLockHint");
    const instanceViewportWidthInput = document.getElementById("instanceViewportWidthInput");
    const instanceViewportHeightInput = document.getElementById("instanceViewportHeightInput");
    const instancePreserveAspectRatioInput = document.getElementById("instancePreserveAspectRatioInput");
    const instanceAutoResizeViewportInput = document.getElementById("instanceAutoResizeViewportInput");
    const instanceSettingsSubmitBtn = document.getElementById("instanceSettingsSubmitBtn");
    const shotBtn = document.getElementById("shotBtn");
    const instanceStatusBadge = document.getElementById("instanceStatus");
    const pluginArgumentModal = pluginArgumentModalElement ? new bootstrap.Modal(pluginArgumentModalElement) : null;
    const screenshotPreviewModal = screenshotPreviewModalElement ? new bootstrap.Modal(screenshotPreviewModalElement) : null;
    const pluginOutputModal = pluginOutputModalElement ? new bootstrap.Modal(pluginOutputModalElement) : null;
    const instanceSettingsModal = instanceSettingsModalElement ? new bootstrap.Modal(instanceSettingsModalElement) : null;
    const pluginCatalog = new Map();
    const pluginOutputStore = new Map();
    const pluginOutputUi = new Map();
    let statusBusy = false;
    const shownMessages = new Set();
    let isEditingUrl = false;
    let liveDisplayEnabled = true;
    let measuredFps = 0;
    let targetFrameIntervalMs = 100;
    let resizeSyncTimer = null;
    let applyingScreencastSettings = false;
    let pendingScreencastSync = false;
    let screencastAvailable = true;
    let pluginPanelWidth = config.defaultPluginPanelWidth || 520;
    let pointerResizeActive = false;
    let pendingPluginInvocation = null;
    let activePluginOutputPluginId = null;
    let browserHubConnectionId = null;
    let screenshotPreviewUrl = null;
    let selectedInstanceId = null;
    let currentInstanceViewport = null;
    let instanceViewportSyncTimer = null;
    let applyingInstanceViewportSync = false;
    let pendingInstanceViewportSync = false;
    let instanceSettingsAspectRatio = null;
    let synchronizingAspectRatio = false;
    let canvasKeyboardCaptureActive = false;

    const postJson = (...args) => {
        const fn = window.BrowserIndex && window.BrowserIndex.postJson ? window.BrowserIndex.postJson : async (url, body) => {
            const r = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body || {}) });
            if (!r.ok) throw new Error('Request failed: ' + r.status);
            return await r.json();
        };
        return fn.apply(null, args);
    };

    // Many helper functions and event handlers follow - to keep this change incremental we will
    // attach a few critical functions and wire up basic behaviors. The rest of the UI code remains
    // executed in-place until further split.

    async function refreshStatus() {
        if (statusBusy) return;
        statusBusy = true;
        try {
            const response = await fetch((window.__apiEndpoints && window.__apiEndpoints.status) || '/api/status');
            if (!response.ok) throw new Error("状态获取失败: " + response.status);
            const data = await response.json();
            if (typeof applyStatus === 'function') {
                applyStatus(data);
            }
        } catch (e) {
            showError(e.message || "状态获取失败。");
        } finally {
            statusBusy = false;
        }
    }

    async function loadPlugins() {
        try {
            const response = await fetch((window.__apiEndpoints && window.__apiEndpoints.plugins) || '/api/plugins');
            if (!response.ok) throw new Error('插件列表获取失败: ' + response.status);
            const payload = await response.json();
            const plugins = (payload && payload.plugins) || [];
            if (typeof renderPlugins === 'function') renderPlugins(plugins);
            const errors = Array.isArray(payload?.errors) ? payload.errors.filter(Boolean) : [];
            if (errors.length) showError(errors.join('； '));
        } catch (e) {
            if (pluginHost) {
                pluginHost.replaceChildren();
                const error = document.createElement('div');
                error.className = 'text-danger';
                error.textContent = e.message || '插件列表获取失败。';
                pluginHost.appendChild(error);
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

    async function navigate() {
        try {
            await postJson('navigation', { url: urlInput.value.trim() });
            isEditingUrl = false;
            urlInput.blur();
        } catch (e) {
            showError(e.message || '导航失败。');
        }
    }

    async function previewScreenshot() {
        if (!shotBtn) return;
        shotBtn.disabled = true;
        clearError();
        try {
            const screenshotUrl = (window.__apiEndpoints && window.__apiEndpoints.screenshot) || '/api/screenshot';
            const response = await fetch(screenshotUrl + '?ts=' + Date.now(), { cache: 'no-store' });
            if (!response.ok) throw new Error('截图失败: ' + response.status);
            const blob = await response.blob();
            if (screenshotPreviewUrl) URL.revokeObjectURL(screenshotPreviewUrl);
            screenshotPreviewUrl = URL.createObjectURL(blob);
            if (screenshotPreviewImage) {
                screenshotPreviewImage.src = screenshotPreviewUrl;
                screenshotPreviewModal.show();
            }
        } catch (e) {
            showError(e.message || '截图失败。');
        } finally {
            shotBtn.disabled = false;
        }
    }

    // Expose a small API for other modules or inline snippets
    window.BrowserUI = Object.assign(window.BrowserUI || {}, {
        refreshStatus,
        loadPlugins,
        selectTab,
        closeInstance,
        navigate,
        previewScreenshot
    });

    // expose plugin maps for the plugins module
    window.BrowserUI.pluginCatalog = pluginCatalog;
    window.BrowserUI.pluginOutputStore = pluginOutputStore;
    window.BrowserUI.pluginOutputUi = pluginOutputUi;

    // kick off initial behaviors
    try {
        setTimeout(() => {
            if (typeof loadPlugins === 'function') loadPlugins().catch(()=>{});
            refreshStatus().catch(()=>{});
        }, 50);
    } catch (e) {}

})();
