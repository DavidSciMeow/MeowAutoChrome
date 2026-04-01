const { contextBridge, ipcRenderer } = require('electron');

// Expose a small safe API to the renderer. Preload runs before any web
// scripts, so exposing `apiBase` here ensures renderer code can build
// absolute URLs even when served from file://
const apiBase = process.env.MEOW_WEBAPI_URL || null;

contextBridge.exposeInMainWorld('meow', {
    ping: () => 'pong',
    apiBase,
    notifyPageChanged: (page) => ipcRenderer.send('meow:navigate-page', page),
    openPath: (targetPath) => ipcRenderer.invoke('meow:open-path', targetPath),
    // helper to build absolute API urls
    getApiUrl: (path) => {
        if (!path) return apiBase || null;
        if (!apiBase) return path;
        // ensure leading slash
        const p = path.startsWith('/') ? path : `/${path}`;
        return apiBase + p;
    }
});
