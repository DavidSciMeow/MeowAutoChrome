// Static tabs rendering helper for WebAPI-hosted assets.
(function () {
    window.BrowserTabs = window.BrowserTabs || {
        updateTabs: (tabs) => {
            const tabsBar = document.getElementById('tabsBar');
            if (!tabsBar) return;
            tabsBar.innerHTML = '';
            for (const t of tabs) {
                const el = document.createElement('div');
                el.className = 'browser-tab';
                el.textContent = t.title || t.id;
                tabsBar.appendChild(el);
            }
        }
    };
})();
