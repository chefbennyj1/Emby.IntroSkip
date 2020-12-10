define(["loading", "dialogHelper", "formDialogStyle"],
    function(loading, dialogHelper) {

        var pluginId = "93A5E794-E0DA-48FD-8D3A-606A20541ED6";
        var iso8601DurationRegex = /(-)?P(?:([.,\d]+)Y)?(?:([.,\d]+)M)?(?:([.,\d]+)W)?(?:([.,\d]+)D)?T(?:([.,\d]+)H)?(?:([.,\d]+)M)?(?:([.,\d]+)S)?/;

        window.parseISO8601Duration = function (iso8601Duration) {
            var matches = iso8601Duration.match(iso8601DurationRegex);

            return {
                minutes: matches[7] === undefined ? 0 : matches[7] < 10 ? "0" + matches[7] : matches[7],
                seconds: matches[8] === undefined ? 0 : matches[8] < 10 ? "0" + matches[8] : matches[8]
            };
        };

        ApiClient.deleteIntroItem = function(seriesId,seasonId, episodeId) {
            var url = this.getUrl('RemoveIntro?EpisodeId=' + episodeId + '&SeasonId=' + seasonId + '&SeriesId=' + seriesId);
            return this.ajax({
                type: "DELETE",
                url: url
            });
        };

        function getSeries() {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('Items?ExcludeLocationTypes=Virtual&Recursive=true&IncludeItemTypes=Series&SortBy=SortName')).then(result => { 
                    resolve(result);
                });
            });
        }

        function getSeasons(seriesId) {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('Items?ExcludeLocationTypes=Virtual&ParentId=' + seriesId + '&IncludeItemTypes=Season&SortBy=SortName')).then(r => {
                    resolve(r);
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

        function getIntros(seriesId, seasonId) {
            return new Promise((resolve, reject) => {

                ApiClient.getJSON(ApiClient.getUrl('SeriesTitleSequences?SeriesId=' + seriesId + "&SeasonId=" + seasonId)).then(result => {
                    resolve(result);
                });

            });
        } 
        
        
        function getTableRowHtml(intro, seriesId, seasonId) {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('Items?Ids=' + intro.InternalId)).then(result => {

                    var html          = '';
                    var episode       = result.Items[0];
                    var startTimespan = parseISO8601Duration(intro.IntroStart);
                    var endTimespan   =  parseISO8601Duration(intro.IntroEnd);

                    html += '<tr data-id="' + episode.Id + '" class="detailTableBodyRow detailTableBodyRow-shaded">';
                    html += '<td data-title="Series" class="detailTableBodyCell fileCell">' + episode.SeriesName + '</td>';
                    html += '<td data-title="Season" class="detailTableBodyCell fileCell">' + episode.SeasonName + '</td>';
                    html += '<td data-title="Season" class="detailTableBodyCell fileCell">Episode: ' + episode.IndexNumber + '</td>';
                    html += '<td data-title="HasIntro" class="detailTableBodyCell fileCell" style="color:' + (intro.HasIntro === true ? "#5EC157" : "orangered") + '">' + intro.HasIntro.toString() + '</td>';
                    html += '<td data-title="Start" class="detailTableBodyCell fileCell">' + startTimespan.minutes + ":" + startTimespan.seconds + '</td>';
                    html += '<td data-title="End" class="detailTableBodyCell fileCell">' + endTimespan.minutes + ":" + endTimespan.seconds + '</td>';
                    html += '<td data-title="Remove" class="detailTableBodyCell fileCell">';
                    html += '<button id="' + episode.Id + '" data-seriesId="' + seriesId + '" data-seasonId="' + seasonId + '" class="fab removeIntroData emby-button"><i class="md-icon">clear</i></button>';
                    html += '</td>';
                    html += '<td class="detailTableBodyCell organizerButtonCell" style="whitespace:no-wrap;"></td>';
                    html += '</tr>';
                    resolve(html);
                });
            });
        }
                      
        function removeIntroItem(seriesId, seasonId, episodeId) {
            return new Promise((resolve, reject) => {
                    ApiClient.deleteIntroItem(seriesId, seasonId, episodeId).then(success => {
                        if (success.statusText === "OK") { 
                            Dashboard.alert("intro removed.");
                        }
                    });
                resolve(true);
            });
        }  

        return function(view) {
            view.addEventListener('viewshow',
                () => {
                    var seriesSelect = view.querySelector('#selectEmbySeries');
                    var seasonSelect = view.querySelector('#selectEmbySeason');
                    getSeries().then(series => {
                        for (let i = 0; i <= series.Items.length - 1; i++) {
                            seriesSelect.innerHTML += '<option value="' + series.Items[i].Id + '">' + series.Items[i].Name + '</option>';
                            getSeasons(series.Items[i].Id).then(seasons => {
                                for (var j = 0; j <= seasons.Items.length - 1; j++) {
                                    
                                    seasonSelect.innerHTML += '<option value="' + seasons.Items[j].Id + '">' + seasons.Items[j].Name + '</option>';

                                    if (i === 0 && j == 0) {
                                        getIntros(series.Items[i].Id, seasons.Items[j].Id).then((result) => {
                                            if (result.EpisodeTitleSequences) {
                                                var averageLength = parseISO8601Duration(result.AverageEpisodeTitleSequenceLength);
                                                view.querySelector('.averageTitleSequenceTime').innerText = averageLength.minutes + ":" + averageLength.seconds;

                                                result.EpisodeTitleSequences.forEach(intro => {
                                                    getTableRowHtml(intro, series.Items[i].Id, seasons.Items[j].Id).then(html => {

                                                        view.querySelector('.introResultBody').innerHTML += html;

                                                        view.querySelectorAll('.removeIntroData i').forEach(btn => {
                                                            btn.addEventListener('click', (elem) => {
                                                                elem.preventDefault();
                                                                var episodeId = elem.target.closest('.fab').id;
                                                                var seriesId = elem.target.closest('.fab').dataset["seriesid"];
                                                                var seasonId = elem.target.closest('.fab').dataset["seasonid"];
                                                                removeIntroItem(seriesId, seasonId, episodeId).then(() => {
                                                                    var index = elem.target.closest('tr').rowIndex;
                                                                    view.querySelector('.introResultBody').deleteRow(index - 1);
                                                                });

                                                            });
                                                        });
                                                    });

                                                });
                                            }
                                        });
                                    }
                                };
                            });



                        };
                    });

                    seasonSelect.addEventListener('change', (e) => {
                        e.preventDefault();
                        view.querySelector('.introResultBody').innerHTML = "";
                        var _seriesId = seriesSelect[seriesSelect.selectedIndex].value;
                        var _seasonId = seasonSelect[seasonSelect.selectedIndex].value;
                        getIntros(_seriesId, _seasonId).then((result) => {
                            if (result.TitleSequences.EpisodeTitleSequences) {
                                var averageLength = parseISO8601Duration(result.AverageEpisodeTitleSequenceLength);
                                view.querySelector('.averageTitleSequenceTime').innerText = averageLength.minutes + ":" + averageLength.seconds;

                                result.TitleSequences.EpisodeTitleSequences.forEach(intro => {
                                    getTableRowHtml(intro, _seriesId, _seasonId).then(html => {
                                        view.querySelector('.introResultBody').innerHTML += html;

                                        view.querySelectorAll('.removeIntroData i').forEach(btn => {
                                            btn.addEventListener('click', (elem) => {
                                                elem.preventDefault();

                                                var episodeId = elem.target.closest('.fab').id;
                                                var seriesId = elem.target.closest('.fab').dataset["seriesid"];
                                                var seasonId = elem.target.closest('.fab').dataset["seasonid"];

                                                removeIntroItem(seriesId, seasonId, episodeId).then(r => {
                                                    var index = elem.target.closest('tr').rowIndex;
                                                    view.querySelector('.introResultBody').deleteRow(index - 1);
                                                });
                                            });
                                        });
                                    });

                                });
                            }
                        });
                    });

                    seriesSelect.addEventListener('change', (e) => {
                        e.preventDefault();
                        seasonSelect.innerHTML = '';
                        var _seriesId = seriesSelect[seriesSelect.selectedIndex].value;
                        getSeasons( _seriesId).then(seasons => {
                            seasons.Items.forEach(season => {
                                seasonSelect.innerHTML += '<option value="' + season.Id + '">' + season.Name + '</option>';
                            });
                            view.querySelector('.introResultBody').innerHTML = '';
                            var _seasonId = seasonSelect[0].value;
                            getIntros(_seriesId, _seasonId).then((result) => {
                                if (result.TitleSequences.EpisodeTitleSequences) {

                                    var averageLength = parseISO8601Duration(result.AverageEpisodeTitleSequenceLength);
                                    view.querySelector('.averageTitleSequenceTime').innerText = averageLength.minutes + ":" + averageLength.seconds;
                                    
                                    result.TitleSequences.EpisodeTitleSequences.forEach(intro => {
                                        getTableRowHtml(intro, _seriesId, _seasonId).then(html => {

                                            view.querySelector('.introResultBody').innerHTML += html;

                                            view.querySelectorAll('.removeIntroData i').forEach(btn => {
                                                btn.addEventListener('click', (elem) => {
                                                    elem.preventDefault();
                                                    var episodeId = elem.target.closest('.fab').id;
                                                    var seriesId = elem.target.closest('.fab').dataset["seriesid"];
                                                    var seasonId = elem.target.closest('.fab').dataset["seasonid"];
                                                    removeIntroItem(seriesId, seasonId, episodeId).then(() => {
                                                        var index = elem.target.closest('tr').rowIndex;
                                                        view.querySelector('.introResultBody').deleteRow(index - 1);
                                                    });

                                                });
                                            });
                                        });

                                    });
                                }
                            });

                        });
                    });
                        
                });
        }
    });