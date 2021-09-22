define(["loading", "dialogHelper", "mainTabsManager", "formDialogStyle", "emby-checkbox", "emby-select", "emby-toggle"],
    function (loading, dialogHelper, mainTabsManager) {

        var pluginId = "93A5E794-E0DA-48FD-8D3A-606A20541ED6";

        function getTabs() {
            return [
                {
                    href: Dashboard.getConfigurationPageUrl('IntroSkipConfigurationPage'),
                    name: 'Activity'
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

        function getSeries() {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('Items?ExcludeLocationTypes=Virtual&Recursive=true&IncludeItemTypes=Series&SortBy=SortName')).then(result => {
                    resolve(result);
                });
            });
        }
        
        function getBaseItem(id) {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('Items?Ids=' + id)).then(result => {
                    resolve(result);
                });
            });
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
                list.forEach(id => {
                    getBaseItem(id).then(result => {
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
                });
            }
        }


        return function (view) {
            view.addEventListener('viewshow', (e) => {

                loading.show();

                mainTabsManager.setTabs(this, 2, getTabs);

                var ignoreListElement = view.querySelector('.ignore-list');

                //How many series to process at once
                var titleSequenceMaxDegreeOfParallelism = view.querySelector('#txtTitleSequenceMaxDegreeOfParallelism');

                ApiClient.getPluginConfiguration(pluginId).then((config) => {

                    titleSequenceMaxDegreeOfParallelism.value = config.MaxDegreeOfParallelism ? config.MaxDegreeOfParallelism : 2;

                    if (config.IgnoredList) {
                        reloadList(config.IgnoredList, ignoreListElement, view);
                    }
                });

                
                //Our ignore list
                var seriesSelect = view.querySelector('#selectEmbySeries');
                getSeries().then(series => {
                    for (let i = 0; i <= series.Items.length - 1; i++) {
                        seriesSelect.innerHTML += '<option value="' + series.Items[i].Id + '">' + series.Items[i].Name + '</option>';
                    }
                });

                var addToIgnoreListBtn = view.querySelector('#btnAddSeriesToIgnoreList');
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

                            Dashboard.processPluginConfigurationUpdateResult(r); 
                        });

                    });
                        
                    loading.hide();
                });
                       

                titleSequenceMaxDegreeOfParallelism.addEventListener('change', (elem) => {
                    elem.preventDefault();
                    ApiClient.getPluginConfiguration(pluginId).then((config) => {
                        config.MaxDegreeOfParallelism = titleSequenceMaxDegreeOfParallelism.value;
                        config.FingerprintingMaxDegreeOfParallelism = titleSequenceMaxDegreeOfParallelism.value;
                        ApiClient.updatePluginConfiguration(pluginId, config).then(() => { });
                    });
                });

                

                loading.hide();
            });
        }
    });