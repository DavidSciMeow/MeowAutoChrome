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
    const playwrightArchivePanel = document.getElementById("playwrightArchivePanel");
    const playwrightArchivePath = document.getElementById("PlaywrightArchivePath");
    const playwrightArchiveChooseBtn = document.getElementById("PlaywrightArchiveChooseBtn");
    const playwrightArchiveValidation = document.getElementById("playwrightArchiveValidation");
    const playwrightDownloadLinks = document.getElementById("playwrightDownloadLinks");
    const playwrightInstallBtn = document.getElementById("PlaywrightInstallBtn");
    const playwrightInstalledBrowsers = document.getElementById("playwrightInstalledBrowsers");
    const playwrightUninstallAllBtn = document.getElementById("PlaywrightUninstallAllBtn");
    let saveTimer = null;
    let activeRequest = null;
    let validatingArchive = false;
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

    function renderPlaywrightDownloadLinks(links) {
        if (!playwrightDownloadLinks) return;
        playwrightDownloadLinks.replaceChildren();
        for (const link of links || []) {
            const row = document.createElement("div");
            const anchor = document.createElement("a");
            anchor.href = link.url;
            anchor.target = "_blank";
            anchor.rel = "noreferrer noopener";
            anchor.textContent = link.label || link.url;
            row.appendChild(anchor);
            if (link.description) {
                const text = document.createElement("span");
                text.className = "text-muted";
                text.textContent = " - " + link.description;
                row.appendChild(text);
            }
            playwrightDownloadLinks.appendChild(row);
        }
    }

    function renderArchiveValidation(container, result, idleText) {
        if (!container) return;
        if (!result) {
            container.replaceChildren();
            container.className = "playwright-archive-validation d-none";
            if (idleText) {
                const title = document.createElement("div");
                title.className = "playwright-archive-validation-title";
                title.textContent = idleText;
                container.appendChild(title);
                container.className = "playwright-archive-validation";
            }
            return;
        }

        container.replaceChildren();
        container.className = "playwright-archive-validation " + (result.isValid ? "is-valid" : "is-invalid");

        const title = document.createElement("div");
        title.className = "playwright-archive-validation-title";
        title.textContent = result.summary || (result.isValid ? "校验通过" : "校验失败");
        container.appendChild(title);

        const detail = document.createElement("div");
        detail.className = "playwright-archive-validation-detail";
        detail.textContent = result.detail || "";
        container.appendChild(detail);

        const checks = document.createElement("div");
        checks.className = "playwright-archive-validation-checks";
        const checkItems = [
            ["文件存在", !!result.exists],
            ["文件名是 chrome-win64.zip", !!result.fileNameMatches],
            ["压缩包可读取", !!result.archiveReadable],
            ["包含 chrome-win64/chrome.exe", !!result.containsExpectedLayout]
        ];
        for (const [label, ok] of checkItems) {
            const item = document.createElement("div");
            item.className = "playwright-archive-validation-check " + (ok ? "is-pass" : "is-fail");
            item.textContent = (ok ? "通过" : "失败") + " · " + label;
            checks.appendChild(item);
        }
        container.appendChild(checks);
    }

    function formatRuntimeSource(runtimeSource) {
        return runtimeSource === "bundled"
            ? "随应用目录"
            : runtimeSource === "managed"
                ? "应用私有目录"
                : runtimeSource === "managed-offline"
                    ? "手动导入目录"
                    : runtimeSource === "global"
                        ? "系统全局缓存"
                        : runtimeSource || "未知来源";
    }

    function renderInstalledBrowsers(items) {
        if (!playwrightInstalledBrowsers) return;
        playwrightInstalledBrowsers.replaceChildren();

        if (!Array.isArray(items) || items.length === 0) {
            const empty = document.createElement("div");
            empty.className = "playwright-installed-browser-empty";
            empty.textContent = "当前没有检测到任何已安装浏览器。请先下载 chrome-win64.zip，再选择本地压缩包导入。";
            playwrightInstalledBrowsers.appendChild(empty);
            return;
        }

        for (const item of items) {
            const row = document.createElement("div");
            row.className = "list-group-item px-0";

            const header = document.createElement("div");
            header.className = "d-flex align-items-start justify-content-between gap-3 flex-wrap";

            const textWrap = document.createElement("div");
            const title = document.createElement("div");
            title.className = "playwright-installed-browser-title";
            title.textContent = item.label + (item.version ? " " + item.version : "") + (item.isActive ? " · 当前生效" : "");
            textWrap.appendChild(title);

            const meta = document.createElement("div");
            meta.className = "playwright-installed-browser-meta";
            meta.textContent = [formatRuntimeSource(item.runtimeSource), item.executablePath || item.installDirectory].filter(Boolean).join(" · ");
            textWrap.appendChild(meta);

            header.appendChild(textWrap);

            const removeButton = document.createElement("button");
            removeButton.type = "button";
            removeButton.className = "btn btn-outline-danger btn-sm";
            removeButton.textContent = "删除";
            removeButton.disabled = !item.canUninstallFromApp;
            removeButton.dataset.browser = item.browser;
            removeButton.dataset.runtimeSource = item.runtimeSource || "";
            header.appendChild(removeButton);

            row.appendChild(header);
            playwrightInstalledBrowsers.appendChild(row);
        }
    }

    async function validateSelectedArchive() {
        const archivePath = playwrightArchivePath?.value?.trim() || "";
        if (!archivePath) {
            renderArchiveValidation(playwrightArchiveValidation, null, "请选择一个 chrome-win64.zip 后，系统会在这里显示校验结果。");
            return null;
        }

        if (validatingArchive) return null;
        validatingArchive = true;
        renderArchiveValidation(playwrightArchiveValidation, {
            isValid: false,
            exists: false,
            fileNameMatches: false,
            archiveReadable: false,
            containsExpectedLayout: false,
            summary: "正在校验压缩包...",
            detail: "正在检查文件是否存在、文件名是否正确，以及压缩包内部结构是否符合预期。"
        });

        try {
            const response = await fetch(resolveApi("playwrightValidateArchive", "/api/playwright/validate-archive"), {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ archivePath })
            });
            const payload = await response.json().catch(() => ({}));
            if (!response.ok) {
                throw new Error(payload.error || payload.message || "压缩包校验失败");
            }
            renderArchiveValidation(playwrightArchiveValidation, payload);
            return payload;
        } catch (error) {
            renderArchiveValidation(playwrightArchiveValidation, {
                isValid: false,
                exists: false,
                fileNameMatches: false,
                archiveReadable: false,
                containsExpectedLayout: false,
                summary: "校验请求失败",
                detail: error.message || "无法完成压缩包校验。"
            });
            return null;
        } finally {
            validatingArchive = false;
        }
    }

    function syncPlaywrightInstallControls(payload) {
        if (playwrightArchivePanel)
            playwrightArchivePanel.classList.remove("d-none");
        if (playwrightInstallBtn)
            playwrightInstallBtn.textContent = "导入并识别压缩包";
        renderPlaywrightDownloadLinks(payload?.downloadLinks || []);
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
        if (playwrightRuntimeHelp) playwrightRuntimeHelp.textContent = (payload.message || (installed ? "Playwright Chromium 已准备完成。" : "未安装时将阻止创建浏览器实例。")) + " 当前来源：" + sourceLabel + "。当前只支持手动导入 chrome-win64.zip。";
        if (playwrightRuntimeOutput) playwrightRuntimeOutput.textContent = payload.output || "";
        if (playwrightRuntimeOutputPanel) playwrightRuntimeOutputPanel.classList.toggle("d-none", !payload.output);
        if (playwrightInstallBtn) playwrightInstallBtn.disabled = false;
        if (playwrightUninstallAllBtn) playwrightUninstallAllBtn.disabled = !(payload.installedBrowsers || []).length;
        renderInstalledBrowsers(payload.installedBrowsers || []);
        syncPlaywrightInstallControls(payload);
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

    async function postPlaywrightAction(apiKey, fallback, successMessage, body) {
        const response = await fetch(resolveApi(apiKey, fallback), {
            method: "POST",
            headers: body ? { "Content-Type": "application/json" } : undefined,
            body: body ? JSON.stringify(body) : undefined
        });
        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
            throw new Error(payload.error || payload.message || "操作失败");
        }

        applyPlaywrightStatus(payload);
        window.dispatchEvent(new CustomEvent("meow:playwright-status", { detail: payload }));
        if (payload.operationState === "skipped")
            return payload;

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

    playwrightInstalledBrowsers?.addEventListener("click", async (event) => {
        const button = event.target instanceof HTMLElement ? event.target.closest("button[data-browser]") : null;
        if (!button) return;
        const browser = button.dataset.browser || "";
        const runtimeSource = button.dataset.runtimeSource || "";
        const browserLabel = button.closest(".list-group-item")?.querySelector(".playwright-installed-browser-title")?.textContent?.trim() || browser;
        if (!browser) return;
        if (!confirm("确定要删除 " + browserLabel + " 吗？这会先关闭所有浏览器实例。")) return;

        button.disabled = true;
        setStatus("正在删除 " + browserLabel + "...", "saving");
        try {
            await postPlaywrightAction("playwrightUninstallBrowser", "/api/playwright/uninstall-browser", browserLabel + " 已删除。", {
                browser,
                runtimeSource: runtimeSource || null
            });
            setStatus(browserLabel + " 已删除。", "success");
        } catch (error) {
            setStatus(error.message || "删除失败。", "error");
            window.showNotification?.(error.message || "删除失败。", "danger");
        } finally {
            await loadPlaywrightStatus();
        }
    });

    playwrightArchiveChooseBtn?.addEventListener("click", async () => {
        try {
            const result = await window.meow?.chooseFile?.([{ name: "ZIP Archives", extensions: ["zip"] }]);
            if (!result || result.canceled || !result.path) return;
            if (playwrightArchivePath) playwrightArchivePath.value = result.path;
            await validateSelectedArchive();
            syncPlaywrightInstallControls(window.__playwrightRuntimeStatus || null);
        } catch {
            setStatus("选择文件失败。", "error");
        }
    });
    playwrightArchivePath?.addEventListener("input", () => {
        syncPlaywrightInstallControls(window.__playwrightRuntimeStatus || null);
    });
    playwrightArchivePath?.addEventListener("change", () => {
        validateSelectedArchive().catch(() => { });
        syncPlaywrightInstallControls(window.__playwrightRuntimeStatus || null);
    });
    playwrightArchivePath?.addEventListener("blur", () => {
        validateSelectedArchive().catch(() => { });
        syncPlaywrightInstallControls(window.__playwrightRuntimeStatus || null);
    });

    playwrightInstallBtn?.addEventListener("click", async () => {
        const archivePath = playwrightArchivePath?.value?.trim() || null;
        if (!archivePath) {
            setStatus("请先选择本地 chrome-win64.zip。", "error");
            window.showNotification?.("在线安装已移除，请先下载并选择 chrome-win64.zip。", "warning");
            return;
        }

        playwrightInstallBtn.disabled = true;
        setStatus("正在识别并导入本地压缩包...", "saving");
        try {
            const validation = await validateSelectedArchive();
            if (!validation?.isValid) {
                setStatus(validation?.summary || "压缩包校验失败。", "error");
                return;
            }

            const payload = await postPlaywrightAction("playwrightInstall", "/api/playwright/install", "已根据压缩包识别结果完成导入。", {
                archivePath
            });
            if (payload?.operationState === "skipped") {
                setStatus(payload.message || "已检测到浏览器已安装，未重复安装。", "saving");
                window.showNotification?.(payload.message || "已检测到浏览器已安装，未重复安装。", "warning");
            } else {
                setStatus("已根据压缩包识别结果完成导入。", "success");
            }
        } catch (error) {
            setStatus(error.message || "安装失败。", "error");
            window.showNotification?.(error.message || "Playwright 浏览器安装失败。", "danger");
        } finally {
            await loadPlaywrightStatus();
        }
    });

    playwrightUninstallAllBtn?.addEventListener("click", async () => {
        if (!confirm("这会删除应用私有目录、离线打包目录以及系统全局 ms-playwright 缓存。确定继续吗？")) return;
        if (playwrightInstallBtn) playwrightInstallBtn.disabled = true;
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
