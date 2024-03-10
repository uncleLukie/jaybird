using jaybird.Services;
using jaybird.Utils;
using Newtonsoft.Json;

namespace jaybird
{
    class Program
    {
        static async Task Main() 
        {
            var config = LoadConfiguration(); 
            var audioService = new AudioService(config);  
            var consoleHelper = new ConsoleHelper(audioService);
            
            Thread consoleThread = new Thread(consoleHelper.Run);
            consoleThread.Start();
            
            string currentStreamUrl = GetStreamUrlForStation(consoleHelper.GetCurrentStation(), config);
            await audioService.PlayStream(currentStreamUrl); 
            
            while (true)
            {
                if (consoleHelper.TogglePlayPauseRequested())
                {
                    await audioService.TogglePlayPause();
                }

                int previousStation = consoleHelper.GetCurrentStation(); 
                Thread.Sleep(100);
                if (previousStation != consoleHelper.GetCurrentStation())
                {
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
