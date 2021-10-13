define(["loading", "dialogHelper", "mainTabsManager", "datetime", "formDialogStyle", "emby-checkbox", "emby-select", "emby-toggle"],
    function (loading, dialogHelper, mainTabsManager, datetime) {

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
                    href: Dashboard.getConfigurationPageUrl('StatsConfigurationPage'),
                    name: 'Stats'
                }];
        }
        

        var pluginId = "93A5E794-E0DA-48FD-8D3A-606A20541ED6";

        async function getSeries() {
            return await ApiClient.getJSON(ApiClient.getUrl('Items?ExcludeLocationTypes=Virtual&Recursive=true&IncludeItemTypes=Series&SortBy=SortName'));
        }

        async function getSeasonStatics() {
            return await ApiClient.getJSON(ApiClient.getUrl(`GetSeasonStatics?SeasonId=11950`));
        }

        function HasIssueIcon(confirmed) {
            return (confirmed ?
                "stroke='black' stroke-width='1' fill='var(--theme-primary-color)'" :
                "stroke='black' stroke-width='1' fill='orange'");
        }

        async function GetStatItems(detectionStats, view) {
            view.querySelector('.tblStatsResultBody').innerHTML = '';
            detectionStats.forEach(async (stat) => {
                var html = await renderTableRowHtml(stat);
                view.querySelector('.tblStatsResultBody').innerHTML += html;

                //sortTable(view);               

            });
        }

        async function renderTableRowHtml() {

            var html = '';
            var date = datetime.parseISO8601Date(errItem.Date, true);

            //html += '<td data-title="WTFBuster" class="detailTableBodyCell" >';
            html += '<br/>';

            html += '<td data-title="Confirmed" class="detailTableBodyCell fileCell">';
            html += '<svg width="30" height="30">';
            html += '<circle cx="15" cy="15" r="10"' + HasIssueIcon(item.HasIssue) + '" />';
            html += '</svg>';
            html += '</td>';

            html += '<td data-title="Date" class="detailTableBodyCell">';
            html += '<span>' + datetime.toLocaleDateString(date) + '</span>';
            html += '</td>';

            html += '<td data-title="Series" class="detailTableBodyCell fileCell">' + item.TVShowName + '</td>';

            html += '<td data-title="Season" class="detailTableBodyCell fileCell">' + item.Season + '</td>';

            html += '<td data-title="Count" class="detailTableBodyCell fileCell">' + item.EpisodeCount + '</td>';

            html += '<td data-title="Detection" class="detailTableBodyCell fileCell">' + item.PercentDetected + '</td>';
            
            return html;


        }



        return function (view) {
            view.addEventListener('viewshow', async () => {

                loading.show();
                mainTabsManager.setTabs(this, 3, getTabs);

                var result = await getSeasonStatics();
                //elements
                var statisticsResultTable = view.querySelector('.tblStatsResultBody');
                var runStatsTaskBtn = view.querySelector('.runStatsTaskBtn');

                runStatsTaskBtn.addEventListener('click', (e) => {
                    e.preventDefault();
                    getSeasonStatics();
                    //var stats = result.DetectionStats;
                    //await GetStatItems(stats, view);
                });

                //Chapter Error Result Table
                //statisticsResultTable.innerHTML += renderTableRowHtml(statItem);
                                      

                loading.hide();
            });

        }
    });