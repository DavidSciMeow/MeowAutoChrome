(function(){
    const tabsBar = document.getElementById('tabsBar');
    const tabsShell = document.getElementById('tabsShell');
    const tabsScrollLeftBtn = document.getElementById('tabsScrollLeftBtn');
    const tabsScrollRightBtn = document.getElementById('tabsScrollRightBtn');
    const tabsStatusPanel = document.getElementById('tabsStatusPanel');
    const urlInput = document.getElementById('urlInput');

    const postJson = (window.BrowserIndex && window.BrowserIndex.postJson) || (async (u,b)=>{ const resolved = (window.__apiEndpoints && window.__apiEndpoints[u]) || u; const r=await fetch(resolved,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(b||{})}); if(!r.ok) throw new Error('Request failed: '+r.status); return await r.json(); });

    function updateTabScrollButtons(){
        if (!tabsBar) return;
        const maxScrollLeft = Math.max(0, tabsBar.scrollWidth - tabsBar.clientWidth);
        if (tabsScrollLeftBtn) tabsScrollLeftBtn.disabled = tabsBar.scrollLeft <= 0;
        if (tabsScrollRightBtn) tabsScrollRightBtn.disabled = tabsBar.scrollLeft >= maxScrollLeft - 1;
    }

    function groupTabsByInstance(tabs){
        const groups = [];
        const groupMap = new Map();
        for (const tab of tabs){
            if (!groupMap.has(tab.instanceId)){
                const group = { instanceId: tab.instanceId, instanceName: tab.instanceName || tab.instanceId, instanceColor: tab.instanceColor || '#2563eb', isSelected: !!tab.isInSelectedInstance, tabs: [] };
                groupMap.set(tab.instanceId, group);
                groups.push(group);
            }
            const group = groupMap.get(tab.instanceId);
            group.isSelected = group.isSelected || !!tab.isInSelectedInstance;
            group.tabs.push(tab);
        }
        return groups;
    }

    function renderTabs(tabs){
        if (!tabsBar) return updateTabScrollButtons();
        tabsBar.innerHTML = '';
        if (!tabs || tabs.length === 0){
            const empty = document.createElement('div'); empty.className='small text-muted'; empty.textContent='暂无 TAB'; tabsBar.appendChild(empty); updateTabScrollButtons(); return;
        }

        for (const group of groupTabsByInstance(tabs)){
            const groupContainer = document.createElement('div');
            groupContainer.className = 'browser-tab-instance-group' + (group.isSelected ? ' browser-tab-instance-group-active' : '');
            groupContainer.style.setProperty('--instance-color', group.instanceColor);

            const groupHeader = document.createElement('div');
            groupHeader.className = 'browser-tab-instance-header';

            const groupLabel = document.createElement('button');
            groupLabel.type='button'; groupLabel.className = 'browser-tab-instance-label' + (group.isSelected ? ' browser-tab-instance-label-active' : '');
            groupLabel.title = '切换到实例：' + group.instanceName; groupLabel.textContent = group.instanceName;
            groupLabel.addEventListener('click', async () => {
                const targetTab = group.tabs.find(t=>t.isSelected) || group.tabs[0]; if (!targetTab) return; try { await window.BrowserUI.selectTab(targetTab.id); } catch(e){ console.warn(e); }
            });
            groupHeader.appendChild(groupLabel);

            const groupSettingsBtn = document.createElement('button'); groupSettingsBtn.type='button'; groupSettingsBtn.className='browser-tab-instance-close btn btn-sm btn-light border-0'; groupSettingsBtn.title='实例设置'; groupSettingsBtn.innerHTML = '<i class="fa-solid fa-sliders"></i>';
            groupSettingsBtn.addEventListener('click', async e => { e.stopPropagation(); await window.BrowserUI.openInstanceSettings?.(group.instanceId, group.instanceName); });
            groupHeader.appendChild(groupSettingsBtn);

            const groupCloseBtn = document.createElement('button'); groupCloseBtn.type='button'; groupCloseBtn.className='browser-tab-instance-close btn btn-sm btn-light border-0'; groupCloseBtn.title='关闭整个实例'; groupCloseBtn.innerHTML = '<i class="fa-solid fa-xmark"></i>';
            groupCloseBtn.addEventListener('click', async e => { e.stopPropagation(); try { await window.BrowserUI.closeInstance(group.instanceId); } catch (ex){ console.warn(ex); } });
            groupHeader.appendChild(groupCloseBtn);

            const groupTabs = document.createElement('div'); groupTabs.className='browser-tab-instance-tabs';

            for (const tab of group.tabs){
                const tabItem = document.createElement('div'); tabItem.className = 'browser-tab-item' + (tab.isSelected ? ' browser-tab-item-active' : ''); tabItem.style.setProperty('--instance-color', tab.instanceColor || '#2563eb');
                const btn = document.createElement('button'); btn.type='button'; btn.className = 'browser-tab btn btn-sm' + (tab.isSelected ? ' browser-tab-active' : ''); btn.title = `[${tab.instanceName || tab.instanceId}] ` + (tab.url || tab.title || 'about:blank'); btn.textContent = tab.title || tab.url || 'about:blank';
                btn.addEventListener('click', async () => { try { await window.BrowserUI.selectTab(tab.id); } catch (e) { console.warn(e); } });

                const closeBtn = document.createElement('button'); closeBtn.type='button'; closeBtn.className='browser-tab-close btn btn-sm btn-light border-0'; closeBtn.title='关闭 TAB'; closeBtn.innerHTML = '<i class="fa-solid fa-xmark"></i>';
                closeBtn.addEventListener('click', async e => { e.stopPropagation(); try { await postJson('tabsClose', { tabId: tab.id }); await window.BrowserUI.refreshStatus(); } catch (err) { console.warn(err); } });

                tabItem.appendChild(btn); tabItem.appendChild(closeBtn); groupTabs.appendChild(tabItem);
            }

            groupContainer.appendChild(groupHeader); groupContainer.appendChild(groupTabs); tabsBar.appendChild(groupContainer);
        }

        updateTabScrollButtons();
    }

    window.BrowserTabs = { renderTabs, renderTabsStatus: function(t){ /* kept for compatibility; status panel rendering remains in-index for now */ }, updateTabScrollButtons, groupTabsByInstance };

    // wire scroll buttons
    try { tabsScrollLeftBtn?.addEventListener('click', () => tabsBar.scrollBy({ left: -240, behavior: 'smooth' })); tabsScrollRightBtn?.addEventListener('click', () => tabsBar.scrollBy({ left: 240, behavior: 'smooth' })); tabsBar?.addEventListener('scroll', updateTabScrollButtons); } catch {}

})();
