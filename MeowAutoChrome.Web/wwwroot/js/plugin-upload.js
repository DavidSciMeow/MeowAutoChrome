(function () {
    const input = document.getElementById('pluginUploadInput');
    const btn = document.getElementById('pluginUploadBtn');
    const clearBtn = document.getElementById('pluginClearBtn');
    const selectedInfo = document.getElementById('selectedFilesInfo');
    const resultList = document.getElementById('resultList');
    const placeholder = document.getElementById('resultPlaceholder');
    const spinner = document.getElementById('uploadSpinner');

    function updateSelectedInfo() {
        if (!input.files || input.files.length === 0) { selectedInfo.textContent = '尚未选择文件。'; return; }
        selectedInfo.textContent = `已选择 ${input.files.length} 个文件（示例：${input.files[0].name}）`;
    }

    input.addEventListener('change', updateSelectedInfo);
    updateSelectedInfo();

    clearBtn.addEventListener('click', () => { input.value = null; updateSelectedInfo(); resultList.replaceChildren(); placeholder.style.display = ''; });

    btn.addEventListener('click', async () => {
        if (!input.files || input.files.length === 0) { alert('请选择要上传的文件或目录'); return; }

        const fd = new FormData();
        for (const f of input.files) fd.append('files', f);

        // show spinner
        spinner.style.display = '';
        btn.disabled = true;
        clearBtn.disabled = true;
        placeholder.style.display = 'none';
        resultList.replaceChildren();

        try {
            const res = await fetch('/api/plugins/upload', { method: 'POST', body: fd });
            const json = await res.json();
            if (!res.ok) {
                const err = document.createElement('div');
                err.className = 'text-danger';
                err.textContent = json.detail || '上传失败';
                resultList.appendChild(err);
                return;
            }

            // render each processed DLL as accordion item
            const processed = json.processed || [];
            if (processed.length === 0) {
                const info = document.createElement('div');
                info.className = 'text-muted';
                info.textContent = '未发现 DLL 文件或没有可注册插件。';
                resultList.appendChild(info);
                return;
            }

            processed.forEach((p, idx) => {
                const id = 'res' + idx;
                const card = document.createElement('div');
                card.className = 'accordion-item';
                const head = document.createElement('h2');
                head.className = 'accordion-header';
                head.id = 'h' + id;
                const btn = document.createElement('button');
                btn.className = 'accordion-button collapsed';
                btn.type = 'button';
                btn.setAttribute('data-bs-toggle', 'collapse');
                btn.setAttribute('data-bs-target', '#' + id);
                btn.setAttribute('aria-expanded', 'false');
                btn.setAttribute('aria-controls', id);
                btn.textContent = `${p.path} (${p.plugins?.length ?? 0} plugin(s))`;
                head.appendChild(btn);

                const bodyWrap = document.createElement('div');
                bodyWrap.id = id;
                bodyWrap.className = 'accordion-collapse collapse';
                const body = document.createElement('div');
                body.className = 'accordion-body';
                const pre = document.createElement('pre');
                pre.textContent = JSON.stringify(p, null, 2);
                body.appendChild(pre);
                bodyWrap.appendChild(body);

                card.appendChild(head);
                card.appendChild(bodyWrap);
                resultList.appendChild(card);
            });

            // refresh installed plugins list
            await loadInstalledPlugins();
        }
        catch (e) {
            const err = document.createElement('div');
            err.className = 'text-danger';
            err.textContent = e.message || String(e);
            resultList.appendChild(err);
        }
        finally {
            spinner.style.display = 'none';
            btn.disabled = false;
            clearBtn.disabled = false;
        }
    });

    async function loadInstalledPlugins() {
        const list = document.getElementById('installedList');
        const placeholder = document.getElementById('installedPlaceholder');
        list.replaceChildren();
        placeholder.style.display = '';
        try {
            const res = await fetch('/api/plugins');
            const json = await res.json();
            // json contains catalog with Plugins array
            const plugins = (json && json.plugins) || [];
            if (!plugins.length) {
                placeholder.textContent = '未发现已安装插件。';
                return;
            }
            placeholder.style.display = 'none';
            for (const p of plugins) {
                const item = document.createElement('div');
                item.className = 'list-group-item d-flex justify-content-between align-items-start';
                const left = document.createElement('div');
                left.innerHTML = `<div class="fw-semibold">${p.name}</div><div class="small text-muted">${p.id}${p.path ? ' — ' + p.path : ''}</div>`;
                const right = document.createElement('div');
                // reload (load assembly again)
                const reloadBtn = document.createElement('button');
                reloadBtn.className = 'btn btn-sm btn-outline-primary me-2';
                reloadBtn.textContent = '重新加载';
                reloadBtn.title = p.path ? '从磁盘重新加载此插件' : '未记录源路径，无法重新加载';
                reloadBtn.disabled = !p.path;
                reloadBtn.addEventListener('click', async () => {
                    if (!p.path) return;
                    if (!confirm('确认重新加载插件 ' + p.name + ' (' + p.id + ')?')) return;
                    reloadBtn.disabled = true;
                    try {
                        const res = await fetch('/api/plugins/load', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify({ path: p.path }) });
                        if (!res.ok) { alert('重新加载失败'); return; }
                        await loadInstalledPlugins();
                    } catch (e) { alert('重新加载异常：' + e.message); }
                    finally { reloadBtn.disabled = false; }
                });

                // unload
                const unloadBtn = document.createElement('button');
                unloadBtn.className = 'btn btn-sm btn-outline-danger me-2';
                unloadBtn.textContent = '卸载';
                unloadBtn.addEventListener('click', async () => {
                    if (!confirm('确认卸载插件 ' + p.name + ' (' + p.id + ')?')) return;
                    unloadBtn.disabled = true;
                    const ures = await fetch('/api/plugins/unload', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify({ pluginId: p.id }) });
                    if (!ures.ok) {
                        alert('卸载失败');
                        unloadBtn.disabled = false;
                        return;
                    }
                    await loadInstalledPlugins();
                });

                // delete files (permanent)
                const delBtn = document.createElement('button');
                delBtn.className = 'btn btn-sm btn-outline-danger';
                delBtn.textContent = '删除文件';
                delBtn.title = '删除插件的文件（若来自上传文件夹则删除整个上传组）';
                delBtn.addEventListener('click', async () => {
                    // show confirm modal
                    const modalBody = document.getElementById('confirmModalBody');
                    const modalTitle = document.getElementById('confirmModalTitle');
                    modalTitle.textContent = '删除插件文件';
                    modalBody.textContent = `确认要删除插件 ${p.name} (${p.id}) 的文件吗？此操作不可撤销。`;
                    const confirmBtn = document.getElementById('confirmModalOk');
                    confirmBtn.disabled = false;
                    const handler = async () => {
                        confirmBtn.disabled = true;
                        try {
                            const resp = await fetch('/api/plugins/delete', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify({ pluginId: p.id }) });
                            if (!resp.ok) { alert('删除失败'); }
                            else { await loadInstalledPlugins(); }
                        } catch (e) { alert('删除异常：' + e.message); }
                        finally {
                            // hide modal
                            const bs = bootstrap.Modal.getInstance(document.getElementById('confirmModal'));
                            if (bs) bs.hide();
                            confirmBtn.removeEventListener('click', handler);
                        }
                    };
                    confirmBtn.addEventListener('click', handler);
                    const modal = new bootstrap.Modal(document.getElementById('confirmModal'));
                    modal.show();
                });

                right.appendChild(reloadBtn);
                right.appendChild(unloadBtn);
                right.appendChild(delBtn);
                item.appendChild(left);
                item.appendChild(right);
                list.appendChild(item);
            }
        }
        catch (e) {
            placeholder.textContent = '加载已安装插件失败。';
        }
    }

    // initial load
    loadInstalledPlugins().catch(()=>{});
})();
