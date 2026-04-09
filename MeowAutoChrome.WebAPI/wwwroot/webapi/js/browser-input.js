// Handles mouse/keyboard input mapping and sending to server via fetch/SignalR
(function () {
    async function postJson(urlOrKey, body) {
        const url = (window.__apiEndpoints && window.__apiEndpoints[urlOrKey]) || urlOrKey;
        const headers = { "Content-Type": "application/json" };
        const res = await fetch(url, { method: 'POST', headers, body: JSON.stringify(body || {}) });
        if (!res.ok) throw new Error('Request failed: ' + res.status);
        return await res.json();
    }

    window.BrowserInput = window.BrowserInput || {
        sendMouse: data => postJson('screencastSettings', data),
        sendKey: data => postJson('screencastSettings', data)
    };
})();
