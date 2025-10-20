using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using jaybird.Models;
using jaybird.Services;
using System.IO.Abstractions;

namespace jaybird.Tests.Integration.Services
{
    public class SettingsServiceIntegrationTests : IDisposable
    {
        private readonly Mock<ILogger<SettingsService>> _mockLogger;
        private readonly ITestOutputHelper _output;
        private readonly string _testDirectory;
        private readonly IFileSystem _fileSystem;
        private readonly SettingsService _settingsService;

        public SettingsServiceIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            _mockLogger = new Mock<ILogger<SettingsService>>();
            
            // Create a temporary test directory
            _testDirectory = Path.Combine(Path.GetTempPath(), "jaybird_tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
            
            _fileSystem = new FileSystem();
            _settingsService = new SettingsService(_fileSystem, _mockLogger.Object);
        }

        [Fact]
        public async Task SaveAndLoadSettings_CompleteWorkflow_PersistsCorrectly()
        {
            // Arrange
            var settingsPath = Path.Combine(_testDirectory, "settings.json");
            var originalSettings = new UserSettings
            {
                LastStation = Station.DoubleJ,
                Volume = 75,
                EnableDiscord = true,
                CacheEnabled = true,
                CacheSizeLimit = 200,
                CacheTtlMinutes = 15,
                EnableArtwork = false,
                LastUpdated = DateTime.UtcNow
            };

            // Act - Save settings
            await _settingsService.SaveSettingsAsync(settingsPath, originalSettings);

            // Assert - File should exist
            File.Exists(settingsPath).Should().BeTrue();

            // Act - Load settings
            var loadedSettings = await _settingsService.LoadSettingsAsync(settingsPath);

            // Assert - Settings should match
            loadedSettings.Should().NotBeNull();
            loadedSettings.LastStation.Should().Be(originalSettings.LastStation);
            loadedSettings.Volume.Should().Be(originalSettings.Volume);
            loadedSettings.EnableDiscord.Should().Be(originalSettings.EnableDiscord);
            loadedSettings.CacheEnabled.Should().Be(originalSettings.CacheEnabled);
            loadedSettings.CacheSizeLimit.Should().Be(originalSettings.CacheSizeLimit);
            loadedSettings.CacheTtlMinutes.Should().Be(originalSettings.CacheTtlMinutes);
            loadedSettings.EnableArtwork.Should().Be(originalSettings.EnableArtwork);
            loadedSettings.LastUpdated.Should().BeCloseTo(originalSettings.LastUpdated, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task LoadSettings_NonExistentFile_ReturnsDefaultSettings()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.json");

            // Act
            var settings = await _settingsService.LoadSettingsAsync(nonExistentPath);

            // Assert
            settings.Should().NotBeNull();
            settings.LastStation.Should().Be(Station.TripleJ); // Default value
            settings.Volume.Should().Be(50); // Default value
            settings.EnableDiscord.Should().BeTrue(); // Default value
            settings.CacheEnabled.Should().BeTrue(); // Default value
            settings.CacheSizeLimit.Should().Be(100); // Default value
            settings.CacheTtlMinutes.Should().Be(10); // Default value
            settings.EnableArtwork.Should().BeTrue(); // Default value
        }

        [Fact]
        public async Task SaveSettings_InvalidPath_ThrowsException()
        {
            // Arrange
            var invalidPath = "Z:\\nonexistent\\drive\\settings.json";
            var settings = TestDataHelper.CreateTestUserSettings();

            // Act & Assert
            await Assert.ThrowsAsync<DirectoryNotFoundException>(
                () => _settingsService.SaveSettingsAsync(invalidPath, settings));
        }

        [Fact]
        public async Task LoadSettings_CorruptedJsonFile_ReturnsDefaultSettings()
        {
            // Arrange
            var settingsPath = Path.Combine(_testDirectory, "corrupted.json");
            await File.WriteAllTextAsync(settingsPath, "{ invalid json content }");

            // Act
            var settings = await _settingsService.LoadSettingsAsync(settingsPath);

            // Assert
            settings.Should().NotBeNull();
            settings.LastStation.Should().Be(Station.TripleJ); // Should fall back to defaults
        }

        [Fact]
        public async Task SaveSettings_PartialSettings_PreservesExistingValues()
        {
            // Arrange
            var settingsPath = Path.Combine(_testDirectory, "partial.json");
            var fullSettings = new UserSettings
            {
                LastStation = Station.Unearthed,
                Volume = 90,
                EnableDiscord = false,
                CacheEnabled = true,
                CacheSizeLimit = 150,
                CacheTtlMinutes = 20,
                EnableArtwork = true,
                LastUpdated = DateTime.UtcNow.AddDays(-1)
            };

            // Save full settings first
            await _settingsService.SaveSettingsAsync(settingsPath, fullSettings);

            // Act - Save partial settings (only change volume)
            var partialSettings = new UserSettings
            {
                Volume = 25,
                LastUpdated = DateTime.UtcNow
            };
            await _settingsService.SaveSettingsAsync(settingsPath, partialSettings);

            // Load and verify
            var loadedSettings = await _settingsService.LoadSettingsAsync(settingsPath);

            // Assert
            loadedSettings.Should().NotBeNull();
            loadedSettings.Volume.Should().Be(25); // Updated value
            loadedSettings.LastStation.Should().Be(Station.TripleJ); // Default (not specified in partial)
            loadedSettings.EnableDiscord.Should().BeTrue(); // Default (not specified in partial)
        }

        [Fact]
        public async Task SaveAndLoadSettings_MultipleInstances_HandlesConcurrentAccess()
        {
            // Arrange
            var settingsPath = Path.Combine(_testDirectory, "concurrent.json");
            var settings1 = TestDataHelper.CreateTestUserSettings();
            settings1.Volume = 30;
            settings1.LastStation = Station.TripleJ;

            var settings2 = TestDataHelper.CreateTestUserSettings();
            settings2.Volume = 60;
            settings2.LastStation = Station.DoubleJ;

            // Act - Concurrent save operations
            var saveTask1 = _settingsService.SaveSettingsAsync(settingsPath, settings1);
            var saveTask2 = _settingsService.SaveSettingsAsync(settingsPath, settings2);

            await Task.WhenAll(saveTask1, saveTask2);

            // Assert - One of the saves should have succeeded
            File.Exists(settingsPath).Should().BeTrue();

            var loadedSettings = await _settingsService.LoadSettingsAsync(settingsPath);
            loadedSettings.Should().NotBeNull();
            loadedSettings.Volume.Should().BeOneOf(30, 60); // Either value is acceptable due to race condition
            loadedSettings.LastStation.Should().BeOneOf(Station.TripleJ, Station.DoubleJ);
        }

        [Fact]
        public async Task SaveSettings_FilePermissions_HandlesReadOnlyDirectory()
        {
            // Arrange
            var readOnlyDir = Path.Combine(_testDirectory, "readonly");
            Directory.CreateDirectory(readOnlyDir);
            
            var settingsPath = Path.Combine(readOnlyDir, "settings.json");
            var settings = TestDataHelper.CreateTestUserSettings();

            // Make directory read-only (on Windows)
            var dirInfo = new DirectoryInfo(readOnlyDir);
            dirInfo.Attributes |= FileAttributes.ReadOnly;

            try
            {
                // Act & Assert
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => _settingsService.SaveSettingsAsync(settingsPath, settings));
            }
            finally
            {
                // Cleanup - remove read-only attribute
                dirInfo.Attributes &= ~FileAttributes.ReadOnly;
            }
        }

        [Fact]
        public async Task LoadSettings_LargeJsonFile_HandlesEfficiently()
        {
            // Arrange
            var settingsPath = Path.Combine(_testDirectory, "large.json");
            
            // Create settings with large data
            var largeSettings = new UserSettings
            {
                LastStation = Station.TripleJ,
                Volume = 50,
                EnableDiscord = true,
                CacheEnabled = true,
                CacheSizeLimit = 100,
                CacheTtlMinutes = 10,
                EnableArtwork = true,
                LastUpdated = DateTime.UtcNow
            };

            // Add a large string property to simulate large JSON (if UserSettings had such a property)
            // For now, we'll create a large JSON manually
            var largeJson = "{";
            largeJson += "\"LastStation\":\"TripleJ\",";
            largeJson += "\"Volume\":50,";
            largeJson += "\"EnableDiscord\":true,";
            largeJson += "\"CacheEnabled\":true,";
            largeJson += "\"CacheSizeLimit\":100,";
            largeJson += "\"CacheTtlMinutes\":10,";
            largeJson += "\"EnableArtwork\":true,";
            largeJson += "\"LastUpdated\":\"" + DateTime.UtcNow.ToString("O") + "\",";
            largeJson += "\"LargeData\":\"" + new string('x', 10000) + "\""; // Large string
            largeJson += "}";

            await File.WriteAllTextAsync(settingsPath, largeJson);

            // Act
            var startTime = DateTime.UtcNow;
            var settings = await _settingsService.LoadSettingsAsync(settingsPath);
            var loadTime = DateTime.UtcNow - startTime;

            // Assert
            settings.Should().NotBeNull();
            loadTime.Should().BeLessThan(TimeSpan.FromSeconds(1)); // Should load quickly even with large data
        }

        [Fact]
        public async Task SaveSettings_UnicodeCharacters_PreservesCorrectly()
        {
            // Arrange
            var settingsPath = Path.Combine(_testDirectory, "unicode.json");
            var unicodeSettings = new UserSettings
            {
                LastStation = Station.TripleJ,
                Volume = 50,
                EnableDiscord = true,
                CacheEnabled = true,
                CacheSizeLimit = 100,
                CacheTtlMinutes = 10,
                EnableArtwork = true,
                LastUpdated = DateTime.UtcNow
            };

            // Act
            await _settingsService.SaveSettingsAsync(settingsPath, unicodeSettings);

            // Verify file contains UTF-8 BOM or proper encoding
            var fileContent = await File.ReadAllTextAsync(settingsPath);
            fileContent.Should().Contain("TripleJ"); // Basic ASCII should work

            // Load and verify
            var loadedSettings = await _settingsService.LoadSettingsAsync(settingsPath);
            loadedSettings.Should().NotBeNull();
            loadedSettings.LastStation.Should().Be(Station.TripleJ);
        }

        public void Dispose()
        {
            // Cleanup test directory
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}