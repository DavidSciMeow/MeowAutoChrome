(function(){
    // Handle frame rendering and FPS tracking
    const canvas = document.getElementById('browserCanvas');
    const fpsTxt = document.getElementById('fpsDisplay');
    const noSig = document.getElementById('noSignal');
    const browserStage = canvas ? canvas.closest('.browser-stage') : null;
    const ctx = canvas ? canvas.getContext('2d') : null;

    let frameCount = 0, lastFpsTime = performance.now(), measuredFps = 0;
    let img = new Image();
    let pending = null, busy = false, activeFrame = null;

    function intervalToFps(intervalMs){ const interval = Math.max(16, Number(intervalMs)||100); return Math.max(1, Math.min(60, Math.round(1000/interval))); }
    function renderFpsDisplay(){ if (fpsTxt) fpsTxt.textContent = 'FPS ' + measuredFps; }
    function updateFps(){ frameCount++; const now = performance.now(); if (now - lastFpsTime >= 1000){ measuredFps = frameCount; renderFpsDisplay(); frameCount=0; lastFpsTime = now; } }

    function getCanvasViewportSize(){ const width = Math.max(320, Math.round(browserStage?.clientWidth || canvas.clientWidth || 1280)); const height = Math.max(240, Math.round(browserStage?.clientHeight || canvas.clientHeight || 800)); return { width, height }; }
    function resizeCanvasBuffer(w,h){ if (!canvas) return; if (canvas.width !== w || canvas.height !== h){ canvas.width = w; canvas.height = h; } }

    img.onload = function(){
        const viewport = getCanvasViewportSize();
        const targetWidth = canvas.width || viewport.width;
        const targetHeight = canvas.height || viewport.height;
        const sourceWidth = activeFrame?.width ?? img.naturalWidth;
        const sourceHeight = activeFrame?.height ?? img.naturalHeight;
        resizeCanvasBuffer(targetWidth, targetHeight);
        if (!ctx) return;
        ctx.fillStyle = '#000'; ctx.fillRect(0,0,targetWidth,targetHeight);
        const scale = Math.min(targetWidth/Math.max(1,sourceWidth), targetHeight/Math.max(1,sourceHeight));
        const drawWidth = Math.max(1, Math.round(sourceWidth*scale));
        const drawHeight = Math.max(1, Math.round(sourceHeight*scale));
        const offsetX = Math.floor((targetWidth-drawWidth)/2);
        const offsetY = Math.floor((targetHeight-drawHeight)/2);
        ctx.drawImage(img, offsetX, offsetY, drawWidth, drawHeight);
        if (noSig) noSig.style.display = 'none';
        updateFps();
        busy = false;
        if (pending !== null){ const n = pending; pending = null; drawFrame(n); }
    };

    function drawFrame(frame){ if (busy){ pending = frame; return; } busy = true; activeFrame = frame; img.src = 'data:image/jpeg;base64,' + frame.data; }

    window.BrowserScreencast = { drawFrame, resizeCanvasBuffer, getCanvasViewportSize };
})();
