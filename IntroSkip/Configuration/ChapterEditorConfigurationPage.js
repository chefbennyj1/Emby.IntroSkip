define(["loading", "dialogHelper", "mainTabsManager", "datetime", "formDialogStyle", "emby-checkbox", "emby-select", "emby-toggle"],
    function (loading, dialogHelper, mainTabsManager, datetime) {
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
        
        function getBaseItem(id) {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('Items?Ids=' + id)).then(result => {
                    resolve(result);
                });
            });
        }

        function getChapterErrors() {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('ChapterErrors')).then(result => {
                    resolve(result);
                });
            });
        }

        function renderTableRowHtml(errItem, baseItem) {
            var html = '';



            html += '<td class="detailTableBodyCell"></td>';

            html += '<td class="detailTableBodyCell" data-title="Date">';
            var date = datetime.parseISO8601Date(errItem.Date, true);
            html += '<span>' + datetime.toLocaleDateString(date) + '</span>';
            html += '</td>';

            html += '<td class="detailTableBodyCell" data-title="TV Show">';
            html += '<span>' + baseItem.SeriesName + '</span>';
            html += '</td>';

            html += '<td class="detailTableBodyCell" data-title="Season">';
            html += '<span>' + baseItem.SeasonName + '</span>';
            html += '</td>';

            html += '<td class="detailTableBodyCell" data-title="Episode">';
            html += '<span>Episode ' + baseItem.IndexNumber + '</span>';
            html += '</td>';

            html += '<td class="detailTableBodyCell" data-title="# of Chapters">';
            html += '<span>' + errItem.ChapterCount + '</span>';
            html += '</td>';

            return html;


        }

        return function (view) {
            view.addEventListener('viewshow', (e) => {
                loading.show();
                mainTabsManager.setTabs(this, 1, getTabs);
                
                //elements
                var autoChapterExtract      = view.querySelector('.chkChapterExtractEvent');
                var chapterInsert           = view.querySelector('.chkChapterInsertEvent');
                var chapterErrorResultTable = view.querySelector('.tblEpisodeChapterErrorResultBody');
                
                //config settings
                ApiClient.getPluginConfiguration(pluginId).then((config) => {
                    //Chapter Insertion Option
                    chapterInsert.checked = config.EnableChapterInsertion;
                    //Auto Chapter Image Extraction
                    autoChapterExtract.checked = config.EnableAutomaticImageExtraction;
                });
                

                //clicks
                chapterInsert.addEventListener('change',
                    (e) => {
                        e.preventDefault();
                        ApiClient.getPluginConfiguration(pluginId).then((config) => {
                            config.EnableChapterInsertion = chapterInsert.checked;
                            ApiClient.updatePluginConfiguration(pluginId, config).then(() => { });
                        });
                    });
                autoChapterExtract.addEventListener('change',
                    (e) => {
                        e.preventDefault();
                        ApiClient.getPluginConfiguration(pluginId).then((config) => {
                            config.EnableAutomaticImageExtraction = autoChapterExtract.checked;
                            ApiClient.updatePluginConfiguration(pluginId, config).then(() => { });
                        });
                    });


                //Chapter Error Result Table
                getChapterErrors().then(errResults => {
                    errResults.forEach(errItem => {
                        getBaseItem(errItem.Id).then(result => {
                            var baseItem = result.Items[0];
                            chapterErrorResultTable.innerHTML += renderTableRowHtml(errItem, baseItem);
                        });
                    });
                });


                loading.hide();
            });

        }
    });