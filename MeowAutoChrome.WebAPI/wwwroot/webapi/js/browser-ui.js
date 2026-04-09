// Static UI bootstrap helpers for WebAPI-hosted assets.
(function () {
    document.addEventListener('DOMContentLoaded', () => {
        // basic hookup for demo purposes
        const connStatus = document.getElementById('connStatus');
        if (connStatus) connStatus.textContent = '未连接';
    });
})();
