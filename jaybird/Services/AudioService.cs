using LibVLCSharp.Shared;
using jaybird.Models;

namespace jaybird.Services;

public class AudioService : IAudioService
{
    public int CurrentVolume => _internalVolume;
    public bool IsInitialized { get; private set; } = false;

    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private AppConfig _config;
    private string? _currentStreamUrl;
    private int _internalVolume = 100;
    private readonly ISettingsService _settingsService;
    private readonly Dictionary<string, (string streamUrl, DateTime cachedAt)> _plsCache = new();
    private readonly TaskCompletionSource<bool> _initializationComplete = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public AudioService(AppConfig config, ISettingsService settingsService)
    {
        _config = config;
        _settingsService = settingsService;
        _internalVolume = Program.UserSettings.LastVolume;

        Utils.DebugLogger.Log($"AudioService created (lazy initialization)", "AudioService");
    }

    public async Task<bool> InitializeAsync(IProgress<double>? progress = null)
    {
        if (IsInitialized)
        {
            return true;
        }

        await _initLock.WaitAsync();
        try
        {
            if (IsInitialized) // Double-check after acquiring lock
            {
                return true;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            Utils.DebugLogger.Log("Initializing LibVLC...", "AudioService");
            progress?.Report(0);

            // Check if plugin cache needs to be generated (first run optimization)
            await EnsurePluginCacheGenerated(progress);

            // Initialize LibVLC Core (optimized with plugin cache)
            var coreInitStart = stopwatch.ElapsedMilliseconds;
            await Task.Run(() =>
            {
                try
                {
                    Core.Initialize();
                    progress?.Report(50);
                }
                catch (Exception ex)
                {
                    Utils.DebugLogger.LogException(ex, "LibVLC Core.Initialize");
                    throw new InvalidOperationException("LibVLC initialization failed. Please ensure LibVLC is properly installed.", ex);
                }
            });
            var coreInitTime = stopwatch.ElapsedMilliseconds - coreInitStart;
            Utils.DebugLogger.Log($"LibVLC Core.Initialize completed in {coreInitTime}ms", "AudioService");

            progress?.Report(70);

            // Create LibVLC and MediaPlayer instances with optimized flags
            // Disable video subsystem entirely since jaybird only needs audio
            _libVLC = new LibVLC(
                "--quiet",                  // Suppress verbose output
                "--no-stats",               // Disable statistics
                "--no-video",               // Disable entire video subsystem (saves 50+ plugins)
                "--no-video-title-show",    // No video title display
                "--avcodec-hw=none",        // Disable hardware video decoding
                "--no-overlay",             // Disable OSD overlays
                "--drop-late-frames"        // Handle network streaming gracefully
            );
            progress?.Report(85);

            _mediaPlayer = new MediaPlayer(_libVLC);
            _mediaPlayer.Volume = _internalVolume;
            progress?.Report(100);

            IsInitialized = true;
            _initializationComplete.SetResult(true);

            stopwatch.Stop();
            Utils.DebugLogger.Log($"AudioService fully initialized in {stopwatch.ElapsedMilliseconds}ms (volume: {_internalVolume}%)", "AudioService");
            return true;
        }
        catch (Exception ex)
        {
            Utils.DebugLogger.LogException(ex, "AudioService.InitializeAsync");
            _initializationComplete.SetResult(false);
            return false;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task EnsurePluginCacheGenerated(IProgress<double>? progress)
    {
        try
        {
            // Get application data directory for cache flag
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "jaybird"
            );
            Directory.CreateDirectory(appDataPath);
            var cacheFlagPath = Path.Combine(appDataPath, "libvlc-cache.flag");

            // Check if cache has already been generated
            if (File.Exists(cacheFlagPath))
            {
                Utils.DebugLogger.Log("LibVLC plugin cache already generated", "AudioService");
                return;
            }

            Utils.DebugLogger.Log("First run detected - generating LibVLC plugin cache...", "AudioService");
            progress?.Report(10);

            // Generate optimized plugin cache
            // This is a one-time operation that significantly speeds up subsequent initializations
            await Task.Run(() =>
            {
                try
                {
                    // Initialize Core first (required before creating LibVLC instance)
                    Core.Initialize();
                    progress?.Report(20);

                    // Create temporary LibVLC instance with cache reset flag
                    using (var tempVlc = new LibVLC("--reset-plugins-cache", "--quiet"))
                    {
                        // Instance creation triggers plugin cache generation
                        Utils.DebugLogger.Log("Plugin cache generated successfully", "AudioService");
                    }
                    progress?.Report(30);
                }
                catch (Exception ex)
                {
                    Utils.DebugLogger.LogException(ex, "AudioService.EnsurePluginCacheGenerated");
                    // Continue anyway - cache generation failure shouldn't block startup
                }
            });

            // Create flag file to prevent regenerating cache on subsequent runs
            File.WriteAllText(cacheFlagPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Utils.DebugLogger.Log($"Cache flag created at: {cacheFlagPath}", "AudioService");
        }
        catch (Exception ex)
        {
            Utils.DebugLogger.LogException(ex, "AudioService.EnsurePluginCacheGenerated");
            // Non-fatal - continue with initialization
        }
    }

    public async Task EnsureInitializedAsync()
    {
        if (!IsInitialized)
        {
            await _initializationComplete.Task;
        }
    }

    public async Task PlayStream(string streamUrl)
    {
        await PlayStream(streamUrl, Region.NSW); // Default to NSW for backward compatibility
    }

    public async Task PlayStream(string streamUrl, Region region)
    {
        try
        {
            await EnsureInitializedAsync();

            Utils.DebugLogger.Log($"Starting playback for stream: {streamUrl} ({region})", "AudioService");

            // Check if it's a direct stream URL or a .pls file
            if (streamUrl.EndsWith(".pls", StringComparison.OrdinalIgnoreCase))
            {
                _currentStreamUrl = await GetActualStreamUrlFromPls(streamUrl);

                if (_currentStreamUrl == null)
                {
                    Utils.DebugLogger.Log("Failed to extract stream URL from PLS file", "AudioService");
                    return;
                }
            }
            else
            {
                // Direct stream URL
                _currentStreamUrl = streamUrl;
            }

            Utils.DebugLogger.Log($"Actual stream URL: {_currentStreamUrl}", "AudioService");
            PlayCurrentStream();
        }
        catch (Exception ex)
        {
            Utils.DebugLogger.LogException(ex, "AudioService.PlayStream");
        }
    }

    public async Task StopStream()
    {
        if (_mediaPlayer?.IsPlaying == true)
        {
            _mediaPlayer.Stop();
        }

        await Task.CompletedTask;
    }

    public async Task TogglePlayPause()
    {
        if (_mediaPlayer == null) return;

        if (_mediaPlayer.IsPlaying)
        {
            _mediaPlayer.Pause();
        }
        else
        {
            if (_currentStreamUrl != null)
            {
                PlayCurrentStream();
            }
        }

        await Task.CompletedTask;
    }

    public void IncreaseVolume()
    {
        if (_internalVolume < 100)
        {
            _internalVolume = Math.Min(_internalVolume + 10, 100);
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Volume = _internalVolume;
            }
            _ = SaveVolumeSettingsAsync();
        }
    }

    public void DecreaseVolume()
    {
        if (_internalVolume > 0)
        {
            _internalVolume = Math.Max(_internalVolume - 10, 0);
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Volume = _internalVolume;
            }
            _ = SaveVolumeSettingsAsync();
        }
    }

    private async Task SaveVolumeSettingsAsync()
    {
        try
        {
            var currentSettings = new UserSettings
            {
                LastStation = Program.UserSettings.LastStation,
                LastVolume = _internalVolume
            };
            await _settingsService.SaveSettingsAsync(currentSettings);
            Utils.DebugLogger.Log($"Volume saved: {_internalVolume}%", "AudioService");
        }
        catch (Exception ex)
        {
            Utils.DebugLogger.LogException(ex, "AudioService.SaveVolumeSettingsAsync");
        }
    }

    private void PlayCurrentStream()
    {
        if (_currentStreamUrl != null && _libVLC != null && _mediaPlayer != null)
        {
            var media = new Media(_libVLC, new Uri(_currentStreamUrl), ":no-video");
            _mediaPlayer.Play(media);
        }
    }

    private async Task<string?> GetActualStreamUrlFromPls(string plsUrl)
    {
        // Check cache first (24-hour TTL for PLS URLs which rarely change)
        if (_plsCache.TryGetValue(plsUrl, out var cached))
        {
            var cacheAge = DateTime.Now - cached.cachedAt;
            if (cacheAge < TimeSpan.FromHours(24))
            {
                Utils.DebugLogger.Log($"PLS cache HIT for {plsUrl}: {cached.streamUrl}", "AudioService");
                return cached.streamUrl;
            }
            else
            {
                // Remove expired cache entry
                _plsCache.Remove(plsUrl);
                Utils.DebugLogger.Log($"PLS cache EXPIRED for {plsUrl}", "AudioService");
            }
        }

        try
        {
            using var client = new HttpClient();
            Utils.DebugLogger.Log($"Fetching PLS file from: {plsUrl}", "AudioService");
            string customPlaylistContent = await client.GetStringAsync(plsUrl);
            Utils.DebugLogger.Log($"PLS content received: {customPlaylistContent.Length} bytes", "AudioService");

            var streamUrl = ParseAndGetFirstUrl(customPlaylistContent);

            // Cache the result
            if (streamUrl != null)
            {
                _plsCache[plsUrl] = (streamUrl, DateTime.Now);
                Utils.DebugLogger.Log($"Cached PLS result for {plsUrl}: {streamUrl}", "AudioService");
            }

            return streamUrl;
        }
        catch (Exception ex)
        {
            Utils.DebugLogger.LogException(ex, "AudioService.GetActualStreamUrlFromPls");
            return null;
        }
    }

    private string? ParseAndGetFirstUrl(string customPlaylistContent)
    {
        string[] lines = customPlaylistContent.Split('\n');

        Dictionary<string, string> properties = new Dictionary<string, string>();

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
            {
                continue;
            }

            string[] parts = line.Split('=');
            if (parts.Length == 2)
            {
                properties[parts[0].Trim()] = parts[1].Trim();
            }
        }

        if (properties.TryGetValue("File1", out string? url))
        {
            return url ?? string.Empty;
        }
        else
        {
            return null;
        }
    }

    public string GetRegionalStreamUrl(Station station, Region region)
    {
        try
        {
            return _config.RegionalApi.GetStreamUrl(station, region);
        }
        catch (ArgumentException)
        {
            // Fallback to original AudioConfig if regional not found
            return station switch
            {
                Station.TripleJ => _config.Audio.TripleJStreamUrl,
                Station.DoubleJ => _config.Audio.DoubleJStreamUrl,
                Station.Unearthed => _config.Audio.UnearthedStreamUrl,
                _ => throw new ArgumentOutOfRangeException(nameof(station), $"Not expected station value: {station}")
            };
        }
    }
}