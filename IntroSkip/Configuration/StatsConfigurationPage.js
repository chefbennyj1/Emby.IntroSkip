define(["loading", "dialogHelper", "mainTabsManager", "datetime", "formDialogStyle", "emby-checkbox", "emby-select", "emby-toggle"],
    function(loading, dialogHelper, mainTabsManager, datetime) {

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
                }
            ];
        }


        var pluginId = "93A5E794-E0DA-48FD-8D3A-606A20541ED6";

        function sortTable(view) {
            var rows, switching, i, x, y, shouldSwitch;
            const table = view.querySelector('.tblStatsResultBody');
            switching = true;
            /* Make a loop that will continue until
            no switching has been done: */
            while (switching) {
                // Start by saying: no switching is done:
                switching = false;
                rows = table.rows;
                /* Loop through all table rows (except the
                first, which contains table headers): */
                for (i = 1; i < (rows.length - 1); i++) {
                    // Start by saying there should be no switching:
                    shouldSwitch = false;
                    /* Get the two elements you want to compare,
                    one from current row and one from the next: */
                    x = parseInt(rows[i].getElementsByTagName("TD")[2].dataset.index);
                    y = parseInt(rows[i + 1].getElementsByTagName("TD")[2].dataset.index);
                    // Check if the two rows should switch place:
                    if (x > y) {
                        // If so, mark as a switch and break the loop:
                        shouldSwitch = true;
                        break;
                    }
                }
                if (shouldSwitch) {
                    /* If a switch has been marked, make the switch
                    and mark that a switch has been done: */
                    rows[i].parentNode.insertBefore(rows[i + 1], rows[i]);
                    switching = true;
                }
            }
        }

        async function getSeries() {
            return await ApiClient.getJSON(ApiClient.getUrl(
                'Items?ExcludeLocationTypes=Virtual&Recursive=true&IncludeItemTypes=Series&SortBy=SortName'));
        }

        async function getSeasons(seriesId) {
            return await ApiClient.getJSON(ApiClient.getUrl(
                `Items?ExcludeLocationTypes=Virtual&ParentId=${seriesId}&IncludeItemTypes=Season&SortBy=SortName`));
        }

        function getBaseItem(id) {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('Items?Ids=' + id)).then(result => {
                    resolve(result);
                });
            });
        }

        function getSeasonStatics() {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl(`GetSeasonStatics?SeasonId=11950`)).then(result => {
                    resolve(result);
                });
            });
        }

        function HasIssueIcon(confirmed) {
            return (confirmed ?
                "stroke='black' stroke-width='1' fill='red'" :
                "stroke='black' stroke-width='1' fill='mediumseagreen'");
        }

        function reloadItems(seasonStats, view) {
             view.querySelector('.tblStatsResultBody').innerHTML = '';
             seasonStats.forEach(statElement =>
            {
                var html = renderTableRowHtml(statElement);
                view.querySelector('.tblStatsResultBody').innerHTML += html;
            });
            //sortTable(view);
        }

        function renderTableRowHtml(statElement) {

            var html = '';
            var date = datetime.parseISO8601Date(statElement.Date, true);
            

            //html += '<tr data-id="' + statItem.SeasonId + '" class="detailTableBodyRow detailTableBodyRow-shaded">';
            //html += '<td data-title="WTFBuster" class="detailTableBodyCell" >';
            html += '<br/>';

            html += '<td data-title="Has Issue" class="detailTableBodyCell fileCell">';
            html += '<svg width="30" height="30">';
            html += '<circle cx="15" cy="15" r="10"' + HasIssueIcon(statElement.HasIssue) + '" />';
            html += '</svg>';
            html += '</td>';

            html += '<td data-title="TV Show" class="detailTableBodyCell fileCell">' + statElement.TVShowName + '</td>';

            html += '<td data-title="Season" class="detailTableBodyCell fileCell">' + statElement.Season + '</td>';

            html += '<td data-title="No. Episodes" class="detailTableBodyCell fileCell">' + statElement.EpisodeCount + '</td>';

            html += '<td data-title="Results" class="detailTableBodyCell fileCell">' + statElement.PercentDetected + '</td>';

            html += '<td data-title="Date" class="detailTableBodyCell">';
            html += '<span>' + datetime.toLocaleDateString(date) + '</span>';
            html += '</td>';

            return html;
        }

        return function(view) {
            view.addEventListener('viewshow',
                async () => {

                    loading.show();
                    //Set Menu Tabs, using # for zerobased index
                    mainTabsManager.setTabs(this, 3, getTabs);

                    //elements
                    //var statisticsResultTable = view.querySelector('.tblStatsResultBody');
                    var runStatsTaskBtn = view.querySelector('.runStatsTaskBtn');

                    runStatsTaskBtn.addEventListener('click',
                        (e) => {
                            e.preventDefault();
                            var result = getSeasonStatics();
                            if (result) {
                                if (result.SeasonStats) {
                                    var seasonStats = result.SeasonStats;
                                    reloadItems(seasonStats, view);
                                }
                            }
                        });

                    var result = getSeasonStatics();
                    if (result) {
                        var seasonStats = result.SeasonStats;
                        reloadItems(seasonStats, view);
                    }

                    loading.hide();
            });

        }
    });