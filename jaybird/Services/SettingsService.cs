using jaybird.Models;
using System.Text.Json;
using System.IO;
using System.Runtime.InteropServices;

namespace jaybird.Services;

public interface ISettingsService
{
    Task<UserSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(UserSettings settings);
}

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly object _saveLock = new object();
    private DateTime _lastSaveTime = DateTime.MinValue;
    private readonly TimeSpan _saveDebounceDelay = TimeSpan.FromMilliseconds(500);

    public SettingsService()
    {
        _settingsFilePath = GetSettingsFilePath();
        EnsureSettingsDirectoryExists();
    }

    public async Task<UserSettings> LoadSettingsAsync()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                Utils.DebugLogger.Log("Settings file not found, creating default settings", "SettingsService");
                var defaultSettings = new UserSettings();
                await SaveSettingsAsync(defaultSettings);
                return defaultSettings;
            }

            var json = await File.ReadAllTextAsync(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<UserSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (settings == null)
            {
                Utils.DebugLogger.Log("Failed to deserialize settings, using defaults", "SettingsService");
                return new UserSettings();
            }

            // Validate and clamp volume to valid range
            settings.LastVolume = Math.Clamp(settings.LastVolume, 0, 100);

            Utils.DebugLogger.Log($"Settings loaded: Station={settings.LastStation}, Volume={settings.LastVolume}%", "SettingsService");
            return settings;
        }
        catch (Exception ex)
        {
            Utils.DebugLogger.LogException(ex, "SettingsService.LoadSettingsAsync");
            Utils.DebugLogger.Log("Error loading settings, using defaults", "SettingsService");
            return new UserSettings();
        }
    }

    public async Task SaveSettingsAsync(UserSettings settings)
    {
        lock (_saveLock)
        {
            // Debounce rapid saves
            var now = DateTime.Now;
            if (now - _lastSaveTime < _saveDebounceDelay)
            {
                Utils.DebugLogger.Log("Save request debounced", "SettingsService");
                return;
            }
            _lastSaveTime = now;
        }

        try
        {
            settings.LastUpdated = DateTime.Now;
            
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Atomic write: write to temp file first, then move
            var tempFilePath = _settingsFilePath + ".tmp";
            await File.WriteAllTextAsync(tempFilePath, json);
            
            // On Windows, we need to handle file replacement differently
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (File.Exists(_settingsFilePath))
                {
                    File.Delete(_settingsFilePath);
                }
            }
            
            File.Move(tempFilePath, _settingsFilePath, overwrite: true);
            
            Utils.DebugLogger.Log($"Settings saved: Station={settings.LastStation}, Volume={settings.LastVolume}%", "SettingsService");
        }
        catch (Exception ex)
        {
            Utils.DebugLogger.LogException(ex, "SettingsService.SaveSettingsAsync");
        }
    }

    private static string GetSettingsFilePath()
    {
        var appDataDir = GetAppDataDirectory();
        return Path.Combine(appDataDir, "jaybird", "settings.json");
    }

    private static string GetAppDataDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Application Support");
        }
        else // Linux and others
        {
            var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (!string.IsNullOrEmpty(xdgConfigHome))
            {
                return xdgConfigHome;
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".config");
        }
    }

    private void EnsureSettingsDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            Utils.DebugLogger.Log($"Created settings directory: {directory}", "SettingsService");
        }
    }
}