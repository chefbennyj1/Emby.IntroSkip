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


        function getSeasonStatistics() {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl(`GetSeasonStatistics`)).then(result => {
                    resolve(result);
                });
            });
        }

        loading.show();
        getSeasonStatistics().then(() => {
            loading.hide();
        });

        //really doesn't like async methods
        /*async function getSeasonStatistics() {
            return await ApiClient.getJSON(ApiClient.getUrl(`GetSeasonStatistics`));
        }*/

        function HasIssueIcon(confirmed) {
            return (confirmed ?
                "stroke='black' stroke-width='1' fill='red'" :
                "stroke='black' stroke-width='1' fill='mediumseagreen'");
        }

        function reloadItems(view) {
            view.querySelector('.tblStatsResultBody').innerHTML = '';
            var statisticsResultTable = view.querySelector('.tblStatsResultBody');
            
            getSeasonStatistics().then(statResults => {
                statResults.forEach(statItem => {
                    statisticsResultTable.innerHTML += renderTableRowHtml(statItem);
                });
                
            });
            //sortTable(view);
        }

        
        function renderTableRowHtml(statElement) {

            var html = '';
            var date = datetime.parseISO8601Date(statElement.Date, true);
            var startTimespan = parseISO8601Duration(statElement.IntroDuration);
            

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

            var duration = "00:" + startTimespan.minutes + ":" + startTimespan.seconds;
            html += '<td data-title="Duration" class="detailTableBodyCell fileCell">' + duration + '</td>';

            html += '<td data-title="Results" class="detailTableBodyCell fileCell">' + statElement.PercentDetected + "%" + '</td>';

            html += '<td data-title="Comments" class="detailTableBodyCell fileCell">' + statElement.Comment + '</td>';

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
                    var runStatsTaskBtn = view.querySelector('.runStatsTaskBtn');

                    //Load the list on launch
                    //reloadItems(view);

                    //update the list on button click
                    runStatsTaskBtn.addEventListener('click', (e) => {
                        e.preventDefault();
                        loading.show();
                        reloadItems(view);
                        loading.hide();
                    });
                    loading.hide();
            });

        }
    });