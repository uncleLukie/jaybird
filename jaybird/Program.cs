namespace jaybird;

using Services;
using Utils;
using Models;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

class Program
{
    public static AppConfig Config { get; private set; } = null!;
    public static UserSettings UserSettings { get; private set; } = null!;

    static async Task Main()
    {
        // Platform info is now displayed in the UI footer
        Utils.DebugLogger.LogStartup();

        Config = LoadConfiguration();
        var settingsService = new SettingsService();
        UserSettings = await settingsService.LoadSettingsAsync();
        var songCacheService = new SongCacheService();
        var timezoneService = new TimezoneService();
        
        AudioService? audioService = null;
        try
        {
            audioService = new AudioService(Config, settingsService);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not initialize AudioService: {ex.Message}");
            Console.WriteLine("Running in test mode without audio functionality.");
        }
        var songRetrievalService = new RegionalSongRetrievalService(Config, timezoneService);
        var discordService = new DiscordService(Config.Discord.ApplicationId);

        // Initialize Discord RPC in background to avoid blocking startup
        _ = Task.Run(() =>
        {
            try
            {
                discordService.Initialize();
                Utils.DebugLogger.Log("Discord RPC initialized", "Program");
            }
            catch (Exception ex)
            {
                Utils.DebugLogger.LogException(ex, "Program.DiscordInitialize");
            }
        });

        var consoleHelper = new ConsoleHelper(audioService!, songRetrievalService, discordService, settingsService, songCacheService, timezoneService, UserSettings);

        AppDomain.CurrentDomain.ProcessExit += (s, e) => discordService.Shutdown();

        // Initialize console helper (loads cached song data for instant display)
        await consoleHelper.InitializeAsync();

        // Get initial station and region from console helper
        var initialStation = consoleHelper.GetCurrentStation();
        var initialRegion = consoleHelper.GetCurrentRegion();
        if (audioService != null)
        {
            string initialStreamUrl = audioService.GetRegionalStreamUrl(initialStation, initialRegion);
            await audioService.PlayStream(initialStreamUrl, initialRegion);
        }

        // Start the main UI
        await consoleHelper.Run();
    }

    public static string GetStreamUrlForStation(Station station, AppConfig config)
    {
        return station switch
        {
            Station.TripleJ => config.Audio.TripleJStreamUrl,
            Station.DoubleJ => config.Audio.DoubleJStreamUrl,
            Station.Unearthed => config.Audio.UnearthedStreamUrl,
            _ => throw new System.ArgumentOutOfRangeException(nameof(station), $"Not expected station value: {station}"),
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

        // Updated to use System.Text.Json for deserialization
        var config = JsonSerializer.Deserialize<AppConfig>(jsonConfig, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (config == null)
        {
            throw new InvalidOperationException("Failed to deserialize appsettings.json.");
        }

        return config;
    }

}
