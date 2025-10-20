namespace jaybird.Utils;

using Services;
using Models;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Runtime.InteropServices;

public class ConsoleHelper(
    AudioService audioService,
    ISongRetrievalService songRetrievalService,
    IDiscordService discordService)
{
    private readonly string[] _stationNames = { "Triple J", "Double J", "Unearthed" };
    private Station _currentStation = Station.TripleJ;
    private bool _isPlaying = true;
    private SongData _currentSong = new SongData
        { Title = "Unknown", Artist = "Unknown", Album = "Unknown", PlayedTime = DateTime.Now };
    private bool _shouldExit = false;
    private readonly object _updateLock = new object();
    private DateTime _lastUpdate = DateTime.MinValue;
    private DateTime _lastAnimationUpdate = DateTime.MinValue;
    private int _lastWindowWidth = 0;
    private int _lastWindowHeight = 0;
    private IRenderable? _currentArtwork = null;

    public async Task InitializeAsync()
    {
        // Fetch initial song data
        Utils.DebugLogger.Log("Initializing UI with initial song data", "ConsoleHelper");
        try
        {
            var song = await songRetrievalService.GetCurrentSongAsync(_currentStation);
            if (song != null)
            {
                _currentSong = song;
                await UpdateArtworkAsync();
                UpdateDiscordPresence();
                Utils.DebugLogger.Log("Initial song data loaded successfully", "ConsoleHelper");
            }
            else
            {
                Utils.DebugLogger.Log("No initial song data available", "ConsoleHelper");
            }
        }
        catch (Exception ex)
        {
            Utils.DebugLogger.LogException(ex, "ConsoleHelper.InitializeAsync");
        }
    }

    public async Task Run()
    {
        Console.CursorVisible = false;
        Console.Clear();

        // Initialize window size tracking
        try
        {
            _lastWindowWidth = Console.WindowWidth;
            _lastWindowHeight = Console.WindowHeight;
        }
        catch
        {
            _lastWindowWidth = 80;
            _lastWindowHeight = 20;
        }

        var initialLayout = GetInitialLayout();

        await AnsiConsole.Live(initialLayout)
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                // Start background tasks
                _ = Task.Run(async () => await PeriodicUpdates(ctx));
                _ = Task.Run(async () => await MonitorWindowResize(ctx));

                // Main input loop
                while (!_shouldExit)
                {
                    if (Console.KeyAvailable)
                    {
                        var keyInfo = Console.ReadKey(intercept: true);
                        await HandleKeyPress(keyInfo, ctx);
                    }

                    await Task.Delay(50); // Small delay to prevent CPU spinning
                }
            });
    }

    private Layout GetInitialLayout()
    {
        try
        {
            return GetResponsiveLayout();
        }
        catch (Exception ex)
        {
            Utils.DebugLogger.LogException(ex, "ConsoleHelper.GetInitialLayout");
            return CreateMinimalLayout();
        }
    }

    private Layout GetResponsiveLayout()
    {
        var height = Console.WindowHeight;
        var width = Console.WindowWidth;

        Utils.DebugLogger.Log($"Choosing layout for terminal size: {width}x{height}", "ConsoleHelper");

        // Priority 1: Ultra-minimal - critical info only (< 8 lines)
        if (height < 8)
        {
            Utils.DebugLogger.Log("Using minimal layout (< 8 lines)", "ConsoleHelper");
            return CreateMinimalLayout();
        }
        // Priority 2: Compact - audio + keybindings (8-14 lines or narrow width)
        else if (height < 14 || width < 60)
        {
            Utils.DebugLogger.Log("Using compact layout (< 14 lines or < 60 width)", "ConsoleHelper");
            return CreateCompactLayout();
        }
        // Priority 3+4: Standard/Full - horizontal layout with animations (14+ lines)
        else
        {
            Utils.DebugLogger.Log("Using standard/full layout (14+ lines)", "ConsoleHelper");
            return CreateStandardLayout();
        }
    }

    private async Task HandleKeyPress(ConsoleKeyInfo keyInfo, LiveDisplayContext ctx)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.Spacebar:
                _isPlaying = !_isPlaying;
                await audioService.TogglePlayPause();
                UpdateDisplay(ctx);
                break;
            case ConsoleKey.C:
                await ChangeStationAndPlay();
                UpdateDisplay(ctx);
                break;
            case ConsoleKey.W:
                audioService.IncreaseVolume();
                UpdateDisplay(ctx);
                break;
            case ConsoleKey.S:
                audioService.DecreaseVolume();
                UpdateDisplay(ctx);
                break;
        }
    }

    private void UpdateDisplay(LiveDisplayContext ctx)
    {
        lock (_updateLock)
        {
            // Throttle updates to max once per 100ms
            var now = DateTime.Now;
            if ((now - _lastUpdate).TotalMilliseconds < 100)
            {
                return;
            }
            _lastUpdate = now;

            // Update animation frame every 300ms if playing
            if (_isPlaying && (now - _lastAnimationUpdate).TotalMilliseconds >= 300)
            {
                StationAsciiArt.IncrementFrame();
                _lastAnimationUpdate = now;
            }

            try
            {
                ctx.UpdateTarget(GetResponsiveLayout());
            }
            catch (Exception ex)
            {
                Utils.DebugLogger.LogException(ex, "ConsoleHelper.UpdateDisplay");
                // Fallback chain: compact -> minimal
                try
                {
                    ctx.UpdateTarget(CreateCompactLayout());
                }
                catch
                {
                    try
                    {
                        ctx.UpdateTarget(CreateMinimalLayout());
                    }
                    catch
                    {
                        // If all layouts fail, skip this update
                    }
                }
            }
        }
    }

    private Layout CreateMinimalLayout()
    {
        // Ultra-minimal - guaranteed to fit in any terminal size
        var stationColor = GetStationColor(_currentStation);
        var statusIcon = _isPlaying ? "▶" : "⏸";
        var volume = audioService.CurrentVolume;

        // Try to fit everything, but prioritize song info
        var height = Console.WindowHeight;
        var content = "";

        // Always show: station, title, artist (bare minimum)
        content += $"[{stationColor}]{statusIcon} {_stationNames[(int)_currentStation]}[/] [{volume}%]\n";
        content += $"[white]{_currentSong.Title}[/]\n";
        content += $"[dim]{_currentSong.Artist}[/]";

        // Add album if we have space (need at least 7 lines total with panel borders)
        if (height >= 7)
        {
            content += $"\n[green]{_currentSong.Album}[/]";
        }

        var panel = new Panel(new Markup(content))
            .Border(BoxBorder.Rounded)
            .BorderColor(stationColor)
            .Expand();

        return new Layout("Root").Update(panel);
    }

    private Layout CreateCompactLayout()
    {
        // Compact - audio info + keybindings (for small terminals)
        var stationColor = GetStationColor(_currentStation);
        var statusIcon = _isPlaying ? "▶" : "⏸";
        var volume = audioService.CurrentVolume;

        var content = new Panel(
            new Markup(
                $"[yellow bold]jaybird[/] [dim]({GetOsName()})[/]\n\n" +
                $"[{stationColor} bold]{statusIcon} {_stationNames[(int)_currentStation]}[/]\n" +
                $"[cyan]♫[/] [white]{_currentSong.Title}[/]\n" +
                $"[magenta]by[/] [white]{_currentSong.Artist}[/]\n" +
                $"[green]from[/] [white]{_currentSong.Album}[/]\n\n" +
                $"[yellow]Vol:[/] [white]{volume}%[/] | " +
                $"[green]C[/]=Station [green]SPC[/]=Play/Pause [green]W/S[/]=Vol [red]^C[/]=Exit"
            )
        )
        .Border(BoxBorder.Rounded)
        .BorderColor(stationColor)
        .Expand();

        return new Layout("Root").Update(content);
    }

    private Layout CreateStandardLayout()
    {
        // Standard - horizontal header with jaybird + station + artwork, audio below
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(10),
                new Layout("Body")
            );

        // Split header into three columns: Jaybird | Station | Artwork
        var headerLayout = layout["Header"].SplitColumns(
            new Layout("JaybirdArt"),
            new Layout("StationArt"),
            new Layout("ArtworkArt")
        );

        var stationColor = GetStationColor(_currentStation);

        // Left: Jaybird ASCII
        var jaybirdPanel = new Panel(new Markup($"[yellow]{StationAsciiArt.GetJaybirdArt()}[/]"))
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Yellow)
            .Header("[yellow bold]jaybird[/]");

        // Middle: Station animation
        var stationPanel = new Panel(new Markup($"[{stationColor}]{StationAsciiArt.GetStationArt(_currentStation, _isPlaying)}[/]"))
            .Border(BoxBorder.Rounded)
            .BorderColor(stationColor)
            .Header($"[{stationColor} bold]{_stationNames[(int)_currentStation]}[/]");

        // Right: Album artwork
        var artworkPanel = _currentArtwork != null
            ? new Panel(_currentArtwork)
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Cyan1)
                .Header("[cyan1]♫ Artwork[/]")
            : new Panel(new Markup("[dim]No artwork[/]"))
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Grey)
                .Header("[dim]♫ Artwork[/]");

        headerLayout["JaybirdArt"].Update(jaybirdPanel);
        headerLayout["StationArt"].Update(stationPanel);
        headerLayout["ArtworkArt"].Update(artworkPanel);

        // Body with song information (controls are integrated)
        layout["Body"].Update(CreateSongPanel());

        return layout;
    }

    private Layout CreateFullLayout()
    {
        // Horizontal layout - jaybird left, station middle, artwork right, audio bottom
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(10),
                new Layout("Body")
            );

        // Split header into three columns: Jaybird | Station | Artwork
        var headerLayout = layout["Header"].SplitColumns(
            new Layout("JaybirdArt"),
            new Layout("StationArt"),
            new Layout("ArtworkArt")
        );

        var stationColor = GetStationColor(_currentStation);

        // Left: Jaybird ASCII
        var jaybirdPanel = new Panel(new Markup($"[yellow]{StationAsciiArt.GetJaybirdArt()}[/]"))
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Yellow)
            .Header("[yellow bold]jaybird[/]");

        // Middle: Station animation
        var stationPanel = new Panel(new Markup($"[{stationColor}]{StationAsciiArt.GetStationArt(_currentStation, _isPlaying)}[/]"))
            .Border(BoxBorder.Rounded)
            .BorderColor(stationColor)
            .Header($"[{stationColor} bold]{_stationNames[(int)_currentStation]}[/]");

        // Right: Album artwork
        var artworkPanel = _currentArtwork != null
            ? new Panel(_currentArtwork)
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Cyan1)
                .Header("[cyan1]♫ Artwork[/]")
            : new Panel(new Markup("[dim]No artwork[/]"))
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Grey)
                .Header("[dim]♫ Artwork[/]");

        headerLayout["JaybirdArt"].Update(jaybirdPanel);
        headerLayout["StationArt"].Update(stationPanel);
        headerLayout["ArtworkArt"].Update(artworkPanel);

        // Body with song information (controls integrated)
        layout["Body"].Update(CreateSongPanel());

        return layout;
    }

    private Panel CreateTitlePanel()
    {
        var grid = new Grid()
            .AddColumn(new GridColumn().NoWrap())
            .AddRow(new FigletText("jaybird")
                .Color(Color.Yellow))
            .AddRow(new Markup($"[grey]Australian ABC Radio Player[/]"))
            .AddRow(new Markup($"[dim]Version 1.0.0[/]"));

        return new Panel(grid)
            .Border(BoxBorder.Heavy)
            .BorderColor(Color.Yellow)
            .Expand();
    }

    private Panel CreateSongPanel()
    {
        // Always use compact panel with integrated controls - footer removed
        return CreateCompactSongPanel();
    }

    private Panel CreateCompactSongPanel()
    {
        // Streamlined song panel that always fits - PRIORITIZES audio info
        // Artwork is now in the header, not here
        var stationColor = GetStationColor(_currentStation);
        var statusIcon = _isPlaying ? "▶" : "⏸";
        var volume = audioService.CurrentVolume;
        var volumeColor = volume > 66 ? "green" : volume > 33 ? "yellow" : "red";

        var songGrid = new Grid()
            .AddColumn()
            .AddRow(new Markup($"[{stationColor} bold]{statusIcon} {_stationNames[(int)_currentStation]}[/] [dim]│[/] [{volumeColor}]{volume}%[/]"))
            .AddRow(new Rule().RuleStyle($"{stationColor}"))
            .AddRow(new Markup($"[bold white]{_currentSong.Title}[/]"))
            .AddRow(new Markup($"[magenta]by[/] [white]{_currentSong.Artist}[/]"))
            .AddRow(new Markup($"[green]from[/] [white]{_currentSong.Album}[/]"))
            .AddEmptyRow()
            .AddRow(new Markup($"[dim][green]C[/]=Station [green]SPC[/]=Play/Pause [green]W/S[/]=Vol±[/]"));

        return new Panel(songGrid)
            .Border(BoxBorder.Rounded)
            .BorderColor(stationColor)
            .Header($"[{stationColor}]♫ Now Playing[/]")
            .Expand();
    }

    private Panel CreateVolumeBar()
    {
        var volume = audioService.CurrentVolume;
        var barWidth = 50;
        var filledWidth = (int)(barWidth * (volume / 100.0));
        var emptyWidth = barWidth - filledWidth;

        var volumeColor = volume > 66 ? "green" : volume > 33 ? "yellow" : "red";

        var bar = new string('━', filledWidth);
        var empty = new string('─', emptyWidth);

        var volumeMarkup = new Markup(
            $"[bold]Volume:[/] [{volumeColor}]🔊 {bar}[/][dim]{empty}[/] [bold {volumeColor}]{volume}%[/]"
        );

        return new Panel(volumeMarkup)
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey);
    }

    private Panel CreateCompactFooter()
    {
        var keybindGrid = new Grid()
            .AddColumn(new GridColumn().PadRight(3))
            .AddColumn(new GridColumn().PadRight(3))
            .AddColumn(new GridColumn().PadRight(3))
            .AddColumn(new GridColumn())
            .AddRow(
                new Markup("[green]C[/][dim]=Station[/]"),
                new Markup("[green]SPC[/][dim]=Play/Pause[/]"),
                new Markup("[green]W/S[/][dim]=Volume[/]"),
                new Markup("[red]^C[/][dim]=Exit[/]")
            );

        return new Panel(keybindGrid)
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey)
            .Expand();
    }

    private Panel CreateFooterPanel()
    {
        var keybindGrid = new Grid()
            .AddColumn(new GridColumn().PadRight(4))
            .AddColumn(new GridColumn().PadRight(4))
            .AddColumn(new GridColumn().PadRight(4))
            .AddColumn(new GridColumn())
            .AddRow(
                new Markup("[bold green on grey11]  C  [/] [grey]Change Station[/]"),
                new Markup("[bold green on grey11] SPC [/] [grey]Play/Pause[/]"),
                new Markup("[bold green on grey11] W/S [/] [grey]Volume ±[/]"),
                new Markup("[bold red on grey11] ^C  [/] [grey]Exit[/]")
            );

        var systemInfo = new Markup(
            $"[dim]{RuntimeInformation.OSDescription} • {RuntimeInformation.ProcessArchitecture} • {RuntimeInformation.FrameworkDescription}[/]"
        );

        var footerGrid = new Grid()
            .AddColumn()
            .AddRow(keybindGrid)
            .AddRow(new Rule().RuleStyle("dim"))
            .AddRow(systemInfo);

        return new Panel(footerGrid)
            .Border(BoxBorder.Heavy)
            .BorderColor(Color.Grey)
            .Expand();
    }

    private async Task ChangeStationAndPlay()
    {
        _currentStation = (Station)(((int)_currentStation + 1) % _stationNames.Length);
        await audioService.PlayStream(Program.GetStreamUrlForStation(_currentStation, Program.Config));
        var newSong = await songRetrievalService.GetCurrentSongAsync(_currentStation);
        if (newSong != null)
        {
            _currentSong = newSong;
            await UpdateArtworkAsync();
        }
        UpdateDiscordPresence();
    }

    private string GetOsName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "macOS";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "Linux";
        return "Unknown OS";
    }

    private Color GetStationColor(Station station)
    {
        return station switch
        {
            Station.TripleJ => Color.Red,
            Station.DoubleJ => Color.Blue,
            Station.Unearthed => Color.Green,
            _ => Color.White
        };
    }

    private async Task MonitorWindowResize(LiveDisplayContext ctx)
    {
        while (!_shouldExit)
        {
            try
            {
                var currentWidth = Console.WindowWidth;
                var currentHeight = Console.WindowHeight;

                if (currentWidth != _lastWindowWidth || currentHeight != _lastWindowHeight)
                {
                    Utils.DebugLogger.Log($"Terminal resized: {_lastWindowWidth}x{_lastWindowHeight} -> {currentWidth}x{currentHeight}", "ConsoleHelper");
                    _lastWindowWidth = currentWidth;
                    _lastWindowHeight = currentHeight;

                    // Re-render artwork with new adaptive size
                    await UpdateArtworkAsync();

                    UpdateDisplay(ctx);
                }
            }
            catch (Exception ex)
            {
                Utils.DebugLogger.LogException(ex, "ConsoleHelper.MonitorWindowResize");
            }

            await Task.Delay(100); // Check every 100ms for resize
        }
    }

    private async Task PeriodicUpdates(LiveDisplayContext ctx)
    {
        while (!_shouldExit)
        {
            try
            {
                var newSong = await songRetrievalService.GetCurrentSongAsync(_currentStation);
                if (newSong != null)
                {
                    _currentSong = newSong;

                    // Fetch artwork for the new song
                    await UpdateArtworkAsync();

                    UpdateDiscordPresence();
                    UpdateDisplay(ctx);
                }
            }
            catch (Exception ex)
            {
                Utils.DebugLogger.LogException(ex, "ConsoleHelper.PeriodicUpdates");
            }

            await Task.Delay(10000);
        }
    }

    private async Task UpdateArtworkAsync()
    {
        try
        {
            // Render artwork for header display (fixed small size)
            _currentArtwork = await ArtworkRenderer.RenderHeaderArtworkAsync(_currentSong.ArtworkUrl, _lastWindowHeight);
        }
        catch (Exception ex)
        {
            Utils.DebugLogger.LogException(ex, "ConsoleHelper.UpdateArtworkAsync");
            _currentArtwork = null;
        }
    }

    private void UpdateDiscordPresence()
    {
        discordService.UpdatePresence(
            $"{_currentSong.Title}",
            $"{_currentSong.Artist}",
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