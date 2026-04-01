// Settings autosave helper
(function () {
    document.addEventListener('DOMContentLoaded', () => {
        const form = document.getElementById('settingsForm');
        if (!form) return;
        form.addEventListener('submit', (e) => e.preventDefault());
        // autosave logic omitted for brevity
    });
})();
