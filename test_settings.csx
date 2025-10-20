#r "jaybird/bin/windows/Debug/net10.0/jaybird.dll"

using jaybird.Services;
using jaybird.Models;

// Test the settings service
var settingsService = new SettingsService();
Console.WriteLine("Testing SettingsService...");

// Load settings (should create defaults)
var settings = await settingsService.LoadSettingsAsync();
Console.WriteLine($"Loaded settings: Station={settings.LastStation}, Volume={settings.LastVolume}%");

// Modify settings
settings.LastStation = Station.DoubleJ;
settings.LastVolume = 75;

// Save settings
await settingsService.SaveSettingsAsync(settings);
Console.WriteLine($"Saved settings: Station={settings.LastStation}, Volume={settings.LastVolume}%");

// Load again to verify persistence
var reloadedSettings = await settingsService.LoadSettingsAsync();
Console.WriteLine($"Reloaded settings: Station={reloadedSettings.LastStation}, Volume={reloadedSettings.LastVolume}%");

Console.WriteLine("Settings test completed successfully!");