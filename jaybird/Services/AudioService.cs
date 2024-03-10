using LibVLCSharp.Shared;

namespace jaybird.Services
{
    public class AudioService : IAudioService
    {
        public int CurrentVolume => _internalVolume;
        
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private AppConfig _config;
        private string? _currentStreamUrl;
        private int _internalVolume = 100;

        static AudioService()
        {
            Core.Initialize();
        }

        public AudioService(AppConfig config)
        {
            _config = config;
            _libVLC = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVLC);
            _mediaPlayer.Volume = _internalVolume;
        }

        public async Task PlayStream(string streamUrl)
        {
            try
            {
                _currentStreamUrl = await GetActualStreamUrlFromPls(streamUrl);

                if (_currentStreamUrl == null)
                {
                    Console.WriteLine("Error extracting stream URL from PLS");
                    return;
                }

                PlayCurrentStream();
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
            }
        }

        public void DecreaseVolume()
        {
            if (_internalVolume > 0)
            {
                _internalVolume = Math.Max(_internalVolume - 10, 0);
                _mediaPlayer.Volume = _internalVolume;
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
