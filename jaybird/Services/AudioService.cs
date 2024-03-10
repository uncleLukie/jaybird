using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace jaybird.Services
{
    public class AudioService : IAudioService
    {
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private AppConfig _config;

        static AudioService()
        {
            Core.Initialize();
        }

        public AudioService(AppConfig config)
        {
            _config = config;
            _libVLC = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVLC);
        }

        public async Task PlayStream(string streamUrl)
        {
            try
            {
                string? actualStreamUrl = await GetActualStreamUrlFromPls(streamUrl);

                if (actualStreamUrl == null)
                {
                    Console.WriteLine("Error extracting stream URL from PLS");
                    return;
                }

                StopStream();

                var media = new Media(_libVLC, new Uri(actualStreamUrl));
                _mediaPlayer.Play(media);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during playback: {ex.Message}");
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

        private async Task<string?> GetActualStreamUrlFromPls(string plsUrl)
        {
            try
            {
                using var client = new HttpClient();
                string customPlaylistContent = await client.GetStringAsync(plsUrl);

                return ParseAndGetFirstUrl(customPlaylistContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading custom playlist content: {ex.Message}");
                return null;
            }
        }

        private string? ParseAndGetFirstUrl(string customPlaylistContent)
        {
            string[] lines = customPlaylistContent.Split('\n');

            // Use a dictionary for better flexibility
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

            // Extract the URL
            if (properties.TryGetValue("File1", out string url))
            {
                return url;
            }
            else
            {
                return null; 
            }
        }
    }
}
