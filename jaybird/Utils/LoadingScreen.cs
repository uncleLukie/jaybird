namespace jaybird.Utils;

using Spectre.Console;
using System.Diagnostics;

public class LoadingScreen
{
    private readonly Dictionary<string, ProgressTask> _tasks = new();
    private readonly Stopwatch _stopwatch = new();
    private bool _isActive = false;

    public enum LoadingStep
    {
        Configuration,
        AudioEngine,
        SongData,
        Stream,
        Discord,
        Complete
    }

    public LoadingScreen()
    {
        _stopwatch.Start();
    }

    public async Task ShowAsync(Func<LoadingScreen, Task> initializationWork)
    {
        _isActive = true;

        await AnsiConsole.Progress()
            .AutoRefresh(true)
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new SpinnerColumn(),
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new ElapsedTimeColumn()
            )
            .StartAsync(async ctx =>
            {
                // Show jaybird ASCII art
                ShowHeader();

                // Create all progress tasks
                _tasks[nameof(LoadingStep.Configuration)] = ctx.AddTask("[grey]Loading configuration...[/]", maxValue: 100);
                _tasks[nameof(LoadingStep.AudioEngine)] = ctx.AddTask("[grey]Initializing audio engine...[/]", maxValue: 100);
                _tasks[nameof(LoadingStep.SongData)] = ctx.AddTask("[grey]Fetching song data...[/]", maxValue: 100);
                _tasks[nameof(LoadingStep.Stream)] = ctx.AddTask("[grey]Starting stream...[/]", maxValue: 100);
                _tasks[nameof(LoadingStep.Discord)] = ctx.AddTask("[grey]Connecting to Discord...[/]", maxValue: 100);

                // Run the initialization work
                await initializationWork(this);

                // Mark all as complete
                foreach (var task in _tasks.Values)
                {
                    if (!task.IsFinished)
                    {
                        task.Value = 100;
                    }
                }

                _isActive = false;
            });
    }

    private void ShowHeader()
    {
        AnsiConsole.Write(new FigletText("jaybird")
            .LeftJustified()
            .Color(Color.Cyan1));

        var bird = StationAsciiArt.GetJaybirdArt();
        AnsiConsole.MarkupLine($"[cyan1]{bird}[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]ABC Radio Player with Discord Rich Presence[/]");
        AnsiConsole.WriteLine();
    }

    public void UpdateProgress(LoadingStep step, double progress, string? statusMessage = null)
    {
        if (!_isActive) return;

        var taskKey = step.ToString();
        if (!_tasks.ContainsKey(taskKey)) return;

        var task = _tasks[taskKey];

        // Update description if provided
        if (statusMessage != null)
        {
            task.Description = GetStepDescription(step, statusMessage);
        }

        // Update progress (clamp between 0 and 100)
        task.Value = Math.Clamp(progress, 0, 100);

        // Mark as complete if at 100%
        if (progress >= 100 && !task.IsFinished)
        {
            task.StopTask();
        }
    }

    public void CompleteStep(LoadingStep step)
    {
        UpdateProgress(step, 100);
    }

    public void StartStep(LoadingStep step, string? message = null)
    {
        UpdateProgress(step, 0, message ?? GetDefaultStepMessage(step));
    }

    private string GetStepDescription(LoadingStep step, string message)
    {
        var color = step switch
        {
            LoadingStep.Configuration => "grey",
            LoadingStep.AudioEngine => "yellow",
            LoadingStep.SongData => "cyan1",
            LoadingStep.Stream => "green",
            LoadingStep.Discord => "blue",
            LoadingStep.Complete => "lime",
            _ => "white"
        };

        return $"[{color}]{message}[/]";
    }

    private string GetDefaultStepMessage(LoadingStep step)
    {
        return step switch
        {
            LoadingStep.Configuration => "Loading configuration...",
            LoadingStep.AudioEngine => "Initializing audio engine...",
            LoadingStep.SongData => "Fetching song data...",
            LoadingStep.Stream => "Starting stream...",
            LoadingStep.Discord => "Connecting to Discord...",
            LoadingStep.Complete => "Ready!",
            _ => "Loading..."
        };
    }

    public void ShowError(string errorMessage)
    {
        _isActive = false;
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[red]✗ Error: {errorMessage.EscapeMarkup()}[/]");
    }

    public void ShowSuccess()
    {
        _stopwatch.Stop();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[lime]✓ Ready! (loaded in {_stopwatch.ElapsedMilliseconds}ms)[/]");
        Thread.Sleep(500); // Brief pause to see success message
    }
}
