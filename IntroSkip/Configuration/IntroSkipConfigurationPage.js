define(["loading", "dialogHelper", "mainTabsManager", "formDialogStyle", "emby-checkbox", "emby-select", "emby-toggle"],
    function (loading, dialogHelper, mainTabsManager) {

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

        //ApiClient.refreshSeasonMetadata = function(seasonId) {
        //    return new Promise((resolve, reject) => {
        //        ApiClient.getJSON(ApiClient.getUrl('Items?' + id +'Refresh?MetadataRefreshMode=FullRefresh&ImageRefreshMode=ValidationOnly&ReplaceAllMetadata=true&ReplaceAllImages=false')).then(result => {
        //            resolve(result);
        //        });
        //    });
        //}
        // http://localhost:8096/emby/Items/12056/Refresh?MetadataRefreshMode=FullRefresh&ImageRefreshMode=ValidationOnly&ReplaceAllMetadata=true&ReplaceAllImages=false

        ApiClient.deleteSeasonData = function (seasonId, removeAll) {
            var url = this.getUrl('RemoveSeasonDataRequest?SeasonId=' + seasonId + '&RemoveAll=' + removeAll);
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

        function getTabs() {
            return [
                {
                    href: Dashboard.getConfigurationPageUrl('IntroSkipConfigurationPage'),
                    name: 'Activity'
                },
                {
                    href: Dashboard.getConfigurationPageUrl('ChapterEditorConfigurationPage'),
                    name: 'Chapters'
                },
                {
                    href: Dashboard.getConfigurationPageUrl('AdvancedSettingsConfigurationPage'),
                    name: 'Advanced'
                }];
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
                    Dashboard.processPluginConfigurationUpdateResult(result); 
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

        function imageLink(baseItem) {
            return ApiClient._serverAddress +
                "/web/index.html#!/item?id=" +
                baseItem.Id +
                "&serverId=" +
                ApiClient._serverInfo.Id;
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

                    html += '<td data-title="EpisodeImage" class="detailTableBodyCell fileCell"><a href="' + imageLink(episode) + '" target="_blank"><img style="width:125px" src="' + ApiClient.getPrimaryImageUrl(episode.Id) + '"/></a></td>';
                    html += '<td data-title="Series" class="detailTableBodyCell fileCell">' + episode.SeriesName + '</td>';
                    html += '<td data-title="Season" class="detailTableBodyCell fileCell">' + episode.SeasonName + '</td>';
                    html += '<td data-title="EpisodeIndex" class="detailTableBodyCell fileCell" data-index="' + episode.IndexNumber + '">Episode: ' + episode.IndexNumber + '</td>';
                    html += '<td data-title="HasSequence" class="detailTableBodyCell fileCell" style="display:flex;">';

                    
                    html += '<div class="selectContainer" style="top:15px">';
                    html += '<select is="emby-select" class="emby-select-withcolor emby-select hasIntroSelect">';
                    html += '<option value="true" ' + (intro.HasSequence ? 'selected' : "") + '>true</option>';
                    html += '<option value="false" ' + (!intro.HasSequence ? 'selected' : "") + '>false</option>';
                    html += '</select>';
                    html += '<div class="selectArrowContainer" style="top:-23px !important"><div style="visibility:hidden;">0</div><i class="selectArrow md-icon"></i></div>';
                    html += '</div>';

                    html += '</td>';
                   
                    html += '<td data-title="Start" class="detailTableBodyCell fileCell"><div contenteditable>' + "00:" + startTimespan.minutes + ":" + startTimespan.seconds + '</div></td>';
                    html += '<td data-title="End" class="detailTableBodyCell fileCell"><div contenteditable>' + "00:" + endTimespan.minutes + ":" + endTimespan.seconds + '<div></td>';
                    
                    html += '<td data-title="titleSequenceDataActions" class="detailTableBodyCell fileCell">';
                    

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


        function reloadItems(titleSequences, view) {
            view.querySelector('.introResultBody').innerHTML = '';
            titleSequences.forEach(intro => {
                getTableRowHtml(intro).then(html => {
                    view.querySelector('.introResultBody').innerHTML += html;


                    view.querySelectorAll('.saveSequence').forEach(btn => {
                        btn.addEventListener('click', (elem) => {
                            elem.preventDefault();
                            var row = elem.target.closest('tr');
                            saveIntro(row, view);
                        });
                    });


                    sortTable(view);

                });
            });
        }

        

         function confirm_dlg(view) {
            var dlg = dialogHelper.createDialog({
                removeOnClose: true,
                size: 'small'
            });

            dlg.classList.add('ui-body-a');
            dlg.classList.add('background-theme-a');

            dlg.classList.add('formDialog');
            dlg.style.maxWidth = '30%';
            dlg.style.maxHeight = '20%';
            
            var html = '';
            html += '<div class="formDialogHeader">';
            html += '<button is="paper-icon-button-light" class="btnCancel autoSize" tabindex="-1"><i class="md-icon">&#xE5C4;</i></button>';
            html += '<h3 class="formDialogHeaderTitle">Reset Season</h3>';
            html += '</div>';
            html += '<div class="formDialogContent" style="margin:2em">';
            html += '<div class="dialogContentInner" style="max-width: 100%; max-height:100%; display: flex;align-items: center;justify-content: center;">';
           
            //Submit - remove all season data
            html += '<button is="emby-button" type="button" class="btnClearAll submit raised button-cancel">';
            html += '<span>Remove All</span>';
            html += '</button>';

            //Keep confirmed user data (the data the user edited in the table)
            html += '<button is="emby-button" type="button" class="btnKeepConfirmed submit raised button-cancel">';
            html += '<span>Keep Edited Content</span>';
            html += '</button>';

            //Cancel
            html += '<button is="emby-button" type="button" class="btnCancel submit raised button-cancel">';
            html += '<span>Cancel</span>';
            html += '</button>';

            html += '</div>';
            html += '</div>';

            dlg.innerHTML = html;

            dlg.querySelectorAll('.btnCancel').forEach(btn => {
                btn.addEventListener('click', (e) => {
                    dialogHelper.close(dlg);
                });
            });

            dlg.querySelector('.btnKeepConfirmed').addEventListener('click', () => {
                clear(false);
            });

            dlg.querySelector('.btnClearAll').addEventListener('click', () => {
                clear(true);
            });

            function clear(removeAll) {
                var seasonSelect = view.querySelector('#selectEmbySeason');
                var seasonId = seasonSelect[seasonSelect.selectedIndex].value;
                ApiClient.deleteSeasonData(seasonId, removeAll).then((result) => {
                    //loading.show();
                    if (result) {
                        reloadItems(result, view);
                    }
                    //loading.hide();
                    dialogHelper.close(dlg);
                });
            }

            dialogHelper.open(dlg);
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
            view.addEventListener('viewshow', (e) => {
                loading.show();

                mainTabsManager.setTabs(this, 0, getTabs);


                document.querySelector('.pageTitle').innerHTML = "Intro Skip " +
                    '<a is="emby-linkbutton" class="raised raised-mini headerHelpButton emby-button" target="_blank" href="https://emby.media/community/index.php?/topic/101687-introskip-instructions-beta-releases/"><i class="md-icon button-icon button-icon-left secondaryText headerHelpButtonIcon">help</i><span class="headerHelpButtonText">Help</span></a>';
                var _seriesId, _seasonId;

                var seriesSelect = view.querySelector('#selectEmbySeries');
                var seasonSelect = view.querySelector('#selectEmbySeason');
                
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
                                        "Reset " + seasonSelect[seasonSelect.selectedIndex].innerHTML;

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
                                    "Reset data for " + seasonSelect[seasonSelect.selectedIndex].innerHTML;

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
                                        "Reset data for " + seasonSelect[seasonSelect.selectedIndex].innerHTML;

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

                removeSeasonalFingerprintButton.addEventListener('click', (e) => {
                    e.preventDefault();
                    confirm_dlg(view);
                });
                 
            });
        }
    });