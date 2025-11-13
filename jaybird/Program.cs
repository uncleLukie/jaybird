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
        Utils.DebugLogger.LogStartup();

        var loadingScreen = new LoadingScreen();

        AudioService? audioService = null;
        RegionalSongRetrievalService? songRetrievalService = null;
        DiscordService? discordService = null;
        ConsoleHelper? consoleHelper = null;

        try
        {
            await loadingScreen.ShowAsync(async (loader) =>
            {
                // STEP 1: Load Configuration (fast)
                loader.StartStep(LoadingScreen.LoadingStep.Configuration);
                Config = LoadConfiguration();
                var settingsService = new SettingsService();
                UserSettings = await settingsService.LoadSettingsAsync();
                var songCacheService = new SongCacheService();
                var timezoneService = new TimezoneService();
                loader.CompleteStep(LoadingScreen.LoadingStep.Configuration);

                // Create services (without initializing LibVLC yet)
                audioService = new AudioService(Config, settingsService);
                songRetrievalService = new RegionalSongRetrievalService(Config, timezoneService);
                discordService = new DiscordService(Config.Discord.ApplicationId);

                // STEP 2: Initialize Audio Engine in parallel with other tasks
                loader.StartStep(LoadingScreen.LoadingStep.AudioEngine, "Initializing LibVLC (this may take a moment)...");

                var audioInitTask = Task.Run(async () =>
                {
                    var progress = new Progress<double>(p =>
                    {
                        loader.UpdateProgress(LoadingScreen.LoadingStep.AudioEngine, p);
                    });

                    try
                    {
                        await audioService.InitializeAsync(progress);
                    }
                    catch (Exception ex)
                    {
                        Utils.DebugLogger.LogException(ex, "AudioService.InitializeAsync");
                        // Continue even if audio fails - app can run without it
                    }
                });

                // STEP 3: Discord RPC initialization (parallel)
                loader.StartStep(LoadingScreen.LoadingStep.Discord);
                var discordTask = Task.Run(() =>
                {
                    try
                    {
                        discordService.Initialize();
                        Utils.DebugLogger.Log("Discord RPC initialized", "Program");
                        loader.CompleteStep(LoadingScreen.LoadingStep.Discord);
                    }
                    catch (Exception ex)
                    {
                        Utils.DebugLogger.LogException(ex, "Program.DiscordInitialize");
                        loader.CompleteStep(LoadingScreen.LoadingStep.Discord);
                    }
                });

                // STEP 4: Create ConsoleHelper and load song data (parallel)
                loader.StartStep(LoadingScreen.LoadingStep.SongData);
                consoleHelper = new ConsoleHelper(audioService, songRetrievalService, discordService, settingsService, songCacheService, timezoneService, UserSettings);
                await consoleHelper.InitializeAsync();
                loader.CompleteStep(LoadingScreen.LoadingStep.SongData);

                // Wait for audio initialization to complete before starting stream
                await audioInitTask;
                loader.CompleteStep(LoadingScreen.LoadingStep.AudioEngine);

                // STEP 5: Start stream (requires audio to be initialized)
                if (audioService.IsInitialized)
                {
                    loader.StartStep(LoadingScreen.LoadingStep.Stream);
                    var initialStation = consoleHelper.GetCurrentStation();
                    var initialRegion = consoleHelper.GetCurrentRegion();
                    string initialStreamUrl = audioService.GetRegionalStreamUrl(initialStation, initialRegion);
                    await audioService.PlayStream(initialStreamUrl, initialRegion);
                    loader.CompleteStep(LoadingScreen.LoadingStep.Stream);
                }

                // Wait for Discord to finish (don't block on it though)
                await Task.WhenAny(discordTask, Task.Delay(2000)); // Max 2 second wait
            });

            loadingScreen.ShowSuccess();

            // Setup cleanup
            if (discordService != null)
            {
                AppDomain.CurrentDomain.ProcessExit += (s, e) => discordService.Shutdown();
            }

            // Start the main UI
            if (consoleHelper != null)
            {
                await consoleHelper.Run();
            }
        }
        catch (Exception ex)
        {
            loadingScreen.ShowError(ex.Message);
            Utils.DebugLogger.LogException(ex, "Program.Main");
            throw;
        }
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
