(function () {
    // Handle frame rendering and FPS tracking
    const canvas = document.getElementById('browserCanvas');
    const fpsTxt = document.getElementById('fpsDisplay');
    const noSig = document.getElementById('noSignal');
    const browserStage = canvas ? canvas.closest('.browser-stage') : null;
    const ctx = canvas ? canvas.getContext('2d') : null;

    let frameCount = 0, lastFpsTime = performance.now(), measuredFps = 0;
    let pending = null, busy = false, activeFrame = null;

    function base64ToArrayBuffer(base64) {
        const binary = atob(base64);
        const len = binary.length;
        const bytes = new Uint8Array(len);
        for (let i = 0; i < len; i++) bytes[i] = binary.charCodeAt(i);
        return bytes.buffer;
    }

    function intervalToFps(intervalMs) { const interval = Math.max(16, Number(intervalMs) || 100); return Math.max(1, Math.min(60, Math.round(1000 / interval))); }
    function renderFpsDisplay() { if (fpsTxt) fpsTxt.textContent = 'FPS ' + measuredFps; }
    function updateFps() { frameCount++; const now = performance.now(); if (now - lastFpsTime >= 1000) { measuredFps = frameCount; renderFpsDisplay(); frameCount = 0; lastFpsTime = now; } }

    function getCanvasViewportSize() { const width = Math.max(320, Math.round(browserStage?.clientWidth || canvas.clientWidth || 1280)); const height = Math.max(240, Math.round(browserStage?.clientHeight || canvas.clientHeight || 800)); return { width, height }; }
    function resizeCanvasBuffer(w, h) { if (!canvas) return; if (canvas.width !== w || canvas.height !== h) { canvas.width = w; canvas.height = h; } }

    async function drawFrame(frame) {
        if (busy) { pending = frame; return; }
        busy = true;
        activeFrame = frame;

        const viewport = getCanvasViewportSize();
        const targetWidth = canvas.width || viewport.width;
        const targetHeight = canvas.height || viewport.height;
        resizeCanvasBuffer(targetWidth, targetHeight);

        if (!ctx) {
            busy = false;
            return;
        }

        // Prefer createImageBitmap (faster decoding, possible off-main-thread).
        if (typeof createImageBitmap === 'function') {
            try {
                const ab = base64ToArrayBuffer(frame.data);
                const blob = new Blob([ab], { type: 'image/jpeg' });
                const bitmap = await createImageBitmap(blob);
                const sourceWidth = bitmap.width;
                const sourceHeight = bitmap.height;
                ctx.fillStyle = '#000'; ctx.fillRect(0, 0, targetWidth, targetHeight);
                const scale = Math.min(targetWidth / Math.max(1, sourceWidth), targetHeight / Math.max(1, sourceHeight));
                const drawWidth = Math.max(1, Math.round(sourceWidth * scale));
                const drawHeight = Math.max(1, Math.round(sourceHeight * scale));
                const offsetX = Math.floor((targetWidth - drawWidth) / 2);
                const offsetY = Math.floor((targetHeight - drawHeight) / 2);
                ctx.drawImage(bitmap, offsetX, offsetY, drawWidth, drawHeight);
                bitmap.close && bitmap.close();
                if (noSig) noSig.style.display = 'none';
                updateFps();
            } catch (err) {
                // Fallback to Image path below if createImageBitmap fails.
                try {
                    const img = new Image();
                    img.onload = function () {
                        const sourceWidth = img.naturalWidth;
                        const sourceHeight = img.naturalHeight;
                        ctx.fillStyle = '#000'; ctx.fillRect(0, 0, targetWidth, targetHeight);
                        const scale = Math.min(targetWidth / Math.max(1, sourceWidth), targetHeight / Math.max(1, sourceHeight));
                        const drawWidth = Math.max(1, Math.round(sourceWidth * scale));
                        const drawHeight = Math.max(1, Math.round(sourceHeight * scale));
                        const offsetX = Math.floor((targetWidth - drawWidth) / 2);
                        const offsetY = Math.floor((targetHeight - drawHeight) / 2);
                        ctx.drawImage(img, offsetX, offsetY, drawWidth, drawHeight);
                        if (noSig) noSig.style.display = 'none';
                        updateFps();
                        busy = false;
                        if (pending !== null) { const n = pending; pending = null; drawFrame(n); }
                    };
                    img.src = 'data:image/jpeg;base64,' + frame.data;
                    return; // early return; pending handled in onload
                } catch (e) {
                    console.warn && console.warn('drawFrame fallback failed', e);
                }
            }
        } else {
            // No createImageBitmap available — use legacy Image() decode path.
            try {
                const img = new Image();
                img.onload = function () {
                    const sourceWidth = img.naturalWidth;
                    const sourceHeight = img.naturalHeight;
                    ctx.fillStyle = '#000'; ctx.fillRect(0, 0, targetWidth, targetHeight);
                    const scale = Math.min(targetWidth / Math.max(1, sourceWidth), targetHeight / Math.max(1, sourceHeight));
                    const drawWidth = Math.max(1, Math.round(sourceWidth * scale));
                    const drawHeight = Math.max(1, Math.round(sourceHeight * scale));
                    const offsetX = Math.floor((targetWidth - drawWidth) / 2);
                    const offsetY = Math.floor((targetHeight - drawHeight) / 2);
                    ctx.drawImage(img, offsetX, offsetY, drawWidth, drawHeight);
                    if (noSig) noSig.style.display = 'none';
                    updateFps();
                    busy = false;
                    if (pending !== null) { const n = pending; pending = null; drawFrame(n); }
                };
                img.src = 'data:image/jpeg;base64,' + frame.data;
                return; // onload will handle busy/pending
            } catch (e) {
                console.warn && console.warn('drawFrame legacy path failed', e);
            }
        }

        // mark not busy here if we got to the end of fast path
        busy = false;
        if (pending !== null) { const n = pending; pending = null; drawFrame(n); }
    }

    window.BrowserScreencast = { drawFrame, resizeCanvasBuffer, getCanvasViewportSize };
})();
