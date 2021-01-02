define(["loading", "dialogHelper", "formDialogStyle", "emby-checkbox", "emby-select", "emby-toggle"],
    function(loading, dialogHelper) {

        var pluginId = "93A5E794-E0DA-48FD-8D3A-606A20541ED6";
        var iso8601DurationRegex = /(-)?P(?:([.,\d]+)Y)?(?:([.,\d]+)M)?(?:([.,\d]+)W)?(?:([.,\d]+)D)?T(?:([.,\d]+)H)?(?:([.,\d]+)M)?(?:([.,\d]+)S)?/;

        window.parseISO8601Duration = function (iso8601Duration) {
            var matches = iso8601Duration.match(iso8601DurationRegex);

            return {
                minutes: matches[7] === undefined ? "00" : matches[7] < 10 ? "0" + matches[7] : matches[7],
                seconds: matches[8] === undefined ? "00" : matches[8] < 10 ? "0" + matches[8] : matches[8]
            };
        };

        ApiClient.deleteAll = function() {
            var url = this.getUrl('RemoveAll');
            return this.ajax({
                type: "DELETE",
                url: url
            });
        };

        ApiClient.deleteIntroItem = function(episodeId) {
            var url = this.getUrl('RemoveIntro?InternalId=' + episodeId);
            return this.ajax({
                type: "DELETE",
                url: url
            });
        };

        ApiClient.deleteIntroItemAndFingerprint = function(episodeId) {
            var url = this.getUrl('RemoveFingerprint?InternalId=' + episodeId);
            return this.ajax({
                type: "DELETE",
                url: url
            });
        };

        ApiClient.deleteSeasonFingerprintData = function(seasonId) {
            var url = this.getUrl('RemoveSeasonFingerprints?SeasonId=' + seasonId);
            return this.ajax({
                type: "DELETE",
                url: url
            });
        };

        ApiClient.getPrimaryImageUrl = function(seriesId) {
            var url = this.getUrl('Items/' +
                seriesId +
                '/Images/Primary?maxHeight=327&amp;maxWidth=236&amp;quality=90');
            return url;
        }

        function openConfirmationDialog() {
            var confirmDlg = dialogHelper.createDialog({
                size: "medium-tall",
                removeOnClose: !1,
                scrollY: true
            });

            confirmDlg.classList.add("formDialog");
            confirmDlg.classList.add("ui-body-a");
            confirmDlg.classList.add("background-theme-a");
            confirmDlg.style.maxWidth = "27%";
            confirmDlg.style.maxHeight = "32%";

            var html = "";
            html += '<div class="formDialogHeader" style="display:flex">';
            html += '<button is="paper-icon-button-light" class="btnCloseDialog autoSize paper-icon-button-light" tabindex="-1"><i class="md-icon">arrow_back</i></button><h3 class="formDialogHeaderTitle"></h3>';
            html += '</div>';
            html += '<div class="formDialogContent" style="margin:2em">';
            html += '<div class="dialogContentInner" style="padding:0">';
            html += '<div style="text-align:center"';

            html += '<h1>You are about to remove all title sequence data.</h1>';
            html += '<h1>Are you sure?</h1>';

            html += '<div style="display:flex">';

            html += '<button is="emby-button" type="submit" class="btnOk raised button-submit block emby-button" style="max-width:40%; margin-right:20%">';
            html += '<span>OK</span>';
            html += '</button>';

            html += '<button is="emby-button" type="submit" class="btnCancel raised button-submit block emby-button" style="max-width:40%">';
            html += '<span>Cancel</span>';
            html += '</button>';

            html += '</div>';

            html += '</div>';
            html += '</div>';

            confirmDlg.innerHTML = html;
            dialogHelper.open(confirmDlg);

            confirmDlg.querySelector('.btnCloseDialog').addEventListener('click',
                (e) => {
                    e.preventDefault();
                    dialogHelper.close(confirmDlg);
                });

            confirmDlg.querySelector('.btnCancel').addEventListener('click',
                (e) => {
                    e.preventDefault();
                    dialogHelper.close(confirmDlg);
                });

            confirmDlg.querySelector('.btnOk').addEventListener('click',
                (e) => {
                    loading.show();
                    ApiClient.deleteAll().then(result => {
                        loading.hide();
                        Dashboard.alert("All data removed.");
                        dialogHelper.close(confirmDlg); 
                    })
                });

        }

        function openSettingsDialog() {
            loading.show();

            var dlg = dialogHelper.createDialog({
                size: "medium-tall",
                removeOnClose: !1,
                scrollY: true
            });

            dlg.classList.add("formDialog");
            dlg.classList.add("ui-body-a");
            dlg.classList.add("background-theme-a");
            dlg.style.maxWidth = "45%";
            dlg.style.maxHeight = "80%";

            var html = '';

            html += '<div class="formDialogHeader" style="display:flex">';
            html += '<button is="paper-icon-button-light" class="btnCloseDialog autoSize paper-icon-button-light" tabindex="-1"><i class="md-icon">arrow_back</i></button><h3 class="formDialogHeaderTitle">Advanced settings</h3>';
            html += '</div>';

            html += '<div class="formDialogContent" style="margin:2em">';
            html += '<div class="dialogContentInner" style="max-height: 42em;">';
            html += '<div style="flex-grow:1;">';
            
            html += '<div class="inputContainer">';
            html += '<label style="width: auto;" class="mdl-switch mdl-js-switch">';
            html += '<input is="emby-toggle" type="checkbox" id="enableItemAddedEvent"  class="chkitemAddedEvent noautofocus mdl-switch__input" data-embytoggle="true">';
            html += '<span class="toggleButtonLabel mdl-switch__label">Enable new episode auto scan</span>';
            html += '<div class="mdl-switch__trackContainer">';
            html += '<div class="mdl-switch__track"></div>';
            html += '<div class="mdl-switch__thumb">';
            html += '<span class="mdl-switch__focus-helper"></span>';
            html += '</div>';
            html += '</div>';
            html += '</label>';
            html += '<div class="fieldDescription">';
            html += 'Auto scan episodes after they are added to the library automatically.';
            html += '</div>';
            html += '</div>';

            html += '<div class="inputContainer">';
            html += '<label class="inputLabel inputLabelUnfocused" for="txtMaxDegreeOfParralelism">Maximum parralel series to process at once:</label> ';
            html += '<input type="number" id="txtMaxDegreeOfParralelism" min="2" max="15" step="1" label="Maximum series to proccess at once:" class="emby-input">';
            html += '<div class="fieldDescription">';
            html += 'The number of series to attempt to proccess at once. Lower powered machines should keep the default of 2.';
            html += '</div>';
            html += '</div>';

              
            html += '<div class="inputContainer">';
            html += '<label class="inputLabel inputLabelUnfocused" for="txtTitleSequenceThreshold">Title sequence duration threshold (seconds):</label> ';
            html += '<input type="number" id="txtTitleSequenceThreshold" min="5" max="15" step="1" label="Title sequence duration threshold (seconds):" class="emby-input">';
            html += '<div class="fieldDescription">';
            html += 'The duration threshold for accepted title sequence lengths. Any match with a duration less then this number will be ignored.';
            html += '</div>';
            html += '</div>';
             
            html += '<div class="inputContainer">';
            html += '<label class="inputLabel inputLabelUnfocused" for="txtTitleSequenceEncodingLength">Title sequence audio encoding length (minutes):</label> ';
            html += '<input type="number" id="txtTitleSequenceEncodingLength" min="10" max="15" step="1" label="Title sequence encoding duration (minutes):" class="emby-input">';
            html += '<div class="fieldDescription">';
            html += 'The duration of episode audio encoding used to find title sequences. Default is 10 minutes. A longer encoding may match episodes with title sequences which appear later in the stream, but will cause longer scans.';
            html += '</div>';
            html += '</div>';


            html += '<div class="inputContainer">';
            html += '<button is="emby-button" type="submit" class="removeAllData raised button-submit block emby-button">';
            html += '<span>Reset title sequence data</span>';
            html += '</button>';
            html += '<div class="fieldDescription">';
            html += 'Remove all title sequence related data and start from scratch.';
            html += '</div>';
            html += '</div>';
               

            html += '</div>';
            html += '</div>';

            dlg.innerHTML = html;
            dialogHelper.open(dlg);

            
            var titleSequenceThresholdInput = dlg.querySelector('#txtTitleSequenceThreshold');
            var titleSequenceEncodingLength = dlg.querySelector('#txtTitleSequenceEncodingLength');
            var maxDegreeOfParralelism      = dlg.querySelector('#txtMaxDegreeOfParralelism');
            var enableItemAddedEventToggle  = dlg.querySelector('#enableItemAddedEvent');
            var removeAllButton             = dlg.querySelector('.removeAllData');

            ApiClient.getPluginConfiguration(pluginId).then((config) => {
                titleSequenceThresholdInput.value  = config.TitleSequenceLengthThreshold ? config.TitleSequenceLengthThreshold : 10.5;
                titleSequenceEncodingLength.value  = config.EncodingLength               ? config.EncodingLength               : 15;
                maxDegreeOfParralelism.value       = config.MaxDegreeOfParallelism       ? config.MaxDegreeOfParallelism       : 2;
                enableItemAddedEventToggle.checked = config.EnableItemAddedTaskAutoRun;
            });

            removeAllButton.addEventListener('click', (e) => {
                    e.preventDefault();
                    openConfirmationDialog();
                });

            enableItemAddedEventToggle.addEventListener('change', (e) => {
                e.preventDefault();
                ApiClient.getPluginConfiguration(pluginId).then((config) => {
                    config.EnableItemAddedTaskAutoRun = enableItemAddedEventToggle.checked;
                    ApiClient.updatePluginConfiguration(pluginId, config).then(() => { });
                });
            }); 

            maxDegreeOfParralelism.addEventListener('change', (e) => {
                e.preventDefault();
                ApiClient.getPluginConfiguration(pluginId).then((config) => {
                    config.MaxDegreeOfParallelism = maxDegreeOfParralelism.value;
                    ApiClient.updatePluginConfiguration(pluginId, config).then(() => { });
                });
            }); 

            titleSequenceThresholdInput.addEventListener('change', (e) => {
                e.preventDefault();
                ApiClient.getPluginConfiguration(pluginId).then((config) => {
                    config.TitleSequenceLengthThreshold = titleSequenceThresholdInput.value;
                    ApiClient.updatePluginConfiguration(pluginId, config).then(() => { });
                });
            }); 

            titleSequenceEncodingLength.addEventListener('change', (e) => {
                e.preventDefault();
                ApiClient.getPluginConfiguration(pluginId).then((config) => {
                    config.EncodingLength = titleSequenceEncodingLength.value;
                    ApiClient.updatePluginConfiguration(pluginId, config).then(() => { });
                });
            });

            dlg.querySelector('.btnCloseDialog').addEventListener('click',() => {
                dialogHelper.close(dlg);
            });

            loading.hide();
        }  

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
        
        function getIntros(seasonId) {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('SeasonTitleSequences?SeasonId=' + seasonId)).then(result => {
                    resolve(result);
                });

            });
        } 
        
        function getTableRowHtml(intro) {
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
                    html += '<td data-title="HasIntro" class="detailTableBodyCell fileCell" style="color:' + (intro.HasIntro === true ? "#5EC157" : "") + '">' + intro.HasIntro.toString() + '</td>';
                    html += '<td data-title="Start" class="detailTableBodyCell fileCell">' + "00:" + startTimespan.minutes + ":" + startTimespan.seconds + '</td>';
                    html += '<td data-title="End" class="detailTableBodyCell fileCell">' + "00:" + endTimespan.minutes + ":" + endTimespan.seconds + '</td>';
                    html += '<td data-title="Remove" class="detailTableBodyCell fileCell">';
                    html += '<p style="margin: .6em 0; vertical-align: middle; display: inline-block;">Remove Title Sequence</p>';
                    html += '<button style="margin-left: 1em;" id="' + episode.Id + '" class="fab removeIntroData emby-button"><i class="md-icon">clear</i></button>';
                    html += '</td>'; 
                    html += '<td data-title="RemoveFingerprint" class="detailTableBodyCell fileCell">';
                    html += '<p style="margin: .6em 0; vertical-align: middle; display: inline-block;">Remove Title Sequence and Fingerprint</p>';
                    html += '<button style="margin-left: 1em;" id="' + episode.Id + '" class="fab removeFingerprint emby-button" style="color:orangered"><i class="md-icon">error_outline</i></button>';
                    html += '</td>';
                    html += '<td class="detailTableBodyCell organizerButtonCell" style="whitespace:no-wrap;"></td>';
                    html += '</tr>';
                    resolve(html);
                });
            });
        }
                      
        function removeIntroItem(episodeId) {
            return new Promise((resolve, reject) => {
                    ApiClient.deleteIntroItem(episodeId).then(result => {
                        if (result.statusText === "OK") { 
                            Dashboard.alert("Title sequence removed.");
                        }
                    });
                resolve(true);
            });
        }  
        
        function removeIntroItemAndFingerprint(episodeId) {
            return new Promise((resolve, reject) => {
                ApiClient.deleteIntroItemAndFingerprint(episodeId).then(result => {
                    if (result.statusText === "OK") { 
                        Dashboard.alert("Title sequence removed.");
                    }
                });
                resolve(true);
            });
        }  

        function sortTable(view) {
            var table, rows, switching, i, x, y, shouldSwitch;
            table = view.querySelector(".tblEpisodeIntroResults");
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
                    x = rows[i].getElementsByTagName("TD")[2];
                    y = rows[i + 1].getElementsByTagName("TD")[2];
                    // Check if the two rows should switch place:
                    if (parseInt(x.innerHTML.toLowerCase().split('episode: ')[1], 10) > parseInt(y.innerHTML.toLowerCase().split('episode: ')[1], 10)) {
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

        return function(view) {
            view.addEventListener('viewshow', () => {

                var _seriesId, _seasonId, _seasonIndexNumber;

                var seriesSelect                    = view.querySelector('#selectEmbySeries');
                var seasonSelect                    = view.querySelector('#selectEmbySeason');
                var settingsButton                  = view.querySelector('#openSettingsDialog');
                var removeSeasonalFingerprintButton = view.querySelector('.removeSeasonalFingerprintData');
                
                getSeries().then(series => {

                    for (let i = 0; i <= series.Items.length - 1; i++) {

                        seriesSelect.innerHTML += '<option value="' + series.Items[i].Id + '">' + series.Items[i].Name + '</option>';
                    }

                    _seriesId = seriesSelect[seriesSelect.selectedIndex].value;

                    view.querySelector('.seriesImg').style = 'background-image: url(' + ApiClient.getPrimaryImageUrl(_seriesId) + ');background-repeat: no-repeat;width: 171px;height: 237px;margin: 0px 4em 3em;box-shadow: rgba(0, 0, 0, 0.5) 1px 1px 14px;background-size: contain;';
                    
                    getSeasons(_seriesId).then(seasons => {

                        for (var j = 0; j <= seasons.Items.length - 1; j++) {
                            seasonSelect.innerHTML += '<option data-index="' + seasons.Items[j].IndexNumber + '" value="' + seasons.Items[j].Id + '">' + seasons.Items[j].Name + '</option>';
                        }

                        _seasonId = seasonSelect[seasonSelect.selectedIndex].value;
                        _seasonIndexNumber = seasonSelect[seasonSelect.selectedIndex].dataset['index'];

                        getIntros(_seasonId).then((result) => {
                            if (result) {
                                if (result.TitleSequences) {
                                    if (result.TitleSequences.Seasons) {
                                        result.TitleSequences.Seasons.forEach(season => {
                                            if (season.IndexNumber == _seasonIndexNumber) {
                                                if (season.Episodes) {

                                                    var averageLength = parseISO8601Duration(result.CommonEpisodeTitleSequenceLength);

                                                    if (removeSeasonalFingerprintButton.classList.contains('hide')) {
                                                        removeSeasonalFingerprintButton.classList.remove('hide');
                                                    }

                                                    removeSeasonalFingerprintButton.querySelector('span').innerHTML =
                                                        "Remove data for " + seasonSelect[seasonSelect.selectedIndex].innerHTML;

                                                    view.querySelector('.averageTitleSequenceTime').innerText = "00:" + averageLength.minutes + ":" + averageLength.seconds;

                                                    season.Episodes.forEach(intro => {
                                                        getTableRowHtml(intro).then(html => {

                                                            view.querySelector('.introResultBody').innerHTML += html;

                                                            sortTable(view);

                                                            view.querySelectorAll('.removeIntroData i').forEach(btn => {
                                                                btn.addEventListener('click', (elem) => {
                                                                    elem.preventDefault();

                                                                    var episodeId = elem.target.closest('.fab').id;

                                                                    removeIntroItem(episodeId).then(() => {
                                                                        var index = elem.target.closest('tr').rowIndex;
                                                                        view.querySelector('.introResultBody').deleteRow(index - 1);
                                                                    });

                                                                });
                                                            });

                                                            view.querySelectorAll('.removeFingerprint i').forEach(btn => {
                                                                btn.addEventListener('click', (elem) => {
                                                                    elem.preventDefault();

                                                                    var episodeId = elem.target.closest('.fab').id;

                                                                    removeIntroItemAndFingerprint(episodeId).then(() => {
                                                                        var index = elem.target.closest('tr').rowIndex;
                                                                        view.querySelector('.introResultBody').deleteRow(index - 1);
                                                                    });

                                                                });
                                                            });

                                                        });
                                                    });
                                                }
                                            }
                                        });

                                    }
                                } else {
                                    view.querySelector('.averageTitleSequenceTime').innerText = "Currently scanning series...";
                                    if (!removeSeasonalFingerprintButton.classList.contains('hide')) {
                                        removeSeasonalFingerprintButton.classList.add('hide');
                                    }
                                }
                            }  
                        });
                    });

                });



                seasonSelect.addEventListener('change', (e) => {
                    e.preventDefault();
                    view.querySelector('.introResultBody').innerHTML = "";
                    _seriesId = seriesSelect[seriesSelect.selectedIndex].value;
                    _seasonId = seasonSelect[seasonSelect.selectedIndex].value;
                    _seasonIndexNumber = seasonSelect[seasonSelect.selectedIndex].dataset['index'];
                    getIntros(_seasonId).then((result) => {
                        if (result) {
                            if (result.TitleSequences) {
                                if (result.TitleSequences.Seasons) {
                                    result.TitleSequences.Seasons.forEach(season => {
                                        if (season.IndexNumber == _seasonIndexNumber) {
                                            if (season.Episodes) {

                                                var averageLength = parseISO8601Duration(result.CommonEpisodeTitleSequenceLength);

                                                if (removeSeasonalFingerprintButton.classList.contains('hide')) {
                                                    removeSeasonalFingerprintButton.classList.remove('hide');
                                                }

                                                removeSeasonalFingerprintButton.querySelector('span').innerHTML =
                                                    "Remove data for " + seasonSelect[seasonSelect.selectedIndex].innerHTML;

                                                view.querySelector('.averageTitleSequenceTime').innerText = "00:" + averageLength.minutes + ":" + averageLength.seconds;

                                                season.Episodes.forEach(intro => {
                                                    getTableRowHtml(intro).then(html => {

                                                        view.querySelector('.introResultBody').innerHTML += html;

                                                        sortTable(view);

                                                        view.querySelectorAll('.removeIntroData i').forEach(btn => {
                                                            btn.addEventListener('click', (elem) => {
                                                                elem.preventDefault();

                                                                var episodeId = elem.target.closest('.fab').id;

                                                                removeIntroItem(episodeId).then(() => {
                                                                    var index = elem.target.closest('tr').rowIndex;
                                                                    view.querySelector('.introResultBody').deleteRow(index - 1);
                                                                });

                                                            });
                                                        });

                                                        view.querySelectorAll('.removeFingerprint i').forEach(btn => {
                                                            btn.addEventListener('click', (elem) => {
                                                                elem.preventDefault();

                                                                var episodeId = elem.target.closest('.fab').id;

                                                                removeIntroItemAndFingerprint(episodeId).then(() => {
                                                                    var index = elem.target.closest('tr').rowIndex;
                                                                    view.querySelector('.introResultBody').deleteRow(index - 1);
                                                                });

                                                            });
                                                        });

                                                    });
                                                });
                                            }
                                        }
                                    });

                                }
                            } else {
                                view.querySelector('.averageTitleSequenceTime').innerText = "Currently scanning series...";
                                if (!removeSeasonalFingerprintButton.classList.contains('hide')) {
                                    removeSeasonalFingerprintButton.classList.add('hide');
                                }
                            }
                        }
                    });
                });

                seriesSelect.addEventListener('change', (e) => {
                    e.preventDefault();
                    seasonSelect.innerHTML = '';

                    _seriesId = seriesSelect[seriesSelect.selectedIndex].value;

                    view.querySelector('.seriesImg').style = 'background-image: url(' + ApiClient.getPrimaryImageUrl(_seriesId) +');background-repeat: no-repeat;width: 171px;height: 237px;margin: 0px 4em 3em;box-shadow: rgba(0, 0, 0, 0.5) 1px 1px 14px;background-size: contain;';


                    getSeasons(_seriesId).then(seasons => {
                        seasons.Items.forEach(season => {
                            seasonSelect.innerHTML += '<option data-index="' + season.IndexNumber + '" value="' + season.Id + '">' + season.Name + '</option>';
                        });

                        view.querySelector('.introResultBody').innerHTML = '';

                        _seasonId = seasonSelect[0].value;
                        _seasonIndexNumber = seasonSelect[seasonSelect.selectedIndex].dataset['index'];

                        getIntros(_seasonId).then((result) => { 
                             if (result) {
                                if (result.TitleSequences) {
                                    if (result.TitleSequences.Seasons) {
                                        result.TitleSequences.Seasons.forEach(season => {
                                            if (season.IndexNumber == _seasonIndexNumber) {
                                                if (season.Episodes) {

                                                    var averageLength = parseISO8601Duration(result.CommonEpisodeTitleSequenceLength);

                                                    if (removeSeasonalFingerprintButton.classList.contains('hide')) {
                                                        removeSeasonalFingerprintButton.classList.remove('hide');
                                                    }

                                                    removeSeasonalFingerprintButton.querySelector('span').innerHTML =
                                                        "Remove data for " + seasonSelect[seasonSelect.selectedIndex].innerHTML;

                                                    view.querySelector('.averageTitleSequenceTime').innerText = "00:" + averageLength.minutes + ":" + averageLength.seconds;

                                                    season.Episodes.forEach(intro => {
                                                        getTableRowHtml(intro).then(html => {

                                                            view.querySelector('.introResultBody').innerHTML += html;

                                                            sortTable(view);

                                                            view.querySelectorAll('.removeIntroData i').forEach(btn => {
                                                                btn.addEventListener('click', (elem) => {
                                                                    elem.preventDefault();

                                                                    var episodeId = elem.target.closest('.fab').id;

                                                                    removeIntroItem(episodeId).then(() => {
                                                                        var index = elem.target.closest('tr').rowIndex;
                                                                        view.querySelector('.introResultBody').deleteRow(index - 1);
                                                                    });

                                                                });
                                                            });

                                                            view.querySelectorAll('.removeFingerprint i').forEach(btn => {
                                                                btn.addEventListener('click', (elem) => {
                                                                    elem.preventDefault();

                                                                    var episodeId = elem.target.closest('.fab').id;

                                                                    removeIntroItemAndFingerprint(episodeId).then(() => {
                                                                        var index = elem.target.closest('tr').rowIndex;
                                                                        view.querySelector('.introResultBody').deleteRow(index - 1);
                                                                    });

                                                                });
                                                            });

                                                        });
                                                    });
                                                }
                                            }
                                        });

                                    }
                                } else {
                                    view.querySelector('.averageTitleSequenceTime').innerText = "Currently scanning series...";
                                    if (!removeSeasonalFingerprintButton.classList.contains('hide')) {
                                        removeSeasonalFingerprintButton.classList.add('hide');
                                    }
                                }
                            } 
                        });

                    });
                }); 

                removeSeasonalFingerprintButton.addEventListener('click', (e) => {
                    e.preventDefault();
                    ApiClient.deleteSeasonFingerprintData(seasonSelect[seasonSelect.selectedIndex].value);
                    view.querySelector('.introResultBody').innerHTML = "";
                });

                settingsButton.addEventListener('click', (e) => {
                    e.preventDefault();
                    openSettingsDialog();
                });
            });
        }
    });