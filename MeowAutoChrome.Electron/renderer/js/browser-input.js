(function () {
    const BTN = ["left", "middle", "right"];

    function btnName(button) {
        return BTN[button] ?? "none";
    }

    function toChromium(canvas, event) {
        const rect = canvas.getBoundingClientRect();
        return {
            x: (event.clientX - rect.left) * (canvas.width / rect.width),
            y: (event.clientY - rect.top) * (canvas.height / rect.height)
        };
    }

    function modifiers(event) {
        return (event.altKey ? 1 : 0)
            | (event.ctrlKey ? 2 : 0)
            | (event.metaKey ? 4 : 0)
            | (event.shiftKey ? 8 : 0);
    }

    function isEditableElement(target) {
        if (!(target instanceof Element))
            return false;

        const tagName = target.tagName.toUpperCase();
        return target.isContentEditable || tagName === "INPUT" || tagName === "TEXTAREA" || tagName === "SELECT";
    }

    function createSender(connection, canvas) {
        return {
            sendMouse(type, event, extra) {
                if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
                    console.debug('BrowserInput.sendMouse: hub not connected', connection && connection.state);
                    return;
                }

                const point = toChromium(canvas, event);
                console.debug('BrowserInput.sendMouse', type, point.x, point.y, 'button', event.button, 'mods', modifiers(event));
                const isClick = type === "mousePressed" || type === "mouseReleased";
                const isWheel = type === "mouseWheel";
                connection.invoke("SendMouseEvent", {
                    type,
                    x: point.x,
                    y: point.y,
                    button: isClick ? btnName(event.button ?? -1) : "none",
                    buttons: event.buttons ?? 0,
                    clickCount: isClick ? 1 : 0,
                    modifiers: modifiers(event),
                    deltaX: isWheel ? (extra?.deltaX ?? 0) : null,
                    deltaY: isWheel ? (extra?.deltaY ?? 0) : null
                }).catch(() => { });
            },
            sendKey(type, event, text = "") {
                if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
                    console.debug('BrowserInput.sendKey: hub not connected', connection && connection.state);
                    return;
                }

                console.debug('BrowserInput.sendKey', type, event.key, 'code', event.code, 'mods', modifiers(event));
                connection.invoke("SendKeyEvent", {
                    type,
                    key: event.key,
                    code: event.code,
                    text,
                    modifiers: modifiers(event),
                    windowsVirtualKeyCode: event.keyCode || 0,
                    nativeVirtualKeyCode: event.keyCode || 0,
                    autoRepeat: !!event.repeat,
                    isKeypad: event.location === 3,
                    isSystemKey: !!event.altKey
                }).catch(() => { });
            }
        };
    }

    function isCanvasKeyboardBlockedByModal() {
        return document.getElementById("pluginArgumentModal")?.classList.contains("show")
            || document.getElementById("screenshotPreviewModal")?.classList.contains("show")
            || document.getElementById("pluginOutputModal")?.classList.contains("show")
            || document.getElementById("instanceSettingsModal")?.classList.contains("show")
            || document.getElementById("createInstanceModal")?.classList.contains("show");
    }

    const BrowserInput = {
        init(connection, canvasEl) {
            if (!canvasEl)
                return;

            try {
                const sender = createSender(connection, canvasEl);
                console.debug('BrowserInput init', { canvas: !!canvasEl, connectionState: connection && connection.state });
                let lastMove = 0;
                let canvasKeyboardCaptureActive = false;

                function shouldForwardCanvasKeyboardEvent(event) {
                    if (!canvasKeyboardCaptureActive)
                        return false;

                    if (!window.BrowserUI?.isLiveDisplayEnabled?.())
                        return false;

                    if (canvasEl.classList.contains("d-none") || isCanvasKeyboardBlockedByModal())
                        return false;

                    if (isEditableElement(event.target) && event.target !== canvasEl)
                        return false;

                    return true;
                }

                canvasEl.addEventListener("mousemove", event => {
                    const now = Date.now();
                    if (now - lastMove < 16)
                        return;

                    lastMove = now;
                    sender.sendMouse("mouseMoved", event);
                });

                canvasEl.addEventListener("mousedown", event => {
                    event.preventDefault();
                    canvasEl.focus();
                    sender.sendMouse("mouseMoved", event);
                    sender.sendMouse("mousePressed", event);
                });

                canvasEl.addEventListener("mouseup", event => {
                    sender.sendMouse("mouseReleased", event);
                });

                canvasEl.addEventListener("wheel", event => {
                    event.preventDefault();
                    sender.sendMouse("mouseWheel", event, { deltaX: event.deltaX, deltaY: event.deltaY });
                }, { passive: false });

                canvasEl.addEventListener("contextmenu", event => event.preventDefault());

                window.addEventListener("pointerdown", event => {
                    const stage = canvasEl.closest(".browser-stage");
                    const target = event.target;
                    const activatedFromStage = target instanceof Node
                        && !!stage
                        && stage.contains(target)
                        && !(document.getElementById("tabsStatusPanel")?.contains(target));

                    canvasKeyboardCaptureActive = activatedFromStage;
                    if (canvasKeyboardCaptureActive)
                        canvasEl.focus();
                    else
                        canvasEl.blur();
                }, true);

                window.addEventListener("blur", () => {
                    canvasKeyboardCaptureActive = false;
                    canvasEl.blur();
                });

                window.addEventListener("keydown", event => {
                    if (canvasKeyboardCaptureActive && event.key === "Escape" && !event.ctrlKey && !event.altKey && !event.metaKey && !event.shiftKey) {
                        event.preventDefault();
                        canvasKeyboardCaptureActive = false;
                        canvasEl.blur();
                        return;
                    }

                    if (!shouldForwardCanvasKeyboardEvent(event))
                        return;

                    event.preventDefault();
                    sender.sendKey("rawKeyDown", event);
                    if (event.key.length === 1 && !event.ctrlKey && !event.metaKey)
                        sender.sendKey("char", event, event.key);
                }, true);

                window.addEventListener("keyup", event => {
                    if (!shouldForwardCanvasKeyboardEvent(event))
                        return;

                    event.preventDefault();
                    sender.sendKey("keyUp", event);
                }, true);

                window.BrowserInput = Object.assign(window.BrowserInput || {}, { sender, connection });
            } catch (error) {
                console.warn("BrowserInput init failed", error);
            }
        }
    };

    window.BrowserInput = BrowserInput;
})();
