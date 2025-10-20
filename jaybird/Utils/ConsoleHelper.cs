namespace jaybird.Utils;

using Services;
using Models;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Runtime.InteropServices;

public class ConsoleHelper
{
    private readonly string[] _stationNames = { "Triple J", "Double J", "Unearthed" };
    private readonly AudioService AudioService;
    private readonly IRegionalSongRetrievalService SongRetrievalService;
    private readonly IDiscordService DiscordService;
    private readonly ISettingsService SettingsService;
    private readonly ISongCacheService SongCacheService;
    private readonly TimezoneService TimezoneService;
    private Station _currentStation;
    private Models.Region _currentRegion; // Default to NSW

    public ConsoleHelper(
        AudioService audioService,
        IRegionalSongRetrievalService songRetrievalService,
        IDiscordService discordService,
        ISettingsService settingsService,
        ISongCacheService songCacheService,
        TimezoneService timezoneService,
        UserSettings initialSettings)
    {
        AudioService = audioService;
        SongRetrievalService = songRetrievalService;
        DiscordService = discordService;
        SettingsService = settingsService;
        SongCacheService = songCacheService;
        TimezoneService = timezoneService;
        _currentStation = initialSettings.LastStation;
        _currentRegion = initialSettings.LastRegion ?? Models.Region.NSW;
    }
    private bool _isPlaying = true;
    private RegionalSongData _currentSong = new RegionalSongData
        { Title = "Unknown", Artist = "Unknown", Album = "Unknown", PlayedTime = DateTime.Now, Region = Models.Region.NSW, IsLive = true };
    private bool _shouldExit = false;
    private readonly object _updateLock = new object();
    private DateTime _lastUpdate = DateTime.MinValue;
    private DateTime _lastAnimationUpdate = DateTime.MinValue;
    private int _lastWindowWidth = 0;
    private int _lastWindowHeight = 0;
    private IRenderable? _currentArtwork = null;

    public async Task InitializeAsync()
    {
        // Fetch initial song data with cache support
        Utils.DebugLogger.Log("Initializing UI with initial song data", "ConsoleHelper");
        try
        {
            // Try cache first for instant display
            var cachedSong = await SongCacheService.GetCachedSongAsync(_currentStation, _currentRegion);
            if (cachedSong != null)
            {
                lock (_updateLock)
                {
                    _currentSong = (RegionalSongData)cachedSong.Song;
                    _currentArtwork = cachedSong.Artwork;
                }
                UpdateDiscordPresence();
                Utils.DebugLogger.Log($"Initial song data loaded from cache for {_currentStation} ({_currentRegion})", "ConsoleHelper");
                
                // Start background refresh to ensure data is fresh
                _ = Task.Run(async () => await RefreshCurrentStationDataAsync());
            }
            else
            {
                // No cache hit, fetch from API
                var song = await SongRetrievalService.GetCurrentSongAsync(_currentStation, _currentRegion);
                if (song != null)
                {
                    lock (_updateLock)
                    {
                        _currentSong = song;
                    }
                    await UpdateArtworkAsync();
                    
                    // Cache the fetched data
                    IRenderable? currentArtwork;
                    lock (_updateLock)
                    {
                        currentArtwork = _currentArtwork;
                    }
                    SongCacheService.CacheSongData(_currentStation, _currentRegion, song, currentArtwork);
                    
                    UpdateDiscordPresence();
                    Utils.DebugLogger.Log("Initial song data loaded from API and cached", "ConsoleHelper");
                }
                else
                {
                    Utils.DebugLogger.Log("No initial song data available, using fallback", "ConsoleHelper");
                    // Create fallback song data to ensure we have something to display
                    var fallbackSong = new RegionalSongData
                    {
                        Title = "Tuned into: " + _stationNames[(int)_currentStation],
                        Artist = "Loading...",
                        Album = "",
                        PlayedTime = DateTime.Now,
                        Region = _currentRegion,
                        IsLive = _currentRegion.IsLiveRegion(),
                        Delay = TimezoneService.GetDelayForRegion(_currentRegion)
                    };
                    
                    lock (_updateLock)
                    {
                        _currentSong = fallbackSong;
                    }
                    
                    UpdateDiscordPresence();
                }
            }
        }
        catch (Exception ex)
        {
            Utils.DebugLogger.LogException(ex, "ConsoleHelper.InitializeAsync");
        }
    }

    public async Task Run()
    {
        try
        {
            Console.CursorVisible = false;
        }
        catch
        {
            // Console might not support cursor visibility in all environments
        }
        try
        {
            Console.Clear();
        }
        catch
        {
            // Console might not support clear in all environments
        }

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
                // Small delay to ensure Live context is ready
                await Task.Delay(100);
                
                // Update display with initial data
                UpdateDisplay(ctx);
                
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

        // Always use the new focused song layout
        return CreateFocusedSongLayout();
    }

    private async Task HandleKeyPress(ConsoleKeyInfo keyInfo, LiveDisplayContext ctx)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.Spacebar:
                _isPlaying = !_isPlaying;
                if (_isPlaying)
                {
                    // Resume playback
                    var streamUrl = AudioService.GetRegionalStreamUrl(_currentStation, _currentRegion);
                    await AudioService.PlayStream(streamUrl, _currentRegion);
                    Utils.DebugLogger.Log("Playback resumed", "ConsoleHelper");
                }
                else
                {
                    // Stop the stream completely to free resources
                    await AudioService.StopStream();
                    Utils.DebugLogger.Log("Playback paused and stream stopped", "ConsoleHelper");
                }
                UpdateDisplay(ctx);
                break;
            case ConsoleKey.C:
                _isPlaying = true; // Ensure playing state when changing stations
                await ChangeStationAndPlay();
                UpdateDisplay(ctx);
                break;
            case ConsoleKey.R:
                await ChangeRegionAndPlay(ctx);
                UpdateDisplay(ctx);
                break;
            case ConsoleKey.W:
                AudioService.IncreaseVolume();
                UpdateDisplay(ctx);
                break;
            case ConsoleKey.S:
                AudioService.DecreaseVolume();
                UpdateDisplay(ctx);
                break;
            case ConsoleKey.Escape:
            case ConsoleKey.Q:
                // Graceful exit
                _shouldExit = true;
                Utils.DebugLogger.Log("User requested exit", "ConsoleHelper");
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

            // No animation needed anymore since we use text status
            // Animation code removed

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
        var delayDisplay = GetRegionDisplayText();
        var statusText = _isPlaying
            ? $"Now Playing - {_stationNames[(int)_currentStation]} {delayDisplay}"
            : $"Paused - {_stationNames[(int)_currentStation]} {delayDisplay}";

        string title, artist, album;
        lock (_updateLock)
        {
            title = _currentSong.Title;
            artist = _currentSong.Artist;
            album = _currentSong.Album;
        }

        // Try to fit everything, but prioritize song info
        int height;
        try
        {
            height = Console.WindowHeight;
        }
        catch
        {
            height = 24; // Default fallback height
        }
        var content = "";

        // Always show: station, title, artist (bare minimum)
        content += $"[{stationColor}]{statusText}[/]\n";
        content += $"[white]{title}[/]\n";
        content += $"[dim]{artist}[/]";

        // Add album if we have space (need at least 7 lines total with panel borders)
        if (height >= 7)
        {
            content += $"\n[green]{album}[/]";
        }

        var panel = new Panel(new Markup(content))
            .Border(BoxBorder.Rounded)
            .BorderColor(stationColor);

        return new Layout("Root").Update(panel);
    }

    private Layout CreateCompactLayout()
    {
        // Compact - audio info + keybindings (for small terminals)
        var stationColor = GetStationColor(_currentStation);
        var delayDisplay = GetRegionDisplayText();
        var statusText = _isPlaying
            ? $"Now Playing - {_stationNames[(int)_currentStation]} {delayDisplay}"
            : $"Paused - {_stationNames[(int)_currentStation]} {delayDisplay}";

        string title, artist, album;
        lock (_updateLock)
        {
            title = _currentSong.Title;
            artist = _currentSong.Artist;
            album = _currentSong.Album;
        }

        var content = new Panel(
            new Markup(
                $"[yellow bold]jaybird[/] [dim]({GetOsName()})[/]\n\n" +
                $"[{stationColor} bold]{statusText}[/]\n" +
                $"[cyan]♫[/] [white]{title}[/]\n" +
                $"[magenta]by[/] [white]{artist}[/]\n" +
                $"[green]from[/] [white]{album}[/]\n\n" +
                $"[green]C[/]=Station  [green]R[/]=Region  [green]SPC[/]=Play/Pause  [green]W/S[/]=Vol  [red]Q/ESC[/]=Exit"
            )
        )
        .Border(BoxBorder.Rounded)
        .BorderColor(stationColor);

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

        // Split header into two columns: Jaybird | Station
        var headerLayout = layout["Header"].SplitColumns(
            new Layout("JaybirdArt"),
            new Layout("StationArt")
        );

        var stationColor = GetStationColor(_currentStation);

        // Left: Jaybird ASCII
        var jaybirdPanel = new Panel(new Markup($"[yellow]{StationAsciiArt.GetJaybirdArt()}[/]"))
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Yellow)
            .Header("[yellow bold]jaybird[/]");

        // Right: Station animation
        var stationPanel = new Panel(new Markup($"[{stationColor}]{StationAsciiArt.GetStationArt(_currentStation, _isPlaying)}[/]"))
            .Border(BoxBorder.Rounded)
            .BorderColor(stationColor)
            .Header($"[{stationColor} bold]{_stationNames[(int)_currentStation]}[/]");

        headerLayout["JaybirdArt"].Update(jaybirdPanel);
        headerLayout["StationArt"].Update(stationPanel);

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

        // Split header into two columns: Jaybird | Station
        var headerLayout = layout["Header"].SplitColumns(
            new Layout("JaybirdArt"),
            new Layout("StationArt")
        );

        var stationColor = GetStationColor(_currentStation);

        // Left: Jaybird ASCII
        var jaybirdPanel = new Panel(new Markup($"[yellow]{StationAsciiArt.GetJaybirdArt()}[/]"))
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Yellow)
            .Header("[yellow bold]jaybird[/]");

        // Right: Station animation
        var stationPanel = new Panel(new Markup($"[{stationColor}]{StationAsciiArt.GetStationArt(_currentStation, _isPlaying)}[/]"))
            .Border(BoxBorder.Rounded)
            .BorderColor(stationColor)
            .Header($"[{stationColor} bold]{_stationNames[(int)_currentStation]}[/]");

        headerLayout["JaybirdArt"].Update(jaybirdPanel);
        headerLayout["StationArt"].Update(stationPanel);

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

    private Layout CreateFocusedSongLayout()
    {
        // New focused layout: artwork on left, song info on right
        var stationColor = GetStationColor(_currentStation);
        var delayDisplay = GetRegionDisplayText();
        var statusText = _isPlaying
            ? $"Now Playing - {_stationNames[(int)_currentStation]} {delayDisplay}"
            : $"Paused - {_stationNames[(int)_currentStation]} {delayDisplay}";

        // Create the main grid with 2 columns
        var mainGrid = new Grid()
            .AddColumn(new GridColumn().Width(18))  // Artwork column (fixed small width)
            .AddColumn(new GridColumn());           // Song info column (flexible)

        // Left: Artwork (if available)
        IRenderable? artwork;
        lock (_updateLock)
        {
            artwork = _currentArtwork;
        }

        if (artwork != null)
        {
            mainGrid.AddRow(artwork, CreateSongInfoGrid(stationColor, statusText));
        }
        else
        {
            mainGrid.AddRow(
                new Markup("[dim]No\nArtwork[/]"),
                CreateSongInfoGrid(stationColor, statusText)
            );
        }

        return new Layout("Root").Update(
            new Panel(mainGrid)
                .Border(BoxBorder.Rounded)
                .BorderColor(stationColor)
                .Header($"[{stationColor}]jaybird[/]")
        );
    }

    private Grid CreateSongInfoGrid(Color stationColor, string statusText)
    {
        string title, artist, album;
        lock (_updateLock)
        {
            title = _currentSong.Title;
            artist = _currentSong.Artist;
            album = _currentSong.Album;
        }

        var volumeBar = CreateVolumeBarMarkup();

        return new Grid()
            .AddColumn()
            .AddRow(new Markup($"[{stationColor} bold]{statusText}[/]"))
            .AddRow(new Rule().RuleStyle($"{stationColor}"))
            .AddRow(new Markup($"[bold white]{title}[/]"))
            .AddRow(new Markup($"[magenta]by[/] [white]{artist}[/]"))
            .AddRow(new Markup($"[green]from[/] [white]{album}[/]"))
            .AddEmptyRow()
            .AddRow(volumeBar)
            .AddEmptyRow()
            .AddRow(new Markup($"[dim][green]C[/]=Station  [green]R[/]=Region  [green]SPC[/]=Play/Pause  [green]W/S[/]=Vol±  [red]Q/ESC[/]=Exit[/]"));
    }

    private Markup CreateVolumeBarMarkup()
    {
        var volume = AudioService.CurrentVolume;
        var volumeColor = volume > 66 ? "green" : volume > 33 ? "yellow" : "red";

        // Estimate available width for the bar itself
        // Account for "Volume: " (8 chars) + " XXX%" (5 chars) + padding/borders (30 chars)
        int availableWidth;
        try
        {
            availableWidth = Console.WindowWidth - 43; // More conservative calculation
        }
        catch
        {
            availableWidth = 30;
        }

        // If too narrow, just show percentage
        if (availableWidth < 15)
        {
            return new Markup($"[bold]Volume:[/] [{volumeColor}]{volume}%[/]");
        }

        // Calculate bar segments - cap at 30 chars for cleaner look
        var barWidth = Math.Min(availableWidth, 30);
        var filledWidth = (int)Math.Round(barWidth * (volume / 100.0));
        var emptyWidth = barWidth - filledWidth;

        var filled = new string('█', filledWidth);
        var empty = new string('░', emptyWidth);

        return new Markup($"[bold]Volume:[/] [{volumeColor}]{filled}{empty}[/] [{volumeColor}]{volume}%[/]");
    }

    private Panel CreateCompactSongPanel()
    {
        // Legacy method - redirect to new layout
        var stationColor = GetStationColor(_currentStation);
        var delayDisplay = GetRegionDisplayText();
        var statusText = _isPlaying
            ? $"Now Playing - {_stationNames[(int)_currentStation]} {delayDisplay}"
            : $"Paused - {_stationNames[(int)_currentStation]} {delayDisplay}";

        return new Panel(CreateSongInfoGrid(stationColor, statusText))
            .Border(BoxBorder.Rounded)
            .BorderColor(stationColor)
            .Header($"[{stationColor}]jaybird[/]");
    }

    private Panel CreateVolumeBar()
    {
        var volume = AudioService.CurrentVolume;
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
            .AddColumn(new GridColumn().PadRight(3))
            .AddColumn(new GridColumn())
            .AddRow(
                new Markup("[green]C[/][dim]=Station[/]"),
                new Markup("[green]R[/][dim]=Region[/]"),
                new Markup("[green]SPC[/][dim]=Play/Pause[/]"),
                new Markup("[green]W/S[/][dim]=Vol[/]"),
                new Markup("[red]Q/ESC[/][dim]=Exit[/]")
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
        var streamUrl = AudioService.GetRegionalStreamUrl(_currentStation, _currentRegion);
        await AudioService.PlayStream(streamUrl, _currentRegion);
        
        // Try cache first for instant station switching
        var cachedSong = await SongCacheService.GetCachedSongAsync(_currentStation, _currentRegion);
        if (cachedSong != null)
        {
            // Instant display from cache
            lock (_updateLock)
            {
                _currentSong = (RegionalSongData)cachedSong.Song;
                _currentArtwork = cachedSong.Artwork;
            }
            UpdateDiscordPresence();
            Utils.DebugLogger.Log($"Instant station switch to {_stationNames[(int)_currentStation]} from cache", "ConsoleHelper");
            
            // Start background refresh to ensure data is fresh
            _ = Task.Run(async () => await RefreshCurrentStationDataAsync());
        }
        else
        {
            // No cache hit, fetch from API
            var newSong = await SongRetrievalService.GetCurrentSongAsync(_currentStation, _currentRegion);
            if (newSong != null)
            {
                lock (_updateLock)
                {
                    _currentSong = newSong;
                }
                await UpdateArtworkAsync();
                UpdateDiscordPresence();
                Utils.DebugLogger.Log($"Station switch to {_stationNames[(int)_currentStation]} from API", "ConsoleHelper");
            }
        }
        
        // Save station change to settings
        await SaveCurrentSettingsAsync();
    }

    private Models.Region? ShowRegionSelectionModal(LiveDisplayContext ctx)
    {
        // Region choices with display names
        var regionChoices = new List<(string Display, Models.Region Region)>
        {
            ("NSW/ACT/VIC/TAS (Live)", Models.Region.NSW),
            ($"QLD ({TimezoneService.GetDelayDisplay(Models.Region.QLD)})", Models.Region.QLD),
            ($"SA ({TimezoneService.GetDelayDisplay(Models.Region.SA)})", Models.Region.SA),
            ($"NT ({TimezoneService.GetDelayDisplay(Models.Region.NT)})", Models.Region.NT),
            ($"WA ({TimezoneService.GetDelayDisplay(Models.Region.WA)})", Models.Region.WA)
        };

        int selectedIndex = regionChoices.FindIndex(r => r.Region == _currentRegion);
        if (selectedIndex == -1) selectedIndex = 0;

        bool selectionMade = false;
        bool cancelled = false;

        while (!selectionMade && !cancelled)
        {
            // Create modal overlay
            var modalContent = CreateRegionSelectionModal(regionChoices, selectedIndex);
            ctx.UpdateTarget(modalContent);

            // Handle keyboard input
            var key = Console.ReadKey(intercept: true);
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    selectedIndex = selectedIndex > 0 ? selectedIndex - 1 : regionChoices.Count - 1;
                    break;
                case ConsoleKey.DownArrow:
                    selectedIndex = selectedIndex < regionChoices.Count - 1 ? selectedIndex + 1 : 0;
                    break;
                case ConsoleKey.Enter:
                    selectionMade = true;
                    break;
                case ConsoleKey.Escape:
                    cancelled = true;
                    break;
            }
        }

        return cancelled ? null : regionChoices[selectedIndex].Region;
    }

    private Layout CreateRegionSelectionModal(List<(string Display, Models.Region Region)> choices, int selectedIndex)
    {
        // Build the selection list
        var selectionText = new List<string>();

        // Add note if on Unearthed station
        if (_currentStation == Station.Unearthed)
        {
            selectionText.Add("[yellow]ℹ[/] [dim]Unearthed is a national stream (LIVE only)[/]");
            selectionText.Add("");
        }

        for (int i = 0; i < choices.Count; i++)
        {
            if (i == selectedIndex)
            {
                selectionText.Add($"[black on yellow]▶ {choices[i].Display}[/]");
            }
            else
            {
                selectionText.Add($"[grey]  {choices[i].Display}[/]");
            }
        }

        selectionText.Add("");
        selectionText.Add("[grey46]↑/↓ Navigate  │  Enter Select  │  Esc Cancel[/]");

        // Create compact modal panel
        var modalContent = new Panel(new Markup(string.Join("\n", selectionText)))
            .Header("[yellow bold] SELECT REGION [/]", Justify.Center)
            .Border(BoxBorder.Heavy)
            .BorderColor(Color.Yellow)
            .Padding(2, 1);

        // Get the FULL current UI layout as background
        var fullCurrentUI = GetResponsiveLayout();

        // Calculate terminal dimensions
        int terminalHeight = 24;
        int terminalWidth = 80;
        try
        {
            terminalHeight = Console.WindowHeight;
            terminalWidth = Console.WindowWidth;
        }
        catch { }

        // Calculate modal positioning to center it
        int modalHeight = choices.Count + 6; // Height of modal with borders
        int topSpace = Math.Max(1, (terminalHeight - modalHeight) / 2);
        int bottomSpace = Math.Max(1, terminalHeight - modalHeight - topSpace);

        // Create a 3-row layout: top space, modal, bottom space
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("TopSpace").Size(topSpace),
                new Layout("Modal").Size(modalHeight),
                new Layout("BottomSpace").Size(bottomSpace)
            );

        // Fill top and bottom with dimmed version of current UI
        var dimmedTopUI = CreateDimmedUISection(true);
        var dimmedBottomUI = CreateDimmedUISection(false);

        layout["TopSpace"].Update(dimmedTopUI);
        layout["BottomSpace"].Update(dimmedBottomUI);

        // Center the modal horizontally
        int modalWidth = 50;
        int leftPadding = Math.Max(0, (terminalWidth - modalWidth) / 2);

        var centeredModal = new Grid()
            .AddColumn(new GridColumn().Width(leftPadding))
            .AddColumn(new GridColumn().Width(modalWidth))
            .AddColumn()
            .AddRow(new Text(""), modalContent, new Text(""));

        layout["Modal"].Update(centeredModal);

        // Wrap everything in a panel with dimmed header to indicate modal mode
        return new Layout("Overlay").Update(
            new Panel(layout)
                .Border(BoxBorder.None)
                .Header("[dim]◄ REGION SELECTION MODE ►  Press ESC to cancel[/]", Justify.Center)
        );
    }

    private Panel CreateDimmedUISection(bool isTop)
    {
        // Get current song info for dimmed display
        string title, artist, album;
        var stationColor = GetStationColor(_currentStation);
        var delayDisplay = GetRegionDisplayText();

        lock (_updateLock)
        {
            title = _currentSong.Title;
            artist = _currentSong.Artist;
            album = _currentSong.Album;
        }

        var statusText = _isPlaying
            ? $"Now Playing - {_stationNames[(int)_currentStation]} {delayDisplay}"
            : $"Paused - {_stationNames[(int)_currentStation]} {delayDisplay}";

        // Create grid to show artwork + info (dimmed)
        var grid = new Grid()
            .AddColumn(new GridColumn().Width(18))  // Artwork column
            .AddColumn(new GridColumn());           // Song info column

        IRenderable? artwork;
        lock (_updateLock)
        {
            artwork = _currentArtwork;
        }

        // Dimmed artwork or placeholder
        var dimmedArtwork = artwork != null
            ? new Panel(new Markup("[grey30]♪[/]")).Border(BoxBorder.None)
            : new Panel(new Markup("[grey30]No\nArtwork[/]")).Border(BoxBorder.None);

        // Dimmed song info
        var dimmedInfo = new Grid()
            .AddColumn()
            .AddRow(new Markup($"[grey30 bold]{statusText}[/]"))
            .AddRow(new Rule().RuleStyle("grey30"))
            .AddRow(new Markup($"[grey27]{title}[/]"))
            .AddRow(new Markup($"[grey27]{artist}[/]"))
            .AddRow(new Markup($"[grey23]{album}[/]"));

        grid.AddRow(dimmedArtwork, dimmedInfo);

        return new Panel(grid)
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey23)
            .Header("[grey23]jaybird[/]");
    }

    private async Task ChangeRegionAndPlay(LiveDisplayContext ctx)
    {
        var newRegion = ShowRegionSelectionModal(ctx);

        if (newRegion.HasValue && newRegion.Value != _currentRegion)
        {
            _currentRegion = newRegion.Value;

            // Clear cache for the new region to force fresh data
            SongRetrievalService.ClearEtagForRegion(_currentStation, _currentRegion);

            // Get regional stream URL and play
            var streamUrl = AudioService.GetRegionalStreamUrl(_currentStation, _currentRegion);
            await AudioService.PlayStream(streamUrl, _currentRegion);

            // Try cache first for instant region switching
            var cachedSong = await SongCacheService.GetCachedSongAsync(_currentStation, _currentRegion);
            if (cachedSong != null)
            {
                // Instant display from cache
                lock (_updateLock)
                {
                    _currentSong = (RegionalSongData)cachedSong.Song;
                    _currentArtwork = cachedSong.Artwork;
                }
                UpdateDiscordPresence();
                Utils.DebugLogger.Log($"Instant region switch to {_currentRegion} from cache", "ConsoleHelper");

                // Start background refresh to ensure data is fresh
                _ = Task.Run(async () => await RefreshCurrentStationDataAsync());
            }
            else
            {
                // No cache hit, fetch from API
                var newSong = await SongRetrievalService.GetCurrentSongAsync(_currentStation, _currentRegion);
                if (newSong != null)
                {
                    lock (_updateLock)
                    {
                        _currentSong = newSong;
                    }
                    await UpdateArtworkAsync();
                    UpdateDiscordPresence();
                    Utils.DebugLogger.Log($"Region switch to {_currentRegion} from API", "ConsoleHelper");
                }
            }

            // Save region change to settings
            await SaveCurrentSettingsAsync();
        }
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

    private string GetRegionDisplayText()
    {
        // Unearthed is national only - always show as LIVE
        if (_currentStation == Station.Unearthed)
        {
            return "(National - LIVE)";
        }

        // Triple J and Double J have regional variations
        var delayDisplay = _currentSong.GetDelayDisplay();
        return $"({_currentRegion.GetDisplayName()} {delayDisplay})";
    }

    private Color GetStationColor(Station station)
    {
        return station switch
        {
            Station.TripleJ => Color.Red,
            Station.DoubleJ => Color.White,
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
                // Only fetch song data when playing to minimize network usage
                if (_isPlaying)
                {
                    var newSong = await SongRetrievalService.GetCurrentSongAsync(_currentStation, _currentRegion);
                    if (newSong != null)
                    {
                        // Check if this is actually different data
                        RegionalSongData currentSong;
                        lock (_updateLock)
                        {
                            currentSong = _currentSong;
                        }

                        var isDifferent = newSong.Title != currentSong.Title || 
                                        newSong.Artist != currentSong.Artist ||
                                        newSong.Album != currentSong.Album;

                        if (isDifferent)
                        {
                            // Update artwork for the new song
                            await UpdateArtworkAsync();
                            
                    // Cache the new data
                    IRenderable? currentArtwork;
                    lock (_updateLock)
                    {
                        _currentSong = newSong;
                        currentArtwork = _currentArtwork;
                    }
                    SongCacheService.CacheSongData(_currentStation, _currentRegion, newSong, currentArtwork);
                            
                            UpdateDiscordPresence();
                            UpdateDisplay(ctx);
                            
                            Utils.DebugLogger.Log($"Song updated: {newSong.Title} by {newSong.Artist}", "ConsoleHelper");
                        }
                        else
                        {
                            Utils.DebugLogger.Log("No song changes detected", "ConsoleHelper");
                        }
                    }
                }
                else
                {
                    Utils.DebugLogger.Log("Skipping song update (paused)", "ConsoleHelper");
                }

                // Periodic cache cleanup
                SongCacheService.CleanupExpiredEntries();
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
            string? artworkUrl;
            lock (_updateLock)
            {
                artworkUrl = _currentSong.ArtworkUrl;
            }

            // Use fixed small size (12 chars) for compact, focused layout
            var artwork = await ArtworkRenderer.RenderArtworkAsync(artworkUrl, 12, SongCacheService);

            lock (_updateLock)
            {
                _currentArtwork = artwork;
            }
        }
        catch (Exception ex)
        {
            Utils.DebugLogger.LogException(ex, "ConsoleHelper.UpdateArtworkAsync");
            lock (_updateLock)
            {
                _currentArtwork = null;
            }
        }
    }

    private void UpdateDiscordPresence()
    {
        string title, artist;
        lock (_updateLock)
        {
            title = _currentSong.Title;
            artist = _currentSong.Artist;
        }

        var delayDisplay = _currentSong.GetDelayDisplay();
        DiscordService.UpdatePresence(
            title,
            artist,
            "jaybird",
            GetCurrentStationSmallImageKey(_currentStation),
            $"Tuned into: {_stationNames[(int)_currentStation]} ({_currentRegion.GetDisplayName()} {delayDisplay})",
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
    public Models.Region GetCurrentRegion() => _currentRegion;

    private async Task SaveCurrentSettingsAsync()
    {
        try
        {
            var currentSettings = new UserSettings
            {
                LastStation = _currentStation,
                LastRegion = _currentRegion,
                LastVolume = AudioService.CurrentVolume
            };
            await SettingsService.SaveSettingsAsync(currentSettings);
        }
        catch (Exception ex)
        {
            Utils.DebugLogger.LogException(ex, "ConsoleHelper.SaveCurrentSettingsAsync");
        }
    }

    private async Task RefreshCurrentStationDataAsync()
    {
        try
        {
            Utils.DebugLogger.Log($"Background refresh for station {_currentStation} ({_currentRegion})", "ConsoleHelper");
            var newSong = await SongRetrievalService.GetCurrentSongAsync(_currentStation, _currentRegion);
            if (newSong != null)
            {
                // Check if this is actually different data
                RegionalSongData currentSong;
                lock (_updateLock)
                {
                    currentSong = _currentSong;
                }

                var isDifferent = newSong.Title != currentSong.Title || 
                                newSong.Artist != currentSong.Artist ||
                                newSong.Album != currentSong.Album;

                if (isDifferent)
                {
                    // Update artwork for the new song
                    await UpdateArtworkAsync();
                    
                    // Cache the new data
                    IRenderable? currentArtwork;
                    lock (_updateLock)
                    {
                        _currentSong = newSong;
                        currentArtwork = _currentArtwork;
                    }
                    
                    SongCacheService.CacheSongData(_currentStation, newSong, currentArtwork);
                    
                    UpdateDiscordPresence();
                    Utils.DebugLogger.Log($"Background refresh updated song: {newSong.Title} by {newSong.Artist}", "ConsoleHelper");
                }
                else
                {
                    Utils.DebugLogger.Log("Background refresh: no changes detected", "ConsoleHelper");
                }
            }
        }
        catch (Exception ex)
        {
            Utils.DebugLogger.LogException(ex, "ConsoleHelper.RefreshCurrentStationDataAsync");
        }
    }
}