define(["loading", "dialogHelper", "mainTabsManager", "formDialogStyle", "emby-checkbox", "emby-select", "emby-toggle"],
    function (loading, dialogHelper, mainTabsManager) {

        var pluginId = "93A5E794-E0DA-48FD-8D3A-606A20541ED6";

        function getTabs() {
            return [
                {
                    href: Dashboard.getConfigurationPageUrl('IntroSkipConfigurationPage'),
                    name: 'Title Sequence Activity Log'
                },
                {
                    href: Dashboard.getConfigurationPageUrl('ChapterEditorConfigurationPage'),
                    name: 'Chapters'
                },
                {
                    href: Dashboard.getConfigurationPageUrl('AdvancedSettingsConfigurationPage'),
                    name: 'Advanced Settings'
                }];
        }

        function getSeries() {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('Items?ExcludeLocationTypes=Virtual&Recursive=true&IncludeItemTypes=Series&SortBy=SortName')).then(result => {
                    resolve(result);
                });
            });
        }

        function getItem(id) {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('Items?Ids=' + id)).then(result => {
                    resolve(result);
                });
            });
        }

        function getListItemHtml(series) {
            var html = '';
            html += '<div class="virtualScrollItem listItem listItem-border focusable listItemCursor listItem-hoverable listItem-withContentWrapper" tabindex="0" draggable="true" data-action="link" style="transform: translate(0px, 0px);">';
            html += '<div class="listItem-content listItemContent-touchzoom">';
            html += '<div class="listItemBody itemAction listItemBody-noleftpadding">';
            html += '<div class="listItemBodyText listItemBodyText-nowrap">' + series.Name + '</div>';
            html += '<div class="listItemBodyText listItemBodyText-secondary listItemBodyText-nowrap">EXTRA ITEM DATA</div>';
            html += '</div>';
            html += '<button title="Remove" aria-label="Remove" type="button" is="paper-icon-button-light" class="listItemButton itemAction paper-icon-button-light icon-button-conditionalfocuscolor removeItemBtn" id="' + series.Id + '">';
            html += '<i class="md-icon">delete</i>';
            html += '</button> ';
            html += '</div>';
            html += '</div>';
            return html;
        }

        

        return function (view) {
            view.addEventListener('viewshow', (e) => {
                loading.show();

                mainTabsManager.setTabs(this, 2, getTabs);

                var ignoreList = view.querySelector('.ignore-list');

                //How many series to process at once
                var titleSequenceMaxDegreeOfParallelism = view.querySelector('#txtTitleSequenceMaxDegreeOfParallelism');
                ApiClient.getPluginConfiguration(pluginId).then((config) => {
                    titleSequenceMaxDegreeOfParallelism.value = config.MaxDegreeOfParallelism ? config.MaxDegreeOfParallelism : 2;
                });

                //Hamming Distance Sensitivity Settings
                var hammingDistanceSens = view.querySelector('#txtHammingDistanceThreshold');
                ApiClient.getPluginConfiguration(pluginId).then((config) => {
                    hammingDistanceSens.value = config.HammingDistanceThreshold ? config.HammingDistanceThreshold : 8;
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
                    
                    getItem(seriesId).then(series => {

                        ignoreList.innerHTML += getListItemHtml(series);

                        ApiClient.getPluginConfiguration(pluginId).then((config) => {

                            if (config.IgnoredSeries) {

                                config.IgnoredSeries.push(seriesId);

                            } else {

                                config.IgnoredSeries = [ seriesId ];

                            }
                            ApiClient.updatePluginConfiguration(pluginId, config).then((result) => {
                                Dashboard.processPluginConfigurationUpdateResult(result); 
                            });
                        });
                        loading.hide();
                    });
                });

                //var removeAllButton = dlg.querySelector('.removeAllData');
                //removeAllButton.addEventListener('click', (e) => {
                //    e.preventDefault();
                //    var message = 'Are you sure you wish to proceed?';
                //    require(['confirm'], function (confirm) {
                //        confirm(message, 'Remove All Data').then(function () {
                //            ApiClient.deleteSeasonData(seasonSelect[seasonSelect.selectedIndex].value).then(result => {
                //                if (result == "OK") {
                //                    ApiClient.deleteAll().then(result => {
                //                        Dashboard.alert("All data removed.");
                //                        dialogHelper.close(confirmDlg);
                //                    });
                //                }
                //            });
                //        });
                //    });
                //});

                titleSequenceMaxDegreeOfParallelism.addEventListener('change', (elem) => {
                    elem.preventDefault();
                    ApiClient.getPluginConfiguration(pluginId).then((config) => {
                        config.MaxDegreeOfParallelism = titleSequenceMaxDegreeOfParallelism.value;
                        config.FingerprintingMaxDegreeOfParallelism = titleSequenceMaxDegreeOfParallelism.value;
                        ApiClient.updatePluginConfiguration(pluginId, config).then(() => { });
                    });
                });

                hammingDistanceSens.addEventListener('change', (elem) => {
                    elem.preventDefault();
                    ApiClient.getPluginConfiguration(pluginId).then((config) => {
                        config.HammingDistanceThreshold = hammingDistanceSens.value;
                        ApiClient.updatePluginConfiguration(pluginId, config).then(() => { });
                    });
                });




                loading.hide();
            });
        }
    });