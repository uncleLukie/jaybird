using LibVLCSharp.Shared;
using jaybird.Models;

namespace jaybird.Services;

public class AudioService : IAudioService
{
    public int CurrentVolume => _internalVolume;

    private LibVLC _libVLC;
    private MediaPlayer _mediaPlayer;
    private AppConfig _config;
    private string? _currentStreamUrl;
    private int _internalVolume = 100;
    private readonly ISettingsService _settingsService;
    private readonly Dictionary<string, (string streamUrl, DateTime cachedAt)> _plsCache = new();

    static AudioService()
    {
        try
        {
            Core.Initialize();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: LibVLC initialization failed: {ex.Message}");
            Console.WriteLine("Audio functionality will be disabled. Please ensure LibVLC is properly installed.");
            throw;
        }
    }

    public AudioService(AppConfig config, ISettingsService settingsService)
    {
        _config = config;
        _settingsService = settingsService;
        // Suppress VLC logging output to prevent console spam
        _libVLC = new LibVLC("--quiet", "--no-stats", "--no-video-title-show");
        _mediaPlayer = new MediaPlayer(_libVLC);
        
        // Load initial volume from settings
        _internalVolume = Program.UserSettings.LastVolume;
        _mediaPlayer.Volume = _internalVolume;
        
        Utils.DebugLogger.Log($"AudioService initialized with volume: {_internalVolume}%", "AudioService");
    }

    public async Task PlayStream(string streamUrl)
    {
        await PlayStream(streamUrl, Region.NSW); // Default to NSW for backward compatibility
    }

    public async Task PlayStream(string streamUrl, Region region)
    {
        try
        {
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
        if (_mediaPlayer.IsPlaying)
        {
            _mediaPlayer.Stop();
        }

        await Task.CompletedTask;
    }

    public async Task TogglePlayPause()
    {
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
            _mediaPlayer.Volume = _internalVolume;
            _ = SaveVolumeSettingsAsync();
        }
    }

    public void DecreaseVolume()
    {
        if (_internalVolume > 0)
        {
            _internalVolume = Math.Max(_internalVolume - 10, 0);
            _mediaPlayer.Volume = _internalVolume;
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
        if (_currentStreamUrl != null)
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