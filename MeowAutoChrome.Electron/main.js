const { app, BrowserWindow, Menu, ipcMain, shell, dialog } = require('electron');
const { spawn } = require('child_process');
const path = require('path');
const fs = require('fs');
const http = require('http');

const DEFAULT_URL = 'http://127.0.0.1:0';
const PROJECT_FILE = path.join(__dirname, '..', 'MeowAutoChrome.WebAPI', 'MeowAutoChrome.WebAPI.csproj');

// Configuration comes exclusively from command-line args passed to Electron.
// Supported args (use dashes, e.g. --webapi-exec=path):
//   --webapi-exec <path>        Path to a packaged backend executable (optional)
//   --skip-start                If present, do not start backend; use --webapi-url instead
//   --webapi-url <url>          URL of an externally-managed backend (used with --skip-start)
//   --webapi-host <host>        Host to bind (default 127.0.0.1)
//   --webapi-base-port <port>   Starting port to try (default 5000)
//   --webapi-port-range <n>     Number of consecutive ports to try (default 20)
function parseArgs(argv) {
    const out = {};
    const a = argv.slice(2);
    for (let i = 0; i < a.length; i++) {
        let s = a[i];
        if (!s) continue;
        if (s.startsWith('--')) {
            s = s.slice(2);
            const eq = s.indexOf('=');
            if (eq !== -1) {
                const k = s.slice(0, eq).replace(/-/g, '_');
                out[k] = s.slice(eq + 1);
            } else {
                const k = s.replace(/-/g, '_');
                const next = a[i + 1];
                if (next && !next.startsWith('--')) { out[k] = next; i++; } else { out[k] = true; }
            }
        }
    }
    return out;
}

const ARGS = parseArgs(process.argv);
const CONFIG = {
    webapi_exec: ARGS.webapi_exec || null,
    skip_start: !!ARGS.skip_start,
    webapi_url: ARGS.webapi_url || null,
    host: ARGS.webapi_host || null,
    base_port: ARGS.webapi_base_port ? Number(ARGS.webapi_base_port) : undefined,
    port_range: ARGS.webapi_port_range ? Number(ARGS.webapi_port_range) : 20,
    passive_attempts: ARGS.webapi_passive_attempts ? Number(ARGS.webapi_passive_attempts) : 10
};

// Attempt to find a packaged/published WebAPI executable in likely locations
function findPackagedWebApiExec() {
    const candidateDirs = app.isPackaged
        ? [
            path.join(process.resourcesPath || '', 'webapi'),
            path.join(process.resourcesPath || '', 'app.asar.unpacked', 'webapi')
        ]
        : [
            path.join(__dirname, 'webapi'),
            path.join(process.resourcesPath || '', 'webapi'),
            path.join(process.resourcesPath || '', 'app', 'webapi'),
            path.join(process.resourcesPath || '', 'app.asar.unpacked', 'webapi'),
            path.join(__dirname, '..', 'webapi')
        ];

    const candidateNames = process.platform === 'win32'
        ? ['MeowAutoChrome.WebAPI.exe', 'MeowAutoChrome.WebAPI']
        : ['MeowAutoChrome.WebAPI', 'MeowAutoChrome.WebAPI.exe'];

    for (const dir of candidateDirs) {
        try {
            if (!dir || !fs.existsSync(dir)) continue;
        } catch (err) { continue; }

        for (const name of candidateNames) {
            const candidate = path.join(dir, name);
            try {
                if (candidate.includes('.asar')) continue;
                if (!fs.existsSync(candidate)) continue;
                if (process.platform !== 'win32') {
                    try { fs.chmodSync(candidate, 0o755); } catch (chmodErr) { console.warn('[main] chmod failed', chmodErr); }
                }
                console.log('[main] found packaged WebAPI executable:', candidate);
                return candidate;
            } catch (e) {
                // ignore and try next candidate
            }
        }
    }

    return null;
}

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

// Instead of probing ports, start the local entry-point executable (or dotnet run)
// and parse its stdout/stderr for the "Now listening on:" message to learn the
// actual bound address. This avoids probing /health and lets the OS pick a
// free port (we request port 0).

function waitForListeningFromStdout(proc, timeoutMs = 30000) {
    return new Promise((resolve, reject) => {
        const regex = /Now listening on:\s*(https?:\/\/\S+)/i;
        let resolved = false;
        const timer = setTimeout(() => {
            if (resolved) return;
            resolved = true;
            cleanup();
            reject(new Error('timeout waiting for listening message'));
        }, timeoutMs);

        const onData = (data) => {
            const s = String(data);
            process.stdout.write(`[webapi] ${s}`);
            const m = s.match(regex);
            if (m && m[1]) {
                if (resolved) return;
                resolved = true;
                cleanup();
                resolve(m[1]);
            }
        };

        const onErr = (data) => {
            const s = String(data);
            process.stderr.write(`[webapi] ${s}`);
            const m = s.match(regex);
            if (m && m[1]) {
                if (resolved) return;
                resolved = true;
                cleanup();
                resolve(m[1]);
            }
        };

        const onExit = (code) => {
            if (resolved) return;
            resolved = true;
            cleanup();
            reject(new Error('process exited before reporting listening'));
        };

        function cleanup() {
            clearTimeout(timer);
            try { proc.stdout?.removeListener('data', onData); } catch (e) { }
            try { proc.stderr?.removeListener('data', onErr); } catch (e) { }
            try { proc.removeListener('exit', onExit); } catch (e) { }
        }

        proc.stdout?.on('data', onData);
        proc.stderr?.on('data', onErr);
        proc.on('exit', onExit);
    });
}

async function startWebApi() {
    // Main process chooses a port and passes it to the backend via args.
    const urlObj = new URL(DEFAULT_URL);
    const host = CONFIG.host || urlObj.hostname || '127.0.0.1';
    let startPort = Number(CONFIG.base_port || urlObj.port || 5000);
    const portRange = Number(CONFIG.port_range || 20);
    let maxPort = startPort + portRange;

    if (CONFIG.skip_start) {
        const useUrl = CONFIG.webapi_url || DEFAULT_URL;
        console.log('[startWebApi] skip_start set, using existing backend at', useUrl);
        return useUrl;
    }

    const killProc = (proc) => new Promise((resolve) => {
        if (!proc || !proc.pid) return resolve();
        try {
            if (process.platform === 'win32') {
                const killer = spawn('taskkill', ['/PID', String(proc.pid), '/T', '/F']);
                killer.on('exit', () => resolve());
            } else {
                try { process.kill(-proc.pid, 'SIGTERM'); } catch (e) { try { process.kill(proc.pid, 'SIGTERM'); } catch (ee) { /* ignore */ } }
                setTimeout(resolve, 300);
            }
        } catch (e) { resolve(); }
    });

    // Attempt to resolve a packaged executable if script did not supply one.
    let execCandidate = CONFIG.webapi_exec || findPackagedWebApiExec();

    // If startPort is 0, choose a random passive port and try a few attempts.
    // This avoids using port 0 (OS-chosen) but still keeps the main-process
    // selecting an ephemeral port.
    if (startPort === 0) {
        const attempts = Number(CONFIG.passive_attempts || 10);
        const tried = new Set();
        const low = 49152, high = 65535;
        let succeeded = false;

        for (let attempt = 0; attempt < attempts; attempt++) {
            // pick a random port in the ephemeral range not tried yet
            let port;
            do { port = Math.floor(Math.random() * (high - low + 1)) + low; } while (tried.has(port));
            tried.add(port);

            const base = `http://${host}:${port}`;
            console.log(`[startWebApi] passive attempt ${attempt + 1}/${attempts}: trying ${base}`);

            let proc = null;
            try {
                if (execCandidate) {
                    const execPath = execCandidate;
                    const opts = { cwd: path.dirname(execPath), env: process.env, stdio: ['ignore', 'pipe', 'pipe'] };
                    if (process.platform !== 'win32') opts.detached = true;
                    proc = spawn(execPath, ['--urls', base], opts);
                    if (opts.detached) proc.unref();
                } else {
                    const args = ['run', '--project', PROJECT_FILE, '--urls', base];
                    const opts = { cwd: path.dirname(PROJECT_FILE), env: process.env, stdio: ['ignore', 'pipe', 'pipe'] };
                    if (process.platform !== 'win32') opts.detached = true;
                    proc = spawn('dotnet', args, opts);
                    if (opts.detached) proc.unref();
                }

                proc.stdout?.on('data', d => process.stdout.write(`[webapi] ${d}`));
                proc.stderr?.on('data', d => process.stderr.write(`[webapi] ${d}`));

                const health = base + '/health';
                try {
                    await waitForReady(health, 10000);
                    // success
                    serverProc = proc;
                    spawnedByApp = true;
                    serverProc.on('exit', (code, signal) => {
                        console.log('WebAPI exited', code, signal);
                        serverProc = null;
                        if (!app.isQuitting) {
                            try { app.quit(); } catch (e) { /* ignore */ }
                        }
                    });
                    serverProc.on('error', err => console.error('WebAPI process error', err));
                    console.log('[startWebApi] passive backend ready at', base);
                    return base;
                } catch (err) {
                    console.warn('[startWebApi] passive attempt failed for', base, err?.message || err);
                    try { await killProc(proc); } catch (e) { }
                    proc = null;
                    // try next random port
                }
            } catch (ex) {
                console.warn('[startWebApi] failed to spawn backend on', base, ex?.message || ex);
                if (proc) await killProc(proc);
                proc = null;
            }
        }

        // If we reach here, passive attempts failed — fall back to explicit iteration
        console.warn('[startWebApi] passive attempts exhausted, falling back to explicit port iteration');
        startPort = Number(CONFIG.base_port || 5000);
        maxPort = startPort + portRange;
    }

    // Re-evaluate packaged exec candidate (in case it appears after passive attempt)
    execCandidate = CONFIG.webapi_exec || findPackagedWebApiExec();

    for (let port = startPort; port <= maxPort; port++) {
        const base = `http://${host}:${port}`;
        console.log('[startWebApi] attempting to start backend at', base);

        let proc = null;
        try {
            if (execCandidate) {
                const execPath = execCandidate;
                const opts = { cwd: path.dirname(execPath), env: process.env, stdio: ['ignore', 'pipe', 'pipe'] };
                if (process.platform !== 'win32') opts.detached = true;
                proc = spawn(execPath, ['--urls', base], opts);
                if (opts.detached) proc.unref();
            } else {
                const args = ['run', '--project', PROJECT_FILE, '--urls', base];
                const opts = { cwd: path.dirname(PROJECT_FILE), env: process.env, stdio: ['ignore', 'pipe', 'pipe'] };
                if (process.platform !== 'win32') opts.detached = true;
                proc = spawn('dotnet', args, opts);
                if (opts.detached) proc.unref();
            }

            proc.stdout?.on('data', d => process.stdout.write(`[webapi] ${d}`));
            proc.stderr?.on('data', d => process.stderr.write(`[webapi] ${d}`));

            const health = base + '/health';
            try {
                await waitForReady(health, 10000);
                serverProc = proc;
                spawnedByApp = true;
                serverProc.on('exit', (code, signal) => {
                    console.log('WebAPI exited', code, signal);
                    serverProc = null;
                    if (!app.isQuitting) {
                        try { app.quit(); } catch (e) { /* ignore */ }
                    }
                });
                serverProc.on('error', err => console.error('WebAPI process error', err));
                console.log('[startWebApi] backend ready at', base);
                return base;
            } catch (e) {
                console.warn('[startWebApi] backend did not become healthy at', base, e?.message || e);
                await killProc(proc);
                proc = null;
            }
        } catch (ex) {
            console.warn('[startWebApi] failed to spawn backend on', base, ex?.message || ex);
            if (proc) await killProc(proc);
            proc = null;
        }
    }

    throw new Error('No available port found to start WebAPI');
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

ipcMain.handle('meow:choose-directory', async () => {
    try {
        const result = await dialog.showOpenDialog({ properties: ['openDirectory'] });
        if (result.canceled || !result.filePaths || result.filePaths.length === 0) return { canceled: true };
        return { canceled: false, path: result.filePaths[0] };
    } catch (err) {
        return { canceled: true, error: err?.message || '选择目录失败' };
    }
});

ipcMain.handle('meow:choose-file', async (_event, filters) => {
    try {
        const normalizedFilters = Array.isArray(filters) && filters.length
            ? filters.map(filter => ({
                name: typeof filter?.name === 'string' && filter.name.trim() ? filter.name.trim() : 'Files',
                extensions: Array.isArray(filter?.extensions) && filter.extensions.length ? filter.extensions.map(item => String(item)) : ['*']
            }))
            : [{ name: 'Files', extensions: ['*'] }];
        const result = await dialog.showOpenDialog({ properties: ['openFile'], filters: normalizedFilters });
        if (result.canceled || !result.filePaths || result.filePaths.length === 0) return { canceled: true };
        return { canceled: false, path: result.filePaths[0] };
    } catch (err) {
        return { canceled: true, error: err?.message || '选择文件失败' };
    }
});
