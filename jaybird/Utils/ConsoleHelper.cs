namespace jaybird.Utils
{
    public class ConsoleHelper
    {
        private bool _isPlaying;
        private int _currentStation;
        private readonly string[] _stationNames = ["Triple J", "Double J", "Unearthed"]; 

        public void Run()
        {
            Console.WriteLine($"Currently playing: {_stationNames[_currentStation]}");
            Console.WriteLine("Press 'Spacebar' to play/pause.");
            Console.WriteLine("Press 'C' to change stations.");


            while (true)
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);

                if (keyInfo.Key == ConsoleKey.Spacebar)
                {
                    _isPlaying = !_isPlaying;
                    Console.WriteLine(_isPlaying ? "Playing" : "Paused");
                }
                else if (keyInfo.Key == ConsoleKey.C)
                {
                    _currentStation = (_currentStation + 1) % _stationNames.Length;
                    Console.WriteLine($"Changed station to: {_stationNames[_currentStation]}");
                }
            }
        }

        public bool IsPlaying() 
        {
            return _isPlaying; 
        }

        public int GetCurrentStation()
        {
            return _currentStation;
        }
    }
}