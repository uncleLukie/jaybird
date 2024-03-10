using jaybird.Services;

namespace jaybird.Utils
{
    public class ConsoleHelper
    {
        private bool _isPlaying = true;
        private int _currentStation;
        private bool _togglePlayPauseRequested = false;
        private readonly string[] _stationNames = { "Triple J", "Double J", "Unearthed" };
        private readonly AudioService _audioService;
        
        public ConsoleHelper(AudioService audioService)
        {
            _audioService = audioService;
            Console.WriteLine($"Currently playing: {_stationNames[_currentStation]}");
            Console.WriteLine("Press 'Spacebar' to play/pause.");
            Console.WriteLine("Press 'C' to change stations.");
            Console.WriteLine("Press 'W' to increase volume.");
            Console.WriteLine("Press 'S' to decrease volume.");
        }

        public void Run()
        {
            while (true)
            {
                var keyInfo = Console.ReadKey(true);

                if (keyInfo.Key == ConsoleKey.Spacebar)
                {
                    _togglePlayPauseRequested = true;
                }
                else if (keyInfo.Key == ConsoleKey.C)
                {
                    _currentStation = (_currentStation + 1) % _stationNames.Length;
                    Console.WriteLine($"Changed station to: {_stationNames[_currentStation]}");
                    _isPlaying = true;
                    _togglePlayPauseRequested = true;
                }
                else if (keyInfo.Key == ConsoleKey.W)
                {
                    _audioService.IncreaseVolume();
                    Console.WriteLine($"Volume Up: {_audioService.CurrentVolume}%");
                }
                else if (keyInfo.Key == ConsoleKey.S)
                {
                    _audioService.DecreaseVolume();
                    Console.WriteLine($"Volume Down: {_audioService.CurrentVolume}%");
                }
            }
        }
        
        public bool TogglePlayPauseRequested()
        {
            if (_togglePlayPauseRequested)
            {
                _togglePlayPauseRequested = false;
                return true;
            }
            return false;
        }

        public int GetCurrentStation()
        {
            return _currentStation;
        }
    }
}
