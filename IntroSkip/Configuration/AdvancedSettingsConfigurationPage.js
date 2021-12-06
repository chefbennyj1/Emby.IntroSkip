define(["loading", "dialogHelper", "mainTabsManager", "formDialogStyle", "emby-checkbox", "emby-select", "emby-toggle"],
    function (loading, dialogHelper, mainTabsManager) {

        var pluginId = "93A5E794-E0DA-48FD-8D3A-606A20541ED6";
        var globalView;
        function getTabs() {
            return [
                {
                    href: Dashboard.getConfigurationPageUrl('IntroSkipConfigurationPage'),
                    name: 'Intros'
                },
                {
                    href: Dashboard.getConfigurationPageUrl('ChapterEditorConfigurationPage'),
                    name: 'Chapters'
                },
                {
                    href: Dashboard.getConfigurationPageUrl('AdvancedSettingsConfigurationPage'),
                    name: 'Advanced'
                },
                {
                    href: Dashboard.getConfigurationPageUrl('AutoSkipConfigurationPage'),
                    name: 'Auto Skip'
                },
                {
                    href: Dashboard.getConfigurationPageUrl('StatsConfigurationPage'),
                    name: 'Stats'
                }];
        }

        function getSeries() {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('Items?ExcludeLocationTypes=Virtual&Recursive=true&IncludeItemTypes=Series&SortBy=SortName')).then(result => {
                    resolve(result);
                });
            });
        }
        
        async function getBaseItem(id) {
            return await ApiClient.getJSON(ApiClient.getUrl('Items?Ids=' + id));
        }

        function getListItemHtml(series, padding) {
            var html = '';
            html += '<div class="virtualScrollItem listItem listItem-border focusable listItemCursor listItem-hoverable listItem-withContentWrapper" tabindex="0" draggable="false" style="transform: translate(0px, ' + padding + 'px);">';
            html += '<div class="listItem-content listItemContent-touchzoom">';
            html += '<div class="listItemBody itemAction listItemBody-noleftpadding">';
            html += '<div class="listItemBodyText listItemBodyText-nowrap">' + series.Name + '</div>';
            html += '<div class="listItemBodyText listItemBodyText-secondary listItemBodyText-nowrap">Will be ignored during intro detection.</div>';
            html += '</div>';
            html += '<button title="Remove" aria-label="Remove" type="button" is="paper-icon-button-light" class="listItemButton itemAction paper-icon-button-light icon-button-conditionalfocuscolor removeItemBtn" id="' + series.Id + '">';
            html += '<i class="md-icon removeItemBtn" style="pointer-events: none;">delete</i>';
            html += '</button> ';
            html += '</div>';
            html += '</div>';
            return html;
        }

        function handleRemoveItemClick(e, element, view) {
            var id = e.target.closest('button').id;
            ApiClient.getPluginConfiguration(pluginId).then((config) => {
                var filteredList = config.IgnoredList.filter(item => item != id);
                config.IgnoredList = filteredList;
                ApiClient.updatePluginConfiguration(pluginId, config).then((r) => {
                    reloadList(filteredList, element, view);
                    loadSeriesSelect(config, view);
                    Dashboard.processPluginConfigurationUpdateResult(r);
                });

            });

        }

        function reloadList(list, element, view) {
            element.innerHTML = '';
            if (list && list.length) {
                var padding = 0;
                list.forEach(async id => {
                    var result = await getBaseItem(id);
                    var baseItem = result.Items[0];
                    element.innerHTML += getListItemHtml(baseItem, padding);
                    padding += 77; //Why is this padding necessary
                    var removeButtons = view.querySelectorAll('.removeItemBtn');
                    removeButtons.forEach(btn => {
                        btn.addEventListener('click',
                            el => {
                                el.preventDefault();
                                handleRemoveItemClick(el, element, view);
                            });
                    });

                });
            }
        }

        function loadSeriesSelect(config, view) {
            var seriesSelect = view.querySelector('#selectEmbySeries');
            seriesSelect.innerHTML = '';
            getSeries().then(series => {
                var seriesItems = series.Items;
                for (let i = 0; i <= seriesItems.length - 1; i++) {
                    if (config.IgnoredList.includes(parseInt(seriesItems[i].Id)) || config.IgnoredList.includes(seriesItems[i].Id)) {
                        continue;
                    }
                    seriesSelect.innerHTML += '<option value="' + seriesItems[i].Id + '">' + seriesItems[i].Name + '</option>';
                }
            });
        }
       

        return function (view) {
            view.addEventListener('viewshow', () => {

                loading.show();
                globalView = view;

                mainTabsManager.setTabs(this, 2, getTabs);

                var ignoreListElement                   = view.querySelector('.ignore-list');
                //How many series to process at once
                var titleSequenceMaxDegreeOfParallelism = view.querySelector('#txtTitleSequenceMaxDegreeOfParallelism');
                var fingerprintMaxDegreeOfParallelism   = view.querySelector('#txtFingerprintMaxDegreeOfParallelism');
                //enable ItemAdded Event Listeners
                var chkEnableItemAddedTaskAutoRun       = view.querySelector('#enableItemAddedTaskAutoRun');
                var chkEnableFastDetect                 = view.querySelector('#enableFastDetect');
                //enable detection task auto run when fingerprinting is complete
                var chkEnableDetectionTaskAutoRun       = view.querySelector('#enableDetectionTaskAutoRun');
                var seriesSelect = view.querySelector('#selectEmbySeries');
                var addToIgnoreListBtn = view.querySelector('#btnAddSeriesToIgnoreList');
                var btnAddAllSeriesToIgnoreList = view.querySelector('#btnAddAllSeriesToIgnoreList');
                ApiClient.getPluginConfiguration(pluginId).then((config) => {

                    titleSequenceMaxDegreeOfParallelism.value = config.MaxDegreeOfParallelism ? config.MaxDegreeOfParallelism : 2;
                    fingerprintMaxDegreeOfParallelism.value   = config.FingerprintingMaxDegreeOfParallelism ? config.FingerprintingMaxDegreeOfParallelism : 2;
                    chkEnableItemAddedTaskAutoRun.checked     = config.EnableItemAddedTaskAutoRun;
                    chkEnableDetectionTaskAutoRun.checked     = config.EnableIntroDetectionAutoRun;
                    chkEnableFastDetect.checked               = config.FastDetect;
                    

                    if (config.IgnoredList) {
                        reloadList(config.IgnoredList, ignoreListElement, view);

                    }

                    loadSeriesSelect(config, view);

                });

                btnAddAllSeriesToIgnoreList.addEventListener('click', (elem) => {
                    elem.preventDefault();

                    loading.show();
                    ApiClient.getPluginConfiguration(pluginId).then((config) => {
                        Array.from(seriesSelect.options).forEach(option => {
                            var seriesId = option.value;
                            if (config.IgnoredList) {

                                config.IgnoredList.push(seriesId);

                            } else {

                                config.IgnoredList = [ seriesId ];

                            }
                        });
                        ApiClient.updatePluginConfiguration(pluginId, config).then((r) => {
                            reloadList(config.IgnoredList, ignoreListElement, view);
                            loadSeriesSelect(config, view);
                            Dashboard.processPluginConfigurationUpdateResult(r);
                            loading.hide();
                        });
                    });
                });

                addToIgnoreListBtn.addEventListener('click', (el) => {
                    el.preventDefault();

                    loading.show();

                    var seriesId = seriesSelect[seriesSelect.selectedIndex].value;

                    ApiClient.getPluginConfiguration(pluginId).then((config) => {

                        if (config.IgnoredList) {

                            config.IgnoredList.push(seriesId);

                        } else {

                            config.IgnoredList = [ seriesId ];

                        }
                        ApiClient.updatePluginConfiguration(pluginId, config).then((r) => {
                            reloadList(config.IgnoredList, ignoreListElement, view);
                            loadSeriesSelect(config, view);
                            Dashboard.processPluginConfigurationUpdateResult(r); 
                        });

                    });
                        
                    loading.hide();
                });

                

                chkEnableFastDetect.addEventListener('change', (elem) => {
                    elem.preventDefault();
                    var fastDetect = chkEnableFastDetect.checked;
                    if (!fastDetect) {
                        var message =
                            "Fast Detect Off will result in near perfect results, at the expense of higher CPU usages.";
                        require(['confirm'],
                            function(confirm) {
                                confirm(message, 'Fast Detect Off').then(function() {
                                    enableFastDetect(fastDetect);
                                }, function() {
                                    chkEnableFastDetect.checked = true;
                                });
                            });
                    } else {
                        enableFastDetect(fastDetect);
                    }
                });

                fingerprintMaxDegreeOfParallelism.addEventListener('change', (elem) => {
                    elem.preventDefault();
                    if (fingerprintMaxDegreeOfParallelism < 2) {
                        fingerprintMaxDegreeOfParallelism.value = 2;
                    }
                    ApiClient.getPluginConfiguration(pluginId).then((config) => {
                        config.FingerprintingMaxDegreeOfParallelism = fingerprintMaxDegreeOfParallelism.value;
                        ApiClient.updatePluginConfiguration(pluginId, config).then(() => { });
                    });
                });

                titleSequenceMaxDegreeOfParallelism.addEventListener('change', (elem) => {
                    elem.preventDefault();
                    ApiClient.getPluginConfiguration(pluginId).then((config) => {
                        config.MaxDegreeOfParallelism = titleSequenceMaxDegreeOfParallelism.value;
                        ApiClient.updatePluginConfiguration(pluginId, config).then(() => { });
                    });
                });

                chkEnableItemAddedTaskAutoRun.addEventListener('change', (elem) => {
                    elem.preventDefault();
                    ApiClient.getPluginConfiguration(pluginId).then((config) => {
                        config.EnableItemAddedTaskAutoRun = chkEnableItemAddedTaskAutoRun.checked; 
                        ApiClient.updatePluginConfiguration(pluginId, config).then(() => { });
                    });
                });

                chkEnableDetectionTaskAutoRun.addEventListener('change', (elem) => {
                    elem.preventDefault();
                    ApiClient.getPluginConfiguration(pluginId).then((config) => {
                        config.EnableIntroDetectionAutoRun = chkEnableDetectionTaskAutoRun.checked; 
                        ApiClient.updatePluginConfiguration(pluginId, config).then(() => { });
                    });
                });

                function enableFastDetect(fastDetect) {
                    ApiClient.getPluginConfiguration(pluginId).then((config) => {
                        config.FastDetect = fastDetect;
                        ApiClient.updatePluginConfiguration(pluginId, config).then(() => {});
                    });
                }


                loading.hide();
            });

           

        }
    });