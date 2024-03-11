namespace jaybird;

using Services;
using Utils;
using Models;
using Newtonsoft.Json;

class Program
{
    public static AppConfig Config { get; private set; }

    static async Task Main()
    {
        Config = LoadConfiguration();
        var audioService = new AudioService(Config);
        var songRetrievalService = new SongRetrievalService(Config);
        var discordService = new DiscordService(Config.Discord.ApplicationId);
        discordService.Initialize();
        var consoleHelper = new ConsoleHelper(audioService, songRetrievalService, discordService);
        
        AppDomain.CurrentDomain.ProcessExit += (s, e) => discordService.Shutdown();

        // Start playing the initial stream.
        Station initialStation = consoleHelper.GetCurrentStation();
        string initialStreamUrl = GetStreamUrlForStation(initialStation, Config);
        await audioService.PlayStream(initialStreamUrl);

        // Start the console helper in a separate task.
        await Task.Run(async () => await consoleHelper.Run());
    }

    public static string GetStreamUrlForStation(Station station, AppConfig config)
    {
        return station switch
        {
            Station.TripleJ => config.Audio.TripleJStreamUrl,
            Station.DoubleJ => config.Audio.DoubleJStreamUrl,
            Station.Unearthed => config.Audio.UnearthedStreamUrl,
            _ => throw new System.ArgumentOutOfRangeException(nameof(station),
                $"Not expected station value: {station}"),
        };
    }

    private static AppConfig LoadConfiguration()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var configPath = Path.Combine(baseDirectory, "config", "appsettings.json");
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Could not find appsettings.json at {configPath}");
        }

        var jsonConfig = File.ReadAllText(configPath);
        return JsonConvert.DeserializeObject<AppConfig>(jsonConfig);
    }
}