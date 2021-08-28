using Emby.AutoOrganize.Data;
using IntroSkip.AudioFingerprinting;
using IntroSkip.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.Linq;

namespace IntroSkip
{
    public class IntroSkipPluginEntryPoint : IServerEntryPoint
    {
        public static IntroSkipPluginEntryPoint Instance { get; set; }
        public ITitleSequenceRepository Repository {get; set;}
        
        private IServerConfigurationManager _config { get; set; }
        private ILogger _logger {get; set;}
        private IJsonSerializer _json { get; set; }

        private readonly int CurrentVersion = 3;

        public IntroSkipPluginEntryPoint(ILogger logger, IServerConfigurationManager config, IJsonSerializer json)
        {
            _logger = logger;
            _json = json;
            _config = config;
            Instance = this;
        }

        public void Dispose()
        {
            var repo = Repository as IDisposable;
            if (repo != null)
            {
                repo.Dispose();
            }
        }

        public void Run()
        {
                    

            try
            {
                Repository = GetRepository();
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error initializing title sequence database", ex);
            }         
             
            _logger.Info("Database loaded Sucessfully...");
            //if(config.Version != CurrentVersion)
            //{   
            //    _logger.Info("Updating database...");
            //    Repository = UpdateDataBase(config);

            //} 

        }

       

        

        private ITitleSequenceRepository GetRepository()
        {
            var repo = new SqliteTitleSequenceRepository(_logger, _config.ApplicationPaths, _json);

            repo.Initialize();

            return repo;
        }
    }
}
