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
    private readonly IDiscordService _discordService = discordService;

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
                    await audioService.TogglePlayPause();
                    break;
                case ConsoleKey.C:
                    await ChangeStationAndPlay();
                    break;
                case ConsoleKey.W:
                    audioService.IncreaseVolume();
                    await RenderKeybindingsAndVolume();
                    break;
                case ConsoleKey.S:
                    audioService.DecreaseVolume();
                    await RenderKeybindingsAndVolume();
                    break;
            }
        }
    }

    private async Task RenderKeybindingsAndVolume()
    {
        var songData = await songRetrievalService.GetCurrentSongAsync(_currentStation);

        AnsiConsole.Clear();
        AnsiConsole.Write(
            new Panel(
                new Markup(
                    $"Currently playing: {_stationNames[(int)_currentStation]}\n" +
                    $"Volume: {audioService.CurrentVolume}%\n" +
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
        await audioService.PlayStream(Program.GetStreamUrlForStation(_currentStation, Program.Config));
        await RenderKeybindingsAndVolume();

        // Update Discord status
        var songData = await songRetrievalService.GetCurrentSongAsync(_currentStation);
        _discordService.UpdatePresence(
            $"Listening to {songData.Title}",
            $"by {songData.Artist}",
            "jaybird",
            GetCurrentStationSmallImageKey(_currentStation)
        );
    }
    
    private string GetCurrentStationSmallImageKey(Station station)
    {
        return station switch
        {
            Station.TripleJ => "triplej",
            Station.DoubleJ => "doublej",
            Station.Unearthed => "unearthed",
            _ => "default_key", // Use a default or generic image key if necessary
        };
    }

    public Station GetCurrentStation() => _currentStation;
}