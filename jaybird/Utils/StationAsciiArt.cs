namespace jaybird.Utils;

using Spectre.Console;
using Models;

public static class StationAsciiArt
{
    private static int _animationFrame = 0;

    public static void IncrementFrame()
    {
        _animationFrame = (_animationFrame + 1) % 3;
    }

    public static string GetStationArt(Station station, bool isPlaying)
    {
        return station switch
        {
            Station.TripleJ => GetTripleJDrum(isPlaying),
            Station.DoubleJ => GetDoubleJDrum(isPlaying),
            Station.Unearthed => GetUnearthedDrum(isPlaying),
            _ => ""
        };
    }

    private static string GetTripleJDrum(bool isPlaying)
    {
        // Red drum with 3 drumsticks
        var frame = isPlaying ? _animationFrame : 0;

        return frame switch
        {
            0 => @"
    ╔═══════════╗
    ║  ┃ ┃ ┃   ║
    ║  ▼ ▼ ▼   ║
    ╠═══════════╣
    ║ ┃┃┃┃┃┃┃┃┃ ║
    ║ ┃┃┃┃┃┃┃┃┃ ║
    ║ ┃┃┃┃┃┃┃┃┃ ║
    ╚═══════════╝",
            1 => @"
    ╔═══════════╗
    ║   ┃ ┃ ┃  ║
    ║   │ │ │  ║
    ╠═══════════╣
    ║ ┃┃┃┃┃┃┃┃┃ ║
    ║ ┃┃┃┃┃┃┃┃┃ ║
    ║ ┃┃┃┃┃┃┃┃┃ ║
    ╚═══════════╝",
            _ => @"
    ╔═══════════╗
    ║  ╲ ╲ ╲   ║
    ║           ║
    ╠═══════════╣
    ║ ┃┃┃┃┃┃┃┃┃ ║
    ║ ┃┃┃┃┃┃┃┃┃ ║
    ║ ┃┃┃┃┃┃┃┃┃ ║
    ╚═══════════╝"
        };
    }

    private static string GetDoubleJDrum(bool isPlaying)
    {
        // Blue drum with 2 drumsticks
        var frame = isPlaying ? _animationFrame : 0;

        return frame switch
        {
            0 => @"
    ╔═══════════╗
    ║    ┃ ┃   ║
    ║    ▼ ▼   ║
    ╠═══════════╣
    ║ ╬╬╬╬╬╬╬╬╬ ║
    ║ ╬╬╬╬╬╬╬╬╬ ║
    ║ ╬╬╬╬╬╬╬╬╬ ║
    ╚═══════════╝",
            1 => @"
    ╔═══════════╗
    ║    ┃ ┃   ║
    ║    │ │   ║
    ╠═══════════╣
    ║ ╬╬╬╬╬╬╬╬╬ ║
    ║ ╬╬╬╬╬╬╬╬╬ ║
    ║ ╬╬╬╬╬╬╬╬╬ ║
    ╚═══════════╝",
            _ => @"
    ╔═══════════╗
    ║    ╲ ╲   ║
    ║           ║
    ╠═══════════╣
    ║ ╬╬╬╬╬╬╬╬╬ ║
    ║ ╬╬╬╬╬╬╬╬╬ ║
    ║ ╬╬╬╬╬╬╬╬╬ ║
    ╚═══════════╝"
        };
    }

    private static string GetUnearthedDrum(bool isPlaying)
    {
        // Green drum
        var frame = isPlaying ? _animationFrame : 0;

        return frame switch
        {
            0 => @"
    ╔═══════════╗
    ║   ┃ ┃    ║
    ║   ▼ ▼    ║
    ╠═══════════╣
    ║ ▓▓▓▓▓▓▓▓▓ ║
    ║ ▓▓▓▓▓▓▓▓▓ ║
    ║ ▓▓▓▓▓▓▓▓▓ ║
    ╚═══════════╝",
            1 => @"
    ╔═══════════╗
    ║   ┃ ┃    ║
    ║   │ │    ║
    ╠═══════════╣
    ║ ▓▓▓▓▓▓▓▓▓ ║
    ║ ▓▓▓▓▓▓▓▓▓ ║
    ║ ▓▓▓▓▓▓▓▓▓ ║
    ╚═══════════╝",
            _ => @"
    ╔═══════════╗
    ║   ╲ ╲    ║
    ║           ║
    ╠═══════════╣
    ║ ▓▓▓▓▓▓▓▓▓ ║
    ║ ▓▓▓▓▓▓▓▓▓ ║
    ║ ▓▓▓▓▓▓▓▓▓ ║
    ╚═══════════╝"
        };
    }

    public static string GetJaybirdArt()
    {
        return @"
   ░░░▒▒▓██
  ░░▒▒▓▓███
 ░▒▒▓▓█████
░░▒▓▓██████>
 ░▒▓▓█████
  ░▒▒▓▓███
   ░░▒▒▓██
    █";
    }
}
