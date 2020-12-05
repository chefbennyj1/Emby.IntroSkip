define(["loading", "dialogHelper", "formDialogStyle"],
    function(loading, dialogHelper) {

        var pluginId = "93A5E794-E0DA-48FD-8D3A-606A20541ED6";
        function getSeries() {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('Items?Recursive=true&IncludeItemTypes=Series')).then(results => {
                    resolve(results);
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

        function getIntros(seriesId) {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('SeriesTitleSequences?SeriesInternalId=' + seriesId)).then(result => {
                    resolve(result);
                });
            });
        } 
           
        function getTableRowHtml(moment, intro) {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('Items?Ids=' + intro.InternalId)).then(result => {
                    var html = '';
                    var episode = result.Items[0];
                   
                    html += '<tr class="detailTableBodyRow detailTableBodyRow-shaded">';
                    html += '<td data-title="Series" class="detailTableBodyCell fileCell">' + episode.SeriesName + '</td>';
                    html += '<td data-title="Season" class="detailTableBodyCell fileCell">' + episode.SeasonName + '</td>';
                    html += '<td data-title="Season" class="detailTableBodyCell fileCell">Episode: ' + episode.IndexNumber + '</td>';
                    html += '<td data-title="HasIntro" class="detailTableBodyCell fileCell">' + intro.HasIntro.toString() + '</td>';
                    html += '<td data-title="Start" class="detailTableBodyCell fileCell">' + moment.duration(intro.IntroStart) + '</td>';
                    html += '<td data-title="End" class="detailTableBodyCell fileCell">' + moment.duration(intro.IntroEnd) + '</td>';
                    html += '<td data-title="Remove" class="detailTableBodyCell fileCell">';
                    html += '<button id="' + episode.InternalId + '" class="fab removeIntroData emby-button"><i class="md-icon">clear</i></button>';
                    html += '</td>';
                    html += '<td class="detailTableBodyCell organizerButtonCell" style="whitespace:no-wrap;"></td>';
                    html += '</tr>';
                    resolve(html);
                });
            });
        }

        return function(view) {
            view.addEventListener('viewshow',
                () => {
                    var seriesSelect = view.querySelector('#selectEmbySeries');

                    require([Dashboard.getConfigurationResourceUrl('momentJs')],
                        (moment) => {  
                           
                            getSeries().then(series => {
                                series.Items.forEach(item => {
                                    seriesSelect.innerHTML += '<option value="' + item.Id + '">' + item.Name + '</option>';
                                });
                                getIntros(series.Items[0].Id).then((result) => { 
                                    var tableResults = view.querySelector('.introResultBody');
                                    result.forEach(intro => {
                                        getTableRowHtml(moment, intro).then(html => {
                                            tableResults.innerHTML += html;
                                        });
                                    });
                                });
                            }); 

                            seriesSelect.addEventListener('change',
                                (e) => {
                                    e.preventDefault();
                                    var tableResults = view.querySelector('.introResultBody');
                                    tableResults.innerHTML = "";
                                    getIntros(seriesSelect[seriesSelect.selectedIndex].value).then((results) => { 
                                       
                                        results.forEach(intro => {
                                            getTableRowHtml(moment, intro).then(html => {
                                                tableResults.innerHTML += html;
                                            });
                                        });
                                    });
                                });
                             
                           
                        });
                });
        }
    });