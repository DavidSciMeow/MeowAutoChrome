const { app, BrowserWindow, Menu, ipcMain, shell } = require('electron');
const { spawn } = require('child_process');
const path = require('path');
const fs = require('fs');
const http = require('http');

const DEFAULT_URL = process.env.MEOW_WEBAPI_URL || 'http://127.0.0.1:5000';
const PROJECT_FILE = path.join(__dirname, '..', 'MeowAutoChrome.WebAPI', 'MeowAutoChrome.WebAPI.csproj');

let serverProc = null;
let spawnedByApp = false;

function updatePageMenuSelection(page) {
    return page;
}

function buildAppMenu(win) {
    const pages = [
        { label: '浏览器', page: 'browser', accelerator: 'CmdOrCtrl+1' },
        { label: '设置', page: 'settings', accelerator: 'CmdOrCtrl+2' },
        { label: '日志', page: 'logs', accelerator: 'CmdOrCtrl+3' },
        { label: '插件', page: 'plugin-upload', accelerator: 'CmdOrCtrl+4' },
        { label: '帮助', page: 'privacy', accelerator: 'CmdOrCtrl+5' }
    ];

    const navigateToPage = (page) => {
        if (!win || win.isDestroyed()) return;
        win.webContents.executeJavaScript(`window.location.hash = ${JSON.stringify('#' + page)};`, true).catch(() => { });
    };

    const pageMenuItems = pages.map(item => ({
        id: `page:${item.page}`,
        label: item.label,
        accelerator: item.accelerator,
        click: () => navigateToPage(item.page)
    }));

    const template = [
        ...pageMenuItems,
        {
            label: '操作',
            submenu: [
                { label: '刷新当前页', accelerator: 'F5', click: () => win?.webContents.reload() },
                { label: '强制刷新', accelerator: 'CmdOrCtrl+Shift+R', click: () => win?.webContents.reloadIgnoringCache() },
                { type: 'separator' },
                { label: '开发者工具', accelerator: 'F12', click: () => win?.webContents.toggleDevTools() }
            ]
        },
        {
            label: '窗口',
            role: 'windowMenu'
        },
        // Help menu removed per user request
    ];

    const menu = Menu.buildFromTemplate(template);
    Menu.setApplicationMenu(menu);
}

// Lightweight HTTP health probe. Returns an object describing result.
function probeHealth(url, timeoutMs = 500) {
    return new Promise((resolve) => {
        const timer = setTimeout(() => {
            resolve({ ok: false, err: 'TIMEOUT' });
        }, timeoutMs);
        try {
            const req = http.get(url, (res) => {
                clearTimeout(timer);
                const ok = res.statusCode === 200;
                // consume body
                res.resume();
                resolve({ ok, status: res.statusCode });
            });
            req.on('error', (err) => {
                clearTimeout(timer);
                resolve({ ok: false, err: err && err.code ? err.code : 'ERROR' });
            });
        } catch (e) {
            clearTimeout(timer);
            resolve({ ok: false, err: 'ERROR' });
        }
    });
}

async function startWebApi() {
    // Try to find an existing healthy WebAPI or a free port to start one.
    const urlObj = new URL(DEFAULT_URL);
    const baseHost = urlObj.hostname || '127.0.0.1';
    const startPort = Number(urlObj.port) || 5000;
    const maxPort = startPort + 20;

    console.log(`startWebApi: probing for existing WebAPI on ${baseHost} ports ${startPort}-${maxPort}`);

    // 1) check if any existing port already has a healthy service
    for (let p = startPort; p <= maxPort; p++) {
        const health = `http://${baseHost}:${p}/health`;
        console.debug(`[startWebApi] probe ${health}`);
        const r = await probeHealth(health, 300);
        if (r.ok) {
            const base = `http://${baseHost}:${p}`;
            console.log('[startWebApi] found existing healthy WebAPI at', base);
            return base; // reuse existing healthy server
        }
    }

    // 2) find first free port (ECONNREFUSED) to bind a new WebAPI
    let chosenPort = null;
    for (let p = startPort; p <= maxPort; p++) {
        const health = `http://${baseHost}:${p}/health`;
        const r = await probeHealth(health, 200);
        if (r.err === 'ECONNREFUSED' || r.err === 'TIMEOUT') {
            chosenPort = p; break;
        }
    }

    if (chosenPort === null) throw new Error('No available port found to start WebAPI');

    const base = `http://${baseHost}:${chosenPort}`;
    const health = base + '/health';

    const spawnAndWatch = (proc) => {
        serverProc = proc;
        spawnedByApp = true;
        console.log('[spawnAndWatch] spawned WebAPI pid=', serverProc.pid);
        serverProc.stdout?.on('data', d => process.stdout.write(`[webapi] ${d}`));
        serverProc.stderr?.on('data', d => process.stderr.write(`[webapi] ${d}`));
        serverProc.on('exit', (code, signal) => {
            console.log('WebAPI exited', code, signal);
            serverProc = null;
            // If the backend we launched exited unexpectedly, quit the app because UI depends on backend
            if (!app.isQuitting) {
                try { app.quit(); } catch (e) { /* ignore */ }
            }
        });
        serverProc.on('error', err => console.error('WebAPI process error', err));
    };

    if (process.env.MEOW_WEBAPI_EXEC) {
        console.log('[startWebApi] using MEOW_WEBAPI_EXEC:', process.env.MEOW_WEBAPI_EXEC);
        const execEnv = Object.assign({}, process.env);
        execEnv.MEOW_ELECTRON = '1';
        execEnv.ASPNETCORE_URLS = base;
        const opts = { cwd: path.dirname(process.env.MEOW_WEBAPI_EXEC), env: execEnv, stdio: ['ignore', 'pipe', 'pipe'] };
        if (process.platform !== 'win32') opts.detached = true;
        console.log('[startWebApi] spawning executable with opts.detached=', !!opts.detached);
        const proc = spawn(process.env.MEOW_WEBAPI_EXEC, [], opts);
        if (opts.detached) proc.unref();
        spawnAndWatch(proc);
        console.log('[startWebApi] waiting for health at', health);
        await waitForReady(health, 30000);
        console.log('[startWebApi] backend ready at', base);
        return base;
    }

    // Fallback to dotnet run (development).
    const args = ['run', '--project', PROJECT_FILE, '--urls', base];
    const env = Object.assign({}, process.env);
    env.MEOW_ELECTRON = '1';
    env.ASPNETCORE_URLS = base;
    const opts = { cwd: path.dirname(PROJECT_FILE), env: env, stdio: ['ignore', 'pipe', 'pipe'] };
    if (process.platform !== 'win32') opts.detached = true;
    console.log('[startWebApi] spawning dotnet with args=', args.join(' '), 'cwd=', opts.cwd, 'detached=', !!opts.detached);
    const proc = spawn('dotnet', args, opts);
    if (opts.detached) proc.unref();
    spawnAndWatch(proc);
    console.log('[startWebApi] waiting for health at', health);
    await waitForReady(health, 30000);
    console.log('[startWebApi] backend ready at', base);
    return base;
}

function stopBackend() {
    if (!serverProc || !spawnedByApp) return;
    try {
        console.log('[stopBackend] stopping backend, pid=', serverProc.pid);
        if (process.platform === 'win32') {
            // Ensure process tree is terminated on Windows (covers dotnet watch spawn children)
            const killer = spawn('taskkill', ['/PID', String(serverProc.pid), '/T', '/F']);
            killer.on('exit', (code) => {
                console.log('[stopBackend] taskkill exited', code);
                serverProc = null; spawnedByApp = false;
            });
        } else {
            // POSIX: kill the process group (requires we spawned with detached:true)
            try {
                process.kill(-serverProc.pid, 'SIGTERM');
                console.log('[stopBackend] signaled process group -' + serverProc.pid);
            } catch (e) {
                try { process.kill(serverProc.pid, 'SIGTERM'); console.log('[stopBackend] signaled pid', serverProc.pid); } catch (ee) { /* ignore */ }
            }
            serverProc = null; spawnedByApp = false;
        }
    } catch (e) {
        console.error('failed to stop backend', e);
    }
}

function waitForReady(url, timeoutMs) {
    const start = Date.now();
    return new Promise((resolve, reject) => {
        const check = () => {
            http.get(url, (res) => {
                if (res.statusCode === 200) return resolve();
                if (Date.now() - start > timeoutMs) return reject(new Error('timeout'));
                setTimeout(check, 500);
            }).on('error', () => {
                if (Date.now() - start > timeoutMs) return reject(new Error('timeout'));
                setTimeout(check, 500);
            });
        };
        check();
    });
}

function createWindow(apiBase) {
    // prefer an application icon when available
    let iconPath = path.join(__dirname, 'favicon.ico');
    if (!fs.existsSync(iconPath)) iconPath = null;

    const win = new BrowserWindow({
        width: 1400,
        height: 900,
        minWidth: 1280,
        minHeight: 720,
        icon: iconPath || undefined,
        webPreferences: {
            preload: path.join(__dirname, 'preload.js'),
            contextIsolation: true,
            nodeIntegration: false
        }
    });
    console.log('[createWindow] apiBase=', apiBase || DEFAULT_URL);
    // Prefer a packaged/local renderer when available (Electron hosts the UI).
    const localIndex = path.join(__dirname, 'renderer', 'index.html');
    if (fs.existsSync(localIndex)) {
        try {
            // pass apiBase as query so page JS can also read from location.search
            win.loadFile(localIndex, { query: { apiBase: apiBase || DEFAULT_URL } });
        } catch (e) {
            // older electron versions may not support query option
            win.loadFile(localIndex);
        }
    } else {
        win.loadURL(apiBase || DEFAULT_URL);
    }

    // When renderer finishes loading, inject the discovered API base so
    // client-side init logic (running from file://) can use an absolute URL.
    win.webContents.on('did-finish-load', () => {
        try {
            const baseToInject = apiBase || DEFAULT_URL;
            const js = `window.__apiBase = ${JSON.stringify(baseToInject)};`;
            win.webContents.executeJavaScript(js).catch(() => { /* ignore */ });
            // ensure __apiEndpoints exists to avoid undefined references
            win.webContents.executeJavaScript(`window.__apiEndpoints = window.__apiEndpoints || {};`).catch(() => { });
            console.log('Injected __apiBase into renderer:', baseToInject);
        } catch (e) {
            console.warn('Failed to inject __apiBase into renderer', e);
        }
    });

    buildAppMenu(win);

    win.webContents.on('did-fail-load', (event, errorCode, errorDescription, validatedURL) => {
        console.warn('Renderer failed to load:', errorCode, errorDescription, validatedURL);
    });
}

app.whenReady().then(async () => {
    console.log('Electron app ready, starting WebAPI...');
    try {
        const apiBase = await startWebApi();
        // expose to renderer preload via env before creating the window
        try { process.env.MEOW_WEBAPI_URL = apiBase; } catch (e) { /* ignore */ }
        createWindow(apiBase);
    } catch (e) {
        console.error('Failed to start WebAPI', e);
        app.quit();
    }
});

app.on('window-all-closed', () => {
    console.log('window-all-closed');
    if (process.platform !== 'darwin') app.quit();
});

app.on('before-quit', () => {
    stopBackend();
});

// Ensure backend is stopped on unexpected exits / signals as well
process.on('exit', () => stopBackend());
['SIGINT', 'SIGTERM', 'SIGHUP'].forEach(sig => {
    process.on(sig, () => {
        stopBackend();
        // re-raise default behavior
        process.exit();
    });
});

ipcMain.on('meow:navigate-page', (_event, page) => {
    if (typeof page !== 'string') return;
    updatePageMenuSelection(page);
});

ipcMain.handle('meow:open-path', async (_event, targetPath) => {
    if (!targetPath || typeof targetPath !== 'string') {
        return { ok: false, message: '路径无效。' };
    }

    const normalizedPath = path.normalize(targetPath.trim());
    if (!normalizedPath) {
        return { ok: false, message: '路径无效。' };
    }

    const openTarget = fs.existsSync(normalizedPath)
        ? normalizedPath
        : path.dirname(normalizedPath);

    if (!openTarget || !fs.existsSync(openTarget)) {
        return { ok: false, message: '目标目录不存在。' };
    }

    try {
        const result = await shell.openPath(openTarget);
        if (result) {
            return { ok: false, message: result };
        }

        return { ok: true, openedPath: openTarget };
    } catch (error) {
        return { ok: false, message: error?.message || '打开文件夹失败。' };
    }
});
