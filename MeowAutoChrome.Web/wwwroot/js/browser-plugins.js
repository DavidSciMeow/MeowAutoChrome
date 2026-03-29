(function(){
    // plugin rendering and output handling moved from Index.cshtml
    const pluginCatalog = window.BrowserUI && window.BrowserUI.pluginCatalog ? window.BrowserUI.pluginCatalog : new Map();
    const pluginOutputStore = window.BrowserUI && window.BrowserUI.pluginOutputStore ? window.BrowserUI.pluginOutputStore : new Map();
    const pluginOutputUi = window.BrowserUI && window.BrowserUI.pluginOutputUi ? window.BrowserUI.pluginOutputUi : new Map();
    const pluginHost = document.getElementById('pluginHost');
    const pluginArgumentModalElement = document.getElementById('pluginArgumentModal');
    const pluginArgumentModal = pluginArgumentModalElement ? new bootstrap.Modal(pluginArgumentModalElement) : null;
    const pluginArgumentForm = document.getElementById('pluginArgumentForm');
    const pluginArgumentModalTitle = document.getElementById('pluginArgumentModalTitle');
    const pluginArgumentModalDescription = document.getElementById('pluginArgumentModalDescription');
    const pluginArgumentSubmitBtn = document.getElementById('pluginArgumentSubmitBtn');
    const pluginOutputModalElement = document.getElementById('pluginOutputModal');
    const pluginOutputModal = pluginOutputModalElement ? new bootstrap.Modal(pluginOutputModalElement) : null;
    const pluginOutputModalTitle = document.getElementById('pluginOutputModalTitle');
    const pluginOutputModalSummary = document.getElementById('pluginOutputModalSummary');
    const pluginOutputModalData = document.getElementById('pluginOutputModalData');
    const pluginOutputModalEntries = document.getElementById('pluginOutputModalEntries');
    let pendingPluginInvocation = null;
    let activePluginOutputPluginId = null;

    function showError(msg){
        if (window.BrowserIndex && window.BrowserIndex.showError) return window.BrowserIndex.showError(msg);
        console.warn(msg);
    }

    function formatPluginOutputTime(ts){ return ts ? new Date(ts).toLocaleString() : ''; }
    function getPluginTarget(plugin, targetId){ if (!plugin || !targetId) return null; return (plugin.controls||[]).find(c=>c.command===targetId) || (plugin.functions||[]).find(f=>f.id===targetId) || null; }

    function summarizePluginOutput(message, data) {
        if (message) return message;
        const first = Object.entries(data||{})[0];
        return first ? (first[0] + ': ' + (first[1]||'')) : '暂无返回值';
    }

    function renderPluginOutputRows(container, data, emptyText){
        container.replaceChildren();
        const entries = Object.entries(data||{});
        const filtered = entries.filter(([k]) => String(k||'').toLowerCase() !== 'state');
        if (!filtered.length){ const empty = document.createElement('div'); empty.className='plugin-output-empty'; empty.textContent=emptyText; container.appendChild(empty); return; }
        for (const [k,v] of filtered){ const row = document.createElement('div'); row.className='plugin-output-row'; const label=document.createElement('div'); label.className='plugin-output-label'; label.textContent=k; const content=document.createElement('div'); content.className='plugin-output-value'; content.textContent=v||''; row.appendChild(label); row.appendChild(content); container.appendChild(row);}    }

    function syncPluginOutputUi(pluginId){
        const ui = pluginOutputUi.get(pluginId); if (!ui) return; const output = pluginOutputStore.get(pluginId); ui.button.disabled = !output; if (!output) { ui.badge.classList.add('d-none'); return; } const unread = Math.min(Number(output.unreadCount ?? (output.entries||[]).length) || 0, 99); ui.badge.textContent = unread>0?String(unread):''; ui.badge.classList.toggle('d-none', unread===0);
    }

    function renderPluginOutputModal(pluginId){
        const output = pluginOutputStore.get(pluginId); if (!output) return; const plugin = pluginCatalog.get(pluginId); const target = getPluginTarget(plugin, output.targetId);
        pluginOutputModalTitle.textContent = plugin?.name ? plugin.name + (target?.name ? ' · ' + target.name : '') : '插件输出';
        pluginOutputModalSummary.textContent = [formatPluginOutputTime(output.updatedAt), summarizePluginOutput(output.message, output.data)].filter(Boolean).join(' · ');
        pluginOutputModalSummary.classList.toggle('d-none', !pluginOutputModalSummary.textContent);
        renderPluginOutputRows(pluginOutputModalData, output.data, '当前还没有结构化返回值。');
        pluginOutputModalEntries.replaceChildren();
        if (!(output.entries||[]).length){ const empty = document.createElement('div'); empty.className='plugin-output-empty'; empty.textContent='还没有输出记录。'; pluginOutputModalEntries.appendChild(empty); return; }
        for (const entry of output.entries){ const item=document.createElement('div'); item.className='plugin-output-entry'; const header=document.createElement('div'); header.className='d-flex justify-content-between align-items-start gap-2 mb-2'; const name=document.createElement('div'); name.className='fw-semibold small'; name.textContent = getPluginTarget(plugin, entry.targetId)?.name || entry.targetId || '插件输出'; const time=document.createElement('div'); time.className='text-muted small'; time.textContent = formatPluginOutputTime(entry.timestampUtc); header.appendChild(name); header.appendChild(time); item.appendChild(header); if (entry.message){ const message=document.createElement('div'); message.className='plugin-output-value mb-2'; message.textContent = entry.message; item.appendChild(message);} const dataContainer=document.createElement('div'); renderPluginOutputRows(dataContainer, entry.data, '本次输出没有附加字段。'); item.appendChild(dataContainer); pluginOutputModalEntries.appendChild(item);}    }

    function openPluginOutputModal(pluginId){ const output = pluginOutputStore.get(pluginId); if (!output || !pluginOutputModal) return; activePluginOutputPluginId = pluginId; try { const updated = { ...(output||{}) }; updated.unreadCount = 0; pluginOutputStore.set(pluginId, updated); } catch {} renderPluginOutputModal(pluginId); syncPluginOutputUi(pluginId); pluginOutputModal.show(); }

    function applyPluginOutputUpdate(update){ if (!update?.pluginId) return; const existing = pluginOutputStore.get(update.pluginId); const mergedData = { ...(existing?.data||{}), ...(update.data||{}) }; const isCurrentModal = activePluginOutputPluginId === update.pluginId && pluginOutputModalElement.classList.contains('show'); const next = { pluginId: update.pluginId, targetId: update.targetId || existing?.targetId || '', message: update.message ?? existing?.message ?? '', data: mergedData, updatedAt: update.timestampUtc || new Date().toISOString(), entries: existing?.entries ? [...existing.entries] : [], targetStates: existing?.targetStates ? { ...existing.targetStates } : {}, unreadCount: Number(existing?.unreadCount || 0) }; if (update.message || Object.keys(update.data || {}).length){ next.entries.unshift({ targetId: next.targetId, message: update.message || '', data: update.data || {}, timestampUtc: next.updatedAt }); next.entries = next.entries.slice(0,30); if (!isCurrentModal) next.unreadCount = (Number(existing?.unreadCount || 0) || 0) + 1; else next.unreadCount = 0; } if (update.state) next.targetStates[next.targetId] = update.state; pluginOutputStore.set(update.pluginId, next); syncPluginOutputUi(update.pluginId); if (isCurrentModal) renderPluginOutputModal(update.pluginId); }

    function createPluginParameterField(parameter, parameterInputs){ const field=document.createElement('div'); const label=document.createElement('label'); label.className='form-label mb-1'; label.textContent = parameter.label + (parameter.required ? ' *' : ''); field.appendChild(label); let input; if (parameter.inputType==='checkbox'){ input=document.createElement('input'); input.type='checkbox'; input.className='form-check-input'; input.checked = String(parameter.defaultValue||'false').toLowerCase() === 'true'; const wrapper=document.createElement('div'); wrapper.className='form-check'; wrapper.appendChild(input); field.appendChild(wrapper);} else if (parameter.inputType==='select'){ input=document.createElement('select'); input.className='form-select form-select-sm'; if (!parameter.required){ const emptyOption=document.createElement('option'); emptyOption.value=''; emptyOption.textContent='请选择'; input.appendChild(emptyOption);} for (const option of parameter.options||[]){ const element=document.createElement('option'); element.value=option.value; element.textContent=option.label; if ((parameter.defaultValue??'')===option.value) element.selected=true; input.appendChild(element);} field.appendChild(input);} else { input=document.createElement('input'); input.type = parameter.inputType==='number' ? 'number' : parameter.inputType==='datetime-local' ? 'datetime-local' : 'text'; input.className='form-control form-control-sm'; input.value = parameter.defaultValue ?? ''; input.placeholder = parameter.description || parameter.label; if (parameter.inputType==='number') input.step='any'; if (parameter.inputType==='datetime-local') input.step='1'; if (parameter.inputType==='guid'){ input.placeholder = parameter.description || '00000000-0000-0000-0000-000000000000'; input.pattern='^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'; input.spellcheck=false; input.autocomplete='off'; } field.appendChild(input);} if (parameter.description){ const help=document.createElement('div'); help.className='form-text mt-1'; help.textContent=parameter.description; field.appendChild(help);} parameterInputs[parameter.name] = { input, parameter }; return field; }

    function collectPluginArguments(parameterInputs){ const argumentsPayload={}; for (const [name, entry] of Object.entries(parameterInputs || {})){ if (typeof entry.input.checkValidity === 'function' && !entry.input.checkValidity()){ entry.input.reportValidity(); entry.input.focus(); return null; } if (entry.parameter.inputType === 'checkbox'){ argumentsPayload[name] = entry.input.checked ? 'true' : 'false'; continue; } const value = (entry.input.value || '').trim(); if (entry.parameter.required && !value){ showError('请填写参数：' + entry.parameter.label); entry.input.focus(); return null; } if (value) argumentsPayload[name] = value; } return argumentsPayload; }

    function openPluginArgumentModal(title, description, parameters, submitText, onSubmit){ if (!pluginArgumentModal) return; const parameterInputs={}; pluginArgumentModalTitle.textContent = title || '插件参数'; pluginArgumentModalDescription.textContent = description || ''; pluginArgumentModalDescription.classList.toggle('d-none', !description); pluginArgumentForm.replaceChildren(); for (const parameter of parameters || []) pluginArgumentForm.appendChild(createPluginParameterField(parameter, parameterInputs)); pluginArgumentSubmitBtn.textContent = submitText || '执行'; pendingPluginInvocation = { parameterInputs, onSubmit }; pluginArgumentModal.show(); setTimeout(()=>{ const firstInput = pluginArgumentForm.querySelector('input, select, textarea'); if (firstInput) firstInput.focus(); },150); }

    // bind submit handler
    pluginArgumentSubmitBtn?.addEventListener('click', async ()=>{
        if (!pendingPluginInvocation) return; const argumentsPayload = collectPluginArguments(pendingPluginInvocation.parameterInputs); if (argumentsPayload === null) return; pluginArgumentSubmitBtn.disabled = true; try { await pendingPluginInvocation.onSubmit(argumentsPayload); pluginArgumentModal.hide(); } catch(e){ showError(e.message || '插件执行失败。'); } finally { pluginArgumentSubmitBtn.disabled = false; }
    });

    // expose plugin API
    window.BrowserPlugins = { renderPluginOutputModal, openPluginArgumentModal, applyPluginOutputUpdate, syncPluginOutputUi, pluginCatalog, pluginOutputStore, pluginOutputUi };

    // listen for plugin output from hub
    if (window.signalR && window.signalR.HubConnectionBuilder) {
        // attach handler after hub is available in global scope (client-side hub setup in browser-index.js)
        const orig = window.signalR.HubConnectionBuilder.prototype.withUrl;
        // best-effort: we assume hub connection will call onReceive and BrowserUI will register the ReceivePluginOutput handler already
    }

})();
