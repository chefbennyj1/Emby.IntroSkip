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
                }
            ];
        }

        var pluginId = "93A5E794-E0DA-48FD-8D3A-606A20541ED6";

        return function (view) {
            view.addEventListener('viewshow', (e) => {
                loading.show();
                mainTabsManager.setTabs(this, 1, getTabs);
                var autoChapterExtract = view.querySelector('.chkChapterExtractEvent');
                var chapterInsert = view.querySelector('.chkChapterInsertEvent');

                //Chapter Insertion Option
                ApiClient.getPluginConfiguration(pluginId).then((config) => {
                    chapterInsert.checked = config.EnableChapterInsertion;
                });

                chapterInsert.addEventListener('change',
                    (e) => {
                        e.preventDefault();
                        ApiClient.getPluginConfiguration(pluginId).then((config) => {
                            config.EnableChapterInsertion = chapterInsert.checked;
                            ApiClient.updatePluginConfiguration(pluginId, config).then(() => { });
                        });
                    });

                //Auto Chapter Image Extraction
                ApiClient.getPluginConfiguration(pluginId).then((config) => {
                    autoChapterExtract.checked = config.EnableAutomaticImageExtraction;
                });

                autoChapterExtract.addEventListener('change',
                    (e) => {
                        e.preventDefault();
                        ApiClient.getPluginConfiguration(pluginId).then((config) => {
                            config.EnableAutomaticImageExtraction = autoChapterExtract.checked;
                            ApiClient.updatePluginConfiguration(pluginId, config).then(() => { });
                        });
                    });

                loading.hide();
            });

        }
    });