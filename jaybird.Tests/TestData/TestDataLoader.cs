using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using jaybird.Models;

namespace jaybird.Tests.TestData
{
    public static class TestDataLoader
    {
        private static readonly string TestDataPath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "",
            "TestData");

        public static async Task<string> LoadApiResponseAsync(string fileName)
        {
            var filePath = Path.Combine(TestDataPath, "api-responses", fileName);
            return await File.ReadAllTextAsync(filePath);
        }

        public static async Task<string> LoadSettingsAsync(string fileName)
        {
            var filePath = Path.Combine(TestDataPath, "settings", fileName);
            return await File.ReadAllTextAsync(filePath);
        }

        public static async Task<SongData> LoadSongDataAsync(string fileName)
        {
            var json = await LoadApiResponseAsync(fileName);
            return System.Text.Json.JsonSerializer.Deserialize<SongData>(json)
                ?? throw new InvalidOperationException($"Failed to deserialize song data from {fileName}");
        }

        public static async Task<UserSettings> LoadUserSettingsAsync(string fileName)
        {
            var json = await LoadSettingsAsync(fileName);
            return System.Text.Json.JsonSerializer.Deserialize<UserSettings>(json)
                ?? throw new InvalidOperationException($"Failed to deserialize user settings from {fileName}");
        }

        public static string GetTestDataPath()
        {
            return TestDataPath;
        }

        public static string GetApiResponsesPath()
        {
            return Path.Combine(TestDataPath, "api-responses");
        }

        public static string GetSettingsPath()
        {
            return Path.Combine(TestDataPath, "settings");
        }
    }
}