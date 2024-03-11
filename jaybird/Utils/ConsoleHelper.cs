namespace jaybird.Utils;

using Services;
using Models;
using Spectre.Console;

public class ConsoleHelper(
    AudioService audioService,
    ISongRetrievalService songRetrievalService,
    IDiscordService discordService)
{
    private readonly string[] _stationNames = { "Triple J", "Double J", "Unearthed" };
    private Station _currentStation = Station.TripleJ;
    private bool _togglePlayPauseRequested = false;
    private SongData _currentSong = new SongData
        { Title = "Unknown", Artist = "Unknown", Album = "Unknown", PlayedTime = DateTime.Now };

    public async Task Run()
    {
        _ = PeriodicUpdates();
        RenderKeybindingsAndVolume();
        UpdateDiscordPresence();

        while (true)
        {
            var keyInfo = Console.ReadKey(intercept: true);
            switch (keyInfo.Key)
            {
                case ConsoleKey.Spacebar:
                    _togglePlayPauseRequested = !_togglePlayPauseRequested;
                    await audioService.TogglePlayPause();
                    break;
                case ConsoleKey.C:
                    await ChangeStationAndPlay();
                    break;
                case ConsoleKey.W:
                    audioService.IncreaseVolume();
                    RenderKeybindingsAndVolume();
                    break;
                case ConsoleKey.S:
                    audioService.DecreaseVolume();
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
                new Markup(
                    $"Currently playing: {_stationNames[(int)_currentStation]}\n" +
                    $"Volume: {audioService.CurrentVolume}%\n" +
                    $"Song: {_currentSong.Title} by {_currentSong.Artist}\n" +
                    $"Album: {_currentSong.Album}\n" +
                    $"Played at: {_currentSong.PlayedTime:G}\n" +
                    "[bold]Keybindings:[/]\n" +
                    "- Press [green]'C'[/] to change stations\n" +
                    "- Press [green]'W'[/] and [green]'S'[/] to adjust volume\n" +
                    "- Press [green]'Spacebar'[/] to play/pause\n" +
                    "- Press [green]'Ctrl + c'[/] to exit app"
                )
            ).Expand().Border(BoxBorder.Rounded)
        );
    }

    private async Task ChangeStationAndPlay()
    {
        _currentStation = (Station)(((int)_currentStation + 1) % _stationNames.Length);
        await audioService.PlayStream(Program.GetStreamUrlForStation(_currentStation, Program.Config));
        _currentSong = await songRetrievalService.GetCurrentSongAsync(_currentStation);
        RenderKeybindingsAndVolume();
        UpdateDiscordPresence();
    }

    private async Task PeriodicUpdates()
    {
        while (true)
        {
            _currentSong = await songRetrievalService.GetCurrentSongAsync(_currentStation);
            UpdateDiscordPresence();
            RenderKeybindingsAndVolume();
            await Task.Delay(10000);
        }
    }

    private void UpdateDiscordPresence()
    {
        discordService.UpdatePresence(
            $"{_currentSong.Title} - {_currentSong.Artist}",
            $"Album: {_currentSong.Album}",
            "jaybird",
            GetCurrentStationSmallImageKey(_currentStation),
            $"Tuned into: {_stationNames[(int)_currentStation]}",
            _currentStation,
            _stationNames
        );
    }

    private string GetCurrentStationSmallImageKey(Station station)
    {
        return station switch
        {
            Station.TripleJ => "triplej",
            Station.DoubleJ => "doublej",
            Station.Unearthed => "unearthed",
            _ => "jaybird",
        };
    }

    public Station GetCurrentStation() => _currentStation;
}