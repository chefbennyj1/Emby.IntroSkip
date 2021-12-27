define(["loading", "dialogHelper", "mainTabsManager", "datetime", "formDialogStyle", "emby-checkbox", "emby-select", "emby-toggle"],
    function(loading, dialogHelper, mainTabsManager, datetime) {

        var iso8601DurationRegex =
            /(-)?P(?:([.,\d]+)Y)?(?:([.,\d]+)M)?(?:([.,\d]+)W)?(?:([.,\d]+)D)?T(?:([.,\d]+)H)?(?:([.,\d]+)M)?(?:([.,\d]+)S)?/;

        window.parseISO8601Duration = function(iso8601Duration) {
            var matches = iso8601Duration.match(iso8601DurationRegex);

            return {
                hours: matches[6] === undefined ? "00" : matches[6] < 10 ? `0${matches[6]}` : matches[6],
                minutes: matches[7] === undefined ? "00" : matches[7] < 10 ? `0${matches[7]}` : matches[7],
                seconds: matches[8] === undefined ? "00" : matches[8] < 10 ? `0${matches[8]}` : matches[8]
            };
        };

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

        //function waitdlg(view) {
        //    var dlg = dialogHelper.createDialog({
        //        removeOnClose: true,
        //        size: 'small'
        //    });

        //    dlg.classList.add('ui-body-a');
        //    dlg.classList.add('background-theme-a');

        //    dlg.classList.add('formDialog');
        //    dlg.style.maxWidth = '30%';
        //    dlg.style.maxHeight = '20%';

        //    var html = '';
        //    html += '<div class="formDialogHeader">';
        //    html += '<button is="paper-icon-button-light" class="btnCancel autoSize" tabindex="-1"><i class="md-icon">&#xE5C4;</i></button>';
        //    html += '<h3 class="formDialogHeaderTitle">Getting Statistics</h3>';
        //    html += '</div>';

        //    html += '<div class="formDialogContent" style="margin:2em">';
        //    html += '<div class="dialogContentInner" style="max-width: 100%; max-height:100%; display: flex;align-items: center;justify-content: center;">';

        //    html += `<h3 class="sectionTitle">${"Please wait while the stats are loaded....."}</h3>`;

        //    html += '<button is="emby-button" type="button" class="btnCancel submit raised button-submit" style="width:20%; margin-left:5%; justify-content: center;">';
        //    html += '<span>OK</span>';
        //    html += '</button>';

        //    /*Cancel
        //    html += '<button is="emby-button" type="button" class="btnCancel submit raised button-cancel">';
        //    html += '<span>Cancel</span>';
        //    html += '</button>';*/

        //    html += '</div>';
        //    html += '</div>';

        //    dlg.innerHTML = html;
            
        //    dlg.querySelectorAll('.btnCancel').forEach(btn => {
        //        btn.addEventListener('click', (e) => {
        //            dialogHelper.close(dlg);
        //        });
        //    });

        //    dialogHelper.open(dlg);

        //    reloadItems(view);
        //}

        var pluginId = "93A5E794-E0DA-48FD-8D3A-606A20541ED6";

        function getSeasonStatistics() {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl(`GetSeasonStatistics`)).then(result => {
                    resolve(result);
                });
            });
        }
         
        function reloadItems(view) {
            view.querySelector('.tblStatsResultBody').innerHTML = '';
            var statisticsResultTable = view.querySelector('.tblStatsResultBody');
            
            getSeasonStatistics().then(statResults => {
                statResults.forEach(statItem => {
                    statisticsResultTable.innerHTML += renderTableRowHtml(statItem);
                });
                loading.hide();
            });
        }

//        async function asyncReloadItems(view) {
//            view.querySelector('.tblStatsResultBody').innerHTML = '';
//            var statisticsResultTable = view.querySelector('.tblStatsResultBody');
//
//            getSeasonStatistics().then(statResults => {
//                statResults.forEach(async (statItem) => {
//                    statisticsResultTable.innerHTML += await renderTableRowHtml(statItem);
//                });
//            });
//        }

        function getStatusRenderData(status) {
            switch (status) {
            case false:
                return {
                    path:
                        "M10,17L5,12L6.41,10.58L10,14.17L17.59,6.58L19,8M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z",
                    color: "green",
                    text: "Complete"
                };
            case true:
                return {
                    path: "M13 14H11V9H13M13 18H11V16H13M1 21H23L12 2L1 21Z",
                    color: "goldenrod",
                    text: "Attention"
                };

            }
        }


        function renderTableRowHtml(statElement) {

            var html = '';
            //var date = datetime.parseISO8601Date(statElement.Date, true);
            var startTimespan = parseISO8601Duration(statElement.IntroDuration);
            var statusRenderData = getStatusRenderData(statElement.HasIssue);

            //html += '<tr data-id="' + statItem.SeasonId + '" class="detailTableBodyRow detailTableBodyRow-shaded">';
            //html += '<td data-title="WTFBuster" class="detailTableBodyCell" >';
            html += '<br/>';

            //html += '<td data-title="Date" class="detailTableBodyCell">';
            //html += '<span>' + datetime.toLocaleDateString(date) + '</span>';
            //html += '</td>';

            html += '<td data-title="Has Issue" class="detailTableBodyCell fileCell">';
            html += '<svg id="statusIcon" style="width:24px;height:24px" viewBox="0 0 24 24">';
            html += '<path fill="' + statusRenderData.color + '" d="' + statusRenderData.path + '"/>';
            html += '</svg>';
            html += '</td>';

           

            html += '<td data-title="TV Show" class="detailTableBodyCell fileCell">' + statElement.TVShowName + '</td>';

            html += '<td data-title="Season" class="detailTableBodyCell fileCell">' + statElement.Season + '</td>';

            html += '<td data-title="Results" class="detailTableBodyCell fileCell">' + statElement.PercentDetected + "%" + '</td>';

            html += '<td data-title="No. Episodes" class="detailTableBodyCell fileCell">' + statElement.EpisodeCount + '</td>';

            var duration = "00:" + startTimespan.minutes + ":" + startTimespan.seconds;
            html += '<td data-title="Duration" class="detailTableBodyCell fileCell">' + duration + '</td>';

            

            //html += '<td data-title="Results" class="detailTableBodyCell fileCell">' + statElement.EndPercentDetected + "%" + '</td>';

            //html += '<td data-title="Comments" class="detailTableBodyCell fileCell">' + statElement.Comment + '</td>';

            

            return html;
        }

        return function(view) {
            view.addEventListener('viewshow',
                async () => {

                    loading.show();
                    //Set Menu Tabs, using # for zerobased index
                    mainTabsManager.setTabs(this, 4, getTabs);

                    //elements
                    var runStatsTaskBtn = view.querySelector('.runStatsTaskBtn');
                    //var getStatsFileBtn = view.querySelector('.getStatsFileBtn');
                    var enableFullStats = view.querySelector('.chkEnableFullStats');


                    //Full Stats Option
                    ApiClient.getPluginConfiguration(pluginId).then((config) => {
                        //Chapter Insertion Option
                        enableFullStats.checked = config.EnableFullStatistics;
                    });
                    
                    enableFullStats.addEventListener('change',
                        (e) => {
                            e.preventDefault();
                            ApiClient.getPluginConfiguration(pluginId).then((config) => {
                                config.EnableFullStatistics = enableFullStats.checked;
                                ApiClient.updatePluginConfiguration(pluginId, config).then(() => { });
                            });
                        });


                    //Get Statistics on button click
                    runStatsTaskBtn.addEventListener('click', async (e) => {
                        e.preventDefault();
                        loading.show();
                        await reloadItems(view);
                    });
                   
                    await reloadItems(view);
                    
            });

        }
    });