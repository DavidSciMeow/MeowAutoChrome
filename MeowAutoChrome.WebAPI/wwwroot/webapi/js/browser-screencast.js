// Static screencast rendering helper for WebAPI-hosted assets.
(function () {
    window.BrowserScreencast = window.BrowserScreencast || {
        renderFrame: (data, width, height) => {
            const img = new Image();
            img.src = 'data:image/jpeg;base64,' + data;
            img.onload = () => {
                const canvas = document.getElementById('browserCanvas');
                if (!canvas) return;
                const ctx = canvas.getContext('2d');
                canvas.width = width || img.width;
                canvas.height = height || img.height;
                ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
            };
        }
    };
})();
