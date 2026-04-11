(function () {
    const pageSections = Array.from(document.querySelectorAll('[data-page]'));
    const pageLinks = Array.from(document.querySelectorAll('[data-page-link]'));
    const defaultPage = 'browser';
    const knownPages = new Set(pageSections.map(section => section.dataset.page).filter(Boolean));
    const pageTitles = {
        browser: '浏览器',
        logs: '日志',
        settings: '设置',
        'plugin-upload': '插件管理',
        privacy: '隐私提示'
    };
    let currentPage = defaultPage;

    function showNotification(message, kind = 'success') {
        const container = document.getElementById('toastContainer') || document.getElementById('pageNotifications');
        if (!container || typeof bootstrap === 'undefined') {
            console.log(message);
            return;
        }

        const toast = document.createElement('div');
        toast.className = 'toast align-items-center text-bg-' + kind + ' border-0';
        toast.setAttribute('role', 'status');
        toast.setAttribute('aria-live', 'polite');
        toast.setAttribute('aria-atomic', 'true');
        toast.innerHTML = '<div class="d-flex"><div class="toast-body small"></div><button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button></div>';
        toast.querySelector('.toast-body').textContent = message || '';
        container.appendChild(toast);
        const instance = new bootstrap.Toast(toast, { delay: 3500 });
        instance.show();
        toast.addEventListener('hidden.bs.toast', () => toast.remove());
    }

    function activatePage(pageName) {
        const page = knownPages.has(pageName) ? pageName : currentPage || defaultPage;
        currentPage = page;
        for (const section of pageSections) {
            section.classList.toggle('d-none', section.dataset.page !== page);
        }

        for (const link of pageLinks) {
            link.classList.toggle('app-nav-link-active', link.dataset.pageLink === page);
        }

        window.meow?.notifyPageChanged?.(page);

        window.dispatchEvent(new CustomEvent('meow:pagechange', { detail: { page } }));
    }

    function currentPageFromHash() {
        const hash = (window.location.hash || '').replace(/^#/, '').trim();
        if (!hash) {
            return defaultPage;
        }
        return knownPages.has(hash) ? hash : currentPage;
    }

    window.showNotification = showNotification;
    window.MeowSite = { activatePage };

    window.addEventListener('hashchange', () => activatePage(currentPageFromHash()));
    activatePage(currentPageFromHash());
})();
