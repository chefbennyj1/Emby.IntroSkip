define(["loading", "dialogHelper", "mainTabsManager", "formDialogStyle", "emby-checkbox", "emby-select", "emby-toggle"],
    function (loading, dialogHelper, mainTabsManager) {
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

        var pluginId = "93A5E794-E0DA-48FD-8D3A-606A20541ED6";

        return function (view) {
            view.addEventListener('viewshow', (e) => {
                loading.show();

                mainTabsManager.setTabs(this, 2, getTabs);

                var titleSequenceMaxDegreeOfParallelism = view.querySelector('#txtTitleSequenceMaxDegreeOfParallelism');

                //var removeAllButton = dlg.querySelector('.removeAllData');
              

                ApiClient.getPluginConfiguration(pluginId).then((config) => {
                    titleSequenceMaxDegreeOfParallelism.value = config.MaxDegreeOfParallelism ? config.MaxDegreeOfParallelism : 2;
                });

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


                loading.hide();
            });
        }
    });