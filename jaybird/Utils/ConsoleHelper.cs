namespace jaybird.Utils;

using Services;
using Models;
using Spectre.Console;
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
    private int _lastWindowWidth = 0;
    private int _lastWindowHeight = 0;

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
        // Priority 2: Compact - audio + keybindings (8-16 lines)
        else if (height < 16 || width < 60)
        {
            Utils.DebugLogger.Log("Using compact layout (< 16 lines or < 60 width)", "ConsoleHelper");
            return CreateCompactLayout();
        }
        // Priority 3: Standard - full layout without ASCII art (16-24 lines)
        else if (height < 24)
        {
            Utils.DebugLogger.Log("Using standard layout (< 24 lines)", "ConsoleHelper");
            return CreateStandardLayout();
        }
        // Priority 4: Full - everything including ASCII art (24+ lines)
        else
        {
            Utils.DebugLogger.Log("Using full layout (24+ lines)", "ConsoleHelper");
            return CreateFullLayout();
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
        // Standard - full UI without ASCII art (medium terminals)
        var height = Console.WindowHeight;

        // Dynamic sizing: prioritize audio info
        var headerSize = Math.Max(4, Math.Min(6, height / 6)); // 4-6 lines
        var footerSize = Math.Max(3, Math.Min(5, height / 8)); // 3-5 lines

        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(headerSize),
                new Layout("Body"),
                new Layout("Footer").Size(footerSize)
            );

        // Header with just branding, no ASCII art
        var titlePanel = new Panel(
            new Markup(
                $"[yellow bold]jaybird[/] - Australian ABC Radio Player\n" +
                $"[dim]{GetOsName()} • {RuntimeInformation.ProcessArchitecture}[/]"
            )
        )
        .Border(BoxBorder.Heavy)
        .BorderColor(Color.Yellow)
        .Expand();

        layout["Header"].Update(titlePanel);

        // Body with song information (same as full)
        layout["Body"].Update(CreateSongPanel());

        // Footer with keybindings (compact version)
        layout["Footer"].Update(CreateCompactFooter());

        return layout;
    }

    private Layout CreateFullLayout()
    {
        var height = Console.WindowHeight;

        // Dynamic sizing based on terminal height - prioritize audio
        int headerSize, footerSize;

        if (height >= 35)
        {
            // Plenty of space - use full sizes
            headerSize = 15;
            footerSize = 8;
        }
        else if (height >= 30)
        {
            // Comfortable space - slightly reduced
            headerSize = 13;
            footerSize = 7;
        }
        else
        {
            // Tight space - minimize header/footer, maximize audio
            headerSize = 11;
            footerSize = 5;
        }

        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(headerSize),
                new Layout("Body"),
                new Layout("Footer").Size(footerSize)
            );

        // Header with OS ASCII art and jaybird branding
        var headerLayout = layout["Header"].SplitColumns(
            new Layout("OsArt").Size(40),
            new Layout("Title")
        );

        var osArt = new Panel(new Markup($"[{OsAsciiArt.GetOsColor()}]{OsAsciiArt.GetOsAsciiArt()}[/]"))
            .Border(BoxBorder.Heavy)
            .BorderColor(OsAsciiArt.GetOsColor())
            .Header($"[bold {OsAsciiArt.GetOsColor()}]{GetOsName()}[/]");

        var titlePanel = CreateTitlePanel();

        headerLayout["OsArt"].Update(osArt);
        headerLayout["Title"].Update(titlePanel);

        // Body with song information
        layout["Body"].Update(CreateSongPanel());

        // Footer - use compact footer when space is tight
        if (height >= 32)
        {
            layout["Footer"].Update(CreateFooterPanel());
        }
        else
        {
            layout["Footer"].Update(CreateCompactFooter());
        }

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
        var stationColor = GetStationColor(_currentStation);
        var statusIcon = _isPlaying ? "▶" : "⏸";

        // Check available height and create appropriate song panel
        var availableHeight = Console.WindowHeight;

        // Compact song panel for tighter spaces (saves ~5 lines)
        if (availableHeight < 25)
        {
            return CreateCompactSongPanel();
        }

        var songGrid = new Grid()
            .AddColumn(new GridColumn().Width(18).PadRight(2))
            .AddColumn(new GridColumn().Padding(0, 0))
            .AddRow(
                new Panel(new Markup($"[bold {stationColor}]{statusIcon}[/]  [{stationColor}]{_stationNames[(int)_currentStation]}[/]"))
                    .Border(BoxBorder.Heavy)
                    .BorderColor(stationColor)
                    .Header("[bold]Station[/]")
            )
            .AddEmptyRow()
            .AddRow(new Markup($"[bold cyan]♫  Now Playing:[/]"))
            .AddRow(new Rule($"[bold white]{_currentSong.Title}[/]").LeftJustified().RuleStyle($"bold {stationColor}"))
            .AddEmptyRow()
            .AddRow(new Markup($"[bold magenta]Artist:[/]  [white]{_currentSong.Artist}[/]"))
            .AddRow(new Markup($"[bold green]Album:[/]   [white]{_currentSong.Album}[/]"))
            .AddRow(new Markup($"[bold yellow]Time:[/]    [white]{_currentSong.PlayedTime:HH:mm:ss}[/]"));

        var volumeBar = CreateVolumeBar();

        var mainGrid = new Grid()
            .AddColumn()
            .AddRow(songGrid)
            .AddEmptyRow()
            .AddRow(volumeBar);

        return new Panel(mainGrid)
            .Border(BoxBorder.Heavy)
            .BorderColor(Color.Cyan1)
            .Header("[bold cyan1]━━━━━━━━━━━━  AUDIO INFO  ━━━━━━━━━━━━[/]")
            .Expand();
    }

    private Panel CreateCompactSongPanel()
    {
        // Streamlined song panel that always fits - PRIORITIZES audio info
        var stationColor = GetStationColor(_currentStation);
        var statusIcon = _isPlaying ? "▶" : "⏸";
        var volume = audioService.CurrentVolume;

        var songGrid = new Grid()
            .AddColumn()
            .AddRow(new Markup($"[{stationColor} bold]{statusIcon} {_stationNames[(int)_currentStation]}[/] [dim]│[/] [yellow]{volume}%[/]"))
            .AddRow(new Rule().RuleStyle($"{stationColor}"))
            .AddRow(new Markup($"[bold white]{_currentSong.Title}[/]"))
            .AddRow(new Markup($"[magenta]by[/] [white]{_currentSong.Artist}[/]"))
            .AddRow(new Markup($"[green]from[/] [white]{_currentSong.Album}[/]"));

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