define(["loading", "dialogHelper", "mainTabsManager", "formDialogStyle", "emby-checkbox", "emby-select", "emby-toggle"],
    function(loading, dialogHelper, mainTabsManager) {

        const pluginId = "93A5E794-E0DA-48FD-8D3A-606A20541ED6";
        var iso8601DurationRegex =
            /(-)?P(?:([.,\d]+)Y)?(?:([.,\d]+)M)?(?:([.,\d]+)W)?(?:([.,\d]+)D)?T(?:([.,\d]+)H)?(?:([.,\d]+)M)?(?:([.,\d]+)S)?/;

        window.parseISO8601Duration = function(iso8601Duration) {
            var matches = iso8601Duration.match(iso8601DurationRegex);

            return {
                hours: matches[6] === undefined ? "00" : matches[6] < 10 ? `0${matches[6]}` : matches[6],
                minutes: matches[7] === undefined ? "00" : matches[7] < 10 ? `0${matches[7]}` : matches[7],
                seconds: matches[8] === undefined ? "00" : matches[8] < 10 ? `0${matches[8]}` : matches[8]
            };
        };

        ApiClient.HasChromaprint = function () {
            const url = this.getUrl("HasChromaprint");
            return url;
        }

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
            var url = this.getUrl(`RemoveSeasonData?SeasonId=${seasonId}`);
            return this.ajax({
                type: "DELETE",
                url: url
            });
        };

        ApiClient.refreshMetadata = function(id) {
            var url = this.getUrl(`Items/${id}/Refresh`);
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
            var url = this.getUrl('UpdateSequence');
            return this.ajax({
                type: "POST",
                url: url,
                data: JSON.stringify(options),
                contentType: 'application/json'
            });
        };

        ApiClient.updateAllSeasonSequences = function(options) {
            var url = this.getUrl('UpdateAllSeasonSequences');
            return this.ajax({
                type: "POST",
                url: url,
                data: JSON.stringify({ "TitleSequencesUpdate": options }),
                contentType: 'application/json'
            });
        }

        ApiClient.getLogoImageUrl = function(id) {
            var url = this.getUrl(`Items/${id}/Images/Logo?maxHeight=327&amp;maxWidth=236&amp;quality=90`);
            return url;
        }

        ApiClient.getPrimaryImageUrl = function(id) {
            var url = this.getUrl(`Items/${id}/Images/Primary?maxHeight=327&amp;maxWidth=236&amp;quality=90`);
            return url;
        }

        ApiClient.getCoverImageUrl = function(id) {
            var url = this.getUrl(`Items/${id}/Images/Primary?maxHeight=500&amp;maxWidth=300&amp;quality=90`);
            return url;
        }
        //http://localhost:8096/emby/videos/48258/stream.mp4?StartTimeTicks=2754166090&VideoCodec=h264&AudioCodec=mp3,aac&VideoBitrate=139616000&AudioBitrate=384000&AudioStreamIndex=1&SubtitleStreamIndex=12&SubtitleMethod=Hls&TranscodingMaxAudioChannels=2&SegmentContainer=m4s,ts&MinSegments=1&BreakOnNonKeyFrames=True&ManifestSubtitles=vtt&h264-profile=high,main,baseline,constrainedbaseline,high10&h264-level=52&TranscodeReasons=AudioCodecNotSupported,DirectPlayError&allowVideoStreamCopy=false  
        ApiClient.getVideoSequence = function(sequence, startTime) {
            var url = this.getUrl("Videos/" + sequence.InternalId + "/stream.mp4?StartTimeTicks=" + startTime + "&VideoCodec=h264&AudioCodec=mp3,aac&VideoBitrate=139616000&AudioBitrate=384000&AudioStreamIndex=1&SubtitleStreamIndex=12&SubtitleMethod=Hls&TranscodingMaxAudioChannels=2&SegmentContainer=m4s,ts&MinSegments=1&BreakOnNonKeyFrames=True&ManifestSubtitles=vtt&h264-profile=high,main,baseline,constrainedbaseline,high10&h264-level=52&TranscodeReasons=AudioCodecNotSupported,DirectPlayError&allowVideoStreamCopy=false" + "&api_key=" + ApiClient._serverInfo.AccessToken + "&n=" + Date.now());
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
                },
                {
                    href: Dashboard.getConfigurationPageUrl('AutoSkipConfigurationPage'),
                    name: 'Auto Skip'
                },
                {
                    href: Dashboard.getConfigurationPageUrl('StatsConfigurationPage'),
                    name: 'Stats'
                }];
        }

        var pagination = {
            StartIndex: 0,
            Limit:5,
            TotalRecordCount:0
        }

        var localImageStore = [];

        function getPagingHtml() {

            var html = '';
            html += '<div class="listPaging">';

            var recordEnd = pagination.StartIndex + pagination.Limit > pagination.TotalRecordCount ? pagination.TotalRecordCount : pagination.StartIndex + pagination.Limit;
            html += '<span style="vertical-align:middle;">';
            html += pagination.StartIndex + 1 + '-' + recordEnd + ' of ' + pagination.TotalRecordCount;

            html += '</span>';

            html += '<div style="display:inline-block;">';

            html += '<button is="paper-icon-button-light" class="btnPreviousPage autoSize" ' + (pagination.StartIndex ? '' : 'disabled') + '><i class="md-icon">&#xE5C4;</i></button>';
            html += '<button is="paper-icon-button-light" class="btnNextPage autoSize" ' + (pagination.StartIndex + pagination.Limit >= pagination.TotalRecordCount ? 'disabled' : '') + '><i class="md-icon">&#xE5C8;</i></button>';

            html += '</div>';


            html += '</div>';

            return html;
        }

        async function getSeries() {
            return await ApiClient.getJSON(ApiClient.getUrl(
                'Items?ExcludeLocationTypes=Virtual&Recursive=true&IncludeItemTypes=Series&SortBy=SortName'));
        }

        async function getEpisode(episodeId) {
            return await ApiClient.getJSON(ApiClient.getUrl(`Items?Ids=${episodeId}`));
        }

        async function getSeasons(seriesId) {
            return await ApiClient.getJSON(ApiClient.getUrl(
                `Items?ExcludeLocationTypes=Virtual&ParentId=${seriesId}&IncludeItemTypes=Season&SortBy=SortName`));
        }

        async function saveAllSeasonSequences(rows, seasonId) {
            var update = [];
            rows.forEach((row) => {
                var id = row.dataset.id;
                update.push({
                    InternalId: id,
                    TitleSequenceStart: row.cells[5].querySelector('div').innerText.replace("00:", "PT").replace(":", "M") + "S",
                    TitleSequenceEnd: row.cells[6].querySelector('div').innerText.replace("00:", "PT").replace(":", "M") + "S",
                    HasTitleSequence: row.cells[4].querySelector('select').value,
                    HasCreditSequence: row.cells[7].querySelector('select').value,
                    SeasonId: seasonId,
                    CreditSequenceStart: 'PT' + row.cells[8].querySelector('div').innerText.replace(":", "H").replace(":", "M").split(":")[0] + "S"
                });
            });

            await ApiClient.updateAllSeasonSequences(update);
        }

        async function saveIntro(row, view) {

            var id = row.dataset.id;
            var seasonSelect = view.querySelector('#selectEmbySeason');
            var options = {
                InternalId         : id,
                TitleSequenceStart : row.cells[5].querySelector('div').innerText.replace("00:", "PT").replace(":", "M") + "S",
                TitleSequenceEnd   : row.cells[6].querySelector('div').innerText.replace("00:", "PT").replace(":", "M") + "S",
                HasTitleSequence   : row.cells[4].querySelector('select').value,
                HasCreditSequence  : row.cells[7].querySelector('select').value,
                SeasonId           : seasonSelect[seasonSelect.selectedIndex].value,
                CreditSequenceStart: row.cells[8].querySelector('div').innerText //.replace("00:", "PT").replace(":", "M") + "S"
            }

            await ApiClient.updateTitleSequence(options);
            
            if (imageExistsInLocalStore(id)) {
                localImageStore = localImageStore.filter(c => c.Id != id);
            }

        }
         
        async function getIntros(seasonId) {
            return await ApiClient.getJSON(ApiClient.getUrl(`SeasonSequences?SeasonId=${seasonId}&StartIndex=${pagination.StartIndex}&Limit=${pagination.Limit}`));
        }

        async function getSequenceVideo(sequence, startTimeTicks) {
            return await ApiClient.getVideoSequence(sequence, startTimeTicks);
        }

        function getSequenceTime(sequence) {
            const titleSequenceStart      = parseISO8601Duration(sequence.TitleSequenceStart);
            const titleSequenceEnd        = parseISO8601Duration(sequence.TitleSequenceEnd);
            const sequenceStartTimeString = titleSequenceStart.hours + ":" + titleSequenceStart.minutes + ":" + titleSequenceStart.seconds;
            const sequenceEndTimeString   = titleSequenceEnd.hours + ":" + titleSequenceEnd.minutes + ":" + titleSequenceEnd.seconds;
            const sequenceStartTime       = new Date('1970-01-01T' + sequenceStartTimeString + 'Z');
            const sequenceEndTime         = new Date('1970-01-01T' + sequenceEndTimeString + 'Z');

            return {
                Start: sequenceStartTime.getTime(),
                End  : sequenceEndTime.getTime()
            }
        }

        //Backend Enum: SequenceImageTypes
        //IntroStart  = 0
        //IntroEnd    = 1
        //CreditStart = 2
        //CreditEnd   = 3

        function getExtractedThumbImage(hasSequence, id, imageFrameTimestamp, sequenceImageType) {
            return new Promise((resolve, reject) => {
                var thumb = !hasSequence
                    ? 'NoTitleSequenceThumbImage'
                    : `ExtractThumbImage?InternalId=${id}&ImageFrameTimestamp=${encodeURIComponent(imageFrameTimestamp)}&SequenceImageType=${sequenceImageType}&api_key=${ApiClient._serverInfo.AccessToken}`;

                
                const url = ApiClient.getUrl(thumb);

                var xhr = new XMLHttpRequest();
                xhr.open("GET", url);
                xhr.responseType = "blob";
                xhr.onload = function ()
                {
                    if (xhr.statusText == "OK") {
                        var reader = new FileReader();
                        reader.readAsDataURL(this.response);
                        reader.onloadend = function() {
                            const base64data = reader.result;
                            resolve(base64data);
                            //return base64data;
                        }
                    } else {
                        resolve("data:image/png;base64,R0lGODlhAQABAIAAAAUEBAAAACwAAAAAAQABAAACAkQBADs=");
                    }
                }
                xhr.send();

            });
          
            
            //return url;
        }

        

        function imageLink(baseItem) {
            return ApiClient._serverAddress +
                "/web/index.html#!/item?id=" +
                baseItem.Id +
                "&serverId=" +
                ApiClient._serverInfo.Id;
        }

        async function renderTableRowHtml(intro) {
            
            var result = await getEpisode(intro.InternalId);

            var html = '';
            var episode = result.Items[0];
            var introStartTimespan = parseISO8601Duration(intro.TitleSequenceStart);
            var introEndTimespan = parseISO8601Duration(intro.TitleSequenceEnd);
            var creditStartTimeSpan = parseISO8601Duration(intro.CreditSequenceStart);

            var hasIntro = intro.HasTitleSequence || (introEndTimespan.minutes !== '00' && introEndTimespan.seconds !== '00'); //<-- looks like we have to check those minute and second values too.
            
            var creditStart = creditStartTimeSpan.hours + ":" + creditStartTimeSpan.minutes + ":" + creditStartTimeSpan.seconds;
            var hasCredit = intro.HasCreditSequence || (creditStartTimeSpan.minutes !== '00');

            html += '<tr data-id="' + episode.Id + '" class="detailTableBodyRow detailTableBodyRow-shaded">';
            
            //Index 2
            html += '<td data-title="EpisodeImage" class="detailTableBodyCell fileCell">'; 
            //html +='<a href="' + imageLink(episode) + '" target="_blank" title="Click to go to Episode">';
            html += '<div style="position:relative; width:175px; height:100px;display:flex; align-items:center; justify-content:center">';
            
            html += '<img style="width:175px; height:100px; position:absolute;" src="' + ApiClient.getPrimaryImageUrl(episode.Id) + '"/>';
            if (hasIntro || introEndTimespan.minutes !== "00" && introEndTimespan.seconds !== "00") {
                html += `<button style="position:absolute; margin-left:1em;" data-id="${episode.Id}" class="playSequence emby-button button-submit fab hide">`;
                html += '<i class="md-icon">play_arrow</i>';
                html += '</button>';
            }
            html += '</div>';
            //html +='</a>'; 
            html += '</td>';
            //Index 3
            html += '<td data-title="Series" class="detailTableBodyCell fileCell">' + episode.SeriesName + '</td>';
            //Index 4
            html += '<td data-title="Season" class="detailTableBodyCell fileCell">' + episode.SeasonName + '</td>';
            //Index 5
            html += '<td data-title="EpisodeIndex" class="detailTableBodyCell fileCell" data-index="' + episode.IndexNumber + '">Episode: ' + episode.IndexNumber + '</td>';

             
            //Index 6
            html += '<td data-title="HasTitleSequence" class="detailTableBodyCell fileCell" style="display:flex;">';
            html += '<div class="selectContainer" style="top:40px">';
            html += '<select is="emby-select" class="emby-select-withcolor emby-select hasIntroSelect">';
            html += '<option value="true" ' + (hasIntro ? 'selected' : "") + '>true</option>';
            html += '<option value="false" ' + (!hasIntro ? 'selected' : "") + '>false</option>';
            html += '</select>';
            html += '<div class="selectArrowContainer" style="top:-23px !important"><div style="visibility:hidden;">0</div><i class="selectArrow md-icon"></i></div>';
            html += '</div>';
            html += '</td">';
                                                                                         
            var introStart = "00:" + introStartTimespan.minutes + ":" + introStartTimespan.seconds;
            var introEnd = "00:" + introEndTimespan.minutes + ":" + introEndTimespan.seconds;


            if (!imageExistsInLocalStore(intro.InternalId)) {
                const introStartImage = await getExtractedThumbImage(hasIntro, intro.InternalId, introStart, 0);
                const introEndImage = await getExtractedThumbImage(hasIntro, intro.InternalId, introEnd, 1);
                const creditStartImage = await getExtractedThumbImage(hasCredit, intro.InternalId, creditStart, 2);
                localImageStore.push({
                    Id                               : intro.InternalId,
                    ExtractedImageTitleSequenceStart : introStartImage,
                    ExtractedImageTitleSequenceEnd   : introEndImage,
                    ExtractedImageCreditSequenceStart: creditStartImage
                });
            }
            
            var imageData = localImageStore.filter(i => i.Id === intro.InternalId)[0];

            var extractedImageTitleSequenceStart  = imageData.ExtractedImageTitleSequenceStart;
            var extractedImageTitleSequenceEnd    = imageData.ExtractedImageTitleSequenceEnd;
            var extractedImageCreditSequenceStart = imageData.ExtractedImageCreditSequenceStart;
          

            //Index 7
            html += '<td style="position:relative" data-title="IntroStart" class="detailTableBodyCell fileCell">';
            html += `<div class="editTimestamp introStartContentEditable" contenteditable>${introStart}</div>`;
            html += `<img class="introStartThumb lazy" style="width:175px; height:100px" src="${extractedImageTitleSequenceStart}"/>`;
            html += '</td>';
            //Index 8
            html += '<td style="position:relative" data-title="IntroEnd" class="detailTableBodyCell fileCell">';
            html += `<div class="editTimestamp introEndContentEditable" contenteditable>${introEnd}</div>`;
            html += `<img class="introEndThumb lazy" style="width:175px; height:100px" src="${extractedImageTitleSequenceEnd}"/>`;
            html += '</td>';
            //Index 9
            html += '<td data-title="HasCreditSequence" class="detailTableBodyCell fileCell" style="display:flex;">';
            html += '<div class="selectContainer" style="top:40px">';
            html += '<select is="emby-select" class="emby-select-withcolor emby-select hasCreditSelect">';
            html += '<option value="true" ' + (hasCredit ? 'selected' : "") + '>true</option>';
            html += '<option value="false" ' + (!hasCredit ? 'selected' : "") + '>false</option>';
            html += '</select>';
            html += '<div class="selectArrowContainer" style="top:-23px !important"><div style="visibility:hidden;">0</div><i class="selectArrow md-icon"></i></div>';
            html += '</div>';
            html += '</td">';
            //Index 10
            html += '<td style="position:relative" data-title="CreditsStart" class="detailTableBodyCell fileCell">';
            html += `<div class="editTimestamp creditStartContentEditable" contenteditable>${creditStart}</div>`;
            html += `<img class="creditStartThumb lazy" style="width:175px; height:100px" src="${extractedImageCreditSequenceStart}"/>`;
            html += '</td>';
            //Index 11
            html += '<td data-title="titleSequenceDataActions" class="detailTableBodyCell fileCell">';

            html += `<button style="margin-left: 1em;" data-id="${episode.Id}" class="saveSequence emby-button button-submit">`;
            html += '<span>Save</span>';
            html += '</button>';

            
            html += '<td class="detailTableBodyCell organizerButtonCell" style="whitespace:no-wrap;"></td>';
            html += '</tr>';

            return html;

        }

        //async function loadImageInLocalCache(hasIntro, hasCredit, intro, introStart, creditStart, introEnd) {
        //    const introStartImage = await getExtractedThumbImage(hasIntro, intro.InternalId, introStart, 0);
        //    const introEndImage = await getExtractedThumbImage(hasIntro, intro.InternalId, introEnd, 1);
        //    const creditStartImage = await getExtractedThumbImage(hasCredit, intro.InternalId, creditStart, 2);
        //    localImageCache.push({
        //        Id                               : intro.InternalId,
        //        ExtractedImageTitleSequenceStart : introStartImage,
        //        ExtractedImageTitleSequenceEnd   : introEndImage,
        //        ExtractedImageCreditSequenceStart: creditStartImage
        //    });
        //}

        function imageExistsInLocalStore(id) {
            return localImageStore.filter(c => c.Id == id).length > 0;
        }
        
        function renderTableItems(sequences, view) {
            
            view.querySelector('.introResultBody').innerHTML = '';
            sequences.forEach(async (sequence) => {
                
                var html = await renderTableRowHtml(sequence);

                var tableBody = view.querySelector('.introResultBody');
                tableBody.innerHTML += html;
                fadeIn(tableBody);

                view.querySelectorAll('.editTimestamp').forEach(edit => {
                    edit.style = "position: absolute;bottom: 7px;left: 1px;color: white;background: black;width: 175px;";
                    fadeIn(edit);
                });
                view.querySelectorAll('.hasIntroSelect').forEach(element => {
                    element.addEventListener('change',
                        async (e) => {
                            e.preventDefault();
                            if (e.target.value === 'false') {  //<--Switch the select box to no intro
                                const row = e.target.closest('tr');
                                row.querySelector('.introStartContentEditable').innerText = "00:00:00";
                                row.querySelector('.introEndContentEditable').innerText = "00:00:00";
                                row.querySelector('.introStartThumb').src = await getExtractedThumbImage(false, e.target.id, 0);
                                row.querySelector('.introEndThumb').src = await getExtractedThumbImage(false, e.target.id, 1);
                            }
                        });
                });

                view.querySelectorAll('.hasCreditSelect').forEach(element => {
                    element.addEventListener('change',
                        async (e) => {
                            e.preventDefault();
                            if (e.target.value === 'false') {    //<--Switch the select box to no credit
                                const row = e.target.closest('tr');
                                row.querySelector('.creditStartContentEditable').innerText = "00:00:00";
                                row.querySelector('.creditStartThumb').src = await getExtractedThumbImage(false, e.target.id, 2);
                            } 
                        });
                });
                

                view.querySelectorAll('.saveSequence').forEach(async (btn) => {
                    btn.addEventListener('click',
                        async (elem) => {
                            elem.preventDefault();
                            var row = elem.target.closest('tr');
                            await saveIntro(row, view);

                            var seriesSelect = view.querySelector('#selectEmbySeries');
                            var seasonSelect = view.querySelector('#selectEmbySeason');
                            
                            var seasonId = seasonSelect[seasonSelect.selectedIndex].value;
                            var seriesId = seriesSelect[seriesSelect.selectedIndex].value;

                            var seasons = await getSeasons(seriesId);
                            var season = seasons.Items.filter(s => s.Id === seasonId)[0];

                            await loadPageData(season, view);
                        });
                });

                view.querySelectorAll('.introStartThumb').forEach(thumb => {
                    thumb.addEventListener('click',
                        (e) => {
                            e.target.closest('td').querySelector('.editTimestamp').focus();
                        });
                });

                view.querySelectorAll('.introEndThumb').forEach(thumb => {
                    thumb.addEventListener('click',
                        (e) => {
                            e.target.closest('td').querySelector('.editTimestamp').focus();
                        });
                });

                view.querySelectorAll('[data-title="EpisodeImage"]').forEach(img => {
                    img.addEventListener('mouseenter',
                        (elem) => {
                            var btn = elem.target.querySelector('.playSequence');
                            if (btn) {
                                btn.classList.remove('hide');
                            }
                        });

                    img.addEventListener('mouseleave',
                        (elem) => {
                            var btn = elem.target.querySelector('.playSequence');
                            if (btn) {
                                btn.classList.add('hide');
                            }
                        });
                });

                view.querySelectorAll('.playSequence').forEach(async (btn) => {
                    btn.addEventListener('click',
                        async (elem) => {
                            elem.preventDefault();
                            var row = elem.target.closest('tr');
                            var id = row.dataset.id;
                            dlgIntroPlayer(view, id);
                        });
                });
                sortTable(view);
                loading.hide();
            });

            
        }

        //function reloadTableItems(sequences, view) {
        //    loading.show();
        //    view.querySelector('.introResultBody').innerHTML = '';
        //    renderTableItems(sequences, view);
        //    loading.hide();
        //}

        async function dlgIntroPlayer(view, id) {
            var dlg = dialogHelper.createDialog({
                removeOnClose: true,
                size: 'small'
            });

            dlg.classList.add('ui-body-a');
            dlg.classList.add('background-theme-a');

            dlg.classList.add('formDialog');
            dlg.style.maxWidth = '25%';
            dlg.style.maxHeight = '55%';
            const seasonSelect = view.querySelector('#selectEmbySeason');
            const result = await getIntros(seasonSelect.value);
            const sequence = result.TitleSequences.filter(s => s.InternalId == id)[0];
            var sequenceTime = getSequenceTime(sequence);
            const sequenceStartTimeTicks = ((sequenceTime.Start * 10000) + 621355968000000000);

            var html = '';
            html += '<div class="formDialogHeader">';
            html += '<button is="paper-icon-button-light" class="btnCancel autoSize" tabindex="-1"><i class="md-icon">&#xE5C4;</i></button>';
            html += `<h3 class="formDialogHeaderTitle">Title Sequence</h3>`;
            html += '</div>';

            html += '<div class="formDialogContent" style="margin:2em">';
            html += '<div class="dialogContentInner" style="max-width: 100%; display: flex;align-items: center;justify-content: center;">';

            html += '<video style="width:100%; height:100%" preload="metadata" autoplay="autoplay" webkit-playsinline="" playsinline="" crossorigin="anonymous" controls src="' + await getSequenceVideo(sequence, sequenceStartTimeTicks) + '"></video>';
            
            html += '</div>';
            html += '</div>';

            dlg.innerHTML = html;

            const video = dlg.querySelector('video');
            video.onprogress = function() {
                if (video.currentTime >= (sequenceTime.End - sequenceTime.Start )/1000) {
                    video.pause();
                }
            };
            dlg.querySelectorAll('.btnCancel').forEach(btn => {
                btn.addEventListener('click', (e) => {
                    dialogHelper.close(dlg);
                });
            });
            dialogHelper.open(dlg);
        }

        function confirm_dlg(view, confirmAction) {
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
            html += `<h3 class="formDialogHeaderTitle">${confirmAction === "ClearAll" ? "Reset Season" : "Confirm Season"}</h3>`;
            html += '</div>';
            html += '<div class="formDialogContent" style="margin:2em">';
            html += '<div class="dialogContentInner" style="max-width: 100%; max-height:100%; display: flex;align-items: center;justify-content: center;">';

            //Submit - Confirm
            html += '<button is="emby-button" type="button" style="width:35%" class="btnConfirm raised button-submit block emby-button">';
            html += '<span>Confirm</span>';
            html += '</button>';

            //Cancel
            html += '<button is="emby-button" type="button" class="btnCancel raised button-cancel">';
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


            dlg.querySelector('.btnConfirm').addEventListener('click', async () => {
                var seasonSelect = view.querySelector('#selectEmbySeason');
                var seasonId = seasonSelect[seasonSelect.selectedIndex].value;
                switch (confirmAction) {
                    case "ClearAll":
                        await clearAll(seasonId, view);
                        dialogHelper.close(dlg);
                        break;
                    case "ConfirmAll":
                        await confirmAll(seasonId, view);
                        dialogHelper.close(dlg);
                        break;
                }
            });

            async function clearAll(seasonId, page) {
                ApiClient.deleteSeasonData(seasonId).then(async () => {
                    //var result = await getIntros(seasonId);
                    var seriesSelect = view.querySelector('#selectEmbySeries');
                    var seriesId = seriesSelect[seriesSelect.selectedIndex].value;
                    var seasons = await getSeasons(seriesId);
                    var season = seasons.Items.filter(s => s.Id === seasonId)[0];
                    pagination.TotalRecordCount = 0;
                    await loadPageData(season, page);
                });
            }

            //async function confirmAll(seasonId, page) {
            //    ApiClient.updateAllSeasonSequences(seasonId).then(async () => {
            //        var result = await getIntros(seasonId);
            //        renderTableItems(result.TitleSequences, page);
            //    });

            //}

            dialogHelper.open(dlg);
        }

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
                    x = parseInt(rows[i].getElementsByTagName("TD")[3].dataset.index);
                    y = parseInt(rows[i + 1].getElementsByTagName("TD")[3].dataset.index);
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

        function fadeIn(element) {
            element.animate([
                // keyframes
                { opacity: '0' },
                { opacity: '1' }
            ], {
                // timing options
                duration: 1000,
                fill: 'forwards',
                delay: 100
            });
        }

        function enableImageCache(enabled) {
            ApiClient.getPluginConfiguration(pluginId).then((config) => {
                config.ImageCache = enabled;
                ApiClient.updatePluginConfiguration(pluginId, config).then((r) => {
                    Dashboard.processPluginConfigurationUpdateResult(r);
                });
            });
        }

        async function loadPageData(season, view) {
            
            const removeSeasonalFingerprintButton = view.querySelector('.removeSeasonalFingerprintData');
            const pagingContainer = view.querySelector('.pagingContainer');
            
            const result = await getIntros(season.Id);

            pagingContainer.innerHTML = '';

            if (result) {
                if (result.TitleSequences) {

                    pagination.TotalRecordCount = result.TotalRecordCount;
                    pagingContainer.innerHTML += getPagingHtml();

                    view.querySelector('.introResultBody').innerHTML = "";
                    const averageLength = parseISO8601Duration(result.CommonEpisodeTitleSequenceLength);

                    removeSeasonalFingerprintButton.querySelector('span').innerHTML = `Reset ${season.Name} Data`;
                    if (removeSeasonalFingerprintButton.classList.contains('hide')) {
                        removeSeasonalFingerprintButton.classList.remove('hide');
                    }

                    view.querySelector('.averageTitleSequenceTime').innerText = `00:${averageLength.minutes}:${averageLength.seconds}`;

                    renderTableItems(result.TitleSequences, view);


                    view.querySelector('.btnPreviousPage').addEventListener('click', async (btn) => {
                        btn.preventDefault();
                       
                        pagination.StartIndex -= pagination.Limit;
                        await loadPageData(season, view);
                         

                    });

                    view.querySelector('.btnNextPage').addEventListener('click', async (btn) => {
                        btn.preventDefault();
                        
                        pagination.StartIndex += pagination.Limit;
                        await loadPageData(season, view);
                         
                    });

                        
                } else {

                    view.querySelector('.averageTitleSequenceTime').innerText = "Currently scanning series...";
                    if (!removeSeasonalFingerprintButton.classList.contains('hide')) {
                        removeSeasonalFingerprintButton.classList.add('hide');
                    }
                }
            }
            
        }

        return function (view) {
            view.addEventListener('viewshow', async () => {

                loading.show();

                if (!ApiClient.HasChromaprint()) {
                    view.querySelector('.chromaprintAlert').classList.remove('hide');
                }

                const isMobile = window.matchMedia("only screen and (max-width: 1676px)").matches;
                if (!isMobile) {
                    view.querySelector('.detailLogo').classList.remove('hide');
                }

                window.onresize = function() {
                    if (isMobile) {
                        view.querySelector('.detailLogo').classList.add('hide');
                    }
                }

                var imageCacheToggle                = view.querySelector('#enableImageCache');
                var seriesSelect                    = view.querySelector('#selectEmbySeries');
                var seasonSelect                    = view.querySelector('#selectEmbySeason');
                var removeSeasonalFingerprintButton = view.querySelector('.removeSeasonalFingerprintData');
                var primaryImage                    = view.querySelector('.detailLogo');


                mainTabsManager.setTabs(this, 0, getTabs);

                ApiClient.getPluginConfiguration(pluginId).then((config) => {
                    imageCacheToggle.checked = config.ImageCache;
                });

                document.querySelector('.pageTitle').innerHTML = "Intro Skip " +
                    '<a is="emby-linkbutton" class="raised raised-mini headerHelpButton emby-button" target="_blank" href="https://emby.media/community/index.php?/topic/101687-introskip-instructions-beta-releases/"><i class="md-icon button-icon button-icon-left secondaryText headerHelpButtonIcon">help</i><span class="headerHelpButtonText">Help</span></a>';

                imageCacheToggle.addEventListener('change', (elem) => {
                    elem.preventDefault();
                    var enabled = view.querySelector('#enableImageCache').checked;
                    enableImageCache(enabled);
                });
                 
                var series = await getSeries();

                for (let i = 0; i <= series.Items.length - 1; i++) {
                    seriesSelect.innerHTML += `<option value="${series.Items[i].Id}">${series.Items[i].Name}</option>`;
                }

                var seriesId = seriesSelect[seriesSelect.selectedIndex].value;
                var seasons = await getSeasons(seriesId);

                for (let j = 0; j <= seasons.Items.length - 1; j++) {
                    seasonSelect.innerHTML += `<option data-index="${seasons.Items[j].IndexNumber}" value="${seasons.Items[j].Id}">${seasons.Items[j].Name}</option>`;
                }

                var seasonId = seasonSelect[seasonSelect.selectedIndex].value;

                var season = seasons.Items.filter(s => s.Id === seasonId)[0];
                
                await loadPageData(season, view);
                
                primaryImage.innerHTML = `<img src="${ApiClient.getPrimaryImageUrl(seriesId)}"/>`;
                fadeIn(primaryImage);

                seasonSelect.addEventListener('change', async (e) => {
                    e.preventDefault();
                    loading.show();
                    seriesId = seriesSelect[seriesSelect.selectedIndex].value;
                    seasonId = seasonSelect[seasonSelect.selectedIndex].value;
                    season = seasons.Items.filter(s => s.Id === seasonId)[0];
                    pagination.StartIndex = 0;
                    pagination.Limit = 5;
                    await loadPageData(season, view);
                    
                });

                seriesSelect.addEventListener('change', async (e) => {
                    e.preventDefault();
                    loading.show();
                    seasonSelect.innerHTML = '';
                    seriesId = seriesSelect[seriesSelect.selectedIndex].value;
                    seasons = await getSeasons(seriesId);
                    for (let j = 0; j <= seasons.Items.length - 1; j++) {
                        seasonSelect.innerHTML += `<option data-index="${seasons.Items[j].IndexNumber}" value="${seasons.Items[j].Id}">${seasons.Items[j].Name}</option>`;
                    }
                    seasonId = seasonSelect[0].value;
                    season = seasons.Items.filter(s => s.Id === seasonId)[0];
                    pagination.StartIndex = 0;
                    pagination.Limit = 5;
                    await loadPageData(season, view);
                    primaryImage.innerHTML = `<img src="${ApiClient.getPrimaryImageUrl(seriesId)}"/>`;
                    fadeIn(primaryImage);
                    
                });

                removeSeasonalFingerprintButton.addEventListener('click', (e) => {
                    e.preventDefault();
                    confirm_dlg(view, "ClearAll");
                });
                
                view.querySelector('.saveAll').addEventListener('click', async (btn) => {
                    btn.preventDefault();
                    loading.show();
                    var rows = view.querySelectorAll('.introResultBody > tr');
                    await saveAllSeasonSequences(rows, seasonId);
                    var introResult = await getIntros(seasonId);
                    renderTableItems(introResult.TitleSequences, view);
                    
                });
                
                

            });
        }
    });