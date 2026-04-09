// Static plugin upload helper for WebAPI-hosted assets.
(function () {
    window.PluginUpload = {
        upload: async (form) => {
            const data = new FormData(form);
            const res = await fetch((window.__apiEndpoints && window.__apiEndpoints.plugins) || '/api/plugins', { method: 'POST', body: data });
            return res.ok;
        }
    };
})();
