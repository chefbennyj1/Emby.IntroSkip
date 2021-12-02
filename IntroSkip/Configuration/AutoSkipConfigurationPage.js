﻿define(["loading", "dialogHelper", "mainTabsManager", "formDialogStyle", "emby-checkbox", "emby-select", "emby-toggle"],
    function(loading, dialogHelper, mainTabsManager) {

        var pluginId = "93A5E794-E0DA-48FD-8D3A-606A20541ED6";

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
                }
            ];
        }

        async function getUsers() {
            return await ApiClient.getJSON(ApiClient.getUrl('Users'));
        }

        async function getUser(id) {
            return await ApiClient.getJSON(ApiClient.getUrl('Users/' + id ));
        }

        //function getUserSelectOptionsHtml(user) {
        //    var html = '';
        //    html += '<option value="' + user.Id + '">' + user.Name + ' </option>';
        //    return html;
        //}

        function getListItemHtml(user, padding) {
            var html = '';
            html += '<div class="virtualScrollItem listItem listItem-border focusable listItemCursor listItem-hoverable listItem-withContentWrapper" tabindex="0" draggable="false" style="transform: translate(0px, ' + padding + 'px);">';
            html += '<div class="listItem-content listItemContent-touchzoom">';
            html += '<div class="listItemBody itemAction listItemBody-noleftpadding">';
            html += '<div class="listItemBodyText listItemBodyText-nowrap">' + user.Name + '</div>';
            html += '<div class="listItemBodyText listItemBodyText-secondary listItemBodyText-nowrap">Intro Auto Skip enabled for this account.</div>';
            html += '</div>';
            html += '<button title="Remove" aria-label="Remove" type="button" is="paper-icon-button-light" class="listItemButton itemAction paper-icon-button-light icon-button-conditionalfocuscolor removeItemBtn" id="' + user.Id + '">';
            html += '<i class="md-icon removeItemBtn" style="pointer-events: none;">delete</i>';
            html += '</button> ';
            html += '</div>';
            html += '</div>';
            return html;
        }

        function handleRemoveItemClick(e, element, view) {
            var id = e.target.closest('button').id;
            ApiClient.getPluginConfiguration(pluginId).then((config) => {
                var filteredList = config.AutoSkipUsers.filter(userId => userId != id);
                config.AutoSkipUsers = filteredList;
                ApiClient.updatePluginConfiguration(pluginId, config).then((r) => {
                    reloadList(filteredList, element, view);
                    loadUsersSelect(config, view);
                    Dashboard.processPluginConfigurationUpdateResult(r);
                });
            });
        }

        function reloadList(list, element, view) {
            element.innerHTML = '';
            if (list && list.length) {
                var padding = 0;
                list.forEach(async item => {
                    var user = await getUser(item);
                    element.innerHTML += getListItemHtml(user, padding);
                    padding += 77; //Why is this padding necessary
                    var removeButtons = view.querySelectorAll('.removeItemBtn');
                    removeButtons.forEach(btn => {
                        btn.addEventListener('click',
                            el => {
                                el.preventDefault();
                                handleRemoveItemClick(el, element, view);
                            });
                    });

                });
            }
        }

        function loadUsersSelect(config, view) {
            var usersSelect = view.querySelector('#selectEmbyUsers');
            usersSelect.innerHTML = '';
            getUsers().then(users => {
                for (let i = 0; i <= users.length - 1; i++) {
                    if (config.AutoSkipUsers.includes(users[i].Id)) {
                        continue;
                    }
                    usersSelect.innerHTML += '<option value="' + users[i].Id + '">' + users[i].Name + '</option>';
                }
            });
        }

        return function(view) {
            view.addEventListener('viewshow',
                async () => {
                    mainTabsManager.setTabs(this, 3, getTabs);
                    var chkEnableAutoSkip = view.querySelector('#autoSkipTitleSequence');
                    var userList = view.querySelector('.user-list');
                    var userSelect = view.querySelector('#selectEmbyUsers');
                    var addToAllowList = view.querySelector('#btnAddUserToAutoSkipList');
                    var chkMessageOnAutoSkipTitleSequence = view.querySelector('#chkMessageOnAutoSkipTitleSequence');
                    var chkIgnoreEpisodeOneTitleSequenceSkip = view.querySelector('#chkIgnoreEpisodeOneTitleSequenceSkip');
                    var txtMessageDuration = view.querySelector('#txtMessageDuration');

                    ApiClient.getPluginConfiguration(pluginId).then((config) => {

                        chkEnableAutoSkip.checked = config.EnableAutoSkipTitleSequence ?? false;
                        chkMessageOnAutoSkipTitleSequence.checked = config.ShowAutoTitleSequenceSkipMessage ?? true;
                        chkIgnoreEpisodeOneTitleSequenceSkip.checked = config.IgnoreEpisodeOneTitleSequenceSkip ?? false;
                        txtMessageDuration.value = config.AutoTitleSequenceSkipMessageDuration;

                        if (config.AutoSkipUsers) {
                            reloadList(config.AutoSkipUsers, userList, view);
                        }

                    });

                    chkIgnoreEpisodeOneTitleSequenceSkip.addEventListener('change', (elem) => {
                        elem.preventDefault();
                        var ignoreEpisodeOneTitleSequenceSkip = chkIgnoreEpisodeOneTitleSequenceSkip.checked;
                        enableIgnoreEpisodeOneTitleSequenceSkip(ignoreEpisodeOneTitleSequenceSkip);
                    });

                    chkEnableAutoSkip.addEventListener('change', (elem) => {
                        elem.preventDefault();
                        var autoSkip = chkEnableAutoSkip.checked;
                        enableAutoSkip(autoSkip);
                    });

                    chkMessageOnAutoSkipTitleSequence.addEventListener('change', (elem) => {
                        elem.preventDefault();
                        var showMessage = chkMessageOnAutoSkipTitleSequence.checked;
                        enableShowMessage(showMessage);
                        if (showMessage) {
                            view.querySelector('.messageDuration').classList.remove('hide');
                        } else {
                            view.querySelector('.messageDuration').classList.add('hide');
                        }
                    });

                    txtMessageDuration.addEventListener('change', (elem) => {
                        elem.preventDefault();
                        var duration = txtMessageDuration.value;
                        updateMessageDuration(duration);
                    });

                    ApiClient.getPluginConfiguration(pluginId).then((config) => {
                        loadUsersSelect(config, view);
                    });
                    //var users = await getUsers();
                    //users.forEach(user => {
                    //    userSelect.innerHTML += getUserSelectOptionsHtml(user);
                    //})
                    
                    addToAllowList.addEventListener('click', (e) => {
                        e.preventDefault();
                        loading.show();

                        var userId = userSelect[userSelect.selectedIndex].value;

                        ApiClient.getPluginConfiguration(pluginId).then((config) => {

                            if (config.AutoSkipUsers) {

                                config.AutoSkipUsers.push(userId);

                            } else {

                                config.AutoSkipUsers = [ userId ];

                            }
                            ApiClient.updatePluginConfiguration(pluginId, config).then((r) => {
                                reloadList(config.AutoSkipUsers, userList, view);
                                loadUsersSelect(config, view);
                                Dashboard.processPluginConfigurationUpdateResult(r); 
                            });

                        });
                        
                        loading.hide();
                    });

                    function enableIgnoreEpisodeOneTitleSequenceSkip(episodeOneTitleSequenceSkip) {
                        ApiClient.getPluginConfiguration(pluginId).then((config) => {
                            config.IgnoreEpisodeOneTitleSequenceSkip = episodeOneTitleSequenceSkip;
                            ApiClient.updatePluginConfiguration(pluginId, config).then(() => {});
                        });
                    }
                    
                    function updateMessageDuration(duration) {
                        ApiClient.getPluginConfiguration(pluginId).then((config) => {
                            config.AutoTitleSequenceSkipMessageDuration = duration;
                            ApiClient.updatePluginConfiguration(pluginId, config).then(() => {});
                        });
                    }

                    function enableAutoSkip(autoSkip) {
                        ApiClient.getPluginConfiguration(pluginId).then((config) => {
                            config.EnableAutoSkipTitleSequence = autoSkip;
                            ApiClient.updatePluginConfiguration(pluginId, config).then(() => {});
                        });
                    }

                    function enableShowMessage(showMessage) {
                        ApiClient.getPluginConfiguration(pluginId).then((config) => {
                            config.ShowAutoTitleSequenceSkipMessage = showMessage;
                            ApiClient.updatePluginConfiguration(pluginId, config).then(() => {});
                        });
                    }
                });
        }
    });