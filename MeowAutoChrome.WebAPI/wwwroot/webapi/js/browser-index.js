// Static browser bootstrap helpers for WebAPI-hosted assets.
(function () {
    // window.__browserConfig can be populated by startup scripts before this file runs.

    const config = window.__browserConfig || {};

    // Centralized API helper using overridable endpoints from window.__apiEndpoints
    function resolveApi(pathKey) {
        try { return (window.__apiEndpoints && window.__apiEndpoints[pathKey]) || pathKey; } catch { return pathKey; }
    }

    // Backwards-compatibility: intercept direct fetch calls to legacy "/Browser/..." endpoints
    // and rewrite them to the new API endpoints so stale cached scripts won't cause runtime errors.
    (function installBrowserUrlRewriter() {
        if (typeof window.fetch !== 'function') return;
        const origFetch = window.fetch.bind(window);
        window.fetch = function (resource, init) {
            try {
                let url = resource;
                if (resource && typeof resource === 'object' && resource.url)
                    url = resource.url;

                if (typeof url === 'string') {
                    // normalize
                    const u = url.replace(/^\s+|\s+$/g, '');
                    if (u.startsWith('/Browser') || u.startsWith('/browser')) {
                        // map legacy routes to new API endpoints
                        const mapping = {
                            '/Browser/Status': resolveApi('status'),
                            '/Browser/Screencast': resolveApi('screencastSettings'),
                            '/Browser/CreateInstance': resolveApi('instances'),
                            '/Browser/CreateTab': resolveApi('tabsNew'),
                            '/Browser/SelectTab': resolveApi('tabsSelect'),
                            '/Browser/CloseTab': resolveApi('tabsClose'),
                            '/Browser/Plugins': resolveApi('plugins'),
                            '/Browser/Screenshot': resolveApi('screenshot'),
                        };

                        for (const [k, v] of Object.entries(mapping)) {
                            if (u.startsWith(k)) {
                                const newUrl = v + u.slice(k.length);
                                if (resource && typeof resource === 'object' && resource.url) {
                                    // clone Request-like object
                                    try {
                                        return origFetch(newUrl, init);
                                    } catch (e) { break; }
                                }
                                resource = newUrl;
                                break;
                            }
                        }
                    }
                }
            } catch (e) { /* swallow */ }
            return origFetch(resource, init);
        };
    })();

    // Minimal public helpers used by the view
    async function postJson(urlOrKey, body) {
        const url = resolveApi(urlOrKey);
        const headers = { "Content-Type": "application/json" };
        if (window.BrowserIndex && window.BrowserIndex.browserHubConnectionId)
            headers["X-BrowserHub-ConnectionId"] = window.BrowserIndex.browserHubConnectionId;
        const res = await fetch(url, { method: 'POST', headers, body: JSON.stringify(body || {}) });
        if (!res.ok) throw new Error('Request failed: ' + res.status);
        return await res.json();
    }

    // Expose a minimal API for other inline scripts
    window.BrowserIndex = Object.assign(window.BrowserIndex || {}, {
        postJson,
        browserHubConnectionId: null
    });

})();
