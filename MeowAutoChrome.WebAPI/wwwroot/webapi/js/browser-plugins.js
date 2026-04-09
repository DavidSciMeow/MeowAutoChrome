// Static plugin UI helpers for WebAPI-hosted assets.
(function () {
    async function getJson(url) {
        const res = await fetch(url);
        if (!res.ok) throw new Error('Request failed: ' + res.status);
        return await res.json();
    }

    window.BrowserPlugins = window.BrowserPlugins || {
        loadCatalog: () => getJson((window.__apiEndpoints && window.__apiEndpoints.plugins) || '/api/plugins')
    };
})();
