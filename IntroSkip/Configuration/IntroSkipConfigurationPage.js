define(["loading", "dialogHelper", "formDialogStyle", "emby-checkbox", "emby-select", "emby-toggle"],
    function (loading, dialogHelper) {

        var pluginId = "93A5E794-E0DA-48FD-8D3A-606A20541ED6";
        var iso8601DurationRegex = /(-)?P(?:([.,\d]+)Y)?(?:([.,\d]+)M)?(?:([.,\d]+)W)?(?:([.,\d]+)D)?T(?:([.,\d]+)H)?(?:([.,\d]+)M)?(?:([.,\d]+)S)?/;

        window.parseISO8601Duration = function (iso8601Duration) {
            var matches = iso8601Duration.match(iso8601DurationRegex);

            return {
                minutes: matches[7] === undefined ? "00" : matches[7] < 10 ? "0" + matches[7] : matches[7],
                seconds: matches[8] === undefined ? "00" : matches[8] < 10 ? "0" + matches[8] : matches[8]
            };
        };

        

        ApiClient.deleteAll = function () {
            var url = this.getUrl('RemoveAll');
            return this.ajax({
                type: "DELETE",
                url: url
            });
        };

        
        ApiClient.deleteIntroItemAndFingerprint = function (episodeId) {
            var url = this.getUrl('RemoveEpisodeTitleSequenceData?InternalId=' + episodeId);
            return this.ajax({
                type: "DELETE",
                url: url
            });
        };

        ApiClient.deleteSeasonData = function (seasonId) {
            var url = this.getUrl('RemoveSeasonDataRequest?SeasonId=' + seasonId);
            return this.ajax({
                type: "DELETE",
                url: url
            });
        };

        ApiClient.getLogoImageUrl = function (id) {
            var url = this.getUrl('Items/' +
                id +
                '/Images/Logo?maxHeight=327&amp;maxWidth=236&amp;quality=90');
            return url;
        }

        ApiClient.getPrimaryImageUrl = function (id) {
            var url = this.getUrl('Items/' +
                id +
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
            dlg.style.maxHeight = "87%";

            var html = '';

            html += '<div class="formDialogHeader" style="display:flex">';
            html += '<button is="paper-icon-button-light" class="btnCloseDialog autoSize paper-icon-button-light" tabindex="-1"><i class="md-icon">arrow_back</i></button><h3 class="formDialogHeaderTitle">Advanced settings</h3>';
            html += '</div>';

            html += '<div class="formDialogContent scrollY" style="margin:2em">';
            html += '<div class="dialogContentInner" style="max-height: 42em; max-width:92%">';
            html += '<div style="flex-grow:1;">';

            html += '<div class="detailSectionHeader" style="margin-bottom:2em;">';
            html += '<h2 style="margin: .6em 0; vertical-align: middle; display: inline-block;">Remove all</h2>';
            html += '<button is="emby-button" class="removeAllData fab emby-input-iconbutton paper-icon-button-light emby-button" style="margin-left: 1em;"><i class="md-icon">clear</i></button>';
            html += '<div class="fieldDescription">';
            html += 'Remove all title sequence related data and start from scratch.';
            html += '</div>';
            html += '</div>';

            html += '<hr>';

            html += '<h2 style="margin: .6em 0; vertical-align: middle; display: inline-block;">';
            html += 'Fingerprinting';
            html += '</h2>';

            //html += '<div class="inputContainer">';
            //html += '<label style="width: auto;" class="mdl-switch mdl-js-switch">';
            //html += '<input is="emby-toggle" type="checkbox" id="enableItemAddedEvent"  class="chkitemAddedEvent noautofocus mdl-switch__input" data-embytoggle="true">';
            //html += '<span class="toggleButtonLabel mdl-switch__label">Enable new episode auto scan</span>';
            //html += '<div class="mdl-switch__trackContainer">';
            //html += '<div class="mdl-switch__track"></div>';
            //html += '<div class="mdl-switch__thumb">';
            //html += '<span class="mdl-switch__focus-helper"></span>';
            //html += '</div>';
            //html += '</div>';
            //html += '</label>';
            //html += '<div class="fieldDescription">';
            //html += 'Auto scan episodes after they are added to the library automatically.';
            //html += '</div>';
            //html += '</div>';

            html += '<div class="inputContainer">';
            html += '<label class="inputLabel inputLabelUnfocused" for="txtFingerprintingMaxDegreeOfParallelism">Maximum parallel series to process at once:</label> ';
            html += '<input type="number" id="txtFingerprintingMaxDegreeOfParallelism" min="2" max="15" step="1" label="Maximum series to proccess at once:" class="emby-input">';
            html += '<div class="fieldDescription">';
            html += 'The number of series to attempt fingerprint proccessing for at once. Lower powered machines should keep the default of 5.';
            html += '</div>';
            html += '</div>';

            //html += '<div class="inputContainer">';
            //html += '<label class="inputLabel inputLabelUnfocused" for="txtTitleSequenceEncodingLength">Fingerprinting audio encoding length (minutes):</label> ';
            //html += '<input type="number" id="txtTitleSequenceEncodingLength" min="10" max="20" step="1" label="Title sequence encoding duration (minutes):" class="emby-input">';
            //html += '<div class="fieldDescription">';
            //html += 'The duration of episode audio encoding used to find title sequences. Default is 10 minutes. A longer encoding may match episodes with title sequences which appear later in the stream, but will cause longer scans.';
            //html += '</div>';
            //html += '</div>';

            html += '<hr>';

            html += '<h2 style="margin: .6em 0; vertical-align: middle; display: inline-block;">';
            html += 'Title Sequence Detection';
            html += '</h2>';

            //html += '<div class="inputContainer">';
            //html += '<label class="inputLabel inputLabelUnfocused" for="txtTitleSequenceThreshold">Title sequence duration threshold (seconds):</label> ';
            //html += '<input type="number" id="txtTitleSequenceThreshold" min="5" max="20" step="1" label="Title sequence duration threshold (seconds):" class="emby-input">';
            //html += '<div class="fieldDescription">';
            //html += 'The duration (in seconds) for accepted title sequence lengths. Any match with a duration less then this number will be ignored. Default: 10';
            //html += '</div>';
            //html += '</div>';

            html += '<div class="inputContainer">';
            html += '<label class="inputLabel inputLabelUnfocused" for="txtMaxDegreeOfParallelism">Maximum parallel series to process at once:</label> ';
            html += '<input type="number" id="txtTitleSequenceMaxDegreeOfParallelism" min="2" max="15" step="1" label="Maximum series to proccess at once:" class="emby-input">';
            html += '<div class="fieldDescription">';
            html += 'The number of series to attempt title sequence proccessing for, at once. Lower powered machines should keep the default of 4.';
            html += '</div>';
            html += '</div>';

            html += '<div class="inputContainer">';
            html += '<button is="emby-button" type="submit" class="btnOk raised button-submit block emby-button">';
            html += '<span>Ok</span>';
            html += '</button>';
            html += '</div>';

            html += '</div>';
            html += '</div>';

            dlg.innerHTML = html;
            dialogHelper.open(dlg);


           
            var titleSequenceMaxDegreeOfParallelism = dlg.querySelector('#txtTitleSequenceMaxDegreeOfParallelism');
            var fingerprintMaxDegreeOfParallelism = dlg.querySelector('#txtFingerprintingMaxDegreeOfParallelism');
            var removeAllButton = dlg.querySelector('.removeAllData');

            ApiClient.getPluginConfiguration(pluginId).then((config) => {                
                titleSequenceMaxDegreeOfParallelism.value = config.MaxDegreeOfParallelism ? config.MaxDegreeOfParallelism : 2;
                fingerprintMaxDegreeOfParallelism.value = config.FingerprintingMaxDegreeOfParallelism;
            });

            removeAllButton.addEventListener('click', (e) => {
                e.preventDefault();
                openConfirmationDialog();
            });
                           

            titleSequenceMaxDegreeOfParallelism.addEventListener('change', (e) => {
                e.preventDefault();
                ApiClient.getPluginConfiguration(pluginId).then((config) => {
                    config.MaxDegreeOfParallelism = titleSequenceMaxDegreeOfParallelism.value;
                    ApiClient.updatePluginConfiguration(pluginId, config).then(() => { });
                });
            });

            fingerprintMaxDegreeOfParallelism.addEventListener('change', (e) => {
                e.preventDefault();
                ApiClient.getPluginConfiguration(pluginId).then((config) => {
                    config.FingerprintingMaxDegreeOfParallelism = fingerprintMaxDegreeOfParallelism.value;
                    ApiClient.updatePluginConfiguration(pluginId, config).then(() => { });
                });
            });


            dlg.querySelector('.btnCloseDialog').addEventListener('click', () => {
                dialogHelper.close(dlg);
            });

            dlg.querySelector('.btnOk').addEventListener('click', () => {
                dialogHelper.close(dlg);
            });

            loading.hide();
        }
                

        function titleSequenceStatusIcon(confirmed) {
            return confirmed ? 
                "M12 2C6.5 2 2 6.5 2 12S6.5 22 12 22 22 17.5 22 12 17.5 2 12 2M12 20C7.59 20 4 16.41 4 12S7.59 4 12 4 20 7.59 20 12 16.41 20 12 20M16.59 7.58L10 14.17L7.41 11.59L6 13L10 17L18 9L16.59 7.58Z" :
                "M12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z";                
        }

        function getSeries() {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('Items?ExcludeLocationTypes=Virtual&Recursive=true&IncludeItemTypes=Series&SortBy=SortName')).then(result => {
                    resolve(result);
                });
            });
        }

        function getEpisode(episodeId) {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('Items?Ids=' + episodeId)).then(result => {
                   resolve(result);
                });
            })
        }

        function getSeasons(seriesId) {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('Items?ExcludeLocationTypes=Virtual&ParentId=' + seriesId + '&IncludeItemTypes=Season&SortBy=SortName')).then(r => {
                    resolve(r);
                });
            });
        }
        function saveIntro(row, view) {             
            var id = row.dataset.id;
            ApiClient.getJSON(ApiClient.getUrl('EpisodeTitleSequence?InternalId=' + id)).then(intro => {
                var url = 'UpdateTitleSequence';
                url += '?InternalId=' + id;
                url += '&TitleSequenceStart=' + row.cells[6].querySelector('div').innerText.replace("00:", "PT").replace(":", "M") + "S";
                url += '&TitleSequenceEnd=' + row.cells[7].querySelector('div').innerText.replace("00:", "PT").replace(":", "M") + "S";
                url += '&HasSequence=' + row.cells[5].querySelector('select').value;
                url += '&SeasonId=' + intro.SeasonId;
                ApiClient.getJSON(ApiClient.getUrl(url)).then(result => {
                    reloadItems(result.TitleSequences, view);                      
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
                getEpisode(intro.InternalId).then(result => {

                    var html = '';
                    var episode = result.Items[0];
                    var startTimespan = parseISO8601Duration(intro.TitleSequenceStart);
                    var endTimespan = parseISO8601Duration(intro.TitleSequenceEnd);

                    html += '<tr data-id="' + episode.Id + '" class="detailTableBodyRow detailTableBodyRow-shaded">';
                    html += '<td data-title="Confirmed" class="detailTableBodyCell fileCell">';
                    html += '<svg style="width:24px;height:24px" viewBox="0 0 24 24">';
                    html += '<path fill="currentColor" d="' + titleSequenceStatusIcon(intro.Confirmed) + '" />';
                    html += '</svg>';
                    html += '</td>';
                    html += '<td data-title="EpisodeImage" class="detailTableBodyCell fileCell"><img style="width:125px" src="' + ApiClient.getPrimaryImageUrl(episode.Id) + '"/></td>';
                    html += '<td data-title="Series" class="detailTableBodyCell fileCell">' + episode.SeriesName + '</td>';
                    html += '<td data-title="Season" class="detailTableBodyCell fileCell">' + episode.SeasonName + '</td>';
                    html += '<td data-title="EpisodeIndex" class="detailTableBodyCell fileCell" data-index="' + episode.IndexNumber + '">Episode: ' + episode.IndexNumber + '</td>';
                    html += '<td data-title="HasSequence" class="detailTableBodyCell fileCell" style="display:flex;">';
                    
                    //html += '<div contenteditable>' + intro.HasSequence.toString() + '</div>';
                    html += '<div class="selectContainer" style="top:15px">';
                    html += '<select is="emby-select" class="emby-select-withcolor emby-select hasIntroSelect">';
                    html += '<option value="true" '  + (intro.HasSequence  ? 'selected' : "") + '>true</option>';
                    html += '<option value="false" ' + (!intro.HasSequence ? 'selected' : "") + '>false</option>';
                    html += '</select>';
                    html += '<div class="selectArrowContainer" style="top:-23px !important"><div style="visibility:hidden;">0</div><i class="selectArrow md-icon"></i></div>';
                    html += '</div>';

                    html +='</td>';
                    //html += '<td data-title="HasSequence" class="detailTableBodyCell fileCell" style="color:' + (intro.HasSequence === true ? "#5EC157" : "") + '"><div contenteditable>' + intro.HasSequence.toString() + '</div></td>';
                    html += '<td data-title="Start" class="detailTableBodyCell fileCell"><div contenteditable>' + "00:" + startTimespan.minutes + ":" + startTimespan.seconds + '</div></td>';
                    html += '<td data-title="End" class="detailTableBodyCell fileCell"><div contenteditable>' + "00:" + endTimespan.minutes + ":" + endTimespan.seconds + '<div></td>';
                    //html += '<td data-title="Remove" class="detailTableBodyCell fileCell">';
                    //html += '<p style="margin: .6em 0; vertical-align: middle; display: inline-block;">Remove Title Sequence</p>';
                    //html += '<button style="margin-left: 1em;" id="' + episode.Id + '" class="fab removeIntroData emby-button"><i class="md-icon">clear</i></button>';
                    //html += '</td>';
                    html += '<td data-title="titleSequenceDataActions" class="detailTableBodyCell fileCell">';
                    //html += '<p style="margin: .6em 0; vertical-align: middle; display: inline-block;">Remove and Re-Scan</p>';
                    html += '<button style="margin-left: 1em;" id="' + episode.Id + '" class="fab removeFingerprint emby-button">';
                    html += '<svg style="width:24px;height:24px" viewBox="0 0 24 24">';
                    html += '<path fill="currentColor" d="M19,4H15.5L14.5,3H9.5L8.5,4H5V6H19M6,19A2,2 0 0,0 8,21H16A2,2 0 0,0 18,19V7H6V19Z" />';
                    html += '</svg>';
                    html += '</button>';

                    html += '<button style="margin-left: 1em;" data-id="' + episode.Id + '" class="saveSequence emby-button button-submit">';
                    html += '<span>Save</span>';
                    html += '</button>';

                    html += '</td>';
                    html += '<td class="detailTableBodyCell organizerButtonCell" style="whitespace:no-wrap;"></td>';
                    html += '</tr>';
                    resolve(html);
                });
            });
        }

        //function removeIntroItem(episodeId) {
        //    return new Promise((resolve, reject) => {
        //        ApiClient.deleteIntroItem(episodeId).then(result => {
        //            if (result.statusText === "OK") {
        //                Dashboard.alert("Title sequence removed.");
        //            }
        //        });
        //        resolve(true);
        //    });
        //}

        function reloadItems(titleSequences, view) {
            view.querySelector('.introResultBody').innerHTML = '';
            titleSequences.forEach(intro => {
                getTableRowHtml(intro).then(html => {
                    view.querySelector('.introResultBody').innerHTML += html;
                    view.querySelectorAll('.removeFingerprint').forEach(btn => {
                        btn.addEventListener('click', (elem) => {
                            elem.preventDefault();
                            var episodeId = elem.target.closest('.fab').id;
                            removeIntroItemAndFingerprint(episodeId).then(() => {
                                var index = elem.target.closest('tr').rowIndex;
                                view.querySelector('.introResultBody').deleteRow(index - 1);
                            });

                        });
                    });

                    view.querySelectorAll('.saveSequence').forEach(btn => {
                        btn.addEventListener('click', (elem) => {
                            elem.preventDefault();                            
                            var row = elem.target.closest('tr');
                            saveIntro(row, view);
                        });
                    });

                    //view.querySelectorAll('.hasIntroSelect').forEach(elem => {
                    //    elem.addEventListener('change', (e) => {
                    //        //e.preventDefault();
                    //        var select = e.target;
                    //        var td = select.closest('td');
                    //        var v = select.options[select.selectedIndex].value;
                    //        if (v == 'true') {
                    //            select.style.color == "#5EC157";
                    //        }
                    //        else {
                    //            select.style.color = "var(--theme-text-color)";
                    //        }
                    //    });
                    //});

                    sortTable(view);
                    
                });
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
            table = view.querySelector('.tblEpisodeIntroResults')
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
                    x = parseInt(rows[i].getElementsByTagName("TD")[4].dataset.index);
                    y = parseInt(rows[i + 1].getElementsByTagName("TD")[4].dataset.index);
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

        return function (view) {
            view.addEventListener('viewshow', () => {
                loading.show();
                document.querySelector('.pageTitle').innerText = "Title Sequences";
                var _seriesId, _seasonId;

                var seriesSelect = view.querySelector('#selectEmbySeries');
                var seasonSelect = view.querySelector('#selectEmbySeason');
                var settingsButton = view.querySelector('#openSettingsDialog');
                var removeSeasonalFingerprintButton = view.querySelector('.removeSeasonalFingerprintData');

                getSeries().then(series => {

                    for (let i = 0; i <= series.Items.length - 1; i++) {

                        seriesSelect.innerHTML += '<option value="' + series.Items[i].Id + '">' + series.Items[i].Name + '</option>';
                    }
                    _seriesId = seriesSelect[seriesSelect.selectedIndex].value;

                    getSeasons(_seriesId).then(seasons => {

                        for (var j = 0; j <= seasons.Items.length - 1; j++) {
                            seasonSelect.innerHTML += '<option data-index="' + seasons.Items[j].IndexNumber + '" value="' + seasons.Items[j].Id + '">' + seasons.Items[j].Name + '</option>';
                        }

                        _seasonId = seasonSelect[seasonSelect.selectedIndex].value;
                        _seasonIndexNumber = seasonSelect[seasonSelect.selectedIndex].dataset['index'];

                        getIntros(_seasonId).then((result) => {
                            if (result) {
                                if (result.TitleSequences) {
                                    var averageLength = parseISO8601Duration(result.CommonEpisodeTitleSequenceLength);

                                    if (removeSeasonalFingerprintButton.classList.contains('hide')) {
                                        removeSeasonalFingerprintButton.classList.remove('hide');
                                    }

                                    removeSeasonalFingerprintButton.querySelector('span').innerHTML =
                                        "Remove data for " + seasonSelect[seasonSelect.selectedIndex].innerHTML;

                                    view.querySelector('.averageTitleSequenceTime').innerText = "00:" + averageLength.minutes + ":" + averageLength.seconds;

                                    var titleSequences = result.TitleSequences;
                                    reloadItems(titleSequences, view);
                                } else {
                                    view.querySelector('.averageTitleSequenceTime').innerText = "Currently scanning series...";
                                    if (!removeSeasonalFingerprintButton.classList.contains('hide')) {
                                        removeSeasonalFingerprintButton.classList.add('hide');
                                    }
                                }                                
                            }
                            loading.hide();
                        });
                    });

                });


                seasonSelect.addEventListener('change', (e) => {
                    e.preventDefault();
                    loading.show();
                    view.querySelector('.introResultBody').innerHTML = "";
                    _seriesId = seriesSelect[seriesSelect.selectedIndex].value;
                    _seasonId = seasonSelect[seasonSelect.selectedIndex].value;
                    _seasonIndexNumber = seasonSelect[seasonSelect.selectedIndex].dataset['index'];
                    getIntros(_seasonId).then((result) => {
                        if (result) {
                            if (result.TitleSequences) {
                                var averageLength = parseISO8601Duration(result.CommonEpisodeTitleSequenceLength);

                                if (removeSeasonalFingerprintButton.classList.contains('hide')) {
                                    removeSeasonalFingerprintButton.classList.remove('hide');
                                }

                                removeSeasonalFingerprintButton.querySelector('span').innerHTML =
                                    "Remove data for " + seasonSelect[seasonSelect.selectedIndex].innerHTML;

                                view.querySelector('.averageTitleSequenceTime').innerText = "00:" + averageLength.minutes + ":" + averageLength.seconds;
                                var titleSequences = result.TitleSequences;
                                reloadItems(titleSequences, view)
                            } else {
                                view.querySelector('.averageTitleSequenceTime').innerText = "Currently scanning series...";
                                if (!removeSeasonalFingerprintButton.classList.contains('hide')) {
                                    removeSeasonalFingerprintButton.classList.add('hide');
                                }
                            }                            
                        }
                        loading.hide();
                    });
                });

                seriesSelect.addEventListener('change', (e) => {
                    e.preventDefault();
                    loading.show();
                    seasonSelect.innerHTML = '';

                    _seriesId = seriesSelect[seriesSelect.selectedIndex].value;

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
                                    var averageLength = parseISO8601Duration(result.CommonEpisodeTitleSequenceLength);

                                    if (removeSeasonalFingerprintButton.classList.contains('hide')) {
                                        removeSeasonalFingerprintButton.classList.remove('hide');
                                    }

                                    removeSeasonalFingerprintButton.querySelector('span').innerHTML =
                                        "Remove data for " + seasonSelect[seasonSelect.selectedIndex].innerHTML;

                                    view.querySelector('.averageTitleSequenceTime').innerText = "00:" + averageLength.minutes + ":" + averageLength.seconds;
                                    var titleSequences = result.TitleSequences;
                                    reloadItems(titleSequences, view)
                                } else {
                                    view.querySelector('.averageTitleSequenceTime').innerText = "Currently scanning series...";
                                    if (!removeSeasonalFingerprintButton.classList.contains('hide')) {
                                        removeSeasonalFingerprintButton.classList.add('hide');
                                    }
                                }                                 
                            }
                            loading.hide();
                        });

                    });
                });

                removeSeasonalFingerprintButton.addEventListener('click', (e) => {
                    e.preventDefault();
                    loading.show();

                    var message = 'Are you sure you wish to proceed?';

                    require(['confirm'], function (confirm) {

                        confirm(message, 'Remove Season Data').then(function () {

                            ApiClient.deleteSeasonData(seasonSelect[seasonSelect.selectedIndex].value).then(result => {
                                if (result == "OK") {
                                    view.querySelector('.introResultBody').innerHTML = "";
                                    getIntros(seasonSelect[seasonSelect.selectedIndex].value).then(r => {
                                        reloadItems(r.TitleSequences, view);
                                    });
                                }
                                
                            });
                        });
                        loading.hide();
                        reloadItems(page, false);
                    });
                               
                });

                settingsButton.addEventListener('click', (e) => {
                    e.preventDefault();
                    openSettingsDialog();
                });
            });
        }
    });