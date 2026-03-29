(function(){
    const BTN = ["left","middle","right"];
    function btnName(b){ return BTN[b] ?? "none"; }

    function toChromium(canvas, e){
        const r = canvas.getBoundingClientRect();
        return {
            x: (e.clientX - r.left) * (canvas.width  / r.width),
            y: (e.clientY - r.top)  * (canvas.height / r.height)
        };
    }

    function mods(e){
        return (e.altKey ? 1 : 0) | (e.ctrlKey ? 2 : 0) | (e.metaKey ? 4 : 0) | (e.shiftKey ? 8 : 0);
    }

    function isEditableElement(target){
        if (!(target instanceof Element)) return false;
        const tagName = target.tagName.toUpperCase();
        return target.isContentEditable || tagName === "INPUT" || tagName === "TEXTAREA" || tagName === "SELECT";
    }

    function createSender(conn, canvas){
        return {
            sendMouse: function(type,e, extra){
                if (!conn || conn.state !== signalR.HubConnectionState.Connected) return;
                const p = toChromium(canvas, e);
                const isClick = type === "mousePressed" || type === "mouseReleased";
                const isWheel = type === "mouseWheel";
                conn.invoke("SendMouseEvent", {
                    type, x: p.x, y: p.y,
                    button: isClick ? btnName(e.button ?? -1) : "none",
                    buttons: e.buttons ?? 0,
                    clickCount: isClick ? 1 : 0,
                    modifiers: mods(e),
                    deltaX: isWheel ? (extra?.deltaX ?? 0) : null,
                    deltaY: isWheel ? (extra?.deltaY ?? 0) : null
                }).catch(()=>{});
            },
            sendKey: function(type, e, text = ""){
                if (!conn || conn.state !== signalR.HubConnectionState.Connected) return;
                conn.invoke("SendKeyEvent", {
                    type,
                    key: e.key,
                    code: e.code,
                    text,
                    modifiers: mods(e),
                    windowsVirtualKeyCode: e.keyCode || 0,
                    nativeVirtualKeyCode: e.keyCode || 0,
                    autoRepeat: !!e.repeat,
                    isKeypad: e.location === 3,
                    isSystemKey: !!e.altKey
                }).catch(()=>{});
            }
        };
    }

    const BrowserInput = {
        init(conn, canvasEl){
            try{
                const sender = createSender(conn, canvasEl);
                let lastMove = 0;
                let canvasKeyboardCaptureActive = false;

                canvasEl?.addEventListener('mousemove', e => {
                    const now = Date.now();
                    if (now - lastMove < 16) return;
                    lastMove = now;
                    sender.sendMouse('mouseMoved', e);
                });

                canvasEl?.addEventListener('mousedown', e => {
                    e.preventDefault();
                    canvasEl.focus();
                    sender.sendMouse('mouseMoved', e);
                    sender.sendMouse('mousePressed', e);
                });

                canvasEl?.addEventListener('mouseup', e => sender.sendMouse('mouseReleased', e));

                canvasEl?.addEventListener('wheel', e => {
                    e.preventDefault();
                    sender.sendMouse('mouseWheel', e, { deltaX: e.deltaX, deltaY: e.deltaY });
                }, { passive: false });

                canvasEl?.addEventListener('contextmenu', e => e.preventDefault());

                window.addEventListener('pointerdown', e => {
                    const target = e.target;
                    const activatedFromStage = target instanceof Node && !!canvasEl && canvasEl.closest('.browser-stage') && canvasEl.closest('.browser-stage').contains(target) && !(document.getElementById('tabsStatusPanel')?.contains(target));
                    canvasKeyboardCaptureActive = activatedFromStage;
                    if (canvasKeyboardCaptureActive) canvasEl.focus(); else canvasEl.blur();
                }, true);

                window.addEventListener('blur', () => { canvasKeyboardCaptureActive = false; canvasEl?.blur(); });

                function isCanvasKeyboardBlockedByModal(){
                    return document.getElementById('pluginArgumentModal')?.classList.contains('show')
                        || document.getElementById('screenshotPreviewModal')?.classList.contains('show')
                        || document.getElementById('pluginOutputModal')?.classList.contains('show')
                        || document.getElementById('instanceSettingsModal')?.classList.contains('show');
                }

                function shouldForwardCanvasKeyboardEvent(e){
                    if (!canvasKeyboardCaptureActive || !liveDisplayEnabled || canvasEl.classList.contains('d-none') || isCanvasKeyboardBlockedByModal()) return false;
                    if (isEditableElement(e.target) && e.target !== canvasEl) return false;
                    return true;
                }

                window.addEventListener('keydown', e => {
                    if (canvasKeyboardCaptureActive && e.key === 'Escape' && !e.ctrlKey && !e.altKey && !e.metaKey && !e.shiftKey){
                        e.preventDefault(); canvasKeyboardCaptureActive = false; canvasEl.blur(); return;
                    }
                    if (!shouldForwardCanvasKeyboardEvent(e)) return;
                    e.preventDefault();
                    sender.sendKey('rawKeyDown', e);
                    if (e.key.length === 1 && !e.ctrlKey && !e.metaKey) sender.sendKey('char', e, e.key);
                }, true);

                window.addEventListener('keyup', e => {
                    if (!shouldForwardCanvasKeyboardEvent(e)) return;
                    e.preventDefault(); sender.sendKey('keyUp', e);
                }, true);

                // expose sender for direct calls
                window.BrowserInput = { sender };
            } catch (ex) {
                console.warn('BrowserInput init failed', ex);
            }
        }
    };

    window.BrowserInput = BrowserInput;
})();
