define(["loading", "dialogHelper", "mainTabsManager", "formDialogStyle", "emby-checkbox", "emby-select", "emby-toggle"],
    function(loading, dialogHelper, mainTabsManager) {

      var pluginId = "93A5E794-E0DA-48FD-8D3A-606A20541ED6";
      var iso8601DurationRegex = /(-)?P(?:([.,\d]+)Y)?(?:([.,\d]+)M)?(?:([.,\d]+)W)?(?:([.,\d]+)D)?T(?:([.,\d]+)H)?(?:([.,\d]+)M)?(?:([.,\d]+)S)?/;

      window.parseISO8601Duration = function(iso8601Duration) {
        var matches = iso8601Duration.match(iso8601DurationRegex);

        return {
          minutes: matches[7] === undefined ? "00" : matches[7] < 10 ? "0" + matches[7] : matches[7],
          seconds: matches[8] === undefined ? "00" : matches[8] < 10 ? "0" + matches[8] : matches[8]
        };
<<<<<<< HEAD
      };

      ApiClient.deleteAll = function() {
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

      ApiClient.deleteSeasonData = function(seasonId) {
        var url = this.getUrl('RemoveSeasonDataRequest?SeasonId=' + seasonId);
        return this.ajax({
          type: "DELETE",
          url: url
        });
      };

      ApiClient.refreshMetadata = function(id) {
        var url = this.getUrl('Items/' + id + '/Refresh');
        return this.ajax({
          type: "POST",
          url: url
        });
      };

      ApiClient.updateChapter = function(id) {
        var url = this.getUrl('UpdateChapter');
        var options = {
          InternalId: id
        }
        return this.ajax({
          type: "POST",
          url: url,
          data: JSON.stringify(options),
          contentType: 'application/json'
        });
      };

      ApiClient.updateTitleSequence = function(options) {
        var url = this.getUrl('UpdateTitleSequence');
        return this.ajax({
          type: "POST",
          url: url,
          data: JSON.stringify(options),
          contentType: 'application/json'
        });
      };

      ApiClient.saveSeasonalIntros = function(seasonId) {
        var url = this.getUrl('ConfirmAllSeasonIntros');
        var options = {
          SeasonId: seasonId
        }
        return this.ajax({
          type: "POST",
          url: url,
          data: JSON.stringify(options),
          contentType: 'application/json'
        });
      }

      ApiClient.getLogoImageUrl = function(id) {
        var url = this.getUrl('Items/' +
          id +
          '/Images/Logo?maxHeight=327&amp;maxWidth=236&amp;quality=90');
        return url;
      }

      ApiClient.getPrimaryImageUrl = function(id) {
        var url = this.getUrl('Items/' +
          id +
          '/Images/Primary?maxHeight=327&amp;maxWidth=236&amp;quality=90');
        return url;
      }

      ApiClient.getCoverImageUrl = function(id) {
        var url = this.getUrl('Items/' +
          id +
          '/Images/Primary?maxHeight=500&amp;maxWidth=300&amp;quality=90');
        return url;
      }

      function getTabs() {
        return [{
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
          }

          function titleSequenceStatusIcon(confirmed) {
            return (confirmed ?
              "stroke='black' stroke-width='1' fill='mediumseagreen'" :
              "stroke='black' stroke-width='1' fill='orange'");
          }

          async function getSeries() {
            return await ApiClient.getJSON(ApiClient.getUrl('Items?ExcludeLocationTypes=Virtual&Recursive=true&IncludeItemTypes=Series&SortBy=SortName'));
          }

          async function getEpisode(episodeId) {
            return await ApiClient.getJSON(ApiClient.getUrl('Items?Ids=' + episodeId));
          }

          async function getSeasons(seriesId) {
            return await ApiClient.getJSON(ApiClient.getUrl('Items?ExcludeLocationTypes=Virtual&ParentId=' +
              seriesId + '&IncludeItemTypes=Season&SortBy=SortName'));
          }

          async function saveIntro(row, view) {

            var id = row.dataset.id;
            var intro = await ApiClient.getJSON(ApiClient.getUrl('EpisodeTitleSequence?InternalId=' + id));
            var options = {
              InternalId: id,
              TitleSequenceStart: row.cells[6].querySelector('div').innerText.replace("00:", "PT").replace(":", "M") + "S",
              TitleSequenceEnd: row.cells[7].querySelector('div').innerText.replace("00:", "PT").replace(":", "M") + "S",
              HasSequence: row.cells[5].querySelector('select').value,
              Confirmed: intro.Confirmed = true,
              SeasonId: intro.SeasonId
            }

            await ApiClient.updateTitleSequence(options);

            //If chapters are enabled, refresh the items metadata, and update the chapter.
            var config = await ApiClient.getPluginConfiguration(pluginId);
            if (config.EnableChapterInsertion) {
              ApiClient.refreshMetadata(id).then(() => {
                ApiClient.updateChapter(id).then(complete => {
                  Dashboard.processPluginConfigurationUpdateResult(complete);
                  resolve(true);
                });
              });
            }
          }

          async function getIntros(seasonId) {
            return await ApiClient.getJSON(ApiClient.getUrl('SeasonTitleSequences?SeasonId=' + seasonId));
          }

          async function getExtractedThumbImage(hasIntro, id, imageFrame, isStart) {
            var thumb = !hasIntro ? 'NoTitleSequenceThumbImage' : 'ExtractThumbImage?InternalId=' + id + "&ImageFrame=" + encodeURIComponent(imageFrame) + "&IsStart=" + isStart;
            return await ApiClient.getUrl(thumb);
          }

          function imageLink(baseItem) {
            return ApiClient._serverAddress + "/web/index.html#!/item?id=" + baseItem.Id + "&serverId=" + ApiClient._serverInfo.Id;
          }

          async function getTableRowHtml(intro) {

            var result = await getEpisode(intro.InternalId);

            var html = '';
            var episode = result.Items[0];
            var startTimespan = parseISO8601Duration(intro.TitleSequenceStart);
            var endTimespan = parseISO8601Duration(intro.TitleSequenceEnd);

            html += '<tr data-id="' + episode.Id + '" class="detailTableBodyRow detailTableBodyRow-shaded">';

            html += '<td data-title="Confirmed" class="detailTableBodyCell fileCell">';
            html += '<svg width="30" height="30">';
            html += '<circle cx="15" cy="15" r="10"' + titleSequenceStatusIcon(intro.Confirmed) + '" />';
            html += '</svg>';
            html += '</td>';

            html += '<td data-title="EpisodeImage" class="detailTableBodyCell fileCell"><a href="' + imageLink(episode) + '" target="_blank" title="Click to go to Episode"><img style="width:125px; height:71px" src="' + ApiClient.getPrimaryImageUrl(episode.Id) + '"/></a></td>';
            html += '<td data-title="Series" class="detailTableBodyCell fileCell">' + episode.SeriesName + '</td>';
            html += '<td data-title="Season" class="detailTableBodyCell fileCell">' + episode.SeasonName + '</td>';
            html += '<td data-title="EpisodeIndex" class="detailTableBodyCell fileCell" data-index="' + episode.IndexNumber + '">Episode: ' + episode.IndexNumber + '</td>';
            html += '<td data-title="HasSequence" class="detailTableBodyCell fileCell" style="display:flex;">';


            html += '<div class="selectContainer" style="top:40px">';
            html += '<select is="emby-select" class="emby-select-withcolor emby-select hasIntroSelect">';
            html += '<option value="true" ' + (intro.HasSequence ? 'selected' : "") + '>true</option>';
            html += '<option value="false" ' + (!intro.HasSequence ? 'selected' : "") + '>false</option>';
            html += '</select>';
            html += '<div class="selectArrowContainer" style="top:-23px !important"><div style="visibility:hidden;">0</div><i class="selectArrow md-icon"></i></div>';
            html += '</div>';

            html += '</td">';

            var start = "00:" + startTimespan.minutes + ":" + startTimespan.seconds;
            var end = "00:" + endTimespan.minutes + ":" + endTimespan.seconds;
            var hasIntro = intro.HasSequence || (endTimespan.minutes !== '00' && endTimespan.seconds !== '00');
=======

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

        ApiClient.refreshMetadata = function (id) {
            var url = this.getUrl('Items/' + id + '/Refresh');
            return this.ajax({
                type: "POST",
                url: url
            });
        };

        ApiClient.updateChapter = function (id) {
            var url = this.getUrl('UpdateChapter');
            var options = {
                InternalId : id
            }
            return this.ajax({
                type: "POST",
                url: url,
                data: JSON.stringify(options),
                contentType: 'application/json'
            });
        };

        ApiClient.updateTitleSequence = function (options) {
            var url = this.getUrl('UpdateTitleSequence');
            return this.ajax({
                type: "POST",
                url: url,
                data: JSON.stringify(options),
                contentType: 'application/json'
            });
        };

        ApiClient.saveSeasonalIntros = function (seasonId) {
            var url = this.getUrl('ConfirmAllSeasonIntros');
            var options = {
                SeasonId: seasonId
            }
            return this.ajax({
                type: "POST",
                url: url,
                data: JSON.stringify(options),
                contentType: 'application/json'
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

        ApiClient.getCoverImageUrl = function (id) {
            var url = this.getUrl('Items/' +
                id +
                '/Images/Primary?maxHeight=500&amp;maxWidth=300&amp;quality=90');
            return url;
        }

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
            });
        }

        function getSeasons(seriesId) {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('Items?ExcludeLocationTypes=Virtual&ParentId=' + seriesId + '&IncludeItemTypes=Season&SortBy=SortName')).then(r => {
                    resolve(r);
                });
            });
        }

        function saveSeasonalIntros(seasonId) {
            return new Promise((resolve, reject) => {
                ApiClient.getJSON(ApiClient.getUrl('ConfirmAllSeasonIntros')).then(result => {
                    resolve(result);
                });
            });
        }

        function saveIntro(row, view) {
            return new Promise((resolve, reject) => {
                var id = row.dataset.id;
                ApiClient.getJSON(ApiClient.getUrl('EpisodeTitleSequence?InternalId=' + id)).then(intro => {

                    var options = {
                        InternalId: id,
                        TitleSequenceStart: row.cells[6].querySelector('div').innerText.replace("00:", "PT")
                            .replace(":", "M") +
                            "S",
                        TitleSequenceEnd: row.cells[7].querySelector('div').innerText.replace("00:", "PT")
                            .replace(":", "M") +
                            "S",
                        HasSequence: row.cells[5].querySelector('select').value,
                        Confirmed: intro.Confirmed = true,
                        SeasonId: intro.SeasonId
                    }

                    ApiClient.updateTitleSequence(options).then(() => {

                        //If chapters are enabled, refresh the items metadata, and update the chapter.
                        ApiClient.getPluginConfiguration(pluginId).then((config) => {
                            if (config.EnableChapterInsertion) {
                                ApiClient.refreshMetadata(id).then(() => {
                                    ApiClient.updateChapter(id).then(complete => {
                                        Dashboard.processPluginConfigurationUpdateResult(complete);
                                        resolve(true);
                                    });
                                });
                            }
                        });

                    });
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

        function getExtractedThumbImage(hasIntro, id, imageFrame, isStart) {
            if (!hasIntro) {
                return ApiClient.getUrl('NoTitleSequenceThumbImage');
            }
            return ApiClient.getUrl('ExtractThumbImage?InternalId=' + id + "&ImageFrame=" + encodeURIComponent(imageFrame) + "&IsStart=" + isStart);
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
                    html += '<path stroke="black" d="' + titleSequenceStatusIcon(intro.Confirmed) + '" />';
                    html += '</svg>';
                    html += '</td>';

                    html += '<td data-title="EpisodeImage" class="detailTableBodyCell fileCell"><a href="' + imageLink(episode) + '" target="_blank" title="Click to go to Episode"><img style="width:125px; height:71px" src="' + ApiClient.getPrimaryImageUrl(episode.Id) + '"/></a></td>';
                    html += '<td data-title="Series" class="detailTableBodyCell fileCell">' + episode.SeriesName + '</td>';
                    html += '<td data-title="Season" class="detailTableBodyCell fileCell">' + episode.SeasonName + '</td>';
                    html += '<td data-title="EpisodeIndex" class="detailTableBodyCell fileCell" data-index="' + episode.IndexNumber + '">Episode: ' + episode.IndexNumber + '</td>';
                    html += '<td data-title="HasSequence" class="detailTableBodyCell fileCell" style="display:flex;">';

                    
                    html += '<div class="selectContainer" style="top:40px">';
                    html += '<select is="emby-select" class="emby-select-withcolor emby-select hasIntroSelect">';
                    html += '<option value="true" ' + (intro.HasSequence ? 'selected' : "") + '>true</option>';
                    html += '<option value="false" ' + (!intro.HasSequence ? 'selected' : "") + '>false</option>';
                    html += '</select>';
                    html += '<div class="selectArrowContainer" style="top:-23px !important"><div style="visibility:hidden;">0</div><i class="selectArrow md-icon"></i></div>';
                    html += '</div>';

                    html += '</td">';
                    var start = "00:" + startTimespan.minutes + ":" + startTimespan.seconds;
                    var end = "00:" + endTimespan.minutes + ":" + endTimespan.seconds;
                    var hasIntro = intro.HasSequence || (endTimespan.minutes !== '00' && endTimespan.seconds !== '00');
                    html += '<td data-title="Start" class="detailTableBodyCell fileCell"><div contenteditable>' + start + '</div><img style="width:175px; height:100px" src="' + getExtractedThumbImage(hasIntro, intro.InternalId, start, true) + '"/></td>';
                    html += '<td data-title="End" class="detailTableBodyCell fileCell"><div contenteditable>' + end + '</div><img style="width:175px; height:100px" src="' + getExtractedThumbImage(hasIntro, intro.InternalId, end, false) + '"/></td>';
                    
                    html += '<td data-title="titleSequenceDataActions" class="detailTableBodyCell fileCell">';  
                    html += '<button style="margin-left: 1em;" data-id="' + episode.Id + '" class="saveSequence emby-button button-submit">';
                    html += '<span>Confirm</span>';
                    html += '</button>';
                    html += '</td>';

                    html += '<td class="detailTableBodyCell organizerButtonCell" style="whitespace:no-wrap;"></td>';
                    html += '</tr>';
                    resolve(html);
                });
            });
        }
<<<<<<< HEAD
>>>>>>> parent of 586deb9 (Refactor javascript. promises to async/await)


        function reloadItems(titleSequences, view) {
            view.querySelector('.introResultBody').innerHTML = '';
            titleSequences.forEach(intro => {
                getTableRowHtml(intro).then(html => {
                    view.querySelector('.introResultBody').innerHTML += html;
                    
                    view.querySelectorAll('.saveSequence').forEach(btn => {
                        btn.addEventListener('click', (elem) => {
                            elem.preventDefault();
                            var row = elem.target.closest('tr');
                            saveIntro(row, view).then(() => {
                                var seasonSelect = view.querySelector('#selectEmbySeason');
                                var _seasonId = seasonSelect[seasonSelect.selectedIndex].value;
                                getIntros(_seasonId).then(result => {
                                    reloadItems(result.TitleSequences, view);
                                });
                            });
                        });
                    });


                    sortTable(view);

<<<<<<< HEAD
          }

          async function reloadItems(titleSequences, view) {
            view.querySelector('.introResultBody').innerHTML = '';
            titleSequences.forEach(async (intro) => {
              var html = await getTableRowHtml(intro);
              view.querySelector('.introResultBody').innerHTML += html;
              view.querySelectorAll('.saveSequence').forEach(btn => {
                btn.addEventListener('click', async (elem) => {
                  elem.preventDefault();
                  var row = elem.target.closest('tr');
                  await saveIntro(row, view);
                  var seasonSelect = view.querySelector('#selectEmbySeason');
                  var seasonId = seasonSelect[seasonSelect.selectedIndex].value;
                  var result = await getIntros(seasonId);
                  await reloadItems(result.TitleSequences, view);
                });
              });
              sortTable(view);
            });
          }

          function confirm_dlg(view, confirmAction) {
=======
=======


        function reloadItems(titleSequences, view) {
            view.querySelector('.introResultBody').innerHTML = '';
            titleSequences.forEach(intro => {
                getTableRowHtml(intro).then(html => {
                    view.querySelector('.introResultBody').innerHTML += html;
                    
                    view.querySelectorAll('.saveSequence').forEach(btn => {
                        btn.addEventListener('click', (elem) => {
                            elem.preventDefault();
                            var row = elem.target.closest('tr');
                            saveIntro(row, view).then(() => {
                                var seasonSelect = view.querySelector('#selectEmbySeason');
                                var _seasonId = seasonSelect[seasonSelect.selectedIndex].value;
                                getIntros(_seasonId).then(result => {
                                    reloadItems(result.TitleSequences, view);
                                });
                            });
                        });
                    });


                    sortTable(view);

>>>>>>> parent of 586deb9 (Refactor javascript. promises to async/await)
                });
            });
        }

        

        function confirm_dlg(view) {
<<<<<<< HEAD
>>>>>>> parent of 586deb9 (Refactor javascript. promises to async/await)
=======
>>>>>>> parent of 586deb9 (Refactor javascript. promises to async/await)
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
<<<<<<< HEAD
<<<<<<< HEAD

            //Submit - Confirm
            html += '<button is="emby-button" type="button" class="btnConfirm raised button-submit block emby-button button-cancel">';
            html += '<span>Confirm</span>';
            html += '</button>';

=======
           
            //Submit - remove all season data
            html += '<button is="emby-button" type="button" class="btnClearAll submit raised button-cancel">';
            html += '<span>Remove All</span>';
            html += '</button>';

=======
           
            //Submit - remove all season data
            html += '<button is="emby-button" type="button" class="btnClearAll submit raised button-cancel">';
            html += '<span>Remove All</span>';
            html += '</button>';

>>>>>>> parent of 586deb9 (Refactor javascript. promises to async/await)
            //Keep confirmed user data (the data the user edited in the table)
            html += '<button is="emby-button" type="button" class="btnKeepConfirmed submit raised button-cancel">';
            html += '<span>Keep Edited Content</span>';
            html += '</button>';

<<<<<<< HEAD
>>>>>>> parent of 586deb9 (Refactor javascript. promises to async/await)
=======
>>>>>>> parent of 586deb9 (Refactor javascript. promises to async/await)
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
<<<<<<< HEAD
<<<<<<< HEAD


            dlg.querySelector('.btnConfirm').addEventListener('click', async () => {
              var seasonSelect = view.querySelector('#selectEmbySeason');
              var seasonId = seasonSelect[seasonSelect.selectedIndex].value;
              switch (confirmAction) {
                case "ClearAll":
                  await clearAll(seasonId).then(() => dialogHelper.close(dlg));
                  break;
                case "ConfirmAll":
                  await confirmAll(seasonId).then(() => dialogHelper.close(dlg));
                  break;
              }
            });

            async function clearAll(seasonId) {
              await ApiClient.deleteSeasonData(seasonId);
              var result = await getIntros(seasonId);
              reloadItems(result.TitleSequences, view);
            }

            async function confirmAll(seasonId) {
              await ApiClient.saveSeasonalIntros(seasonId);
              var result = getIntros(seasonId);
              reloadItems(result.TitleSequences, view);
            }

            dialogHelper.open(dlg);
          }
=======

            dlg.querySelector('.btnKeepConfirmed').addEventListener('click', () => {
                clear(false);
            });

            dlg.querySelector('.btnClearAll').addEventListener('click', () => {
                clear(true);
            });

            function clear(removeAll) {
                var seasonSelect = view.querySelector('#selectEmbySeason');
                var seasonId = seasonSelect[seasonSelect.selectedIndex].value;
                ApiClient.deleteSeasonData(seasonId, removeAll).then(() => {
                    getIntros(seasonId).then(result => {
                        reloadItems(result.TitleSequences, view);
                        dialogHelper.close(dlg);
                    });
                });
            }
=======

            dlg.querySelector('.btnKeepConfirmed').addEventListener('click', () => {
                clear(false);
            });

            dlg.querySelector('.btnClearAll').addEventListener('click', () => {
                clear(true);
            });

            function clear(removeAll) {
                var seasonSelect = view.querySelector('#selectEmbySeason');
                var seasonId = seasonSelect[seasonSelect.selectedIndex].value;
                ApiClient.deleteSeasonData(seasonId, removeAll).then(() => {
                    getIntros(seasonId).then(result => {
                        reloadItems(result.TitleSequences, view);
                        dialogHelper.close(dlg);
                    });
                });
            }

            dialogHelper.open(dlg);
         }
>>>>>>> parent of 586deb9 (Refactor javascript. promises to async/await)

            dialogHelper.open(dlg);
         }
>>>>>>> parent of 586deb9 (Refactor javascript. promises to async/await)

          function sortTable(view) {
            var rows, switching, i, x, y, shouldSwitch;
            const table = view.querySelector('.tblEpisodeIntroResults');
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
<<<<<<< HEAD
          }

          return function(view) {
            view.addEventListener('viewshow', async () => {

              loading.show();
=======
        }

        function renderLogoImage(baseItem) {
            return new Promise((resolve, reject) => {
                var seriesSelect = view.querySelector('#selectEmbySeries');
                seriesId = seriesSelect[seriesSelect.selectedIndex].value;
                ApiClient.getLogoImageUrl(baseItem.seriesId).then(result => {
                    resolve(result);
                });
            });
        }

        return function (view) {
            view.addEventListener('viewshow', (e) => {
                
                loading.show();

                const isMobile = window.matchMedia("only screen and (max-width: 760px)").matches;
                if(!isMobile){
                    view.querySelector('.detailLogo').classList.remove('hide');
                }else {
                    
                }
>>>>>>> parent of 586deb9 (Refactor javascript. promises to async/await)

              const isMobile = window.matchMedia("only screen and (max-width: 760px)").matches;
              if (!isMobile) {
                view.querySelector('.detailLogo').classList.remove('hide');
              }

              mainTabsManager.setTabs(this, 0, getTabs);

<<<<<<< HEAD
<<<<<<< HEAD

              document.querySelector('.pageTitle').innerHTML = "Intro Skip " +
                '<a is="emby-linkbutton" class="raised raised-mini headerHelpButton emby-button" target="_blank" href="https://emby.media/community/index.php?/topic/101687-introskip-instructions-beta-releases/"><i class="md-icon button-icon button-icon-left secondaryText headerHelpButtonIcon">help</i><span class="headerHelpButtonText">Help</span></a>';

              var seriesId, seasonId;

              var seriesSelect = view.querySelector('#selectEmbySeries');
              var seasonSelect = view.querySelector('#selectEmbySeason');

              var removeSeasonalFingerprintButton = view.querySelector('.removeSeasonalFingerprintData');
              var confirmSeasonalIntros = view.querySelector('.confirmSeasonalIntros');

              var series = await getSeries();

              for (let i = 0; i <= series.Items.length - 1; i++) {
                seriesSelect.innerHTML += '<option value="' + series.Items[i].Id + '">' + series.Items[i].Name + '</option>';
              }

              seriesId = seriesSelect[seriesSelect.selectedIndex].value;
              view.querySelector('.detailLogo').innerHTML = '<img style="width:225px" src="' + ApiClient.getPrimaryImageUrl(seriesId) + '"/>';

              var seasons = await getSeasons(seriesId);

              for (var j = 0; j <= seasons.Items.length - 1; j++) {
                seasonSelect.innerHTML += '<option data-index="' + seasons.Items[j].IndexNumber + '" value="' + seasons.Items[j].Id + '">' + seasons.Items[j].Name + '</option>';
              }

              seasonId = seasonSelect[seasonSelect.selectedIndex].value;
              var seasonIndexNumber = seasonSelect[seasonSelect.selectedIndex].dataset['index'];

              //Update the confirm button text
              view.querySelector('.confirmSeasonalIntros span').innerHTML = "Confirm All Season " + seasonIndexNumber + " Data";

              //Get the intros for the season
              var result = await getIntros(seasonId);
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
                  await reloadItems(titleSequences, view);

                } else {

                  view.querySelector('.averageTitleSequenceTime').innerText = "Currently scanning series...";
                  if (!removeSeasonalFingerprintButton.classList.contains('hide')) {
                    removeSeasonalFingerprintButton.classList.add('hide');
                  }

                }
              }

              loading.hide();


              seasonSelect.addEventListener('change', async (e) => {
                e.preventDefault();
                loading.show();
                view.querySelector('.introResultBody').innerHTML = "";
                seriesId = seriesSelect[seriesSelect.selectedIndex].value;
                seasonId = seasonSelect[seasonSelect.selectedIndex].value;
                var _seasonIndexNumber = seasonSelect[seasonSelect.selectedIndex].dataset['index'];

                //Update the confirm button text
                view.querySelector('.confirmSeasonalIntros span').innerHTML =
                  "Confirm All Season " + _seasonIndexNumber + " Data";

                var result = await getIntros(seasonId);
                if (result) {
                  if (result.TitleSequences) {
                    var averageLength = parseISO8601Duration(result.CommonEpisodeTitleSequenceLength);

                    if (removeSeasonalFingerprintButton.classList.contains('hide')) {
                      removeSeasonalFingerprintButton.classList.remove('hide');
                    }

                    removeSeasonalFingerprintButton.querySelector('span').innerHTML =
                      "Reset " + seasonSelect[seasonSelect.selectedIndex].innerHTML + " Data";

                    view.querySelector('.averageTitleSequenceTime').innerText = "00:" + averageLength.minutes + ":" + averageLength.seconds;
                    var titleSequences = result.TitleSequences;
                    await reloadItems(titleSequences, view);
                  } else {
                    view.querySelector('.averageTitleSequenceTime').innerText = "Currently scanning series...";
                    if (!removeSeasonalFingerprintButton.classList.contains('hide')) {
                      removeSeasonalFingerprintButton.classList.add('hide');
                    }
                  }
                }
                loading.hide();

              });

              seriesSelect.addEventListener('change', async (e) => {
                e.preventDefault();
                loading.show();
                seasonSelect.innerHTML = '';

                seriesId = seriesSelect[seriesSelect.selectedIndex].value;

                view.querySelector('.detailLogo').innerHTML = '<img style="width:225px" src="' + ApiClient.getPrimaryImageUrl(seriesId) + '"/>';

                var seasons = await getSeasons(seriesId);
                seasons.Items.forEach(season => {
                  seasonSelect.innerHTML += '<option data-index="' + season.IndexNumber + '" value="' + season.Id + '">' + season.Name + '</option>';
                });

                view.querySelector('.introResultBody').innerHTML = '';

                seasonId = seasonSelect[0].value;
                var _seasonIndexNumber = seasonSelect[seasonSelect.selectedIndex].dataset['index'];

                //Update the confirm button text
                view.querySelector('.confirmSeasonalIntros span').innerHTML =
                  "Confirm All Season " + _seasonIndexNumber + " Data";

                var result = await getIntros(seasonId);
                if (result) {
                  if (result.TitleSequences) {
                    var averageLength = parseISO8601Duration(result.CommonEpisodeTitleSequenceLength);

                    if (removeSeasonalFingerprintButton.classList.contains('hide')) {
                      removeSeasonalFingerprintButton.classList.remove('hide');
                    }

                    removeSeasonalFingerprintButton.querySelector('span').innerHTML =
                      "Reset " + seasonSelect[seasonSelect.selectedIndex].innerText + "Data";

                    view.querySelector('.averageTitleSequenceTime').innerText = "00:" + averageLength.minutes + ":" + averageLength.seconds;
                    var titleSequences = result.TitleSequences;
                    await reloadItems(titleSequences, view);
                  } else {
                    view.querySelector('.averageTitleSequenceTime').innerText = "Currently scanning series...";
                    if (!removeSeasonalFingerprintButton.classList.contains('hide')) {
                      removeSeasonalFingerprintButton.classList.add('hide');
                    }
                  }
                }
                loading.hide();
              });

              removeSeasonalFingerprintButton.addEventListener('click', (e) => {
                e.preventDefault();
                confirm_dlg(view, "ClearAll");
              });

              confirmSeasonalIntros.addEventListener('click', (e) => {
                e.preventDefault();
                confirm_dlg(view, "ConfirmAll");
              });
=======
                document.querySelector('.pageTitle').innerHTML = "Intro Skip " +
                    '<a is="emby-linkbutton" class="raised raised-mini headerHelpButton emby-button" target="_blank" href="https://emby.media/community/index.php?/topic/101687-introskip-instructions-beta-releases/"><i class="md-icon button-icon button-icon-left secondaryText headerHelpButtonIcon">help</i><span class="headerHelpButtonText">Help</span></a>';
                var _seriesId, _seasonId;

                var seriesSelect = view.querySelector('#selectEmbySeries');
                var seasonSelect = view.querySelector('#selectEmbySeason');
                
                var removeSeasonalFingerprintButton = view.querySelector('.removeSeasonalFingerprintData');
                var confirmSeasonalFingerprintButton = view.querySelector('.confirmSeasonalFingerprintData');


                var chkConfirmSeasonalIntroData = view.querySelector('.chkShowConfirmSeasonalIntroData');


                getSeries().then(series => {

                    for (let i = 0; i <= series.Items.length - 1; i++) {
                        seriesSelect.innerHTML += '<option value="' + series.Items[i].Id + '">' + series.Items[i].Name + '</option>';
                    }

                    _seriesId = seriesSelect[seriesSelect.selectedIndex].value;
                    view.querySelector('.detailLogo').innerHTML = '<img style="width:225px" src="' + ApiClient.getPrimaryImageUrl(_seriesId) + '"/>';

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

=======
                document.querySelector('.pageTitle').innerHTML = "Intro Skip " +
                    '<a is="emby-linkbutton" class="raised raised-mini headerHelpButton emby-button" target="_blank" href="https://emby.media/community/index.php?/topic/101687-introskip-instructions-beta-releases/"><i class="md-icon button-icon button-icon-left secondaryText headerHelpButtonIcon">help</i><span class="headerHelpButtonText">Help</span></a>';
                var _seriesId, _seasonId;

                var seriesSelect = view.querySelector('#selectEmbySeries');
                var seasonSelect = view.querySelector('#selectEmbySeason');
                
                var removeSeasonalFingerprintButton = view.querySelector('.removeSeasonalFingerprintData');
                var confirmSeasonalFingerprintButton = view.querySelector('.confirmSeasonalFingerprintData');


                var chkConfirmSeasonalIntroData = view.querySelector('.chkShowConfirmSeasonalIntroData');


                getSeries().then(series => {

                    for (let i = 0; i <= series.Items.length - 1; i++) {
                        seriesSelect.innerHTML += '<option value="' + series.Items[i].Id + '">' + series.Items[i].Name + '</option>';
                    }

                    _seriesId = seriesSelect[seriesSelect.selectedIndex].value;
                    view.querySelector('.detailLogo').innerHTML = '<img style="width:225px" src="' + ApiClient.getPrimaryImageUrl(_seriesId) + '"/>';

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

>>>>>>> parent of 586deb9 (Refactor javascript. promises to async/await)
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
<<<<<<< HEAD

                    view.querySelector('.detailLogo').innerHTML = '<img style="width:225px" src="' + ApiClient.getPrimaryImageUrl(_seriesId) + '"/>';

=======

                    view.querySelector('.detailLogo').innerHTML = '<img style="width:225px" src="' + ApiClient.getPrimaryImageUrl(_seriesId) + '"/>';

>>>>>>> parent of 586deb9 (Refactor javascript. promises to async/await)
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

                confirmSeasonalIntros.addEventListener('click', (e) => {
                    e.preventDefault();
                    seasonId = seasonSelect[seasonSelect.selectedIndex].value;
                    ApiClient.saveSeasonalIntros(_seasonId);
                });
                 
<<<<<<< HEAD
>>>>>>> parent of 586deb9 (Refactor javascript. promises to async/await)
=======
>>>>>>> parent of 586deb9 (Refactor javascript. promises to async/await)
            });
          }
        });