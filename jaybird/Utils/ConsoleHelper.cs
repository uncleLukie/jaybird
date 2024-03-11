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
        var grid = new Grid()
            .AddColumn(new GridColumn().PadRight(1))
            .AddColumn()
            .AddRow("[bold]Tuned into:[/]", $"{_stationNames[(int)_currentStation]}")
            .AddRow("[bold]Volume:[/]", $"[yellow]{audioService.CurrentVolume}%[/]")
            .AddRow("[bold]Song:[/]", $"[blue]{_currentSong.Title}[/] by [blue]{_currentSong.Artist}[/]")
            .AddRow("[bold]Album:[/]", $"[green]{_currentSong.Album}[/]")
            .AddRow("[bold]Played at:[/]", $"[purple]{_currentSong.PlayedTime:G}[/]")
            .AddEmptyRow()
            .AddRow("[bold underline]Keybindings:[/]")
            .AddEmptyRow()
            .AddRow("Press [green]'C'[/] to change stations", "Press [green]'W'[/] and [green]'S'[/] to adjust volume")
            .AddRow("Press [green]'Spacebar'[/] to play/pause", "Press [red]'Ctrl + c'[/] to exit app");

        var panel = new Panel(grid)
            .Expand()
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey)
            .Header("[yellow]jaybird[/]");

        AnsiConsole.Write(panel);
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