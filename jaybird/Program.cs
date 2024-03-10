using jaybird.Services;
using jaybird.Utils;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace jaybird
{
    class Program
    {
        static async Task Main() 
        {
            var config = LoadConfiguration(); 
            var audioService = new AudioService(config);  
            var consoleHelper = new ConsoleHelper();
            
            Thread consoleThread = new Thread(consoleHelper.Run);
            consoleThread.Start();
            
            // Initial play without checking play state
            string currentStreamUrl = GetStreamUrlForStation(consoleHelper.GetCurrentStation(), config);
            await audioService.PlayStream(currentStreamUrl); 
            
            while (true)
            {
                // Toggle play/pause based on console input
                if (consoleHelper.TogglePlayPauseRequested())
                {
                    await audioService.TogglePlayPause();
                }

                int previousStation = consoleHelper.GetCurrentStation(); 
                Thread.Sleep(100); // Reduce CPU usage
                if (previousStation != consoleHelper.GetCurrentStation())
                {
                    // Stop and play the new station stream
                    string newStreamUrl = GetStreamUrlForStation(consoleHelper.GetCurrentStation(), config);
                    await audioService.PlayStream(newStreamUrl); 
                }
            }
        }

        private static string GetStreamUrlForStation(int stationIndex, AppConfig config)
        {
            switch(stationIndex)
            {
                case 0:  return config.Audio.TripleJStreamUrl;  
                case 1:  return config.Audio.DoubleJStreamUrl;
                case 2:  return config.Audio.UnearthedStreamUrl;
                default: throw new ArgumentException("Invalid station index");
            }
        }

        private static AppConfig LoadConfiguration()
        {
            string baseDirectory = AppContext.BaseDirectory;
            
            string configPath = Path.Combine(baseDirectory, "config", "appsettings.json");
            
            Console.WriteLine($"Config path: {configPath}");
            
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"Could not find appsettings.json at {configPath}");
            }

            string jsonConfig = File.ReadAllText(configPath);
            return JsonConvert.DeserializeObject<AppConfig>(jsonConfig);
        }
    }
    
    public struct AppConfig 
    {
        public AudioConfig Audio {get; set;} 
    }

    public struct AudioConfig
    {
        public string TripleJStreamUrl { get; set; }
        public string DoubleJStreamUrl { get; set; }
        public string UnearthedStreamUrl { get; set; }
    }
}
