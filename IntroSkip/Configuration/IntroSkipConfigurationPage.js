define(["loading", "dialogHelper", "formDialogStyle"],
    function(loading, dialogHelper) {

        var pluginId = "93A5E794-E0DA-48FD-8D3A-606A20541ED6";
        var iso8601DurationRegex = /(-)?P(?:([.,\d]+)Y)?(?:([.,\d]+)M)?(?:([.,\d]+)W)?(?:([.,\d]+)D)?T(?:([.,\d]+)H)?(?:([.,\d]+)M)?(?:([.,\d]+)S)?/;

        window.parseISO8601Duration = function (iso8601Duration) {
            var matches = iso8601Duration.match(iso8601DurationRegex);

            return {
                minutes: matches[7] === undefined ? 0 : matches[7],
                seconds: matches[8] === undefined ? 0 : matches[8]
            };
        };

        ApiClient.deleteIntroItem = function(id) {
            var url = this.getUrl("RemoveIntro?InternalId=" + id);
            return this.ajax({
                type: "DELETE",
                url: url
            });
        };

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
           
        function getTableRowHtml(intro) {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('Items?Ids=' + intro.InternalId)).then(result => {
                    var html = '';
                    var episode = result.Items[0];
                    var startTimespan = parseISO8601Duration(intro.IntroStart);
                    var endTimespan =  parseISO8601Duration(intro.IntroEnd);
                    html += '<tr data-id="' + episode.Id + '" class="detailTableBodyRow detailTableBodyRow-shaded">';
                    html += '<td data-title="Series" class="detailTableBodyCell fileCell">' + episode.SeriesName + '</td>';
                    html += '<td data-title="Season" class="detailTableBodyCell fileCell">' + episode.SeasonName + '</td>';
                    html += '<td data-title="Season" class="detailTableBodyCell fileCell">Episode: ' + episode.IndexNumber + '</td>';
                    html += '<td data-title="HasIntro" class="detailTableBodyCell fileCell">' + intro.HasIntro.toString() + '</td>';
                    html += '<td data-title="Start" class="detailTableBodyCell fileCell">' + startTimespan.minutes + ":" + startTimespan.seconds + '</td>';
                    html += '<td data-title="End" class="detailTableBodyCell fileCell">' + endTimespan.minutes + ":" + endTimespan.seconds + '</td>';
                    html += '<td data-title="Remove" class="detailTableBodyCell fileCell">';
                    html += '<button id="' + episode.Id + '" class="fab removeIntroData emby-button"><i class="md-icon">clear</i></button>';
                    html += '</td>';
                    html += '<td class="detailTableBodyCell organizerButtonCell" style="whitespace:no-wrap;"></td>';
                    html += '</tr>';
                    resolve(html);
                });
            });
        }

        function removeTableRow(view) {
            //view.querySelector('.introResultBody').innerHTML = '';
            //var seriesSelect = view.querySelector('#selectEmbySeries');
            //getIntros(seriesSelect[seriesSelect.selectedIndex].value).then((results) => {
            //    results.forEach(intro => {
            //        getTableRowHtml(intro).then(html => {
            //            view.querySelector('.introResultBody').innerHTML += html;
            //            view.querySelectorAll('.removeIntroData i').forEach(btn => { 
            //                btn.addEventListener('click',
            //                    (elem) => {
            //                        elem.preventDefault();
            //                        var id = elem.target.closest('.fab').id;
            //                        removeIntroItem(id);
            //                        removeTableRow(id, view);
            //                    });
            //            });
            //        });
            //    });
            //});
        }

        function removeIntroItem(id) {
            return new Promise((resolve, reject) => {
                    ApiClient.deleteIntroItem(id).then(success => {
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
                    
                            getSeries().then(series => {
                                series.Items.forEach(item => {
                                    seriesSelect.innerHTML += '<option value="' + item.Id + '">' + item.Name + '</option>';
                                });
                                getIntros(series.Items[0].Id).then((result) => {
                                    result.forEach(intro => {
                                        getTableRowHtml(intro).then(html => {
                                            view.querySelector('.introResultBody').innerHTML += html;
                                            view.querySelectorAll('.removeIntroData i').forEach(btn => {
                                                btn.addEventListener('click',
                                                    (elem) => {
                                                        elem.preventDefault();
                                                        var id = elem.target.closest('.fab').id;
                                                        removeIntroItem(id).then(r => {
                                                            var index = elem.target.closest('tr').rowIndex;
                                                            view.querySelector('.introResultBody').deleteRow(index -1);
                                                        });
                                                        
                                                    });
                                            });
                                        });
                                    });
                                });
                            });


                            seriesSelect.addEventListener('change',
                                (e) => {
                                    e.preventDefault();
                                    view.querySelector('.introResultBody').innerHTML = "";
                                    getIntros(seriesSelect[seriesSelect.selectedIndex].value).then((results) => {
                                        results.forEach(intro => {
                                            getTableRowHtml(intro).then(html => {
                                                view.querySelector('.introResultBody').innerHTML += html;
                                                view.querySelectorAll('.removeIntroData i').forEach(btn => { 
                                                    btn.addEventListener('click',
                                                        (elem) => {
                                                            elem.preventDefault();
                                                            var id = elem.target.closest('.fab').id;
                                                            removeIntroItem(id).then(r => {
                                                                var index = elem.target.closest('tr').rowIndex;
                                                                view.querySelector('.introResultBody').deleteRow(index -1);
                                                            }); 
                                                        });
                                                });
                                            });
                                        });
                                    });
                                });

                        
                });
        }
    });