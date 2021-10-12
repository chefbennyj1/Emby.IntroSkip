define(["loading", "dialogHelper", "mainTabsManager", "formDialogStyle", "emby-checkbox", "emby-select", "emby-toggle"],
    function (loading, dialogHelper, mainTabsManager) {

        var pluginId = "93A5E794-E0DA-48FD-8D3A-606A20541ED6";

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
                }];
        }

        async function getSeries() {
            return await ApiClient.getJSON(ApiClient.getUrl(
                'Items?ExcludeLocationTypes=Virtual&Recursive=true&IncludeItemTypes=Series&SortBy=SortName'));
        }
        
        async function getBaseItem(id) {
            return await ApiClient.getJSON(ApiClient.getUrl(`Items?Ids=${id}`));
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
                    Dashboard.processPluginConfigurationUpdateResult(r);
                });

            });

        }

        function reloadList(list, element, view) {
            element.innerHTML = '';
            if (list && list.length) {
                var padding = 0;
                list.forEach(async (id) => {
                    var result = await getBaseItem(id);
                    var baseItem = result.Items[0];
                    element.innerHTML += getListItemHtml(baseItem, padding);
                    padding += 77; //Why is this padding necessary
                    var removeButtons = view.querySelectorAll('.removeItemBtn');
                    removeButtons.forEach(btn => {
                        btn.addEventListener('click', el => {
                            el.preventDefault();
                            handleRemoveItemClick(el, element, view);
                        });
                    });

                });
            }
        }

       

        return function (view) {
            view.addEventListener('viewshow', async () => {

                loading.show();

                mainTabsManager.setTabs(this, 2, getTabs);

                var ignoreListElement = view.querySelector('.ignore-list');

                //How many series to process at once
                var titleSequenceMaxDegreeOfParallelism = view.querySelector('#txtTitleSequenceMaxDegreeOfParallelism');
                var fingerprintMaxDegreeOfParallelism = view.querySelector('#txtFingerprintMaxDegreeOfParallelism');

                //enable ItemAdded Event Listeners
                var chkEnableItemAddedTaskAutoRun = view.querySelector('#enableItemAddedTaskAutoRun');

                var chkEnableFastDetect = view.querySelector('#enableFastDetect');

                var confidenceInput = view.querySelector('#txtTitleSequenceSamplingWeightConfidence');
                
                //enable detection task auto run when fingerprinting is complete
                var chkEnableDetectionTaskAutoRun = view.querySelector('#enableDetectionTaskAutoRun');

                var config = await ApiClient.getPluginConfiguration(pluginId);

                titleSequenceMaxDegreeOfParallelism.value = config.MaxDegreeOfParallelism ? config.MaxDegreeOfParallelism : 2;

                fingerprintMaxDegreeOfParallelism.value = config.FingerprintingMaxDegreeOfParallelism ? config.FingerprintingMaxDegreeOfParallelism : 2;

                chkEnableItemAddedTaskAutoRun.checked = config.EnableItemAddedTaskAutoRun;

                chkEnableDetectionTaskAutoRun.checked = config.EnableIntroDetectionAutoRun;

                chkEnableFastDetect.checked = config.FastDetect;

                confidenceInput.value = config.DetectionConfidence;

                if (!chkEnableFastDetect.checked) {
                    confidenceInput.closest('.inputContainer').classList.remove('hide');
                }

                if (config.IgnoredList) {
                    reloadList(config.IgnoredList, ignoreListElement, view);
                }

                //Our ignore list
                var seriesSelect = view.querySelector('#selectEmbySeries');
                var series = await getSeries();
                for (let i = 0; i <= series.Items.length - 1; i++) {
                    seriesSelect.innerHTML += `<option value="${series.Items[i].Id}">${series.Items[i].Name}</option>`;
                }

                var addToIgnoreListBtn = view.querySelector('#btnAddSeriesToIgnoreList');
                
                addToIgnoreListBtn.addEventListener('click', async (el) => {
                    el.preventDefault();

                    loading.show();

                    var seriesId = seriesSelect[seriesSelect.selectedIndex].value;
                    
                    config = await ApiClient.getPluginConfiguration(pluginId);

                    if (config.IgnoredList) {
                        config.IgnoredList.push(seriesId);
                    } else {
                        config.IgnoredList = [seriesId];
                    }

                    var r = await ApiClient.updatePluginConfiguration(pluginId, config);

                    reloadList(config.IgnoredList, ignoreListElement, view);

                    Dashboard.processPluginConfigurationUpdateResult(r);
                    loading.hide();
                });

                chkEnableFastDetect.addEventListener('change', async (elem) => {
                    elem.preventDefault();

                    config = await ApiClient.getPluginConfiguration(pluginId);

                    var fastDetect = chkEnableFastDetect.checked;
                    config.FastDetect = fastDetect;

                    await ApiClient.updatePluginConfiguration(pluginId, config);

                    if (!fastDetect) {
                        if (confidenceInput.closest('.inputContainer').classList.contains('hide')) {
                            confidenceInput.closest('.inputContainer').classList.remove('hide');
                        }
                    } else {
                        if (!confidenceInput.closest('.inputContainer').classList.contains('hide')) {
                            confidenceInput.closest('.inputContainer').classList.add('hide');
                        }
                    }
                });

                confidenceInput.addEventListener('change', async (elem) => {
                    elem.preventDefault();
                    
                    config = await ApiClient.getPluginConfiguration(pluginId);
                    config.DetectionConfidence = confidenceInput.value;

                    await ApiClient.updatePluginConfiguration(pluginId, config);
                });

                fingerprintMaxDegreeOfParallelism.addEventListener('change', async (elem) => {
                    elem.preventDefault();

                    if (fingerprintMaxDegreeOfParallelism < 2) {
                        fingerprintMaxDegreeOfParallelism.value = 2;
                    }
                    
                    config = await ApiClient.getPluginConfiguration(pluginId);
                    config.FingerprintingMaxDegreeOfParallelism = fingerprintMaxDegreeOfParallelism.value;

                    await ApiClient.updatePluginConfiguration(pluginId, config);
                });

                titleSequenceMaxDegreeOfParallelism.addEventListener('change', async (elem) => {
                    elem.preventDefault();

                    config = await ApiClient.getPluginConfiguration(pluginId);
                    config.MaxDegreeOfParallelism = titleSequenceMaxDegreeOfParallelism.value;

                    await ApiClient.updatePluginConfiguration(pluginId, config);
                });

                chkEnableItemAddedTaskAutoRun.addEventListener('change', async (elem) => {
                    elem.preventDefault();

                    config = ApiClient.getPluginConfiguration(pluginId);
                    config.EnableItemAddedTaskAutoRun = chkEnableItemAddedTaskAutoRun.checked;

                    await ApiClient.updatePluginConfiguration(pluginId, config);
                });

                chkEnableDetectionTaskAutoRun.addEventListener('change', async (elem) => {
                    elem.preventDefault();

                    config = await ApiClient.getPluginConfiguration(pluginId);
                    config.EnableIntroDetectionAutoRun = chkEnableDetectionTaskAutoRun.checked;

                    await ApiClient.updatePluginConfiguration(pluginId, config);
                });

                loading.hide();
            });
        }
    });