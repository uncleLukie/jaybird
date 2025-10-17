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
    public static AppConfig Config { get; private set; }

    static async Task Main()
    {
        // Display platform information for diagnostics
        DisplayPlatformInfo();

        Config = LoadConfiguration();
        var audioService = new AudioService(Config);
        var songRetrievalService = new SongRetrievalService(Config);
        var discordService = new DiscordService(Config.Discord.ApplicationId);
        discordService.Initialize();
        var consoleHelper = new ConsoleHelper(audioService, songRetrievalService, discordService);
        
        AppDomain.CurrentDomain.ProcessExit += (s, e) => discordService.Shutdown();
        
        Station initialStation = consoleHelper.GetCurrentStation();
        string initialStreamUrl = GetStreamUrlForStation(initialStation, Config);
        await audioService.PlayStream(initialStreamUrl);
        
        await Task.Run(async () => await consoleHelper.Run());
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

    private static void DisplayPlatformInfo()
    {
        var platform = GetPlatformName();
        var architecture = RuntimeInformation.ProcessArchitecture;
        var framework = RuntimeInformation.FrameworkDescription;

        Console.WriteLine("==============================================");
        Console.WriteLine("  jaybird - ABC Radio CLI Player");
        Console.WriteLine("==============================================");
        Console.WriteLine($"Platform:     {platform}");
        Console.WriteLine($"Architecture: {architecture}");
        Console.WriteLine($"Framework:    {framework}");
        Console.WriteLine("==============================================");
        Console.WriteLine();
    }

    private static string GetPlatformName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "macOS";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "Linux";
        return "Unknown";
    }
}
