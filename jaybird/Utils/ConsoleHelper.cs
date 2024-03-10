namespace jaybird.Utils;

using Services;
using Models;
using Spectre.Console;

public class ConsoleHelper
{
    private readonly AudioService _audioService;
    private readonly ISongRetrievalService _songRetrievalService;
    private readonly string[] _stationNames = { "Triple J", "Double J", "Unearthed" };
    private Station _currentStation = Station.TripleJ;
    private bool _togglePlayPauseRequested = false;

    public ConsoleHelper(AudioService audioService, ISongRetrievalService songRetrievalService)
    {
        _audioService = audioService;
        _songRetrievalService = songRetrievalService;
    }

    public async Task Run()
    {
        await RenderKeybindingsAndVolume();

        while (true)
        {
            var keyInfo = Console.ReadKey(intercept: true);
            switch (keyInfo.Key)
            {
                case ConsoleKey.Spacebar:
                    _togglePlayPauseRequested = !_togglePlayPauseRequested;
                    await _audioService.TogglePlayPause();
                    break;
                case ConsoleKey.C:
                    await ChangeStationAndPlay();
                    break;
                case ConsoleKey.W:
                    _audioService.IncreaseVolume();
                    await RenderKeybindingsAndVolume();
                    break;
                case ConsoleKey.S:
                    _audioService.DecreaseVolume();
                    await RenderKeybindingsAndVolume();
                    break;
            }
        }
    }

    private async Task RenderKeybindingsAndVolume()
    {
        var songData = await _songRetrievalService.GetCurrentSongAsync(_currentStation);

        AnsiConsole.Clear();
        AnsiConsole.Write(
            new Panel(
                new Markup(
                    $"Currently playing: {_stationNames[(int)_currentStation]}\n" +
                    $"Volume: {_audioService.CurrentVolume}%\n" +
                    $"Song: {songData.Title} by {songData.Artist}\n" +
                    $"Album: {songData.Album}\n" +
                    $"Played at: {songData.PlayedTime:G}\n" +
                    "[bold]Keybindings:[/]\n" +
                    "- Press [green]'C'[/] to change stations\n" +
                    "- Press [green]'W'[/] and [green]'S'[/] to adjust volume\n" +
                    "- Press [green]'Spacebar'[/] to play/pause"
                )
            ).Expand().Border(BoxBorder.Rounded)
        );
    }

    private async Task ChangeStationAndPlay()
    {
        _currentStation = (Station)(((int)_currentStation + 1) % _stationNames.Length);
        await _audioService.PlayStream(Program.GetStreamUrlForStation(_currentStation, Program.Config));
        await RenderKeybindingsAndVolume();
    }

    public Station GetCurrentStation() => _currentStation;
}