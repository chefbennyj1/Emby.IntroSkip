define(["loading", "dialogHelper", "formDialogStyle"],
    function(loading, dialogHelper) {

        var pluginId = "93A5E794-E0DA-48FD-8D3A-606A20541ED6";
                
        //Probably doesn't need an configuration, but it's set up if we need it

        return function(view) {
            view.addEventListener('viewshow',
                () => {

                    ApiClient.getPluginConfiguration(pluginId).then(
                        (config) => {

                        });


                });
        }
    });