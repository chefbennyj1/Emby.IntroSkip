define(["loading", "dialogHelper", "formDialogStyle"],
    function(loading, dialogHelper) {

        var pluginId = "93A5E794-E0DA-48FD-8D3A-606A20541ED6";
                
        function getEpisodeBaseItem(id) {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('Items?Ids=' + id)).then(result => {
                    resolve(result);
                });
            });
        } 

        function getTableRows(intros) {
            return new Promise((resolve, reject) => {
                    function getTorrentResultTableHtml(torrents) {
            var html = '';
            intros.forEach(intro => {
                getEpisodeBaseItem(intro.InternalId).then(result => {
                    html += '<tr class="detailTableBodyRow detailTableBodyRow-shaded" id="' + intro.InternalId + '">';
                    html += '<td data-title="Series" class="detailTableBodyCell fileCell">' + result.Name + '</td>';
                    
                    html += '<td data-title="Remove" class="detailTableBodyCell fileCell">';
                    html += '<button id="' +
                        torrent.Hash +
                        '" class="fab removeTorrent emby-button"><i class="md-icon">clear</i></button>';
                    html += '</td>';

                    html += '<td class="detailTableBodyCell organizerButtonCell" style="whitespace:no-wrap;"></td>';

                    html += '</tr>';
                });
            });

            return html;
        }

            });
        }

        return function(view) {
            view.addEventListener('viewshow',
                () => {
                    ApiClient.getPluginConfiguration(pluginId).then(
                        (config) => {
                            var tableResults = view.querySelector('.introResultBody');
                            if (config.Intros && config.Intros.length) {
                                tableResults.InnerHTML += getTableRowsHtml(config.Intros); 
                            }
                            if (config.SavedIntros && config.SavedIntros.length) {
                                tableResults.InnerHTML += getTableRowsHtml(config.SavedIntros);
                            }
                        });
                });
        }
    });