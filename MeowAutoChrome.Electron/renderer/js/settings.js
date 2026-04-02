(function () {
    const form = document.getElementById("settingsForm");
    const status = document.getElementById("settingsAutoSaveStatus");
    const navLinks = document.getElementById("settingsNavLinks");
    if (!form) return;
    const fields = form.querySelectorAll("input, textarea, select");
    const settingsFilePath = document.getElementById("settingsFilePath");
    const defaultUserDataDirectory = document.getElementById("defaultUserDataDirectory");
    let saveTimer = null;
    let activeRequest = null;
    let lastSavedState = new URLSearchParams(new FormData(form)).toString();

    function resolveApi(key, fallback) {
        return (window.__apiEndpoints && window.__apiEndpoints[key]) || fallback;
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
            ScreencastFps: payload.screencastFps,
            UserDataDirectory: payload.userDataDirectory,
            UserAgent: payload.userAgent
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
        lastSavedState = new URLSearchParams(new FormData(form)).toString();
        setStatus("已加载设置", "success");
    }

    async function loadSettings() {
        try {
            const response = await fetch(resolveApi("settings", "/api/settings"), { cache: "no-store" });
            if (!response.ok) throw new Error("设置读取失败: " + response.status);
            applySettings(await response.json());
        } catch (error) {
            setStatus("设置加载失败。", "error");
            window.showNotification?.(error.message || "设置加载失败。", "danger");
        }
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

    buildSettingsNav();
    loadSettings().catch(() => { });
})();
