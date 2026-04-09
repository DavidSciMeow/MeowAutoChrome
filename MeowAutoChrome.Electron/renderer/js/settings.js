(function () {
    const form = document.getElementById("settingsForm");
    const status = document.getElementById("settingsAutoSaveStatus");
    const navLinks = document.getElementById("settingsNavLinks");
    if (!form) return;
    const fields = form.querySelectorAll("input, textarea, select");
    const settingsFilePath = document.getElementById("settingsFilePath");
    const defaultUserDataDirectory = document.getElementById("defaultUserDataDirectory");
    const playwrightRuntimeState = document.getElementById("playwrightRuntimeState");
    const playwrightRuntimeLocation = document.getElementById("playwrightRuntimeLocation");
    const playwrightRuntimeScriptPath = document.getElementById("playwrightRuntimeScriptPath");
    const playwrightRuntimeHelp = document.getElementById("playwrightRuntimeHelp");
    const playwrightRuntimeOutputPanel = document.getElementById("playwrightRuntimeOutputPanel");
    const playwrightRuntimeOutput = document.getElementById("playwrightRuntimeOutput");
    const playwrightInstallMode = document.getElementById("PlaywrightInstallMode");
    const playwrightInstallBtn = document.getElementById("PlaywrightInstallBtn");
    const playwrightUninstallBtn = document.getElementById("PlaywrightUninstallBtn");
    const playwrightUninstallAllBtn = document.getElementById("PlaywrightUninstallAllBtn");
    let saveTimer = null;
    let activeRequest = null;
    let lastSavedState = new URLSearchParams(new FormData(form)).toString();

    function resolveApi(key, fallback) {
        const resolved = (window.__apiEndpoints && window.__apiEndpoints[key]) || null;
        if (resolved) return resolved;
        if (fallback && typeof window.meow?.getApiUrl === "function" && /^\//.test(fallback)) {
            return window.meow.getApiUrl(fallback);
        }
        return fallback;
    }

    function buildSettingsNav() {
        if (!navLinks) return;
        navLinks.replaceChildren();

        form.querySelectorAll("section.settings-card").forEach((section, index) => {
            if (!section.id)
                section.id = "settingsSection" + (index + 1);

            const title = section.querySelector(".card-header.fw-semibold")?.textContent?.trim();
            if (!title) return;

            const link = document.createElement("a");
            link.className = "settings-nav-link";
            link.href = "#" + section.id;
            link.textContent = title;
            navLinks.appendChild(link);
        });
    }

    function setStatus(message, kind) {
        if (!status) return;
        status.textContent = message;
        status.className = "settings-autosave-status settings-autosave-status-" + kind;
    }

    async function saveSettings() {
        const state = new URLSearchParams(new FormData(form)).toString();
        if (state === lastSavedState) { setStatus("已自动保存", "success"); return; }

        activeRequest?.abort();
        activeRequest = new AbortController();
        setStatus("正在自动保存...", "saving");

        try {
            const response = await fetch(resolveApi("settingsAutosave", form.action), {
                method: "POST",
                body: new FormData(form),
                headers: { "X-Requested-With": "XMLHttpRequest" },
                signal: activeRequest.signal
            });

            const payload = await response.json().catch(() => ({ message: "设置保存失败。" }));
            if (!response.ok) {
                setStatus(payload.message || "设置保存失败。", "error");
                return;
            }

            lastSavedState = state;
            setStatus(payload.message || "设置已自动保存。", "success");
        } catch (error) {
            if (error.name === "AbortError") return;
            setStatus("设置保存失败。", "error");
        }
    }

    function scheduleSave() {
        clearTimeout(saveTimer);
        setStatus("检测到变更...", "saving");
        saveTimer = setTimeout(() => { saveSettings().catch(() => { }); }, 500);
    }

    // Plugin panel width control removed from settings UI.

    function applySettings(payload) {
        if (!payload) return;
        const byId = {
            SearchUrlTemplate: payload.searchUrlTemplate,
            UserDataDirectory: payload.userDataDirectory,
            UserAgent: payload.userAgent,
            PluginDirectory: payload.pluginDirectory || payload.defaultPluginDirectory
        };

        Object.entries(byId).forEach(([id, value]) => {
            const element = document.getElementById(id);
            if (element && value !== undefined && value !== null) {
                element.value = String(value);
            }
        });

        const allowInstanceOverride = document.getElementById("AllowInstanceUserAgentOverride");
        if (allowInstanceOverride) allowInstanceOverride.checked = !!payload.allowInstanceUserAgentOverride;

        // Headless control removed from settings UI — browser toolbar handles it now.

        if (settingsFilePath) settingsFilePath.textContent = payload.settingsFilePath || "未知";
        if (defaultUserDataDirectory) defaultUserDataDirectory.textContent = payload.defaultUserDataDirectory || "未知";
        const defaultPluginDirectory = document.getElementById('defaultPluginDirectory');
        if (defaultPluginDirectory) defaultPluginDirectory.textContent = payload.defaultPluginDirectory || "未知";
        lastSavedState = new URLSearchParams(new FormData(form)).toString();
        setStatus("已加载设置", "success");
    }

    async function loadSettings() {
        try {
            const response = await fetch(resolveApi("settings", "/api/settings"), { cache: "no-store" });
            if (!response.ok) throw new Error("设置读取失败: " + response.status);
            applySettings(await response.json());
        } catch (error) {
            // Try fallback to plugin roots endpoint (may be available even if full settings API is not)
            try {
                const r2 = await fetch(resolveApi("pluginsRoot", "/api/plugins/root"), { cache: "no-store" });
                if (r2.ok) {
                    const p = await r2.json();
                    applySettings({ pluginDirectory: p.pluginDirectory, defaultPluginDirectory: p.defaultPluginDirectory, settingsFilePath: "未知" });
                    setStatus("已加载插件根目录信息", "success");
                    return;
                }
            } catch { }

            setStatus("设置加载失败。", "error");
            window.showNotification?.(error.message || "设置加载失败。", "danger");
        }
    }

    function applyPlaywrightStatus(payload) {
        if (!payload) {
            if (playwrightRuntimeState) playwrightRuntimeState.textContent = "状态读取失败";
            if (playwrightRuntimeLocation) playwrightRuntimeLocation.textContent = "未知";
            if (playwrightRuntimeScriptPath) playwrightRuntimeScriptPath.textContent = "未知";
            if (playwrightRuntimeHelp) playwrightRuntimeHelp.textContent = "无法读取 Playwright 运行时状态。";
            if (playwrightUninstallAllBtn) playwrightUninstallAllBtn.disabled = true;
            if (playwrightInstallMode) playwrightInstallMode.value = "online";
            return;
        }

        const installed = !!payload.isInstalled;
        const canUninstallFromApp = !!payload.canUninstallFromApp;
        const sourceLabel = payload.runtimeSource === "bundled"
            ? "随应用打包目录"
            : payload.runtimeSource === "managed"
                ? "应用私有目录"
                : payload.runtimeSource === "managed-offline"
                    ? "离线压缩包解压目录"
                    : payload.runtimeSource === "global"
                        ? "系统全局缓存"
                        : "未检测到";
        if (playwrightRuntimeState) playwrightRuntimeState.textContent = installed ? "已安装，可直接使用浏览器能力" : "未安装，当前必须先安装 Chromium";
        if (playwrightRuntimeLocation) playwrightRuntimeLocation.textContent = payload.browserInstallDirectory || "未知";
        if (playwrightRuntimeScriptPath) playwrightRuntimeScriptPath.textContent = payload.scriptPath || "未找到";
        if (playwrightRuntimeHelp) playwrightRuntimeHelp.textContent = (payload.message || (installed ? "Playwright Chromium 已准备完成。" : "未安装时将阻止创建浏览器实例。")) + " 当前来源：" + sourceLabel + "。" + (payload.offlinePackageAvailable ? " 已检测到离线压缩包。" : " 未检测到离线压缩包。");
        if (playwrightRuntimeOutput) playwrightRuntimeOutput.textContent = payload.output || "";
        if (playwrightRuntimeOutputPanel) playwrightRuntimeOutputPanel.classList.toggle("d-none", !payload.output);
        if (playwrightInstallBtn) playwrightInstallBtn.disabled = installed;
        if (playwrightUninstallBtn) playwrightUninstallBtn.disabled = !installed || !canUninstallFromApp;
        if (playwrightUninstallAllBtn) playwrightUninstallAllBtn.disabled = !installed;
        if (playwrightInstallMode) {
            const offlineOption = playwrightInstallMode.querySelector('option[value="offline"]');
            if (offlineOption) offlineOption.disabled = !payload.offlinePackageAvailable;
            if (payload.offlinePackageAvailable) {
                playwrightInstallMode.value = "offline";
            } else if (playwrightInstallMode.value === "offline" && !payload.offlinePackageAvailable) {
                playwrightInstallMode.value = "online";
            }
        }
    }

    async function loadPlaywrightStatus() {
        try {
            const response = await fetch(resolveApi("playwrightStatus", "/api/playwright/status"), { cache: "no-store" });
            if (!response.ok) throw new Error("Playwright 状态读取失败: " + response.status);
            const payload = await response.json();
            applyPlaywrightStatus(payload);
            return payload;
        } catch (error) {
            applyPlaywrightStatus(null);
            return null;
        }
    }

    async function postPlaywrightAction(apiKey, fallback, successMessage) {
        const response = await fetch(resolveApi(apiKey, fallback), { method: "POST" });
        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
            throw new Error(payload.error || payload.message || "操作失败");
        }

        applyPlaywrightStatus(payload);
        window.dispatchEvent(new CustomEvent("meow:playwright-status", { detail: payload }));
        if (successMessage) window.showNotification?.(successMessage, "success");
        return payload;
    }

    fields.forEach(field => { field.addEventListener("input", scheduleSave); field.addEventListener("change", scheduleSave); });
    // Plugin panel width control removed — no event bindings.

    // Open UserData directory button (opens folder in OS file explorer)
    const userDataOpenBtn = document.getElementById('UserDataDirectoryOpenBtn');
    userDataOpenBtn?.addEventListener('click', async () => {
        const pathEl = document.getElementById('UserDataDirectory');
        const target = (pathEl?.value?.trim()) || (defaultUserDataDirectory?.textContent?.trim()) || '';
        if (!target) {
            setStatus('目录为空。', 'error');
            return;
        }
        try {
            const result = await window.meow?.openPath?.(target);
            if (!result?.ok) {
                setStatus(result?.message || '打开目录失败。', 'error');
                return;
            }
            window.showNotification?.('目录已打开。', 'success');
        } catch (err) {
            setStatus('打开目录失败。', 'error');
        }
    });

    // Plugin directory open/choose buttons
    const pluginDirOpenBtn = document.getElementById('PluginDirectoryOpenBtn');
    pluginDirOpenBtn?.addEventListener('click', async () => {
        const pathEl = document.getElementById('PluginDirectory');
        const defaultEl = document.getElementById('defaultPluginDirectory');
        const target = (pathEl?.value?.trim()) || (defaultEl?.textContent?.trim()) || '';
        if (!target) { setStatus('插件目录为空。', 'error'); return; }
        try {
            const result = await window.meow?.openPath?.(target);
            if (!result?.ok) { setStatus(result?.message || '打开目录失败。', 'error'); return; }
            window.showNotification?.('目录已打开。', 'success');
        } catch (err) { setStatus('打开目录失败。', 'error'); }
    });

    const pluginDirChooseBtn = document.getElementById('PluginDirectoryChooseBtn');
    pluginDirChooseBtn?.addEventListener('click', async () => {
        try {
            const r = await window.meow?.chooseDirectory?.();
            if (!r || r.canceled) return;
            const el = document.getElementById('PluginDirectory');
            if (el) { el.value = r.path; scheduleSave(); }
        } catch (e) { setStatus('选择目录失败。', 'error'); }
    });

    buildSettingsNav();
    loadSettings().catch(() => { });
    loadPlaywrightStatus().catch(() => { });

    window.addEventListener("meow:playwright-status", event => {
        applyPlaywrightStatus(event.detail || null);
    });

    playwrightInstallBtn?.addEventListener("click", async () => {
        const mode = playwrightInstallMode?.value || "online";
        playwrightInstallBtn.disabled = true;
        if (playwrightUninstallBtn) playwrightUninstallBtn.disabled = true;
        setStatus(mode === "offline" ? "正在离线安装 Playwright Chromium..." : "正在在线安装 Playwright Chromium...", "saving");
        try {
            const installUrl = "/api/playwright/install?mode=" + encodeURIComponent(mode);
            await postPlaywrightAction("playwrightInstall", installUrl, mode === "offline" ? "Playwright Chromium 离线安装完成。" : "Playwright Chromium 在线安装完成。");
            setStatus(mode === "offline" ? "Playwright Chromium 离线安装完成。" : "Playwright Chromium 在线安装完成。", "success");
        } catch (error) {
            setStatus(error.message || "安装失败。", "error");
            window.showNotification?.(error.message || "Playwright Chromium 安装失败。", "danger");
        } finally {
            await loadPlaywrightStatus();
        }
    });

    playwrightUninstallBtn?.addEventListener("click", async () => {
        if (!confirm("卸载 Playwright Chromium 前会先关闭所有浏览器实例。确定继续吗？")) return;
        playwrightInstallBtn && (playwrightInstallBtn.disabled = true);
        playwrightUninstallBtn.disabled = true;
        if (playwrightUninstallAllBtn) playwrightUninstallAllBtn.disabled = true;
        setStatus("正在卸载 Playwright Chromium...", "saving");
        try {
            await postPlaywrightAction("playwrightUninstall", "/api/playwright/uninstall", "Playwright Chromium 已卸载。");
            setStatus("Playwright Chromium 已卸载。", "success");
        } catch (error) {
            setStatus(error.message || "卸载失败。", "error");
            window.showNotification?.(error.message || "Playwright Chromium 卸载失败。", "danger");
        } finally {
            await loadPlaywrightStatus();
        }
    });

    playwrightUninstallAllBtn?.addEventListener("click", async () => {
        if (!confirm("这会删除应用私有目录、离线打包目录以及系统全局 ms-playwright 缓存。确定继续吗？")) return;
        if (playwrightInstallBtn) playwrightInstallBtn.disabled = true;
        if (playwrightUninstallBtn) playwrightUninstallBtn.disabled = true;
        playwrightUninstallAllBtn.disabled = true;
        setStatus("正在彻底卸载全部 Playwright 缓存...", "saving");
        try {
            await postPlaywrightAction("playwrightUninstallAll", "/api/playwright/uninstall?all=true", "Playwright 浏览器缓存已全部卸载。");
            setStatus("Playwright 浏览器缓存已全部卸载。", "success");
        } catch (error) {
            setStatus(error.message || "彻底卸载失败。", "error");
            window.showNotification?.(error.message || "Playwright 彻底卸载失败。", "danger");
        } finally {
            await loadPlaywrightStatus();
        }
    });

    // One-click reset button
    const resetBtn = document.getElementById('ResetSettingsBtn');
    resetBtn?.addEventListener('click', async () => {
        if (!confirm('确定要将所有设置恢复为默认值吗？此操作会覆盖当前配置。')) return;
        setStatus('正在重置设置...', 'saving');
        try {
            const r = await fetch(resolveApi('settingsReset', '/api/settings/reset'), { method: 'POST' });
            if (!r.ok) {
                const payload = await r.json().catch(() => null);
                setStatus(payload?.message || '重置失败。', 'error');
                window.showNotification?.(payload?.message || '重置失败。', 'danger');
                return;
            }
            const payload = await r.json();
            applySettings(payload);
            setStatus('设置已重置为默认值。', 'success');
            window.showNotification?.('设置已重置为默认值。', 'success');
            try { window.BrowserUI?.refreshStatus?.(); window.BrowserUI?.loadPlugins?.(); } catch { }
        } catch (e) {
            setStatus('重置失败。', 'error');
            window.showNotification?.('重置失败。', 'danger');
        }
    });
})();
