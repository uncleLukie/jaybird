using Spectre.Console;
using jaybird.Services;
using System;

namespace jaybird.Utils
{
    public class ConsoleHelper
    {
        private readonly AudioService _audioService;
        private readonly string[] _stationNames = { "Triple J", "Double J", "Unearthed" };
        private int _currentStation;
        private bool _togglePlayPauseRequested = false;

        public ConsoleHelper(AudioService audioService)
        {
            _audioService = audioService;
        }

        public void Run()
        {
            RenderKeybindingsAndVolume();

            while (true)
            {
                var keyInfo = Console.ReadKey(true);
                switch (keyInfo.Key)
                {
                    case ConsoleKey.Spacebar:
                        _togglePlayPauseRequested = true;
                        break;
                    case ConsoleKey.C:
                        ChangeStation();
                        break;
                    case ConsoleKey.W:
                        _audioService.IncreaseVolume();
                        RenderKeybindingsAndVolume();
                        break;
                    case ConsoleKey.S:
                        _audioService.DecreaseVolume();
                        RenderKeybindingsAndVolume();
                        break;
                }
            }
        }

        private void RenderKeybindingsAndVolume()
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(
                new Panel(
                        new Markup($"Currently playing: {_stationNames[_currentStation]}\n" +
                                   $"Volume: {_audioService.CurrentVolume}%\n" +
                                   "[bold]Keybindings:[/]\n" +
                                   "- Press [green]'C'[/] to change stations\n" +
                                   "- Press [green]'W'[/] and [green]'S'[/] to adjust volume\n" +
                                   "- Press [green]'Spacebar'[/] to play/pause"))
                    .Expand()
                    .Border(BoxBorder.Rounded)
            );
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

        private void ChangeStation()
        {
            _currentStation = (_currentStation + 1) % _stationNames.Length;
            RenderKeybindingsAndVolume();
        }
    }
}
