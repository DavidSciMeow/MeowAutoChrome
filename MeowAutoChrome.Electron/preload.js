const { contextBridge, ipcRenderer } = require('electron');

// Expose a small safe API to the renderer. Preload runs before any web
// scripts. Prefer an `apiBase` passed as a query param when the page is
// loaded (Electron `loadFile(..., { query: { apiBase } })`). Do not read
// configuration from environment variables here.
function readApiBaseFromSearch() {
    try {
        const u = new URL(window.location.href);
        const p = u.searchParams.get('apiBase');
        return p || null;
    } catch (e) {
        return null;
    }
}
const apiBase = readApiBaseFromSearch();

contextBridge.exposeInMainWorld('meow', {
    ping: () => 'pong',
    apiBase,
    notifyPageChanged: (page) => ipcRenderer.send('meow:navigate-page', page),
    openPath: (targetPath) => ipcRenderer.invoke('meow:open-path', targetPath),
    chooseDirectory: () => ipcRenderer.invoke('meow:choose-directory'),
    chooseFile: (filters) => ipcRenderer.invoke('meow:choose-file', filters),
    // helper to build absolute API urls
    getApiUrl: (path) => {
        if (!path) return apiBase || null;
        if (!apiBase) return path;
        // ensure leading slash
        const p = path.startsWith('/') ? path : `/${path}`;
        return apiBase + p;
    }
});

