(function () {
    const input = document.getElementById('pluginUploadInput');
    const btn = document.getElementById('pluginUploadBtn');
    const clearBtn = document.getElementById('pluginClearBtn');
    const selectedInfo = document.getElementById('selectedFilesInfo');
    const resultList = document.getElementById('resultList');
    const placeholder = document.getElementById('resultPlaceholder');
    const spinner = document.getElementById('uploadSpinner');
    const installedList = document.getElementById('installedList');
    const installedPlaceholder = document.getElementById('installedPlaceholder');

    if (!input || !btn || !clearBtn || !selectedInfo || !resultList || !placeholder || !spinner || !openDirBtn) {
        return;
    }

    function resolveApi(key, fallback) {
        return (window.__apiEndpoints && window.__apiEndpoints[key]) || fallback;
    }

    function updateSelectedInfo() {
        if (!input || !selectedInfo) return;
        if (!input.files || input.files.length === 0) { selectedInfo.textContent = '尚未选择文件。'; return; }
        selectedInfo.textContent = `已选择 ${input.files.length} 个文件（示例：${input.files[0].name}）`;
    }

    input.addEventListener('change', updateSelectedInfo);
    updateSelectedInfo();

    clearBtn.addEventListener('click', () => { input.value = null; updateSelectedInfo(); resultList.replaceChildren(); placeholder.style.display = ''; });

    btn.addEventListener('click', async () => {
        if (!input.files || input.files.length === 0) { window.showNotification?.('请选择要上传的文件或目录', 'warning'); return; }

        const fd = new FormData();
        for (const f of input.files) fd.append('files', f);

        // show spinner
        if (spinner) spinner.style.display = '';
        if (btn) btn.disabled = true;
        if (clearBtn) clearBtn.disabled = true;
        if (placeholder) placeholder.style.display = 'none';
        resultList?.replaceChildren();

        try {
            const res = await fetch(resolveApi('pluginsUpload', '/api/plugins/upload'), { method: 'POST', body: fd });
            const json = await res.json();
            if (!res.ok) {
                const err = document.createElement('div'); err.className = 'text-danger'; err.textContent = json.detail || json.error || '上传失败'; resultList.appendChild(err); return;
            }

            const processed = json.processed || [];
            if (processed.length === 0) {
                const info = document.createElement('div'); info.className = 'text-muted'; info.textContent = '未发现 DLL 文件或没有可注册插件。'; resultList.appendChild(info); return;
            }

            processed.forEach((p, idx) => {
                const id = 'res' + idx;
                const card = document.createElement('div'); card.className = 'accordion-item';
                const head = document.createElement('h2'); head.className = 'accordion-header'; head.id = 'h' + id;
                const btn = document.createElement('button'); btn.className = 'accordion-button collapsed'; btn.type = 'button'; btn.setAttribute('data-bs-toggle', 'collapse'); btn.setAttribute('data-bs-target', '#' + id); btn.setAttribute('aria-expanded', 'false'); btn.setAttribute('aria-controls', id);
                btn.textContent = `${p.path} (${p.plugins?.length ?? 0} plugin(s))`;
                head.appendChild(btn);

                const bodyWrap = document.createElement('div'); bodyWrap.id = id; bodyWrap.className = 'accordion-collapse collapse';
                const body = document.createElement('div'); body.className = 'accordion-body';
                const pre = document.createElement('pre'); pre.textContent = JSON.stringify(p, null, 2); body.appendChild(pre); bodyWrap.appendChild(body);

                card.appendChild(head); card.appendChild(bodyWrap); resultList.appendChild(card);
            });

            await window.BrowserUI.loadPlugins?.();
            await loadInstalledPlugins();
        }
        catch (e) {
            const err = document.createElement('div'); err.className = 'text-danger'; err.textContent = e.message || String(e); resultList.appendChild(err);
        }
        finally {
            if (spinner) spinner.style.display = 'none';
            if (btn) btn.disabled = false;
            if (clearBtn) clearBtn.disabled = false;
        }
    });

    // "打开插件目录" 按钮已移除（设置页提供该功能），因此这里不再绑定打开目录的处理器。

    async function loadInstalledPlugins() {
        if (!installedList || !installedPlaceholder) {
            return;
        }

        installedList.replaceChildren();
        installedPlaceholder.style.display = '';

        try {
            const res = await fetch(resolveApi('plugins', '/api/plugins'));
            const json = await res.json();
            const plugins = (json && json.plugins) || [];
            if (!plugins.length) {
                installedPlaceholder.textContent = '未发现已安装插件。';
                return;
            }

            installedPlaceholder.style.display = 'none';
            for (const plugin of plugins) {
                const item = document.createElement('div');
                item.className = 'list-group-item d-flex justify-content-between align-items-start gap-3';

                const meta = document.createElement('div');
                meta.innerHTML = '<div class="fw-semibold"></div><div class="small text-muted"></div>';
                meta.querySelector('.fw-semibold').textContent = plugin.name || plugin.id;
                meta.querySelector('.small').textContent = plugin.id || '';

                const actions = document.createElement('div');
                actions.className = 'd-flex gap-2 flex-wrap';

                const unloadBtn = document.createElement('button');
                unloadBtn.type = 'button';
                unloadBtn.className = 'btn btn-sm btn-outline-danger';
                unloadBtn.textContent = '卸载';
                unloadBtn.addEventListener('click', async () => {
                    try {
                        unloadBtn.disabled = true;
                        const response = await fetch(resolveApi('pluginsUnload', '/api/plugins/unload'), {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify({ pluginId: plugin.id })
                        });
                        if (!response.ok) throw new Error('卸载失败: ' + response.status);
                        await loadInstalledPlugins();
                        await window.BrowserUI.loadPlugins?.();
                    } catch (error) {
                        window.showNotification?.(error.message || '卸载失败。', 'danger');
                    } finally {
                        unloadBtn.disabled = false;
                    }
                });
                actions.appendChild(unloadBtn);

                const deleteBtn = document.createElement('button');
                deleteBtn.type = 'button';
                deleteBtn.className = 'btn btn-sm btn-outline-secondary';
                deleteBtn.textContent = '删除文件';
                deleteBtn.addEventListener('click', async () => {
                    const modalElement = document.getElementById('confirmModal');
                    const okButton = document.getElementById('confirmModalOk');
                    const body = document.getElementById('confirmModalBody');
                    if (!modalElement || !okButton || !body) {
                        return;
                    }

                    body.textContent = '确认删除插件 ' + (plugin.name || plugin.id) + ' 的文件吗？此操作不可撤销。';
                    const modal = new bootstrap.Modal(modalElement);
                    const handler = async () => {
                        try {
                            okButton.disabled = true;
                            const response = await fetch(resolveApi('pluginsDelete', '/api/plugins/delete'), {
                                method: 'POST',
                                headers: { 'Content-Type': 'application/json' },
                                body: JSON.stringify({ pluginId: plugin.id })
                            });
                            if (!response.ok) throw new Error('删除失败: ' + response.status);
                            modal.hide();
                            await loadInstalledPlugins();
                            await window.BrowserUI.loadPlugins?.();
                        } catch (error) {
                            window.showNotification?.(error.message || '删除失败。', 'danger');
                        } finally {
                            okButton.disabled = false;
                            okButton.removeEventListener('click', handler);
                        }
                    };

                    okButton.addEventListener('click', handler);
                    modal.show();
                });
                actions.appendChild(deleteBtn);

                item.appendChild(meta);
                item.appendChild(actions);
                installedList.appendChild(item);
            }
        } catch (error) {
            installedPlaceholder.textContent = error.message || '加载已安装插件失败。';
        }
    }

    loadInstalledPlugins().catch(() => { });

})();
