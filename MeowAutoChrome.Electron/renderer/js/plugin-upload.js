(function () {
    const openUploadModalBtn = document.getElementById('openPluginUploadModalBtn');
    const openPluginDirectoryBtn = document.getElementById('openPluginDirectoryBtn');
    const uploadModalElement = document.getElementById('pluginUploadModal');
    const scanResultModalElement = document.getElementById('pluginScanResultModal');
    const fileInput = document.getElementById('pluginUploadFileInput');
    const directoryInput = document.getElementById('pluginUploadDirectoryInput');
    const dropZone = document.getElementById('pluginUploadDropZone');
    const selectFilesBtn = document.getElementById('pluginSelectFilesBtn');
    const selectDirectoryBtn = document.getElementById('pluginSelectDirectoryBtn');
    const clearSelectionBtn = document.getElementById('pluginClearSelectionBtn');
    const uploadSubmitBtn = document.getElementById('pluginUploadSubmitBtn');
    const uploadSpinner = document.getElementById('pluginUploadSpinner');
    const selectedInfo = document.getElementById('pluginSelectedFilesInfo');
    const installedList = document.getElementById('installedAssembliesList');
    const installedPlaceholder = document.getElementById('installedAssembliesPlaceholder');
    const resultList = document.getElementById('pluginScanResultList');
    const resultPlaceholder = document.getElementById('pluginScanResultPlaceholder');

    if (!openUploadModalBtn || !openPluginDirectoryBtn || !uploadModalElement || !scanResultModalElement || !fileInput || !directoryInput || !dropZone || !selectFilesBtn || !selectDirectoryBtn || !clearSelectionBtn || !uploadSubmitBtn || !uploadSpinner || !selectedInfo || !installedList || !installedPlaceholder || !resultList || !resultPlaceholder || typeof bootstrap === 'undefined') {
        return;
    }

    const uploadModal = new bootstrap.Modal(uploadModalElement);
    const scanResultModal = new bootstrap.Modal(scanResultModalElement);
    const selectedFiles = [];

    function resolveApi(key, fallback) {
        return (window.__apiEndpoints && window.__apiEndpoints[key]) || fallback;
    }

    function fileKey(file) {
        const relativePath = file._meowRelativePath || file.webkitRelativePath || '';
        return `${relativePath}::${file.name}::${file.size}::${file.lastModified}`;
    }

    function updateSelectedInfo() {
        if (!selectedFiles.length) {
            selectedInfo.textContent = '尚未选择文件。';
            return;
        }

        const preview = selectedFiles.slice(0, 3).map(file => file._meowRelativePath || file.webkitRelativePath || file.name).join('，');
        const suffix = selectedFiles.length > 3 ? ` 等 ${selectedFiles.length} 个文件` : ` 共 ${selectedFiles.length} 个文件`;
        selectedInfo.textContent = `已选择 ${preview}${suffix}`;
    }

    function clearSelection() {
        selectedFiles.length = 0;
        fileInput.value = '';
        directoryInput.value = '';
        updateSelectedInfo();
    }

    function appendFiles(files) {
        const seen = new Set(selectedFiles.map(fileKey));
        for (const file of files) {
            const key = fileKey(file);
            if (seen.has(key)) {
                continue;
            }

            seen.add(key);
            selectedFiles.push(file);
        }

        updateSelectedInfo();
    }

    async function readEntry(entry, parentPath = '') {
        if (!entry) {
            return [];
        }

        if (entry.isFile) {
            return new Promise(resolve => {
                entry.file(file => {
                    try {
                        Object.defineProperty(file, '_meowRelativePath', {
                            value: parentPath ? `${parentPath}/${file.name}` : file.name,
                            configurable: true
                        });
                    } catch {
                    }
                    resolve([file]);
                }, () => resolve([]));
            });
        }

        if (!entry.isDirectory) {
            return [];
        }

        const reader = entry.createReader();
        const children = [];
        while (true) {
            const entries = await new Promise(resolve => reader.readEntries(resolve, () => resolve([])));
            if (!entries.length) {
                break;
            }

            children.push(...entries);
        }

        const nestedFiles = [];
        for (const child of children) {
            const nextParentPath = parentPath ? `${parentPath}/${entry.name}` : entry.name;
            nestedFiles.push(...await readEntry(child, nextParentPath));
        }

        return nestedFiles;
    }

    async function extractFilesFromDataTransfer(dataTransfer) {
        if (!dataTransfer) {
            return [];
        }

        if (dataTransfer.items && dataTransfer.items.length) {
            const files = [];
            for (const item of Array.from(dataTransfer.items)) {
                const entry = typeof item.webkitGetAsEntry === 'function' ? item.webkitGetAsEntry() : null;
                if (entry) {
                    files.push(...await readEntry(entry));
                    continue;
                }

                const file = item.getAsFile?.();
                if (file) {
                    files.push(file);
                }
            }

            return files;
        }

        return Array.from(dataTransfer.files || []);
    }

    function setUploadingState(isUploading) {
        uploadSpinner.style.display = isUploading ? '' : 'none';
        uploadSubmitBtn.disabled = isUploading;
        clearSelectionBtn.disabled = isUploading;
        selectFilesBtn.disabled = isUploading;
        selectDirectoryBtn.disabled = isUploading;
    }

    function createBadge(label, className) {
        const badge = document.createElement('span');
        badge.className = className;
        badge.textContent = label;
        return badge;
    }

    function renderScanResults(processed) {
        resultList.replaceChildren();

        if (!processed.length) {
            resultPlaceholder.style.display = '';
            resultPlaceholder.textContent = '未发现可扫描的 DLL。';
            return;
        }

        resultPlaceholder.style.display = 'none';
        processed.forEach(item => {
            const inspection = item.inspection || {};
            const errors = Array.isArray(item.errors) ? item.errors : [];
            const loadedPlugins = Array.isArray(item.plugins) ? item.plugins : [];
            const inspectionPlugins = Array.isArray(inspection.plugins) ? inspection.plugins : [];

            const card = document.createElement('div');
            card.className = 'card plugin-scan-card';

            const body = document.createElement('div');
            body.className = 'card-body';

            const header = document.createElement('div');
            header.className = 'd-flex justify-content-between align-items-start gap-3 flex-wrap';

            const titleWrap = document.createElement('div');
            const title = document.createElement('div');
            title.className = 'fw-semibold';
            title.textContent = inspection.fileName || item.path || '未知程序集';
            const path = document.createElement('div');
            path.className = 'small text-muted text-break';
            path.textContent = item.path || inspection.assemblyPath || '';
            titleWrap.appendChild(title);
            titleWrap.appendChild(path);

            const badgeWrap = document.createElement('div');
            badgeWrap.className = 'd-flex gap-2 flex-wrap align-items-center';
            badgeWrap.appendChild(inspection.contractVersionMatches
                ? createBadge('Contract 匹配', 'badge text-bg-success')
                : createBadge('Contract 不匹配', 'badge text-bg-warning'));
            if (loadedPlugins.length) {
                badgeWrap.appendChild(createBadge(`已加载 ${loadedPlugins.length} 个插件`, 'badge text-bg-primary'));
            }

            header.appendChild(titleWrap);
            header.appendChild(badgeWrap);
            body.appendChild(header);

            if (inspection.referencesContracts || inspection.compatibilityMessage) {
                const contractInfo = document.createElement('div');
                contractInfo.className = 'small mt-3';
                contractInfo.innerHTML = `<div>插件 Contract：${inspection.referencedContractsVersion || '未知'}</div><div>当前宿主 Contract：${inspection.hostContractsVersion || '未知'}</div><div class="mt-1 ${inspection.contractVersionMatches ? 'text-success' : 'text-warning'}">${inspection.compatibilityMessage || ''}</div>`;
                body.appendChild(contractInfo);
            }

            const pluginNames = loadedPlugins.length
                ? loadedPlugins.map(plugin => plugin.name || plugin.id)
                : inspectionPlugins.map(plugin => plugin.name || plugin.typeName || plugin.id);
            if (pluginNames.length) {
                const names = document.createElement('div');
                names.className = 'small mt-3';
                names.textContent = `发现插件：${pluginNames.join('，')}`;
                body.appendChild(names);
            }

            if (errors.length) {
                const errorList = document.createElement('ul');
                errorList.className = 'small text-danger mt-3 mb-0 ps-3';
                errors.forEach(error => {
                    const li = document.createElement('li');
                    li.textContent = error;
                    errorList.appendChild(li);
                });
                body.appendChild(errorList);
            }

            card.appendChild(body);
            resultList.appendChild(card);
        });
    }

    async function openPluginDirectory() {
        try {
            const response = await fetch(resolveApi('pluginsRoot', '/api/plugins/root'), { cache: 'no-store' });
            const payload = await response.json().catch(() => ({}));
            if (!response.ok) {
                throw new Error(payload?.error || '读取插件目录失败');
            }

            const target = payload.pluginDirectory || payload.defaultPluginDirectory;
            if (!target) {
                throw new Error('插件目录为空');
            }

            const result = await window.meow?.openPath?.(target);
            if (!result?.ok) {
                throw new Error(result?.message || '打开插件目录失败');
            }

            window.showNotification?.('插件目录已打开。', 'success');
        } catch (error) {
            window.showNotification?.(error.message || '打开插件目录失败。', 'danger');
        }
    }

    async function uploadSelectedFiles() {
        if (!selectedFiles.length) {
            window.showNotification?.('请先选择要上传的文件或文件夹。', 'warning');
            return;
        }

        const formData = new FormData();
        selectedFiles.forEach(file => {
            const relativePath = file._meowRelativePath || file.webkitRelativePath || file.name;
            formData.append('files', file, relativePath);
        });

        setUploadingState(true);
        try {
            const response = await fetch(resolveApi('pluginsUpload', '/api/plugins/upload'), {
                method: 'POST',
                body: formData
            });
            const payload = await response.json().catch(() => ({}));
            if (!response.ok) {
                throw new Error(payload?.detail || payload?.error || '上传失败');
            }

            renderScanResults(Array.isArray(payload.processed) ? payload.processed : []);
            uploadModal.hide();
            scanResultModal.show();
            clearSelection();
            await loadInstalledAssemblies();
            await window.BrowserUI.loadPlugins?.();
        } catch (error) {
            window.showNotification?.(error.message || '上传失败。', 'danger');
        } finally {
            setUploadingState(false);
        }
    }

    function renderAssemblyItem(item) {
        const row = document.createElement('div');
        row.className = 'list-group-item plugin-assembly-item d-flex justify-content-between align-items-start gap-3 flex-wrap';
        const statusLabel = !item.contractVersionMatches
            ? 'Contract 不匹配'
            : item.enabled && item.loaded
                ? '已启用'
                : item.enabled
                    ? '未加载'
                    : '已关闭';
        const statusClass = !item.contractVersionMatches
            ? 'badge text-bg-warning'
            : item.enabled && item.loaded
                ? 'badge text-bg-success'
                : 'badge text-bg-secondary';

        const meta = document.createElement('div');
        meta.className = 'flex-grow-1 min-w-0';

        const titleRow = document.createElement('div');
        titleRow.className = 'd-flex align-items-center gap-2 flex-wrap';
        const title = document.createElement('div');
        title.className = 'fw-semibold';
        title.textContent = item.fileName || '未知程序集';
        titleRow.appendChild(title);
        titleRow.appendChild(createBadge(statusLabel, statusClass));
        meta.appendChild(titleRow);

        const pluginNames = Array.isArray(item.plugins) ? item.plugins.map(plugin => plugin.name || plugin.typeName || plugin.id).filter(Boolean) : [];
        const detail = document.createElement('div');
        detail.className = 'small text-muted mt-1';
        detail.textContent = pluginNames.length ? pluginNames.join('，') : '未解析出插件名称';
        meta.appendChild(detail);

        const path = document.createElement('div');
        path.className = 'small text-muted text-break mt-1';
        path.textContent = item.assemblyPath || '';
        meta.appendChild(path);

        if (item.compatibilityMessage) {
            const contract = document.createElement('div');
            contract.className = `small mt-2 ${item.contractVersionMatches ? 'text-muted' : 'text-warning'}`;
            contract.textContent = item.compatibilityMessage;
            meta.appendChild(contract);
        }

        if (Array.isArray(item.errors) && item.errors.length) {
            const errorList = document.createElement('ul');
            errorList.className = 'small text-danger mb-0 mt-2 ps-3';
            item.errors.forEach(error => {
                const li = document.createElement('li');
                li.textContent = error;
                errorList.appendChild(li);
            });
            meta.appendChild(errorList);
        }

        const toggleWrap = document.createElement('div');
        toggleWrap.className = 'form-check form-switch plugin-assembly-switch';
        const toggle = document.createElement('input');
        toggle.className = 'form-check-input';
        toggle.type = 'checkbox';
        toggle.role = 'switch';
        toggle.checked = !!item.enabled;
        toggle.disabled = !item.contractVersionMatches;
        const toggleLabel = document.createElement('label');
        toggleLabel.className = 'form-check-label small text-muted';
        toggleLabel.textContent = item.contractVersionMatches ? '启用' : '不可启用';
        toggleWrap.appendChild(toggle);
        toggleWrap.appendChild(toggleLabel);

        toggle.addEventListener('change', async () => {
            const desiredState = toggle.checked;
            toggle.disabled = true;
            try {
                const response = await fetch(resolveApi('pluginsAssemblyState', '/api/plugins/assembly-state'), {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ assemblyPath: item.assemblyPath, enabled: desiredState })
                });
                const payload = await response.json().catch(() => ({}));
                if (!response.ok) {
                    throw new Error(payload?.error || '切换插件状态失败');
                }

                await loadInstalledAssemblies();
                await window.BrowserUI.loadPlugins?.();
            } catch (error) {
                toggle.checked = !desiredState;
                window.showNotification?.(error.message || '切换插件状态失败。', 'danger');
            } finally {
                toggle.disabled = !item.contractVersionMatches;
            }
        });

        row.appendChild(meta);
        row.appendChild(toggleWrap);
        return row;
    }

    async function loadInstalledAssemblies() {
        installedList.replaceChildren();
        installedPlaceholder.style.display = '';

        try {
            const response = await fetch(resolveApi('pluginsInstalled', '/api/plugins/installed'), { cache: 'no-store' });
            const payload = await response.json().catch(() => ({}));
            if (!response.ok) {
                throw new Error(payload?.error || '加载已安装插件失败');
            }

            const assemblies = Array.isArray(payload.assemblies) ? payload.assemblies : [];
            if (!assemblies.length) {
                installedPlaceholder.textContent = '未发现已安装插件。';
                return;
            }

            installedPlaceholder.style.display = 'none';
            assemblies.forEach(item => {
                installedList.appendChild(renderAssemblyItem(item));
            });
        } catch (error) {
            installedPlaceholder.textContent = error.message || '加载已安装插件失败。';
        }
    }

    openUploadModalBtn.addEventListener('click', () => {
        clearSelection();
        uploadModal.show();
    });
    openPluginDirectoryBtn.addEventListener('click', openPluginDirectory);
    selectFilesBtn.addEventListener('click', () => fileInput.click());
    selectDirectoryBtn.addEventListener('click', () => directoryInput.click());
    clearSelectionBtn.addEventListener('click', clearSelection);
    uploadSubmitBtn.addEventListener('click', uploadSelectedFiles);

    fileInput.addEventListener('change', () => {
        appendFiles(Array.from(fileInput.files || []));
        fileInput.value = '';
    });
    directoryInput.addEventListener('change', () => {
        const files = Array.from(directoryInput.files || []);
        files.forEach(file => {
            if (file.webkitRelativePath) {
                try {
                    Object.defineProperty(file, '_meowRelativePath', {
                        value: file.webkitRelativePath,
                        configurable: true
                    });
                } catch {
                }
            }
        });
        appendFiles(files);
        directoryInput.value = '';
    });

    ['dragenter', 'dragover'].forEach(eventName => {
        dropZone.addEventListener(eventName, event => {
            event.preventDefault();
            event.stopPropagation();
            dropZone.classList.add('plugin-upload-dropzone-active');
        });
    });
    ['dragleave', 'dragend', 'drop'].forEach(eventName => {
        dropZone.addEventListener(eventName, event => {
            event.preventDefault();
            event.stopPropagation();
            dropZone.classList.remove('plugin-upload-dropzone-active');
        });
    });
    dropZone.addEventListener('drop', async event => {
        const files = await extractFilesFromDataTransfer(event.dataTransfer);
        appendFiles(files);
    });

    window.addEventListener('meow:pagechange', event => {
        if (event.detail?.page === 'plugin-upload') {
            loadInstalledAssemblies().catch(() => { });
        }
    });

    updateSelectedInfo();
    loadInstalledAssemblies().catch(() => { });
})();
