(function () {
    const tabsBar = document.getElementById('tabsBar');
    const tabsScrollLeftBtn = document.getElementById('tabsScrollLeftBtn');
    const tabsScrollRightBtn = document.getElementById('tabsScrollRightBtn');
    const tabsStatusPanel = document.getElementById('tabsStatusPanel');

    const postJson = (window.BrowserIndex && window.BrowserIndex.postJson) || (async (u, b) => { const resolved = (window.__apiEndpoints && window.__apiEndpoints[u]) || u; const r = await fetch(resolved, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(b || {}) }); if (!r.ok) throw new Error('Request failed: ' + r.status); return await r.json(); });

    function updateTabScrollButtons() {
        if (!tabsBar) return;
        const maxScrollLeft = Math.max(0, tabsBar.scrollWidth - tabsBar.clientWidth);
        if (tabsScrollLeftBtn) tabsScrollLeftBtn.disabled = tabsBar.scrollLeft <= 0;
        if (tabsScrollRightBtn) tabsScrollRightBtn.disabled = tabsBar.scrollLeft >= maxScrollLeft - 1;
    }

    function groupTabsByInstance(tabs) {
        const groups = [];
        const groupMap = new Map();
        for (const tab of tabs) {
            if (!groupMap.has(tab.instanceId)) {
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

    function renderTabs(tabs) {
        if (!tabsBar) return updateTabScrollButtons();
        tabsBar.innerHTML = '';
        if (!tabs || tabs.length === 0) {
            const empty = document.createElement('div'); empty.className = 'small text-muted'; empty.textContent = '暂无 TAB'; tabsBar.appendChild(empty); updateTabScrollButtons(); return;
        }

        for (const group of groupTabsByInstance(tabs)) {
            const groupContainer = document.createElement('div');
            groupContainer.className = 'browser-tab-instance-group' + (group.isSelected ? ' browser-tab-instance-group-active' : '');
            groupContainer.style.setProperty('--instance-color', group.instanceColor);

            const groupHeader = document.createElement('div');
            groupHeader.className = 'browser-tab-instance-header';

            const groupLabel = document.createElement('button');
            groupLabel.type = 'button'; groupLabel.className = 'browser-tab-instance-label' + (group.isSelected ? ' browser-tab-instance-label-active' : '');
            groupLabel.title = '切换到实例：' + group.instanceName; groupLabel.textContent = group.instanceName;
            groupLabel.addEventListener('click', async () => {
                const targetTab = group.tabs.find(t => t.isSelected) || group.tabs[0]; if (!targetTab) return; try { await window.BrowserUI.selectTab(targetTab.id); } catch (e) { console.warn(e); }
            });
            groupHeader.appendChild(groupLabel);

            const groupSettingsBtn = document.createElement('button'); groupSettingsBtn.type = 'button'; groupSettingsBtn.className = 'browser-tab-instance-close btn btn-sm btn-light border-0'; groupSettingsBtn.title = '实例设置'; groupSettingsBtn.innerHTML = '<i class="fa-solid fa-sliders"></i>';
            groupSettingsBtn.addEventListener('click', async e => { e.stopPropagation(); await window.BrowserUI.openInstanceSettings?.(group.instanceId, group.instanceName); });
            groupHeader.appendChild(groupSettingsBtn);

            // per-instance new-tab button (matches style of settings / close)
            const groupNewTabBtn = document.createElement('button');
            groupNewTabBtn.type = 'button';
            groupNewTabBtn.className = 'browser-tab-instance-close btn btn-sm btn-light border-0';
            groupNewTabBtn.title = '为该实例新建 TAB';
            groupNewTabBtn.innerHTML = '<i class="fa-solid fa-plus"></i>';
            groupNewTabBtn.addEventListener('click', async e => {
                e.stopPropagation();
                try {
                    await postJson('tabsNew', { instanceId: group.instanceId, url: 'about:blank' });
                    await window.BrowserUI.refreshStatus?.();
                } catch (err) { console.warn('create tab for instance failed', err); }
            });
            groupHeader.appendChild(groupNewTabBtn);

            const groupCloseBtn = document.createElement('button'); groupCloseBtn.type = 'button'; groupCloseBtn.className = 'browser-tab-instance-close btn btn-sm btn-light border-0'; groupCloseBtn.title = '关闭整个实例'; groupCloseBtn.innerHTML = '<i class="fa-solid fa-xmark"></i>';
            groupCloseBtn.addEventListener('click', async e => { e.stopPropagation(); try { await window.BrowserUI.closeInstance(group.instanceId); } catch (ex) { console.warn(ex); } });
            groupHeader.appendChild(groupCloseBtn);

            const groupTabs = document.createElement('div'); groupTabs.className = 'browser-tab-instance-tabs';

            for (const tab of group.tabs) {
                const tabItem = document.createElement('div'); tabItem.className = 'browser-tab-item' + (tab.isSelected ? ' browser-tab-item-active' : ''); tabItem.style.setProperty('--instance-color', tab.instanceColor || '#2563eb');
                const btn = document.createElement('button'); btn.type = 'button'; btn.className = 'browser-tab btn btn-sm' + (tab.isSelected ? ' browser-tab-active' : ''); btn.title = `[${tab.instanceName || tab.instanceId}] ` + (tab.url || tab.title || 'about:blank'); btn.textContent = tab.title || tab.url || 'about:blank';
                btn.addEventListener('click', async () => { try { await window.BrowserUI.selectTab(tab.id); } catch (e) { console.warn(e); } });

                const closeBtn = document.createElement('button'); closeBtn.type = 'button'; closeBtn.className = 'browser-tab-close btn btn-sm btn-light border-0'; closeBtn.title = '关闭 TAB'; closeBtn.innerHTML = '<i class="fa-solid fa-xmark"></i>';
                closeBtn.addEventListener('click', async e => { e.stopPropagation(); try { await postJson('tabsClose', { tabId: tab.id }); await window.BrowserUI.refreshStatus(); } catch (err) { console.warn(err); } });

                tabItem.appendChild(btn); tabItem.appendChild(closeBtn); groupTabs.appendChild(tabItem);
            }

            groupContainer.appendChild(groupHeader); groupContainer.appendChild(groupTabs); tabsBar.appendChild(groupContainer);
        }

        updateTabScrollButtons();
    }

    function renderTabsStatus(tabs) {
        if (!tabsStatusPanel) return;
        tabsStatusPanel.replaceChildren();

        const groups = groupTabsByInstance(tabs || []);

        // Empty state: show centered title + message
        if (!groups.length) {
            const wrapper = document.createElement('div');
            wrapper.className = 'tabs-empty-centered';

            const header = document.createElement('div');
            header.className = 'tabs-status-header';
            header.innerHTML = '<h3 class="tabs-status-title">当前 TAB 状态</h3>';
            wrapper.appendChild(header);

            const empty = document.createElement('div');
            empty.className = 'tabs-status-empty';
            empty.textContent = '暂无实例或标签页。';
            wrapper.appendChild(empty);

            tabsStatusPanel.appendChild(wrapper);
            return;
        }

        // Non-empty: list groups (no top header), stretch to full width
        const container = document.createElement('div');
        container.className = 'tabs-status-list-container';

        for (const group of groups) {
            const groupNode = document.createElement('section');
            groupNode.className = 'tabs-instance-group';
            groupNode.style.setProperty('--instance-color', group.instanceColor);

            const groupHeader = document.createElement('div');
            groupHeader.className = 'tabs-instance-group-header';

            const title = document.createElement('div');
            title.className = 'tabs-instance-group-title';
            title.textContent = group.instanceName;
            groupHeader.appendChild(title);

            const actions = document.createElement('div');
            actions.className = 'tabs-status-actions';

            const settingsButton = document.createElement('button');
            settingsButton.type = 'button';
            settingsButton.className = 'btn btn-sm btn-outline-secondary';
            settingsButton.innerHTML = '<i class="fa-solid fa-sliders me-1"></i>设置';
            settingsButton.addEventListener('click', async () => {
                await window.BrowserUI.openInstanceSettings?.(group.instanceId, group.instanceName);
            });
            actions.appendChild(settingsButton);

            const closeInstanceButton = document.createElement('button');
            closeInstanceButton.type = 'button';
            closeInstanceButton.className = 'btn btn-sm btn-outline-danger';
            closeInstanceButton.innerHTML = '<i class="fa-solid fa-xmark me-1"></i>关闭实例';
            closeInstanceButton.addEventListener('click', async () => {
                await window.BrowserUI.closeInstance?.(group.instanceId);
            });
            actions.appendChild(closeInstanceButton);

            groupHeader.appendChild(actions);
            groupNode.appendChild(groupHeader);

            // Render tabs as a simple list without showing URLs
            const list = document.createElement('ul');
            list.className = 'tabs-status-list list-unstyled mb-2';

            for (const tab of group.tabs) {
                const tabId = tab.id || tab.Id;
                const li = document.createElement('li');
                li.className = 'tabs-status-item' + ((tab.isSelected || tab.IsSelected || tab.isActive || tab.IsActive) ? ' active' : '');
                li.tabIndex = 0;

                const name = document.createElement('span');
                name.className = 'tabs-status-name';
                name.textContent = tab.title || tab.Title || 'about:blank';
                li.appendChild(name);

                const btns = document.createElement('div');
                btns.className = 'tabs-status-actions d-inline-block ms-2';

                const closeTabButton = document.createElement('button');
                closeTabButton.type = 'button';
                closeTabButton.className = 'btn btn-sm btn-outline-secondary tabs-status-close';
                closeTabButton.innerHTML = '<i class="fa-solid fa-xmark"></i>';
                closeTabButton.addEventListener('click', async event => {
                    event.stopPropagation();
                    await postJson('tabsClose', { tabId });
                    await window.BrowserUI.refreshStatus?.();
                });
                btns.appendChild(closeTabButton);

                li.appendChild(btns);

                li.addEventListener('click', async () => {
                    await window.BrowserUI.selectTab?.(tabId);
                });
                li.addEventListener('keydown', async event => {
                    if (event.key === 'Enter' || event.key === ' ') {
                        event.preventDefault();
                        await window.BrowserUI.selectTab?.(tabId);
                    }
                });

                list.appendChild(li);
            }

            groupNode.appendChild(list);
            container.appendChild(groupNode);
        }

        tabsStatusPanel.appendChild(container);
    }

    window.BrowserTabs = { renderTabs, renderTabsStatus, updateTabScrollButtons, groupTabsByInstance };
    window.renderTabs = renderTabs;
    window.renderTabsStatus = renderTabsStatus;

    // wire scroll buttons
    try { tabsScrollLeftBtn?.addEventListener('click', () => tabsBar.scrollBy({ left: -240, behavior: 'smooth' })); tabsScrollRightBtn?.addEventListener('click', () => tabsBar.scrollBy({ left: 240, behavior: 'smooth' })); tabsBar?.addEventListener('scroll', updateTabScrollButtons); } catch { }

})();
