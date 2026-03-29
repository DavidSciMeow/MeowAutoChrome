(function () {
    const form = document.getElementById("settingsForm");
    const status = document.getElementById("settingsAutoSaveStatus");
    const navLinks = document.getElementById("settingsNavLinks");
    const fields = form.querySelectorAll("input, textarea, select");
    const pluginPanelWidthInput = document.getElementById("PluginPanelWidth");
    const pluginPanelWidthValue = document.getElementById("pluginPanelWidthValue");
    let saveTimer = null;
    let activeRequest = null;
    let lastSavedState = new URLSearchParams(new FormData(form)).toString();

    function buildSettingsNav() {
        navLinks.replaceChildren();

        form.querySelectorAll("section.settings-card").forEach((section, index) => {
            if (!section.id)
                section.id = "settingsSection" + (index + 1);

            const title = section.querySelector(".card-header.fw-semibold")?.textContent?.trim();
            if (!title)
                return;

            const link = document.createElement("a");
            link.className = "settings-nav-link";
            link.href = "#" + section.id;
            link.textContent = title;
            navLinks.appendChild(link);
        });
    }

    function setStatus(message, kind) {
        status.textContent = message;
        status.className = "settings-autosave-status settings-autosave-status-" + kind;
    }

    async function saveSettings() {
        const state = new URLSearchParams(new FormData(form)).toString();
        if (state === lastSavedState) {
            setStatus("已自动保存", "success");
            return;
        }

        activeRequest?.abort();
        activeRequest = new AbortController();
        setStatus("正在自动保存...", "saving");

        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: new FormData(form),
                headers: { "X-Requested-With": "XMLHttpRequest" },
                signal: activeRequest.signal
            });

            const payload = await response.json().catch(() => ({ message: "设置保存失败。" }));
            if (!response.ok) {
                setStatus(payload.message || "设置保存失败。", "error");
                showNotification(payload.message || "设置保存失败。", 'danger');
                return;
            }

            lastSavedState = state;
            setStatus(payload.message || "设置已自动保存。", "success");
            showNotification(payload.message || "设置已自动保存。", 'success');
        } catch (error) {
            if (error.name === "AbortError")
                return;

            setStatus("设置保存失败。", "error");
            showNotification('设置保存失败。', 'danger');
        }
    }

    function scheduleSave() {
        clearTimeout(saveTimer);
        setStatus("检测到变更...", "saving");
        saveTimer = setTimeout(() => { saveSettings().catch(() => { }); }, 500);
    }

    function renderPluginPanelWidth() {
        if (!pluginPanelWidthInput || !pluginPanelWidthValue)
            return;

        pluginPanelWidthValue.textContent = pluginPanelWidthInput.value + " px";
    }

    fields.forEach(field => {
        field.addEventListener("input", scheduleSave);
        field.addEventListener("change", scheduleSave);
    });

    pluginPanelWidthInput?.addEventListener("input", renderPluginPanelWidth);
    pluginPanelWidthInput?.addEventListener("change", renderPluginPanelWidth);

    buildSettingsNav();
    renderPluginPanelWidth();
})();
