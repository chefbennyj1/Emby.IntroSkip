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

        function getTableRowsHtml(introData) {
            return new Promise((resolve, reject) => {
                for (var i = 0; i <= introData.length - 1; i++) {
                    getEpisodeBaseItem(introData[i].Id).then(episode => {
                        var html = '';

                        html += '<tr class="detailTableBodyRow detailTableBodyRow-shaded" id="' + introData.Id + '">';
                        html += '<td data-title="SeriesName" class="detailTableBodyCell fileCell">' +
                            episode.SeriesName +
                            '</td>';
                        html += '<td data-title="Season" class="detailTableBodyCell fileCell">' +
                            episode.ParentIndexNumber +
                            '</td>';
                        html += '<td data-title="Episode" class="detailTableBodyCell fileCell">' +
                            episode.IndexNumber +
                            '</td>';
                        html += '<td data-title="HasIntro" class="detailTableBodyCell fileCell">' +
                            introData.HasIntro +
                            '</td>';
                        html += '<td data-title="IntroStart" class="detailTableBodyCell fileCell">' +
                            introData.IntroStart +
                            '</td>';
                        html += '<td data-title="IntroEnd" class="detailTableBodyCell fileCell">' +
                            introData.IntroEnd +
                            '</td>';  

                        return html;
                    });
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


                    var seriesSelect = view.querySelector('#selectSeriesList');
                    var episodeList = view.querySelector('#selectEpisodeList');
                    ApiClient.getJSON(ApiClient.getUrl("Items?Recursive=true&IncludeItemTypes=Series")).then((result) => {
                        
                        result.Items.forEach(item => {
                            var option = '<option value="' + item.Id + '">' + item.Name + '</option>';
                            seriesSelect.innerHTML += option;
                        });
                    });
                      //&Fields=Path
                    seriesSelect.addEventListener('change', (e) => {
                        ApiClient.getJSON(ApiClient.getUrl("Items?Recursive=true&ParentId=" + seriesSelect.value + '&IncludeItemTypes=Episode')).then((result) => {
                            result.Items.forEach(item => {
                                var option = '<option value="' + item.Id + '">' + item.Name + ' - ' + item.SeasonName + ' - Episode ' + item.IndexNumber + '</option>';
                                episodeList.innerHTML += option; 
                            });
                        });
                    });

                    view.querySelector('.addBtn').addEventListener('click', e => {

                        var introData = {
                            Id: episodeList.value
                        }

                        ApiClient.getPluginConfiguration(pluginId).then(
                            (config) => {
                                if (!config.Intros) {
                                    var intros = [];
                                    intros.push(introData);
                                    config.Intros = intros;
                                } else {
                                    config.Intros.push(introData);
                                }
                                ApiClient.updatePluginConfiguration(pluginId, config).then(function() {});
                            });

                    });

                    
                   
                });
        }
    });